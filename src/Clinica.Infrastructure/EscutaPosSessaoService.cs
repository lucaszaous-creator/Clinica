using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Infrastructure;

/// <summary>
/// A ESCUTA PÓS-SESSÃO (nov/2026, Fila 360) — ver <see cref="IEscutaPosSessaoService"/> e <see cref="EscutaPosSessao"/>.
/// As regras (quem pede escuta, o que o registro exige) são de <see cref="EscutaNaTela"/>; aqui moram as barreiras,
/// as leituras e a gravação, com a auditoria no MESMO SaveChanges do ato.
/// </summary>
public sealed class EscutaPosSessaoService(ClinicaDbContext db, IClinicaRepositorio repo, Func<DateTime>? relogio = null) : IEscutaPosSessaoService
{
    private DateTime Agora => relogio?.Invoke() ?? DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,
        TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")), DateTimeKind.Unspecified);
    private DateOnly Hoje => DateOnly.FromDateTime(Agora);

    private const string SemAcesso = "Seu acesso não permite esta ação.";

    /// <summary>O login ativo, com o acesso do acompanhamento (e o do prontuário quando o ato é dado de saúde).</summary>
    private async Task<UsuarioSistema> Usuario(int id, bool prontuario, CancellationToken ct)
    {
        var u = await db.Usuarios.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (u is not { Ativo: true } || u.Travado(Agora) || !AcompanhamentoPacienteService.PodeAcompanhar(u)
            || prontuario && !u.Pode(Permissao.VerProntuario))
            throw new UnauthorizedAccessException(SemAcesso);
        return u;
    }

    /// <summary>O profissional que lê o aviso: login ativo com o prontuário (não precisa do acesso do acompanhamento).</summary>
    private async Task<UsuarioSistema> Leitor(int id, CancellationToken ct)
    {
        var u = await db.Usuarios.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (u is not { Ativo: true } || u.Travado(Agora) || !u.Pode(Permissao.VerProntuario))
            throw new UnauthorizedAccessException(SemAcesso);
        return u;
    }

    public async Task<IReadOnlyList<SessaoParaEscutar>> ParaEscutarAsync(int usuarioId, CancellationToken ct = default)
    {
        await Usuario(usuarioId, prontuario: false, ct);
        var hoje = Hoje;
        var inicio = hoje.AddDays(-EscutaNaTela.DiasParaEscutar);
        var naJanela = await db.Atendimentos.AsNoTracking()
            .Where(a => a.RealizadoEm != null && a.EstornadoEm == null && a.Data >= inicio && a.Data <= hoje && a.Modalidade != ModalidadeAtendimento.Consulta)
            .Select(a => new SessaoRealizada(a.Id, a.PacienteId, a.Data, a.Modalidade)).ToListAsync(ct);
        if (naJanela.Count == 0) return [];

        var idsSessao = naJanela.Select(s => s.AtendimentoId).ToList();
        var escutadas = (await db.EscutasPosSessao.AsNoTracking().Where(e => e.CanceladaEm == null && idsSessao.Contains(e.AtendimentoId))
            .Select(e => e.AtendimentoId).ToListAsync(ct)).ToHashSet();
        var pendentes = naJanela.Where(s => !escutadas.Contains(s.AtendimentoId)).ToList();
        if (pendentes.Count == 0) return [];

        var motivos = await MotivosAsync(pendentes, hoje, ct);
        var pedem = pendentes.Where(s => motivos.ContainsKey(s.AtendimentoId)).ToList();
        if (pedem.Count == 0) return [];

        // Sequencial no mesmo contexto: é o mesmo DbContext.
        var idsPacientes = pedem.Select(s => s.PacienteId).Distinct().ToList();
        var pacientes = await db.Pacientes.AsNoTracking().Where(p => idsPacientes.Contains(p.Id))
            .Select(p => new { p.Id, p.Nome, p.Telefone }).ToDictionaryAsync(p => p.Id, ct);
        var idsPedem = pedem.Select(s => (int?)s.AtendimentoId).ToList();
        var profissionalDaSessao = (await db.Agendamentos.AsNoTracking().Where(a => idsPedem.Contains(a.AtendimentoId))
                .Select(a => new { a.AtendimentoId, a.ProfissionalId }).ToListAsync(ct))
            .GroupBy(a => a.AtendimentoId!.Value).ToDictionary(g => g.Key, g => g.Select(x => x.ProfissionalId).FirstOrDefault(p => p != null));
        var idsProf = profissionalDaSessao.Values.Where(p => p != null).Select(p => p!.Value).Distinct().ToList();
        var nomes = await db.Profissionais.AsNoTracking().Where(p => idsProf.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Nome, ct);
        // Só quem RECUSOU fica sem mensagem (a mesma regra da fila do acompanhamento).
        var liberados = (await repo.PacientesComMensagensLiberadasAsync(idsPacientes, ct)).ToHashSet();

        return pedem
            .Where(s => pacientes.ContainsKey(s.PacienteId))
            .Select(s =>
            {
                var p = pacientes[s.PacienteId];
                var prof = profissionalDaSessao.GetValueOrDefault(s.AtendimentoId);
                return new SessaoParaEscutar(s.AtendimentoId, s.PacienteId, p.Nome, p.Telefone, s.Data, s.Modalidade,
                    prof, prof is { } id && nomes.TryGetValue(id, out var nome) ? nome : "—", motivos[s.AtendimentoId],
                    !string.IsNullOrWhiteSpace(p.Telefone) && liberados.Contains(s.PacienteId));
            })
            .OrderBy(s => s.Motivo == MotivoDaEscuta.VoltouPelaRetencao ? 0 : 1).ThenBy(s => s.Data).ThenBy(s => s.Paciente)
            .ToList();
    }

    /// <summary>
    /// Por que cada sessão pede escuta (só as que pedem entram no dicionário): as sessões anteriores dos pacientes e a
    /// volta dos casos de RECALL (a do novo no BSV já é a 1ª sessão), pela mesma regra da fila.
    /// </summary>
    private async Task<Dictionary<int, MotivoDaEscuta>> MotivosAsync(IReadOnlyList<SessaoRealizada> sessoes, DateOnly ate, CancellationToken ct)
    {
        var idsPacientes = sessoes.Select(s => s.PacienteId).Distinct().ToList();
        var historico = (await db.Atendimentos.AsNoTracking()
                .Where(a => idsPacientes.Contains(a.PacienteId) && a.RealizadoEm != null && a.EstornadoEm == null && a.Data <= ate)
                .Select(a => new { a.Id, a.PacienteId, a.Data, a.Modalidade, a.RealizadoEm }).ToListAsync(ct))
            .ToLookup(a => a.PacienteId);
        var recalls = (await db.Acompanhamentos.AsNoTracking()
                .Where(c => idsPacientes.Contains(c.PacienteId) && c.Tipo == TipoAcompanhamento.Recall)
                .Select(c => new { c.PacienteId, c.Tipo, c.Modalidade, c.ReferenciaEm }).ToListAsync(ct))
            .ToLookup(c => c.PacienteId);

        var motivos = new Dictionary<int, MotivoDaEscuta>();
        foreach (var s in sessoes)
        {
            var doPaciente = historico[s.PacienteId].ToList();
            var voltas = recalls[s.PacienteId]
                .Select(c => AcompanhamentoPacienteService.PrimeiraVolta(c.Tipo, c.Modalidade, c.ReferenciaEm,
                    doPaciente.Select(a => (a.Modalidade, a.Data, a.RealizadoEm))))
                .Where(v => v is not null).Select(v => v!.Value).ToList();
            var motivo = EscutaNaTela.Motivo(s, doPaciente.Select(a => new SessaoRealizada(a.Id, a.PacienteId, a.Data, a.Modalidade)), voltas);
            if (motivo is { } m) motivos[s.AtendimentoId] = m;
        }
        return motivos;
    }

    public async Task<IReadOnlyList<EscutaPosSessao>> DoPacienteAsync(int usuarioId, int pacienteId, CancellationToken ct = default)
    {
        await Usuario(usuarioId, prontuario: true, ct);
        return await db.EscutasPosSessao.AsNoTracking().Where(e => e.PacienteId == pacienteId)
            .OrderByDescending(e => e.SessaoEm).ThenByDescending(e => e.Id).ToListAsync(ct);
    }

    public async Task<EscutaPosSessao> RegistrarAsync(int usuarioId, RegistrarEscuta pedido, CancellationToken ct = default)
    {
        var u = await Usuario(usuarioId, prontuario: true, ct);
        if (EscutaNaTela.Recusa(pedido.Como, pedido.Relato) is { } recusa) throw new InvalidOperationException(recusa);
        if (!Enum.IsDefined(pedido.Canal)) throw new InvalidOperationException("Confira por onde a resposta veio.");

        var sessao = await db.Atendimentos.AsNoTracking()
            .Where(a => a.Id == pedido.AtendimentoId && a.RealizadoEm != null && a.EstornadoEm == null)
            .Select(a => new SessaoRealizada(a.Id, a.PacienteId, a.Data, a.Modalidade)).SingleOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("A sessão não foi encontrada (ou foi estornada). Atualize a fila.");

        var relato = string.IsNullOrWhiteSpace(pedido.Relato) ? null : pedido.Relato.Trim();
        var existente = await db.EscutasPosSessao.AsNoTracking().FirstOrDefaultAsync(e => e.AtendimentoId == sessao.AtendimentoId && e.CanceladaEm == null, ct);
        if (existente is not null)
        {
            // O segundo clique (ou a resposta perdida) com o MESMO conteúdo devolve a mesma escuta.
            if (existente.Como == pedido.Como && existente.Relato == relato) return existente;
            throw new InvalidOperationException($"Esta sessão já tem escuta registrada por {existente.RegistradoPor}. Para corrigir, cancele-a com o motivo e registre de novo.");
        }

        var motivos = await MotivosAsync([sessao], sessao.Data, ct);
        if (!motivos.TryGetValue(sessao.AtendimentoId, out var motivo))
            throw new InvalidOperationException("Esta sessão não pede escuta: ela não é a primeira do tratamento nem a volta pela retenção.");
        var profissional = await db.Agendamentos.AsNoTracking().Where(a => a.AtendimentoId == sessao.AtendimentoId && a.ProfissionalId != null)
            .Select(a => a.ProfissionalId).FirstOrDefaultAsync(ct);

        var agora = Agora;
        var escuta = new EscutaPosSessao
        {
            PacienteId = sessao.PacienteId, AtendimentoId = sessao.AtendimentoId, SessaoEm = sessao.Data, Modalidade = sessao.Modalidade,
            ProfissionalId = profissional, Motivo = motivo, Como = pedido.Como, Relato = relato, Canal = pedido.Canal,
            RegistradoEm = agora, RegistradoPor = u.Login
        };
        db.EscutasPosSessao.Add(escuta);
        db.Auditoria.Add(new EventoAuditoria
        {
            DataHora = agora, Operador = u.Login, Acao = "EscutaRegistrada", PacienteId = sessao.PacienteId,
            Detalhe = $"{EscutaNaTela.Rotulo(pedido.Como)} · sessão de {sessao.Data:dd/MM/yyyy} · {EscutaNaTela.Rotulo(motivo)}"
        });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            throw new InvalidOperationException("Outra pessoa acabou de registrar a escuta desta sessão. Atualize a fila.");
        }
        return escuta;
    }

    public async Task CancelarAsync(int usuarioId, int escutaId, string motivo, CancellationToken ct = default)
    {
        var u = await Usuario(usuarioId, prontuario: true, ct);
        var texto = motivo?.Trim() ?? string.Empty;
        if (texto.Length < 5) throw new InvalidOperationException("Escreva o motivo do cancelamento (pelo menos 5 caracteres).");
        if (texto.Length > 500) throw new InvalidOperationException("O motivo passa de 500 caracteres.");
        var e = await db.EscutasPosSessao.SingleOrDefaultAsync(x => x.Id == escutaId, ct)
            ?? throw new InvalidOperationException("A escuta não foi encontrada.");
        if (e.CanceladaEm is not null) return;
        var agora = Agora;
        e.CanceladaEm = agora;
        e.CanceladaPor = u.Login;
        e.MotivoCancelamento = texto;
        db.Auditoria.Add(new EventoAuditoria
        {
            DataHora = agora, Operador = u.Login, Acao = "EscutaCancelada", PacienteId = e.PacienteId,
            Detalhe = $"Escuta da sessão de {e.SessaoEm:dd/MM/yyyy} cancelada: {texto}"
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<AvisoDePiora>> AvisosDoProfissionalAsync(int usuarioId, CancellationToken ct = default)
    {
        var u = await Leitor(usuarioId, ct);
        if (u.ProfissionalId is not { } profissional) return [];
        return await db.EscutasPosSessao.AsNoTracking()
            .Where(e => e.ProfissionalId == profissional && e.Como == ComoEstaPaciente.Pior && e.CanceladaEm == null && e.CienteEm == null)
            .OrderByDescending(e => e.RegistradoEm)
            .Select(e => new AvisoDePiora(e.Id, e.PacienteId, e.Paciente!.Nome, e.SessaoEm, e.Modalidade, e.Relato, e.RegistradoEm, e.RegistradoPor))
            .ToListAsync(ct);
    }

    public async Task CienteAsync(int usuarioId, int escutaId, CancellationToken ct = default)
    {
        var u = await Leitor(usuarioId, ct);
        var e = await db.EscutasPosSessao.SingleOrDefaultAsync(x => x.Id == escutaId, ct)
            ?? throw new InvalidOperationException("A escuta não foi encontrada.");
        if (e.ProfissionalId != u.ProfissionalId && u.Perfil != PerfilAcesso.Gerente)
            throw new UnauthorizedAccessException("Só o profissional da sessão (ou a gerência) marca o aviso como lido.");
        if (e.CienteEm is not null) return;
        var agora = Agora;
        e.CienteEm = agora;
        e.CientePor = u.Login;
        db.Auditoria.Add(new EventoAuditoria
        {
            DataHora = agora, Operador = u.Login, Acao = "EscutaCiente", PacienteId = e.PacienteId,
            Detalhe = $"Ciente da piora relatada na escuta da sessão de {e.SessaoEm:dd/MM/yyyy}"
        });
        await db.SaveChangesAsync(ct);
    }
}
