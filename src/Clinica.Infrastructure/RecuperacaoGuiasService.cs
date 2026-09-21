using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Infrastructure;

public sealed record FiltroRecuperacaoGuias(DateOnly Inicio, DateOnly Fim, int Pagina = 0);
public sealed record ItemRecuperacaoGuias(int Id, string Paciente, string Medico, DateTime Data,
    string Convenio, int? EvolucaoId, bool PodeRecuperar, string Situacao, string Versao, bool Escrita);
public sealed record PreviaRecuperacaoGuias(IReadOnlyList<ItemRecuperacaoGuias> Itens, bool Mais);
public sealed record PedidoRecuperacaoGuia(int Id, string Versao);
public sealed record ResultadoRecuperacaoGuia(int Id, bool Sucesso, int Guias, string Mensagem);

/// <summary>Recuperação administrativa explícita, com prévia e revalidação por sessão.</summary>
public sealed class RecuperacaoGuiasService(ClinicaDbContext db, IClinicaRepositorio repo, AgendaService agenda, AtendimentoService atendimentos)
{
    private async Task<UsuarioSistema> Autorizar(int id, CancellationToken ct)
    {
        var u = await db.Usuarios.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id, ct);
        if (u is not { Ativo: true, Perfil: PerfilAcesso.Gerente }
            || !u.Pode(Permissao.VerProntuario | Permissao.VerFaturamento | Permissao.LancarAtendimento))
            throw new UnauthorizedAccessException("A recuperação de guias exige o Gerente Geral autorizado.");
        return u;
    }
    public async Task<PreviaRecuperacaoGuias> PreverAsync(int usuarioId, FiltroRecuperacaoGuias filtro, CancellationToken ct = default)
    {
        await Autorizar(usuarioId, ct);
        if (filtro.Inicio > filtro.Fim || filtro.Fim == DateOnly.MaxValue || filtro.Pagina is < 0 or > 10000)
            throw new InvalidOperationException("Confira o período e a página.");
        var desde = filtro.Inicio.ToDateTime(TimeOnly.MinValue); var ate = filtro.Fim.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var ids = await db.Agendamentos.AsNoTracking().Where(a => a.DataHora >= desde && a.DataHora < ate
            && (a.Status == StatusAgendamento.Agendado || a.Status == StatusAgendamento.Realizado)
            && (a.Status == StatusAgendamento.Agendado || a.FimAtendimentoEm == null
                || a.AtendimentoId == null || !a.Atendimento!.Codigos.Any())
            && db.Evolucoes.Any(e => e.CanceladaEm == null && (e.AgendamentoId == a.Id
                || e.AgendamentoId == null && e.PacienteId == a.PacienteId && e.Data == DateOnly.FromDateTime(a.DataHora))))
            .OrderBy(a => a.DataHora).ThenBy(a => a.Id).Skip(filtro.Pagina * 50).Take(51).Select(a => a.Id).ToListAsync(ct);
        var itens = new List<ItemRecuperacaoGuias>();
        foreach (var id in ids.Take(50)) itens.Add((await Conferir(id, ct)).Item);
        return new(itens, ids.Count > 50);
    }

    private async Task<(ItemRecuperacaoGuias Item, Agendamento Horario, Evolucao? Evolucao)> Conferir(int id, CancellationToken ct)
    {
        var a = await db.Agendamentos.Include(a => a.Paciente).Include(a => a.Profissional)
            .Include(a => a.Atendimento).ThenInclude(a => a!.Codigos).SingleOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new InvalidOperationException("Sessão não encontrada.");
        var data = DateOnly.FromDateTime(a.DataHora);
        var evolucoes = await db.Evolucoes.Where(e => e.CanceladaEm == null && (e.AgendamentoId == id
            || e.AgendamentoId == null && e.PacienteId == a.PacienteId && e.Data == data)).ToListAsync(ct);
        var vinculadas = evolucoes.Where(e => e.AgendamentoId == id).ToList();
        var e = vinculadas.Count == 1 ? vinculadas[0] : vinculadas.Count == 0 && evolucoes.Count == 1 ? evolucoes[0] : null;
        var horariosDoDia = await db.Agendamentos.AsNoTracking().Where(h => h.PacienteId == a.PacienteId
            && h.DataHora >= data.ToDateTime(TimeOnly.MinValue) && h.DataHora < data.AddDays(1).ToDateTime(TimeOnly.MinValue)).ToListAsync(ct);
        var vinculosDoDia = await db.Evolucoes.AsNoTracking().Where(v => v.CanceladaEm == null && v.PacienteId == a.PacienteId && v.Data == data)
            .Select(v => new Evolucao { Id = v.Id, PacienteId = v.PacienteId, Data = v.Data, AgendamentoId = v.AgendamentoId,
                AtendimentoId = v.AtendimentoId, ProfissionalId = v.ProfissionalId }).ToListAsync(ct);
        // Exatamente o mesmo vínculo que acende "Escrito" no Meu dia, além da validação
        // de conteúdo e autoria médica abaixo. Enfermagem fica em outra tabela.
        var escrita = e != null && ConsultorioService.EvolucaoDoHorario(vinculosDoDia, a.Id, a.PacienteId, data, horariosDoDia)?.Id == e.Id;
        string? erro = null;
        if (a.Status is not (StatusAgendamento.Agendado or StatusAgendamento.Realizado)) erro = "Horário cancelado, falta ou substituição: conferir na agenda.";
        else if (a.DataHora > ConclusaoAutomaticaService.Agora) erro = "Sessão futura: não recuperar antes do atendimento.";
        else if (a.Status == StatusAgendamento.Realizado && a.FimAtendimentoEm != null
            && (a.Atendimento?.Codigos.Count > 0 || !CatalogoConvenios.GeraGuia(a.Paciente!.ConvenioCodigo)))
            erro = "A sessão já está concluída com códigos; nenhum será substituído ou duplicado.";
        else if (a.Atendimento?.EstornadoEm != null) erro = "Atendimento estornado: conferir no faturamento.";
        else if (a.Status == StatusAgendamento.Realizado && a.AtendimentoId == null) erro = "Realizado sem vínculo: localize o atendimento original.";
        else if (e is null) erro = "Vínculo ambíguo: confira qual evolução pertence à sessão.";
        // Agenda legada pode não identificar o médico. A evolução médica identificada
        // é a fonte de autoria; não preenchemos nem trocamos o responsável da agenda.
        else if (e.PacienteId != a.PacienteId || e.Data != data || e.ProfissionalId == null
            || a.ProfissionalId != null && e.ProfissionalId != a.ProfissionalId)
            erro = "Confira paciente, data e médico da evolução original.";
        else if (e.AtendimentoId != null && e.AtendimentoId != a.AtendimentoId) erro = "A evolução já pertence a outro atendimento.";
        else if (!escrita) erro = "A sessão não está identificada como Escrito no consultório: conferir o vínculo antes de concluir.";
        else if (!ProntuarioService.TemRegistro(e) && !await db.MapasCorporais.AnyAsync(m => m.EvolucaoId == e.Id
            && (m.Pontos.Any() || m.Observacoes != null && m.Observacoes.Trim() != ""), ct)) erro = "Não há evolução médica preenchida nem mapa registrado.";
        else if (e.AgendamentoId == null && horariosDoDia.Count != 1)
            erro = "Há mais de um horário no dia: vincule a evolução explicitamente pela ficha.";
        else if (await db.Atendimentos.AnyAsync(t => t.PacienteId == a.PacienteId && t.Data == data
            && t.Id != a.AtendimentoId && t.EstornadoEm == null, ct)) erro = "Há outro atendimento no dia: confira as guias e o vínculo antes de recuperar.";
        else if (a.Paciente!.ConvenioADefinir && a.Atendimento?.Codigos.Any(c => c.Status != StatusCodigo.NaoAplicavel) != true)
            erro = "Convênio a definir: confira o convênio da sessão na ficha.";
        else if (a.Atendimento?.Codigos.Count > 0 && a.Atendimento.Codigos.All(c => c.Status == StatusCodigo.NaoAplicavel)
            && CatalogoConvenios.GeraGuia(a.Paciente!.ConvenioCodigo))
            erro = "Os códigos existentes estão não aplicáveis: revisar a situação no faturamento sem substituí-los.";
        if (erro == null && a.Atendimento is {} t && (t.PacienteId != a.PacienteId || t.Data != data || t.Modalidade != a.ModalidadePrevista))
            erro = "Os dados do atendimento original divergem da sessão.";
        var versao = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new {
            a.Id, a.PacienteId, a.ProfissionalId, a.DataHora, a.Status, a.AtendimentoId, a.FimAtendimentoEm,
            a.ModalidadePrevista, a.ModalidadeCodigo, a.EspecialidadeConsultaCodigo, a.PrimeiroCodigo,
            a.Paciente!.ConvenioCodigo, a.Paciente.Convenio, Evolucao = e == null ? null : new {
                e.Id, e.AgendamentoId, e.AtendimentoId, e.CriadoEm, e.AtualizadoEm, e.TextoEvolucao }, erro }))));
        return (new(a.Id, a.Paciente.Nome, a.Profissional?.Nome ?? "Não informado", a.DataHora, a.Paciente.ConvenioNome,
            e?.Id, erro == null, erro ?? (a.Atendimento?.Codigos.Count > 0 ? "Concluir preservando códigos existentes"
                : !CatalogoConvenios.GeraGuia(a.Paciente.ConvenioCodigo) ? "Concluir sem guia, conforme configuração do convênio"
                : e!.AgendamentoId == null ? $"Vincular evolução #{e.Id} e gerar guias" : "Pronta para recuperar as guias"), versao, escrita), a, e);
    }

    public async Task<ResultadoRecuperacaoGuia> RecuperarAsync(int usuarioId, PedidoRecuperacaoGuia pedido, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (db.Database.IsNpgsql()) await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(20260916, {pedido.Id})", ct);
        db.ChangeTracker.Clear();
        var u = await Autorizar(usuarioId, ct);
        var (item, a, e) = await Conferir(pedido.Id, ct);
        if (a.Atendimento is { EstornadoEm: null }
            && (a.Atendimento.Codigos.Count > 0 || !CatalogoConvenios.GeraGuia(a.Paciente!.ConvenioCodigo))
            && a.Status == StatusAgendamento.Realizado && a.FimAtendimentoEm != null)
            return new(a.Id, true, 0, "O atendimento já possui códigos. Nenhuma nova guia foi criada.");
        if (!item.PodeRecuperar) throw new InvalidOperationException(item.Situacao);
        if (!string.Equals(item.Versao, pedido.Versao, StringComparison.Ordinal))
            throw new InvalidOperationException("A sessão mudou desde a prévia. Atualize e confira novamente.");
        const string operador = "sistema:recuperacao-guias";
        var anteriores = a.Atendimento?.Codigos.Count(c => c.Status != StatusCodigo.NaoAplicavel) ?? 0;
        var geraGuia = CatalogoConvenios.GeraGuia(a.Paciente!.ConvenioCodigo);
        if (e!.AgendamentoId == null) await new ProntuarioService(repo).VincularAoHorarioAsync(e.Id, a.Id, operador, ct);
        if (a.AtendimentoId != null && geraGuia) await atendimentos.RecuperarGuiasAusentesAsync(a, operador, ct);
        var fim = await agenda.ConcluirAtendimentoClinicoAsync(a.Id, operador, ct, u.Id,
            permitirEnfermagemPosterior: true, reprocessarPresenca: false);
        var quantidade = fim.Atendimento.Codigos.Count(c => c.Status != StatusCodigo.NaoAplicavel);
        if (quantidade == 0 && geraGuia) throw new InvalidOperationException("Nenhuma guia aplicável: confira convênio e modalidade. Recuperação revertida.");
        await repo.RegistrarAuditoriaAsync(new EventoAuditoria { Operador = operador, PacienteId = a.PacienteId,
            Acao = "RecuperacaoGuiaSessaoEscrita", Detalhe = $"Sessão original {a.Id}; evolução {e.Id}; atendimento {fim.Atendimento.Id}; {quantidade} guias; acesso administrativo {u.Id}. Recuperação administrativa; autoria e data clínica preservadas; não representa nova avaliação ou assinatura médica." }, ct);
        await repo.SalvarAsync(ct); await tx.CommitAsync(ct);
        return new(a.Id, true, quantidade - anteriores, "Sessão concluída; códigos existentes preservados e guias ausentes geradas quando aplicáveis. A enfermagem pode registrar depois na sessão original.");
    }
    public async Task<IReadOnlyList<ResultadoRecuperacaoGuia>> RecuperarLoteAsync(int usuarioId, PedidoRecuperacaoGuia[] pedidos, CancellationToken ct = default)
    {
        await Autorizar(usuarioId, ct);
        if (pedidos is null || pedidos.Length is < 1 or > 50 || pedidos.Any(p => p is null) || pedidos.Select(p => p.Id).Distinct().Count() != pedidos.Length)
            throw new InvalidOperationException("Selecione entre 1 e 50 sessões diferentes.");
        var resultado = new List<ResultadoRecuperacaoGuia>();
        foreach (var p in pedidos)
        {
            try { resultado.Add(await RecuperarAsync(usuarioId, p, ct)); }
            catch (UnauthorizedAccessException) { throw; }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { Clinica.Application.Diagnostico.Registrar($"Recuperação da sessão {p.Id} pendente", ex);
                resultado.Add(new(p.Id, false, 0, ex is InvalidOperationException ? ex.Message : "Falha ao recuperar. Atualize a prévia antes de repetir.")); }
        }
        return resultado;
    }
}
