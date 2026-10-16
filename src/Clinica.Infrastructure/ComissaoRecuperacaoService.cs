using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Infrastructure;

/// <summary>
/// A comissão de recuperação (out/2026, pedido da clínica): <b>atendente assume o paciente
/// que sumiu → paciente volta → conclui o atendimento → a guia nasce → a gestão valida</b>.
/// Só existe comissão depois do fluxo COMPLETO, e quem decide é a gestão, na verificação do
/// Gerente — a recepção e o faturamento só fazem a parte deles (assumir e falar), sem ver valor.
///
/// A apuração é LEITURA dos fatos que já existem: o caso do acompanhamento, os contatos dele
/// (quem assumiu e quando) e a sessão realizada. Nada é marcado no caminho, e por isso o
/// relatório não pode ficar "fora de sincronia" com a fila — os dois leem a mesma linha.
/// </summary>
public sealed class ComissaoRecuperacaoService(ClinicaDbContext db) : IComissaoRecuperacaoService
{
    /// <summary>Os registros que o SISTEMA escreve no histórico — não contam como "a atendente assumiu".</summary>
    private static readonly string[] RegistrosDoSistema =
        ["Novo ciclo de recall", "Indicação de novo paciente BSV", "Contato do recall anterior"];

    private static DateTime Agora => DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,
        TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")), DateTimeKind.Unspecified);

    public async Task<IReadOnlyList<RegraComissaoRecuperacao>> RegrasAsync(CancellationToken ct = default)
        => await db.RegrasComissaoRecuperacao.AsNoTracking()
            .OrderByDescending(r => r.Ativa).ThenByDescending(r => r.VigenteDe).ThenByDescending(r => r.Id).ToListAsync(ct);

    public async Task<RegraComissaoRecuperacao> SalvarRegraAsync(RegraComissaoRecuperacao dados, string operador, CancellationToken ct = default)
    {
        if (ComissaoRecuperacaoRegras.Impedimento(dados) is { } erro) throw new InvalidOperationException(erro);
        RegraComissaoRecuperacao regra;
        if (dados.Id == 0)
        {
            regra = new RegraComissaoRecuperacao { CriadaEm = Agora, CriadaPor = operador };
            db.RegrasComissaoRecuperacao.Add(regra);
        }
        else
        {
            regra = await db.RegrasComissaoRecuperacao.SingleOrDefaultAsync(r => r.Id == dados.Id, ct)
                    ?? throw new InvalidOperationException("Regra não encontrada — atualize a tela.");
        }
        // Cópia campo a campo — o que não estiver aqui some na primeira edição.
        regra.Tipo = dados.Tipo;
        regra.Modalidade = dados.Modalidade;
        regra.Valor = Math.Round(dados.Valor, 2);
        regra.VigenteDe = dados.VigenteDe;
        regra.VigenteAte = dados.VigenteAte;
        regra.Ativa = dados.Ativa;
        regra.Observacao = string.IsNullOrWhiteSpace(dados.Observacao) ? null : dados.Observacao.Trim();
        db.Auditoria.Add(new EventoAuditoria { Operador = operador, Acao = dados.Id == 0 ? "RegraComissaoRecuperacaoCriada" : "RegraComissaoRecuperacaoEditada",
            Detalhe = ComissaoRecuperacaoRegras.Descrever(regra) });
        await db.SaveChangesAsync(ct);
        return regra;
    }

    public async Task DesativarRegraAsync(int id, string operador, CancellationToken ct = default)
    {
        var regra = await db.RegrasComissaoRecuperacao.SingleOrDefaultAsync(r => r.Id == id, ct)
                    ?? throw new InvalidOperationException("Regra não encontrada — atualize a tela.");
        if (!regra.Ativa) return;
        regra.Ativa = false;
        db.Auditoria.Add(new EventoAuditoria { Operador = operador, Acao = "RegraComissaoRecuperacaoDesativada", Detalhe = ComissaoRecuperacaoRegras.Descrever(regra) });
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<LinhaRecuperacao>> ApurarAsync(DateOnly inicio, DateOnly fim, CancellationToken ct = default)
    {
        if (fim < inicio) throw new InvalidOperationException("O fim do período é anterior ao início.");

        // 1) As sessões realizadas no período — estornadas inclusive, porque uma comissão já
        // validada de sessão estornada depois precisa APARECER (a gestão revê); não validada, sai.
        var sessoes = await db.Atendimentos.AsNoTracking()
            .Where(a => a.RealizadoEm != null && a.Data >= inicio && a.Data <= fim)
            .Select(a => new { a.Id, a.PacienteId, a.Data, a.Modalidade, a.RealizadoEm, a.EstornadoEm })
            .ToListAsync(ct);
        if (sessoes.Count == 0) return [];
        var pacientes = sessoes.Select(s => s.PacienteId).Distinct().ToList();

        // 2) Os casos desses pacientes e os contatos (é o histórico que diz quem assumiu e quando).
        var casos = await db.Acompanhamentos.AsNoTracking().Where(c => pacientes.Contains(c.PacienteId))
            .Select(c => new { c.Id, c.PacienteId, c.Tipo, c.Modalidade, c.CriadoEm }).ToListAsync(ct);
        if (casos.Count == 0) return [];
        var idsCasos = casos.Select(c => c.Id).ToList();
        var contatos = (await db.ContatosAcompanhamento.AsNoTracking().Where(c => idsCasos.Contains(c.AcompanhamentoPacienteId))
            .Select(c => new { c.AcompanhamentoPacienteId, c.Em, c.Resultado, c.ResponsavelId, c.Canal }).ToListAsync(ct))
            .ToLookup(c => c.AcompanhamentoPacienteId);

        // As sessões do paciente desde o começo do caso mais antigo: é com elas que se sabe se
        // esta foi a PRIMEIRA volta (só a primeira fecha o fluxo — a segunda já é tratamento).
        var menorInicio = DateOnly.FromDateTime(casos.Min(c => c.CriadoEm));
        var todasSessoes = (await db.Atendimentos.AsNoTracking()
            .Where(a => pacientes.Contains(a.PacienteId) && a.RealizadoEm != null && a.EstornadoEm == null && a.Data >= menorInicio && a.Data <= fim)
            .Select(a => new { a.Id, a.PacienteId, a.Data, a.Modalidade }).ToListAsync(ct)).ToLookup(a => a.PacienteId);

        var achadas = new List<(int AtendimentoId, int CasoId, TipoAcompanhamento Tipo, int UsuarioId, DateTime AssumidoEm, bool TeveContato, int Tentativas)>();
        foreach (var s in sessoes)
        {
            var fimDaSessao = s.RealizadoEm ?? s.Data.ToDateTime(TimeOnly.MaxValue);
            (int CasoId, TipoAcompanhamento Tipo, int UsuarioId, DateTime AssumidoEm, DateTime UltimoDono, bool TeveContato, int Tentativas)? melhor = null;
            foreach (var c in casos.Where(c => c.PacienteId == s.PacienteId && RegrasAcompanhamento.Corresponde(c.Tipo, c.Modalidade, s.Modalidade)))
            {
                var doCaso = contatos[c.Id].Where(x => x.Em <= fimDaSessao).OrderBy(x => x.Em).ToList();
                // O ciclo vigente na hora da sessão começa no último registro de abertura (novo
                // recall / indicação). Contato de um ciclo ANTERIOR não assume o ciclo de agora.
                var abertura = doCaso.LastOrDefault(x => x.Resultado is "Novo ciclo de recall" or "Indicação de novo paciente BSV")?.Em ?? c.CriadoEm;
                var primeiraVolta = todasSessoes[s.PacienteId]
                    .Where(a => RegrasAcompanhamento.Corresponde(c.Tipo, c.Modalidade, a.Modalidade) && a.Data >= DateOnly.FromDateTime(abertura))
                    .OrderBy(a => a.Data).ThenBy(a => a.Id).FirstOrDefault();
                // Sessão estornada não está em todasSessoes — mas a decisão sobre ela pode existir;
                // para ela, o "primeira volta" se confere pela própria data.
                if (primeiraVolta is not null && primeiraVolta.Id != s.Id && s.EstornadoEm is null) continue;
                var donos = doCaso.Where(x => x.Em >= abertura && x.ResponsavelId != null && !RegistrosDoSistema.Contains(x.Resultado)).ToList();
                if (donos.Count == 0) continue; // voltou sem ninguém ter assumido: não é recuperação de ninguém
                var dono = donos[^1].ResponsavelId!.Value;
                var dela = donos.Where(x => x.ResponsavelId == dono).ToList();
                var tentativas = dela.Count(x => x.Canal != null);
                var candidato = (c.Id, c.Tipo, dono, dela[0].Em, donos[^1].Em, tentativas > 0, tentativas);
                // Dois casos fechados pela mesma sessão (recall de BSV e indicação de BSV): uma
                // comissão só, de quem assumiu POR ÚLTIMO.
                if (melhor is null || candidato.Item5 > melhor.Value.UltimoDono) melhor = candidato;
            }
            if (melhor is { } m) achadas.Add((s.Id, m.CasoId, m.Tipo, m.UsuarioId, m.AssumidoEm, m.TeveContato, m.Tentativas));
        }
        if (achadas.Count == 0) return [];

        var idsSessoes = achadas.Select(a => a.AtendimentoId).ToList();
        var decisoes = await db.ComissoesRecuperacao.AsNoTracking()
            .Where(c => idsSessoes.Contains(c.AtendimentoId) && c.CanceladaEm == null).ToDictionaryAsync(c => c.AtendimentoId, ct);
        var comGuia = (await db.Codigos.AsNoTracking()
            .Where(c => idsSessoes.Contains(c.AtendimentoId) && c.Status != StatusCodigo.NaoAplicavel)
            .Select(c => c.AtendimentoId).Distinct().ToListAsync(ct)).ToHashSet();
        var profissionais = await db.Agendamentos.AsNoTracking()
            .Where(a => a.AtendimentoId != null && idsSessoes.Contains(a.AtendimentoId.Value) && a.ProfissionalId != null)
            .Select(a => new { AtendimentoId = a.AtendimentoId!.Value, Nome = a.Profissional!.Nome })
            .ToListAsync(ct);
        var profissionalPorSessao = profissionais.GroupBy(p => p.AtendimentoId).ToDictionary(g => g.Key, g => g.First().Nome);
        var nomesPacientes = await db.Pacientes.AsNoTracking().Where(p => pacientes.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Nome, ct);
        var idsUsuarios = achadas.Select(a => a.UsuarioId).Distinct().ToList();
        var usuarios = await db.Usuarios.AsNoTracking().Where(u => idsUsuarios.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Nome + (u.Ativo ? "" : " (inativo)"), ct);
        var regras = await db.RegrasComissaoRecuperacao.AsNoTracking().ToListAsync(ct);
        var porId = sessoes.ToDictionary(s => s.Id);

        var linhas = new List<LinhaRecuperacao>();
        foreach (var a in achadas)
        {
            var s = porId[a.AtendimentoId];
            decisoes.TryGetValue(s.Id, out var d);
            // Estornada e sem decisão: o fluxo desfez — não é recuperação a validar.
            if (s.EstornadoEm != null && d is null) continue;
            var regra = ComissaoRecuperacaoRegras.Vigente(regras, a.Tipo, s.Modalidade, s.Data);
            var situacao = d?.Decisao switch
            {
                DecisaoComissaoRecuperacao.Validada => SituacaoRecuperacao.Validada,
                DecisaoComissaoRecuperacao.Recusada => SituacaoRecuperacao.Recusada,
                _ => regra is null ? SituacaoRecuperacao.SemValor : SituacaoRecuperacao.AValidar
            };
            linhas.Add(new LinhaRecuperacao(s.Id, a.CasoId, s.PacienteId, nomesPacientes.GetValueOrDefault(s.PacienteId, "—"),
                a.Tipo, s.Modalidade, a.UsuarioId, usuarios.GetValueOrDefault(a.UsuarioId, "—"), a.AssumidoEm, s.Data,
                profissionalPorSessao.GetValueOrDefault(s.Id, "—"), a.TeveContato, a.Tentativas, comGuia.Contains(s.Id),
                s.EstornadoEm != null, situacao, regra?.Valor, regra is null ? null : ComissaoRecuperacaoRegras.Descrever(regra),
                d?.Id, d?.Valor, d?.Observacao, d?.DecididaPor, d?.DecididaEm));
        }
        return linhas.OrderBy(l => l.Situacao).ThenBy(l => l.Atendente).ThenBy(l => l.RetornoEm).ThenBy(l => l.Paciente).ToList();
    }

    public async Task<int> ValidarAsync(IReadOnlyList<int> atendimentoIds, string operador, CancellationToken ct = default)
    {
        if (atendimentoIds.Count == 0) return 0;
        var linhas = await LinhasAsync(atendimentoIds, ct);
        var logins = await LoginsAsync(linhas, ct);
        var erros = linhas.Select(l => ComissaoRecuperacaoRegras.ImpedimentoDeValidar(l, operador, logins.GetValueOrDefault(l.UsuarioId, ""))).OfType<string>().ToList();
        if (linhas.Count < atendimentoIds.Distinct().Count()) erros.Add("Alguma recuperação saiu da lista (a sessão foi estornada ou outra pessoa decidiu) — atualize a tela.");
        if (erros.Count > 0) throw new InvalidOperationException(string.Join("\n", erros));
        foreach (var l in linhas)
        {
            db.ComissoesRecuperacao.Add(new ComissaoRecuperacao
            {
                AcompanhamentoPacienteId = l.AcompanhamentoId, AtendimentoId = l.AtendimentoId, PacienteId = l.PacienteId,
                UsuarioId = l.UsuarioId, AssumidoEm = l.AssumidoEm, RetornoEm = l.RetornoEm, Decisao = DecisaoComissaoRecuperacao.Validada,
                Valor = l.ValorProposto!.Value, RegraDescricao = l.RegraProposta, DecididaEm = Agora, DecididaPor = operador
            });
            db.Auditoria.Add(new EventoAuditoria { Operador = operador, Acao = "ComissaoRecuperacaoValidada",
                Detalhe = $"{l.Atendente} · {l.Paciente} · retorno em {l.RetornoEm:dd/MM/yyyy} · {l.ValorProposto:C}" });
        }
        await SalvarAsync(ct);
        return linhas.Count;
    }

    public async Task RecusarAsync(int atendimentoId, string justificativa, string operador, CancellationToken ct = default)
    {
        justificativa = justificativa?.Trim() ?? "";
        if (justificativa.Length < 5) throw new InvalidOperationException("Escreva por que a recuperação foi recusada (pelo menos 5 caracteres).");
        var l = (await LinhasAsync([atendimentoId], ct)).SingleOrDefault()
                ?? throw new InvalidOperationException("A recuperação saiu da lista — atualize a tela.");
        if (l.Situacao is SituacaoRecuperacao.Validada or SituacaoRecuperacao.Recusada)
            throw new InvalidOperationException("Esta recuperação já foi decidida — desfaça a decisão antes.");
        db.ComissoesRecuperacao.Add(new ComissaoRecuperacao
        {
            AcompanhamentoPacienteId = l.AcompanhamentoId, AtendimentoId = l.AtendimentoId, PacienteId = l.PacienteId,
            UsuarioId = l.UsuarioId, AssumidoEm = l.AssumidoEm, RetornoEm = l.RetornoEm, Decisao = DecisaoComissaoRecuperacao.Recusada,
            Valor = 0, Observacao = justificativa, DecididaEm = Agora, DecididaPor = operador
        });
        db.Auditoria.Add(new EventoAuditoria { Operador = operador, Acao = "ComissaoRecuperacaoRecusada",
            Detalhe = $"{l.Atendente} · {l.Paciente} · {justificativa}" });
        await SalvarAsync(ct);
    }

    public async Task CancelarDecisaoAsync(int comissaoId, string motivo, string operador, CancellationToken ct = default)
    {
        motivo = motivo?.Trim() ?? "";
        if (motivo.Length < 5) throw new InvalidOperationException("Escreva o motivo para desfazer a decisão (pelo menos 5 caracteres).");
        var c = await db.ComissoesRecuperacao.SingleOrDefaultAsync(x => x.Id == comissaoId, ct)
                ?? throw new InvalidOperationException("Decisão não encontrada — atualize a tela.");
        if (c.CanceladaEm != null) return;
        c.CanceladaEm = Agora; c.CanceladaPor = operador; c.MotivoCancelamento = motivo;
        db.Auditoria.Add(new EventoAuditoria { Operador = operador, Acao = "ComissaoRecuperacaoDesfeita",
            Detalhe = $"{c.Decisao} de {c.Valor:C} (retorno em {c.RetornoEm:dd/MM/yyyy}) · {motivo}" });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Reapura as linhas pedidas pela MESMA regra da tela — a validação não confia no que o clique viu.</summary>
    private async Task<IReadOnlyList<LinhaRecuperacao>> LinhasAsync(IReadOnlyList<int> atendimentoIds, CancellationToken ct)
    {
        var datas = await db.Atendimentos.AsNoTracking().Where(a => atendimentoIds.Contains(a.Id)).Select(a => a.Data).ToListAsync(ct);
        if (datas.Count == 0) return [];
        return (await ApurarAsync(datas.Min(), datas.Max(), ct)).Where(l => atendimentoIds.Contains(l.AtendimentoId)).ToList();
    }

    private async Task<Dictionary<int, string>> LoginsAsync(IEnumerable<LinhaRecuperacao> linhas, CancellationToken ct)
    {
        var ids = linhas.Select(l => l.UsuarioId).Distinct().ToList();
        return await db.Usuarios.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Login, ct);
    }

    private async Task SalvarAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            throw new InvalidOperationException("Outra pessoa decidiu esta recuperação ao mesmo tempo — atualize a tela.");
        }
    }
}
