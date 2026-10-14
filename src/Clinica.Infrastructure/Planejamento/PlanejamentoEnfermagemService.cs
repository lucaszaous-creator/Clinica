using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Clinica.Application.Abstracoes;
using Clinica.Application.Tablet;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Infrastructure.Planejamento;

/// <summary>Avisos compartilhados, sem atribuição de responsável ou escrita em Agendamentos.</summary>
public sealed class PlanejamentoEnfermagemService(ClinicaDbContext db, TimeProvider? tempo = null) : IPlanejamentoEnfermagemService
{
    private DateTime Agora => DateTime.SpecifyKind(TimeZoneInfo.ConvertTime((tempo ?? TimeProvider.System).GetUtcNow(),
        TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")).DateTime, DateTimeKind.Unspecified);
    private static string Hash(object valor) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(valor))));
    private static InvalidOperationException Erro(string texto) => ErroFormularioTablet.Criar(texto);
    private static string Modalidade(ModalidadeAtendimento m) => m == ModalidadeAtendimento.BsvApenas ? "BSV"
        : m == ModalidadeAtendimento.BsvComAcupuntura ? "BSV + acupuntura" : RotulosEnum.De(m);

    private async Task<UsuarioSistema> Usuario(int id, CancellationToken ct)
    {
        var u = await db.Usuarios.AsNoTracking().Include(x => x.Profissional).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (u is null || !AcessoPlanejamentoEnfermagem.Consultar(u) || u.Travado(Agora)
            || u.Perfil == PerfilAcesso.Enfermagem && u.Profissional?.Ativo != true)
            throw new UnauthorizedAccessException("Seu acesso não permite consultar o planejamento da enfermagem.");
        return u;
    }

    private void ValidarPeriodo(DateTime inicio, DateTime fim, int pagina)
    {
        if (inicio.Kind != DateTimeKind.Unspecified || fim.Kind != DateTimeKind.Unspecified || inicio >= fim
            || fim - inicio > TimeSpan.FromDays(1) || inicio.Date != fim.AddTicks(-1).Date
            || Math.Abs((inicio.Date - Agora.Date).TotalDays) > 366 || pagina is < 0 or > 10000)
            throw Erro("Escolha uma data no intervalo de um ano e um período dentro do mesmo dia.");
    }

    public async Task<ResumoPeriodoEnfermagem> PeriodoAsync(int usuarioId, PeriodoEnfermagem periodo, CancellationToken ct = default)
    {
        await Usuario(usuarioId, ct);
        ValidarPeriodo(periodo.Inicio, periodo.Fim, periodo.Pagina);
        return await LerPeriodo(periodo, ct);
    }

    public async Task<IReadOnlyList<DiaPlanejamentoEnfermagem>> SemanaAsync(int usuarioId, SemanaEnfermagem semana, CancellationToken ct = default)
    {
        await Usuario(usuarioId, ct);
        var inicio = semana.Inicio.ToDateTime(TimeOnly.MinValue); var fim = inicio.AddDays(7);
        ValidarPeriodo(inicio, inicio.AddDays(1), 0); ValidarPeriodo(fim.AddDays(-1), fim, 0);
        // A visão semanal traz somente contagens; nomes são consultados ao abrir o dia.
        var horarios = await ConsultaHorarios(inicio, fim).Select(a => new { a.DataHora, a.ModalidadePrevista,
            Duracao = a.DuracaoMinutos ?? (a.Profissional != null ? a.Profissional.DuracaoPadraoMinutos : null) ?? 30 }).ToListAsync(ct);
        var bloqueios = await db.BloqueiosAgenda.AsNoTracking().Where(b => b.Inicio < fim && b.Fim > inicio)
            .Select(b => new { b.Inicio, b.Fim }).ToListAsync(ct);
        return Enumerable.Range(0, 7).Select(i =>
        {
            var dia = inicio.AddDays(i); var ate = dia.AddDays(1);
            var linhas = horarios.Where(a => a.DataHora < ate && a.DataHora.AddMinutes(a.Duracao) > dia).ToList();
            return new DiaPlanejamentoEnfermagem(DateOnly.FromDateTime(dia), linhas.Count,
                linhas.Count(a => a.ModalidadePrevista == ModalidadeAtendimento.BsvApenas),
                linhas.Count(a => a.ModalidadePrevista == ModalidadeAtendimento.BsvComAcupuntura),
                bloqueios.Count(b => b.Inicio < ate && b.Fim > dia));
        }).ToList();
    }

    internal IQueryable<Agendamento> ConsultaHorarios(DateTime inicio, DateTime fim) => db.Agendamentos.AsNoTracking()
            .Where(a => (a.Status == StatusAgendamento.Agendado || a.Status == StatusAgendamento.Realizado)
                && a.DataHora < fim
                && a.DataHora.AddMinutes(a.DuracaoMinutos ?? (a.Profissional != null ? a.Profissional.DuracaoPadraoMinutos : null) ?? 30) > inicio)
            .OrderBy(a => a.DataHora).ThenBy(a => a.Id);

    private async Task<ResumoPeriodoEnfermagem> LerPeriodo(PeriodoEnfermagem periodo, CancellationToken ct)
    {
        // Os totais e a referência incluem TODAS as linhas; somente os detalhes são paginados.
        var linhas = await ConsultaHorarios(periodo.Inicio, periodo.Fim)
            .Select(a => new { a.Id, a.DataHora, a.ModalidadePrevista, a.ModalidadeCodigo, a.Status,
                a.ProfissionalId, Medico = a.Profissional != null ? a.Profissional.Nome : "Médico não informado",
                a.SalaId, Sala = a.Sala != null ? a.Sala.Nome : "Sala não informada",
                Duracao = a.DuracaoMinutos ?? (a.Profissional != null ? a.Profissional.DuracaoPadraoMinutos : null) ?? 30,
                Paciente = a.Paciente!.Nome, a.PacienteId }).ToListAsync(ct);
        var bloqueios = await db.BloqueiosAgenda.AsNoTracking().Where(b => b.Inicio < periodo.Fim && b.Fim > periodo.Inicio)
            .OrderBy(b => b.Id).Select(b => new { b.Id, b.Inicio, b.Fim, b.ProfissionalId, b.SalaId,
                Medico = b.Profissional != null ? b.Profissional.Nome : "", Sala = b.Sala != null ? b.Sala.Nome : "" }).ToListAsync(ct);
        var referencia = Hash(new { periodo.Inicio, periodo.Fim,
            Linhas = linhas.Select(a => new { a.Id, a.PacienteId, a.DataHora, a.Duracao, a.ModalidadePrevista, a.ModalidadeCodigo, a.Status, a.ProfissionalId, a.SalaId }),
            Bloqueios = bloqueios.Select(b => new { b.Id, b.Inicio, b.Fim, b.ProfissionalId, b.SalaId }) });
        var bsv = linhas.Count(a => a.ModalidadePrevista == ModalidadeAtendimento.BsvApenas);
        var dupla = linhas.Count(a => a.ModalidadePrevista == ModalidadeAtendimento.BsvComAcupuntura);
        return new(periodo.Inicio, periodo.Fim, linhas.Count, bsv, dupla, linhas.Count - bsv - dupla, referencia, Agora,
            linhas.Skip(periodo.Pagina * 50).Take(50).Select(a => new HorarioPlanejamento(a.Id, a.Paciente, a.DataHora,
                a.DataHora.AddMinutes(a.Duracao), Modalidade(a.ModalidadePrevista), a.Medico, a.Sala, RotulosEnum.De(a.Status))).ToList(),
            bloqueios.Select(b => new BloqueioPlanejamento(b.Inicio, b.Fim,
                string.Join(" · ", new[] { b.Medico, b.Sala }.Where(x => x.Length > 0)) is { Length: > 0 } recurso ? recurso : "Clínica")).ToList(),
            periodo.Pagina, linhas.Count > (periodo.Pagina + 1) * 50);
    }

    private IQueryable<SolicitacaoEnfermagem> Pedidos => db.Set<SolicitacaoEnfermagem>().Include(x => x.Autora);
    private bool Ativa(SolicitacaoEnfermagem p) => p.Situacao is "Novo" or "Visto" && p.Validade > Agora;
    private LinhaSolicitacaoEnfermagem Linha(SolicitacaoEnfermagem p, UsuarioSistema u) => new(p.Id, p.Inicio, p.Fim,
        p.Validade, Modalidade(p.Modalidade), p.Modalidade, p.Quantidade, p.Autora?.Nome ?? "Enfermagem", p.AutoraId,
        p.Situacao is "Novo" or "Visto" && !Ativa(p) ? "Expirado" : p.Situacao, p.Observacao, p.VistaEm, p.VistaPor,
        p.TotalReferencia, Ativa(p), Ativa(p) && AcessoPlanejamentoEnfermagem.Tratar(u),
        Ativa(p) && p.AutoraId == u.Id && AcessoPlanejamentoEnfermagem.Solicitar(u), p.Versao);

    public async Task<IReadOnlyList<LinhaSolicitacaoEnfermagem>> ListarAsync(int usuarioId, DateOnly? dia = null, CancellationToken ct = default)
    {
        var u = await Usuario(usuarioId, ct);
        var de = dia?.ToDateTime(TimeOnly.MinValue) ?? Agora.Date.AddDays(-30);
        var ate = dia.HasValue ? de.AddDays(1) : Agora.Date.AddDays(367);
        if (Math.Abs((de - Agora.Date).TotalDays) > 366) throw Erro("Escolha uma data no intervalo de um ano.");
        // A caixa geral mostra avisos da equipe inteira, independentemente do filtro de médico da agenda.
        var pedidos = await Pedidos.AsNoTracking().Where(x => x.Inicio >= de && x.Inicio < ate)
            .OrderBy(x => x.Inicio).ThenBy(x => x.Id).ToListAsync(ct);
        return pedidos.Select(p => Linha(p, u)).OrderByDescending(p => p.Ativa && p.Situacao == "Novo")
            .ThenByDescending(p => p.Ativa).ThenBy(p => p.Inicio).ToList();
    }

    public async Task<(int Novos, int Abertos)> ContarAsync(int usuarioId, CancellationToken ct = default)
    {
        var u = await Usuario(usuarioId, ct);
        if (!AcessoPlanejamentoEnfermagem.Tratar(u)) throw new UnauthorizedAccessException();
        var agora = Agora;
        var contagens = await db.Set<SolicitacaoEnfermagem>().AsNoTracking()
            .Where(p => (p.Situacao == "Novo" || p.Situacao == "Visto") && p.Validade > agora)
            .GroupBy(p => p.Situacao).Select(g => new { Situacao = g.Key, Total = g.Count() }).ToListAsync(ct);
        return (contagens.Where(c => c.Situacao == "Novo").Sum(c => c.Total), contagens.Sum(c => c.Total));
    }

    public async Task<DetalheSolicitacaoEnfermagem> DetalheAsync(int usuarioId, int id, CancellationToken ct = default)
    {
        var u = await Usuario(usuarioId, ct);
        var p = await Pedidos.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new RecursoClinicoIndisponivel();
        var periodo = await LerPeriodo(new(p.Inicio, p.Fim), ct);
        var eventos = await db.Set<EventoSolicitacaoEnfermagem>().AsNoTracking().Where(x => x.SolicitacaoEnfermagemId == id)
            .OrderBy(x => x.Id).Select(x => new EventoPlanejamento(x.Em, x.Autor, x.Acao, x.Observacao)).ToListAsync(ct);
        return new(Linha(p, u), periodo, p.AgendaReferencia != periodo.Referencia, eventos);
    }

    public async Task<int> CriarAsync(int usuarioId, CriarSolicitacaoEnfermagem pedido, CancellationToken ct = default)
    {
        var u = await Usuario(usuarioId, ct);
        if (!AcessoPlanejamentoEnfermagem.Solicitar(u)) throw new UnauthorizedAccessException("Somente a enfermagem envia pedidos.");
        var hash = Hash(pedido);
        var anterior = await db.Set<SolicitacaoEnfermagem>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.AutoraId == u.Id && x.Idempotencia == pedido.Idempotencia, ct);
        if (anterior is not null)
        {
            if (anterior.PedidoHash != hash) throw new ConflitoClinicoTablet("Este envio já foi usado com outro conteúdo. Atualize a tela.");
            return anterior.Id;
        }
        ValidarPeriodo(pedido.Inicio, pedido.Fim, 0);
        if (pedido.Idempotencia == Guid.Empty || pedido.Inicio <= Agora || pedido.Validade.Kind != DateTimeKind.Unspecified
            || pedido.Validade <= Agora || pedido.Validade > pedido.Inicio || pedido.Quantidade is < 1 or > 30
            || pedido.Modalidade is not (ModalidadeAtendimento.BsvApenas or ModalidadeAtendimento.BsvComAcupuntura)
            || pedido.Observacao is null || pedido.Observacao.Length > 1000)
            throw Erro("Informe um período futuro, de 1 a 30 atendimentos BSV e uma validade até o início do período.");
        var periodo = await LerPeriodo(new(pedido.Inicio, pedido.Fim), ct);
        if (pedido.AgendaReferencia != periodo.Referencia)
            throw new ConflitoClinicoTablet("A agenda mudou. Atualize e confira o período antes de enviar o pedido.");
        var p = new SolicitacaoEnfermagem { AutoraId = u.Id, Idempotencia = pedido.Idempotencia, PedidoHash = hash,
            Inicio = pedido.Inicio, Fim = pedido.Fim, Validade = pedido.Validade, CriadaEm = Agora,
            Modalidade = pedido.Modalidade, Quantidade = pedido.Quantidade, Observacao = pedido.Observacao.Trim(),
            AgendaReferencia = periodo.Referencia, TotalReferencia = periodo.Total };
        p.Eventos.Add(new() { Idempotencia = pedido.Idempotencia, PedidoHash = hash, UsuarioId = u.Id,
            Autor = u.Nome, Em = Agora, Acao = "Pedido enviado", Observacao = p.Observacao });
        db.Set<SolicitacaoEnfermagem>().Add(p);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            var repetido = await db.Set<SolicitacaoEnfermagem>().AsNoTracking()
                .SingleOrDefaultAsync(x => x.AutoraId == u.Id && x.Idempotencia == pedido.Idempotencia, ct);
            if (repetido?.PedidoHash == hash) return repetido.Id;
            throw;
        }
        return p.Id;
    }

    public async Task AtualizarAsync(int usuarioId, int id, AcaoSolicitacaoEnfermagem pedido, CancellationToken ct = default)
    {
        var u = await Usuario(usuarioId, ct);
        if (pedido.Idempotencia == Guid.Empty || pedido.Observacao is null || pedido.Observacao.Length > 1000
            || pedido.Acao is not ("visto" or "encerrar" or "retirar")) throw Erro("Confira a ação e a observação do aviso.");
        if (pedido.Acao == "retirar" ? !AcessoPlanejamentoEnfermagem.Solicitar(u) : !AcessoPlanejamentoEnfermagem.Tratar(u))
            throw new UnauthorizedAccessException("Seu acesso não permite esta ação.");
        var p = await Pedidos.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new RecursoClinicoIndisponivel();
        if (pedido.Acao == "retirar" && p.AutoraId != u.Id) throw new UnauthorizedAccessException("Somente a autora pode retirar este pedido.");
        var hash = Hash(pedido);
        var evento = await db.Set<EventoSolicitacaoEnfermagem>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.SolicitacaoEnfermagemId == id && x.Idempotencia == pedido.Idempotencia, ct);
        if (evento is not null)
        {
            if (evento.UsuarioId != u.Id || evento.PedidoHash != hash) throw new ConflitoClinicoTablet("O envio já foi utilizado. Atualize o aviso.");
            return;
        }
        // A leitura compartilhada não atribui responsabilidade; duas pessoas abrindo não criam duas tarefas.
        if (pedido.Acao == "visto" && (p.VistaEm != null || !Ativa(p))) return;
        if (p.Versao != pedido.Versao) throw new ConflitoClinicoTablet("Este aviso foi atualizado por outra pessoa. Atualize antes de continuar.");
        if (!Ativa(p)) throw Erro("Este aviso já foi encerrado, retirado ou expirou.");
        if (pedido.Acao == "visto") { p.Situacao = "Visto"; p.VistaEm = Agora; p.VistaPor = u.Nome; }
        if (pedido.Acao == "encerrar") p.Situacao = "Encerrado";
        if (pedido.Acao == "retirar") p.Situacao = "Retirado";
        p.Versao = Guid.NewGuid();
        p.Eventos.Add(new() { Idempotencia = pedido.Idempotencia, PedidoHash = hash, UsuarioId = u.Id, Autor = u.Nome,
            Em = Agora, Acao = pedido.Acao == "visto" ? "Visto pela recepção" : pedido.Acao == "retirar" ? "Pedido retirado" : "Aviso encerrado",
            Observacao = pedido.Observacao.Trim() });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            if (pedido.Acao == "visto" && await db.Set<SolicitacaoEnfermagem>().AnyAsync(x => x.Id == id && x.VistaEm != null, ct)) return;
            throw new ConflitoClinicoTablet("O aviso mudou durante o envio. Atualize para conferir o resultado.");
        }
    }
}
