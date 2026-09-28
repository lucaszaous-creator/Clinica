using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Infrastructure;

public sealed class AcompanhamentoPacienteService(ClinicaDbContext db, IClinicaRepositorio repo,
    PacoteService pacotes) : IAcompanhamentoPacienteService
{
    public const string ChaveProfissional = "AcompanhamentoBsvProfissionalId";
    public const string ChaveResponsavel = "AcompanhamentoResponsavelPadraoId";
    private static DateTime Agora => DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,
        TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")), DateTimeKind.Unspecified);
    private static DateOnly Hoje => DateOnly.FromDateTime(Agora);
    private static bool PodeAcompanhar(UsuarioSistema u) => u.Pode(Permissao.VerFichaPaciente)
        && (u.Pode(Permissao.GerenciarCampanhas) || u.Pode(Permissao.VerFaturamento));

    private async Task<UsuarioSistema> Usuario(int id, Permissao permissao, CancellationToken ct)
    {
        var u = await db.Usuarios.Include(x => x.Profissional).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (u is not { Ativo: true } || !(permissao == Permissao.GerenciarCampanhas ? PodeAcompanhar(u) : u.Pode(permissao)) || u.Travado(Agora))
            throw new UnauthorizedAccessException("Seu acesso não permite esta ação.");
        return u;
    }

    private async Task<int> Parametro(string chave, CancellationToken ct)
        => int.TryParse(await db.Configuracoes.Where(x => x.Chave == chave).Select(x => x.Valor).SingleOrDefaultAsync(ct), out var id) ? id : 0;

    public async Task<ConfiguracaoAcompanhamento> ConfiguracaoAsync(int usuarioId, CancellationToken ct = default)
    {
        await Usuario(usuarioId, Permissao.GerenciarCampanhas, ct);
        var usuarios = await db.Usuarios.Include(u => u.Profissional).Where(u => u.Ativo).ToListAsync(ct);
        return new(await Parametro(ChaveProfissional, ct), await Parametro(ChaveResponsavel, ct),
            await db.Profissionais.Where(p => p.Ativo).OrderBy(p => p.Nome).Select(p => new OpcaoAcompanhamento(p.Id, p.Nome)).ToListAsync(ct),
            usuarios.Where(PodeAcompanhar).OrderBy(u => u.Nome).Select(u => new OpcaoAcompanhamento(u.Id, u.Nome)).ToList(),
            await db.MotivosAcompanhamento.Where(m => m.Ativo).OrderBy(m => m.Nome).Select(m => new OpcaoAcompanhamento(m.Id, m.Nome)).ToListAsync(ct));
    }

    public async Task ConfigurarAsync(int usuarioId, int profissionalId, int responsavelId, CancellationToken ct = default)
    {
        var u = await Usuario(usuarioId, Permissao.GerenciarCampanhas, ct);
        if (u.Perfil != PerfilAcesso.Gerente) throw new UnauthorizedAccessException("A configuração é exclusiva da gestão.");
        if (!await db.Profissionais.AnyAsync(p => p.Id == profissionalId && p.Ativo, ct)) throw new InvalidOperationException("Escolha o profissional responsável pelas indicações de BSV.");
        if (responsavelId > 0) await Usuario(responsavelId, Permissao.GerenciarCampanhas, ct);
        foreach (var (chave, valor) in new[] { (ChaveProfissional, profissionalId), (ChaveResponsavel, responsavelId) })
        {
            var c = await db.Configuracoes.FindAsync([chave], ct);
            if (c == null) db.Configuracoes.Add(new() { Chave = chave, Valor = valor.ToString() });
            else c.Valor = valor.ToString();
        }
        db.Auditoria.Add(new() { Operador = u.Login, Acao = "ConfigurarAcompanhamento", Detalhe = $"Profissional {profissionalId}; responsável {responsavelId}." });
        await db.SaveChangesAsync(ct);
    }

    public async Task AdicionarMotivoAsync(int usuarioId, string nome, CancellationToken ct = default)
    {
        var u = await Usuario(usuarioId, Permissao.GerenciarCampanhas, ct);
        if (u.Perfil != PerfilAcesso.Gerente) throw new UnauthorizedAccessException("Somente a gestão cadastra motivos.");
        nome = nome.Trim();
        if (nome.Length is < 3 or > 100) throw new InvalidOperationException("Informe um motivo de 3 a 100 caracteres.");
        if (await db.MotivosAcompanhamento.AnyAsync(m => m.Nome.ToLower() == nome.ToLower(), ct)) return;
        db.MotivosAcompanhamento.Add(new() { Nome = nome });
        db.Auditoria.Add(new() { Operador = u.Login, Acao = "CadastrarMotivoRecall", Detalhe = nome });
        await db.SaveChangesAsync(ct);
    }

    public async Task<EstadoIndicacaoBsv> EstadoBsvAsync(int usuarioId, int agendamentoId, CancellationToken ct = default)
    {
        var u = await Usuario(usuarioId, Permissao.EditarProntuario, ct);
        var a = await db.Agendamentos.AsNoTracking().SingleOrDefaultAsync(a => a.Id == agendamentoId && a.ProfissionalId == u.ProfissionalId, ct);
        if (a == null) return new(false, false);
        var indicado = await db.Acompanhamentos.AnyAsync(x => x.PacienteId == a.PacienteId && x.Tipo == TipoAcompanhamento.NovoBsv, ct);
        var permitido = u.Pode(Permissao.Prescrever) && u.Profissional?.Ativo == true
            && u.ProfissionalId == await Parametro(ChaveProfissional, ct)
            && a.ModalidadePrevista == ModalidadeAtendimento.Consulta
            && a.Status is StatusAgendamento.Agendado or StatusAgendamento.Realizado;
        return new(permitido, indicado);
    }

    public async Task IndicarBsvAsync(int usuarioId, int agendamentoId, CancellationToken ct = default)
    {
        var estado = await EstadoBsvAsync(usuarioId, agendamentoId, ct);
        if (!estado.PodeIndicar) throw new UnauthorizedAccessException("A indicação deve ser feita pelo profissional configurado durante a consulta.");
        if (estado.Indicado) return;
        var responsavel = await Parametro(ChaveResponsavel, ct);
        if (responsavel > 0) await Usuario(responsavel, Permissao.GerenciarCampanhas, ct);
        var u = await Usuario(usuarioId, Permissao.Prescrever, ct);
        var a = await db.Agendamentos.SingleAsync(a => a.Id == agendamentoId, ct);
        var caso = new AcompanhamentoPaciente { PacienteId = a.PacienteId, Tipo = TipoAcompanhamento.NovoBsv,
            Modalidade = ModalidadeAtendimento.BsvApenas, ReferenciaEm = Agora, CriadoEm = Agora, CriadoPor = u.Login,
            AgendamentoOrigemId = a.Id, ProfissionalId = u.ProfissionalId, ResponsavelId = responsavel > 0 ? responsavel : null, ProximoContato = Hoje };
        caso.Contatos.Add(new() { Idempotencia = Guid.NewGuid(), Em = Agora, Operador = u.Login, Resultado = "Indicação de novo paciente BSV", ResponsavelId = caso.ResponsavelId, ProximoContato = Hoje });
        db.Acompanhamentos.Add(caso);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            if (!await db.Acompanhamentos.AnyAsync(x => x.PacienteId == a.PacienteId && x.Tipo == TipoAcompanhamento.NovoBsv, ct)) throw;
        }
    }

    public async Task<int> GerarRecallAsync(int usuarioId, int dias, ModalidadeAtendimento? modalidade = null, CancellationToken ct = default)
    {
        try { return await GerarRecallCoreAsync(usuarioId, dias, modalidade, ct); }
        catch (DbUpdateException ex) when (ex is DbUpdateConcurrencyException ||
            ex.GetBaseException() is Npgsql.PostgresException { SqlState: "23505", ConstraintName: "IX_Acompanhamentos_PacienteId_Tipo_Modalidade" })
        {
            // Two desks may open recall together. SaveChanges rolled back the batch;
            // re-read once, preserving the journey created by the other operator.
            db.ChangeTracker.Clear();
            return await GerarRecallCoreAsync(usuarioId, dias, modalidade, ct);
        }
    }

    private async Task<int> GerarRecallCoreAsync(int usuarioId, int dias, ModalidadeAtendimento? modalidade, CancellationToken ct)
    {
        var u = await Usuario(usuarioId, Permissao.GerenciarCampanhas, ct);
        if (dias is < 1 or > 3650 || modalidade is {} m && !Enum.IsDefined(m)) throw new InvalidOperationException("Informe de 1 a 3650 dias e uma modalidade válida.");
        var corte = Hoje.AddDays(-dias);
        var bases = await db.Atendimentos.AsNoTracking().Where(a => a.RealizadoEm != null && a.EstornadoEm == null
                && (modalidade == null || a.Modalidade == modalidade))
            .GroupBy(a => new { a.PacienteId, a.Modalidade }).Select(g => new { g.Key.PacienteId, g.Key.Modalidade, Ultima = g.Max(a => a.Data) })
            .Where(a => a.Ultima <= corte).ToListAsync(ct);
        var ids = bases.Select(x => x.PacienteId).Distinct().ToList();
        var existentes = await db.Acompanhamentos.Where(x => ids.Contains(x.PacienteId)).ToListAsync(ct);
        var sessoes = await db.Atendimentos.AsNoTracking().Where(a => ids.Contains(a.PacienteId) && a.RealizadoEm != null && a.EstornadoEm == null)
            .Select(a => new { a.PacienteId, a.Modalidade, a.Data, a.Id, ProfissionalId = db.Agendamentos.Where(h => h.AtendimentoId == a.Id).Select(h => h.ProfissionalId).FirstOrDefault() }).ToListAsync(ct);
        var ultimaPorModalidade = sessoes.GroupBy(a => (a.PacienteId, a.Modalidade))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.Data).ThenByDescending(a => a.Id).First());
        var legados = await db.Contatos.AsNoTracking().Where(c => ids.Contains(c.PacienteId) && c.Tipo == TipoContato.Recall && c.EnviadoEm != null).ToListAsync(ct);
        var agendados = await db.Agendamentos.AsNoTracking().Where(a => ids.Contains(a.PacienteId)
            && a.Status == StatusAgendamento.Agendado && a.DataHora >= Hoje.ToDateTime(TimeOnly.MinValue))
            .Select(a => new { a.PacienteId, a.ModalidadePrevista }).ToListAsync(ct);
        var quantidade = 0;
        foreach (var b in bases)
        {
            if (agendados.Any(a => a.PacienteId == b.PacienteId && a.ModalidadePrevista == b.Modalidade)) continue;
            var caso = existentes.SingleOrDefault(x => x.PacienteId == b.PacienteId && x.Tipo == TipoAcompanhamento.Recall && x.Modalidade == b.Modalidade);
            var referencia = b.Ultima.ToDateTime(TimeOnly.MinValue);
            if (caso != null && caso.ReferenciaEm >= referencia) continue;
            if (caso == null)
            {
                caso = new() { PacienteId = b.PacienteId, Tipo = TipoAcompanhamento.Recall, Modalidade = b.Modalidade,
                    CriadoEm = Agora, CriadoPor = u.Login };
                db.Acompanhamentos.Add(caso);
                foreach (var anterior in legados.Where(c => c.PacienteId == b.PacienteId && c.EnviadoEm >= referencia))
                    caso.Contatos.Add(new() { Idempotencia = Guid.NewGuid(), Em = anterior.EnviadoEm!.Value,
                        Operador = anterior.EnviadoPor ?? "Registro anterior", Canal = anterior.Canal,
                        Resultado = "Contato do recall anterior", Observacao = "Registro preservado do fluxo anterior. " + anterior.Comentario,
                        ResponsavelId = usuarioId });
            }
            caso.ProfissionalId = ultimaPorModalidade[(b.PacienteId, b.Modalidade)].ProfissionalId;
            caso.ReferenciaEm = referencia;
            caso.Etapa = EtapaAcompanhamento.AContatar;
            caso.ProximoContato = Hoje;
            caso.EncerradoEm = null; caso.MotivoEncerramento = null; caso.EncerradoPor = null; caso.MotivoId = null;
            caso.Versao = Guid.NewGuid();
            caso.Contatos.Add(new() { Idempotencia = Guid.NewGuid(), Em = Agora, Operador = u.Login,
                Resultado = "Novo ciclo de recall", Observacao = $"Última sessão da modalidade em {b.Ultima:dd/MM/yyyy}.", ResponsavelId = caso.ResponsavelId, ProximoContato = Hoje });
            quantidade++;
        }
        await db.SaveChangesAsync(ct);
        return quantidade;
    }

    public async Task<IReadOnlyList<LinhaAcompanhamento>> ListarAsync(int usuarioId, CancellationToken ct = default)
    {
        await Usuario(usuarioId, Permissao.GerenciarCampanhas, ct);
        var casos = await db.Acompanhamentos.AsNoTracking().Include(x => x.Paciente).Include(x => x.Responsavel).Include(x => x.Motivo).ToListAsync(ct);
        if (casos.Count == 0) return [];
        var ids = casos.Select(x => x.PacienteId).Distinct().ToList();
        var menorData = casos.Min(x => x.ReferenciaEm).Date;
        var sessoes = await db.Atendimentos.AsNoTracking().Where(a => ids.Contains(a.PacienteId) && a.RealizadoEm != null && a.EstornadoEm == null && a.Data >= DateOnly.FromDateTime(menorData))
            .Select(a => new { a.PacienteId, a.Modalidade, a.Data, a.RealizadoEm }).ToListAsync(ct);
        var horarios = await db.Agendamentos.AsNoTracking().Where(a => ids.Contains(a.PacienteId) && a.DataHora >= menorData)
            .Select(a => new { a.Id, a.PacienteId, a.ModalidadePrevista, a.DataHora, a.Status, a.ProfissionalId }).ToListAsync(ct);
        var sessoesPorPaciente = sessoes.ToLookup(a => a.PacienteId);
        var horariosPorPaciente = horarios.ToLookup(a => a.PacienteId);
        var contatos = await db.ContatosAcompanhamento.AsNoTracking().Where(c => c.Canal != null && c.Em >= c.AcompanhamentoPaciente!.ReferenciaEm)
            .GroupBy(c => c.AcompanhamentoPacienteId).Select(g => new { Id = g.Key, Total = g.Count(), Ultimo = g.Max(c => c.Em) }).ToDictionaryAsync(c => c.Id, ct);
        var consentidos = (await repo.PacientesComConsentimentoVigenteAsync(FinalidadeConsentimento.ComunicacaoEMarketing, ids, ct)).ToHashSet();
        var comPacote = (await repo.PacotesDosPacientesAsync(ids, ct)).GroupBy(p => p.PacienteId)
            .Where(g => pacotes.Descrever(g, Hoje).Any(p => p.Ativo && (p.SaldoSessoes == null || p.SaldoSessoes > 0))).Select(g => g.Key).ToHashSet();
        var profissionais = await db.Profissionais.AsNoTracking().ToDictionaryAsync(p => p.Id, p => p.Nome, ct);
        var convenios = await db.Convenios.AsNoTracking().ToDictionaryAsync(c => c.Codigo, c => c.Nome, ct);
        var resultado = new List<LinhaAcompanhamento>();
        foreach (var c in casos)
        {
            bool Corresponde(ModalidadeAtendimento m) => RegrasAcompanhamento.Corresponde(c.Tipo, c.Modalidade, m);
            var realizou = sessoesPorPaciente[c.PacienteId].Any(a => Corresponde(a.Modalidade)
                && (c.Tipo == TipoAcompanhamento.Recall ? a.Data > DateOnly.FromDateTime(c.ReferenciaEm) : a.RealizadoEm >= c.ReferenciaEm));
            var correspondentes = horariosPorPaciente[c.PacienteId].Where(a => Corresponde(a.ModalidadePrevista) && a.DataHora >= c.ReferenciaEm).ToList();
            var agendado = correspondentes.Where(a => a.Status == StatusAgendamento.Agendado && a.DataHora.Date >= Agora.Date).OrderBy(a => a.DataHora).FirstOrDefault();
            var ultimoHorario = correspondentes.OrderByDescending(a => a.DataHora).ThenByDescending(a => a.Id).FirstOrDefault();
            var cancelou = ultimoHorario?.Status == StatusAgendamento.Cancelado;
            var faltou = ultimoHorario?.Status == StatusAgendamento.Faltou;
            var pendente = c.EncerradoEm == null && !realizou && agendado == null;
            var situacao = c.EncerradoEm != null ? "Encerrado com justificativa" : realizou ? "Sessão realizada" : agendado != null ? "Agendado"
                : cancelou ? "Cancelou" : faltou ? "Faltou"
                : ultimoHorario?.Status == StatusAgendamento.Agendado ? "Conferir comparecimento" : RegrasAcompanhamento.Rotulo(c.Etapa);
            contatos.TryGetValue(c.Id, out var contato);
            resultado.Add(new(c.Id, c.PacienteId, c.Paciente!.Nome, c.Paciente.Telefone,
                c.Paciente.ConvenioCodigo is {} codigo && convenios.TryGetValue(codigo, out var convenio) ? convenio : RotulosEnum.De(c.Paciente.Convenio),
                c.Tipo, c.Modalidade, c.ProfissionalId is {} pid && profissionais.TryGetValue(pid, out var nome) ? nome : "—", c.ProfissionalId,
                c.ResponsavelId, c.Responsavel == null ? "A assumir" : c.Responsavel.Nome + (c.Responsavel.Ativo ? "" : " (inativo)"), c.ReferenciaEm,
                Hoje.DayNumber - DateOnly.FromDateTime(c.ReferenciaEm).DayNumber, situacao, pendente, pendente && c.ProximoContato < Hoje,
                c.ProximoContato, c.Etapa, contato?.Total ?? 0, contato?.Ultimo, null, consentidos.Contains(c.PacienteId), comPacote.Contains(c.PacienteId),
                agendado?.DataHora, cancelou, faltou, c.Versao, c.MotivoEncerramento, c.Motivo?.Nome));
        }
        return resultado.OrderByDescending(x => x.Atrasado).ThenBy(x => x.ProximoContato).ThenBy(x => x.Paciente).ToList();
    }

    public async Task<IReadOnlyList<ContatoAcompanhamento>> HistoricoAsync(int usuarioId, int id, CancellationToken ct = default)
    {
        await Usuario(usuarioId, Permissao.GerenciarCampanhas, ct);
        var paciente = await db.Acompanhamentos.Where(c => c.Id == id).Select(c => c.PacienteId).SingleAsync(ct);
        var contatos = await db.ContatosAcompanhamento.AsNoTracking().Include(c => c.AcompanhamentoPaciente)
            .Where(c => c.AcompanhamentoPaciente!.PacienteId == paciente).OrderByDescending(c => c.Em).ThenByDescending(c => c.Id).ToListAsync(ct);
        foreach (var c in contatos)
            c.Resultado = (c.AcompanhamentoPaciente!.Tipo == TipoAcompanhamento.NovoBsv ? "Novos BSV" : "Recall " + RotulosEnum.De(c.AcompanhamentoPaciente.Modalidade)) + " · " + c.Resultado;
        return contatos;
    }

    public async Task ValidarContatoAsync(int usuarioId, int id, CancellationToken ct = default)
    {
        await Usuario(usuarioId, Permissao.GerenciarCampanhas, ct);
        var c = await db.Acompanhamentos.AsNoTracking().Include(c => c.Paciente).SingleAsync(c => c.Id == id, ct);
        if (string.IsNullOrWhiteSpace(c.Paciente!.Telefone)) throw new InvalidOperationException("Atualize o telefone do paciente antes de contatar.");
        if (!(await repo.PacientesComConsentimentoVigenteAsync(FinalidadeConsentimento.ComunicacaoEMarketing, [c.PacienteId], ct)).Contains(c.PacienteId))
            throw new InvalidOperationException("Contato não autorizado no cadastro do paciente.");
    }

    public async Task AtualizarAsync(int usuarioId, int id, AtualizarAcompanhamento pedido, CancellationToken ct = default)
    {
        var u = await Usuario(usuarioId, Permissao.GerenciarCampanhas, ct);
        if (pedido.Idempotencia == Guid.Empty || !Enum.IsDefined(pedido.Etapa) || pedido.Canal is {} canal && !Enum.IsDefined(canal)) throw new InvalidOperationException("Confira o resultado e o canal do contato.");
        if (await db.ContatosAcompanhamento.AnyAsync(c => c.AcompanhamentoPacienteId == id && c.Idempotencia == pedido.Idempotencia, ct)) return;
        var caso = await db.Acompanhamentos.SingleAsync(c => c.Id == id, ct);
        if (caso.Versao != pedido.Versao) throw new InvalidOperationException("Outra pessoa atualizou este acompanhamento. Atualize a lista antes de salvar.");
        if (pedido.Encerrar && pedido.Reabrir) throw new InvalidOperationException("Escolha encerrar ou reabrir.");
        if (caso.EncerradoEm != null && !pedido.Reabrir) throw new InvalidOperationException("Reabra o acompanhamento antes de registrar novas ações.");
        var observacao = pedido.Observacao?.Trim() ?? "";
        if (observacao.Length is < 5 or > 2000) throw new InvalidOperationException("Registre o resultado ou a justificativa (5 a 2000 caracteres).");
        // A autoria vem da sessão validada, inclusive para clientes antigos que enviem outro responsável.
        if (!pedido.Encerrar && (pedido.ProximoContato == null || pedido.ProximoContato < Hoje)) throw new InvalidOperationException("Defina o próximo contato para hoje ou uma data futura.");
        if (pedido.MotivoId is {} motivo && !await db.MotivosAcompanhamento.AnyAsync(m => m.Id == motivo && m.Ativo, ct)) throw new InvalidOperationException("Escolha um motivo ativo.");
        if (pedido.Encerrar && pedido.MotivoId == null) throw new InvalidOperationException("Selecione o motivo da não conformidade.");
        if (pedido.Canal != null) await ValidarContatoAsync(usuarioId, id, ct);
        if (pedido.Encerrar && pedido.Canal == null && !await db.ContatosAcompanhamento.AnyAsync(c => c.AcompanhamentoPacienteId == id && c.Canal != null && c.Em >= caso.ReferenciaEm, ct)
            && u.Perfil != PerfilAcesso.Gerente) throw new InvalidOperationException("Registre uma tentativa de contato antes de encerrar. Casos sem possibilidade de contato devem ser avaliados pela gestão.");
        caso.ResponsavelId = u.Id;
        caso.ProximoContato = pedido.ProximoContato ?? caso.ProximoContato;
        caso.Etapa = pedido.Etapa; caso.MotivoId = pedido.MotivoId;
        caso.EncerradoEm = pedido.Encerrar ? Agora : null;
        caso.EncerradoPor = pedido.Encerrar ? u.Login : null;
        caso.MotivoEncerramento = pedido.Encerrar ? observacao : null;
        caso.Versao = Guid.NewGuid();
        caso.Contatos.Add(new() { Idempotencia = pedido.Idempotencia, Em = Agora, Operador = u.Login, Canal = pedido.Canal,
            Resultado = pedido.Encerrar ? "Não conformidade — encerrado" : pedido.Reabrir ? "Reaberto" : RegrasAcompanhamento.Rotulo(pedido.Etapa),
            Observacao = observacao, ResponsavelId = u.Id, ProximoContato = pedido.Encerrar ? null : pedido.ProximoContato });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new InvalidOperationException("Outra pessoa atualizou este acompanhamento. Atualize a lista antes de salvar."); }
    }
}
