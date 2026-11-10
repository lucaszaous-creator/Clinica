using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Infrastructure;

/// <summary>Uma mensagem da liberação que o CRM ainda precisa mandar.</summary>
public sealed record AvisoDaLiberacao(int Id, string Telefone, string Texto);

/// <summary>
/// O PEDIDO DE LIBERAÇÃO DA TRAVA DE CONSULTA, de ponta a ponta (nov/2026, pedido da gerência):
/// a secretária pede → a gerência recebe no WhatsApp e na conversa com o sistema → responde
/// "sim" ou "não" → liberado, o horário é marcado pela MESMA agenda de sempre, a secretária é
/// avisada e o paciente recebe a confirmação.
///
/// Quem decide é um LOGIN: o celular que responde é o cadastrado em Acessos para aquela pessoa
/// (o mesmo mecanismo do WhatsApp da equipe), e ela precisa estar marcada como quem aprova
/// (<see cref="UsuarioSistema.AprovaLiberacaoDeTrava"/>). O serviço usa o banco direto, como o
/// dos pedidos à recepção: ele roda no app (o pedido) e na ponte do CRM (a decisão), e a
/// ponte tem papel próprio no Postgres (deploy/ponte-crm/papel-ponte-crm.sql).
///
/// ⚠️ A decisão é reivindicada com um UPDATE condicional (pendente → decidido): dois gerentes
/// respondendo ao mesmo tempo não marcam dois horários — o segundo lê "já foi decidido".
/// </summary>
public sealed class LiberacaoDeTravaService : ILiberacaoDeTravaService
{
    private readonly ClinicaDbContext db;
    private readonly AgendaService agenda;
    private readonly Func<DateTime> agora;

    public LiberacaoDeTravaService(ClinicaDbContext db, AgendaService agenda) : this(db, agenda, () => DateTime.Now) { }

    public LiberacaoDeTravaService(ClinicaDbContext db, AgendaService agenda, Func<DateTime> agora)
    {
        this.db = db;
        this.agenda = agenda;
        this.agora = agora;
    }

    /// <summary>
    /// Para quem só tem o banco na mão (o assistente do WhatsApp, que a ponte constrói com o
    /// contexto): monta a agenda com as mesmas peças do app.
    /// </summary>
    public static LiberacaoDeTravaService Sobre(ClinicaDbContext db, Func<DateTime>? agora = null)
    {
        var repo = new ClinicaRepositorio(db);
        var parametros = new ParametrosService(repo);
        var atendimentos = new AtendimentoService(repo, parametros: parametros, consultas: new ConsultaService(repo));
        return new LiberacaoDeTravaService(db, new AgendaService(repo, atendimentos, parametros), agora ?? (() => DateTime.Now));
    }

    // ================================================================ pedir

    /// <summary>Quem aprova hoje: ativos e marcados em Acessos. A tela avisa quando não há ninguém.</summary>
    public async Task<IReadOnlyList<UsuarioSistema>> AprovadoresAsync(CancellationToken ct = default)
        => await db.Usuarios.AsNoTracking().Where(u => u.Ativo && u.AprovaLiberacaoDeTrava && u.Perfil == PerfilAcesso.Gerente).OrderBy(u => u.Nome).ToListAsync(ct);

    /// <summary>O pedido ainda aberto deste paciente para este dia, se houver — a tela mostra em vez de pedir de novo.</summary>
    public async Task<LiberacaoDeTrava?> PendenteAsync(int pacienteId, DateOnly dia, CancellationToken ct = default)
    {
        var (inicio, fim, momento) = (dia.ToDateTime(TimeOnly.MinValue), dia.AddDays(1).ToDateTime(TimeOnly.MinValue), agora());
        return await db.LiberacoesDeTrava.AsNoTracking()
            .Where(l => l.PacienteId == pacienteId && l.Estado == LiberacaoDeTrava.Pendente && l.ExpiraEm > momento
                        && l.DataHora >= inicio && l.DataHora < fim)
            .OrderByDescending(l => l.Id).FirstOrDefaultAsync(ct);
    }

    public async Task<PedidoDeLiberacaoCriado> PedirAsync(NovoPedidoDeLiberacao novo, int usuarioId, CancellationToken ct = default)
    {
        var motivo = (novo.Motivo ?? "").Trim();
        if (motivo.Length == 0)
            throw new InvalidOperationException("Diga o motivo do pedido — é ele que a gerência lê para decidir.");
        if (motivo.Length > LiberacaoDeTravaRegras.MotivoMaximo) motivo = motivo[..LiberacaoDeTravaRegras.MotivoMaximo];

        var eu = await db.Usuarios.AsNoTracking().SingleOrDefaultAsync(u => u.Id == usuarioId && u.Ativo, ct)
            ?? throw new InvalidOperationException("Usuário não encontrado.");
        if (!eu.Pode(Permissao.EditarAgenda))
            throw new UnauthorizedAccessException("Pedir liberação é de quem marca na agenda.");

        var modalidade = CatalogoModalidades.Base(novo.ModalidadeCodigo);
        var trava = await agenda.TravaDeConsultaAsync(novo.PacienteId, novo.DataHora, modalidade, particular: false,
                        novo.AgendamentoARemarcarId, ct)
            ?? throw new InvalidOperationException("Não há trava de consulta para este horário — marque normalmente.");
        if (await PendenteAsync(novo.PacienteId, DateOnly.FromDateTime(novo.DataHora), ct) is { } aberto)
            throw new InvalidOperationException(
                $"Já existe o pedido nº {aberto.Id} para este paciente neste dia, feito por {aberto.PedidoPorNome} "
                + $"às {aberto.PedidoEm:HH:mm} — aguardando a gerência.");

        var aprovadores = await AprovadoresAsync(ct);
        if (aprovadores.Count == 0)
            throw new InvalidOperationException(
                "Ninguém está marcado para aprovar liberações. A direção marca em Gerente › Acessos, no "
                + "cadastro de cada gerente (\"Aprova liberação de trava de consulta\").");

        var paciente = await db.Pacientes.AsNoTracking().SingleOrDefaultAsync(p => p.Id == novo.PacienteId, ct)
            ?? throw new InvalidOperationException("Paciente não encontrado.");
        var profissional = novo.ProfissionalId is { } pid
            ? await db.Profissionais.AsNoTracking().Where(p => p.Id == pid).Select(p => p.NomeCurto ?? p.Nome).SingleOrDefaultAsync(ct)
            : null;

        var momento = agora();
        var pedido = new LiberacaoDeTrava
        {
            PacienteId = paciente.Id, PacienteNome = Cortar(paciente.Nome, 200), ConvenioNome = Cortar(trava.ConvenioNome, 120),
            DataHora = novo.DataHora, ProfissionalId = novo.ProfissionalId, ProfissionalNome = Cortar(profissional ?? "", 120),
            SalaId = novo.SalaId, DuracaoMinutos = novo.DuracaoMinutos, ModalidadeCodigo = novo.ModalidadeCodigo,
            EspecialidadeConsultaCodigo = novo.EspecialidadeConsultaCodigo, Encaixe = novo.Encaixe,
            ColetaPreventivo = novo.ColetaPreventivo, AgendamentoARemarcarId = novo.AgendamentoARemarcarId, Observacoes = string.IsNullOrWhiteSpace(novo.Observacoes) ? null : Cortar(novo.Observacoes.Trim(), 500),
            PrazoDias = trava.PrazoDias, ConsultaAnteriorEm = trava.ConsultaEm, AtendimentoAnteriorNumero = trava.AtendimentoNumero,
            Motivo = motivo, PedidoPorId = eu.Id, PedidoPorNome = Cortar(eu.Nome, 120), PedidoEm = momento,
            ExpiraEm = LiberacaoDeTravaRegras.ExpiraEm(momento, novo.DataHora), Estado = LiberacaoDeTrava.Pendente,
        };
        db.LiberacoesDeTrava.Add(pedido);
        db.Auditoria.Add(new EventoAuditoria
        {
            DataHora = momento, Operador = eu.Nome, Acao = "LiberacaoDeTravaPedida", PacienteId = paciente.Id,
            Detalhe = Cortar($"Consulta de {novo.DataHora:dd/MM/yyyy HH:mm} antes do prazo de {trava.PrazoDias} dias do {trava.ConvenioNome} "
                             + $"(última em {trava.ConsultaEm:dd/MM/yyyy}). Motivo: {motivo}", 1000),
        });
        // O pedido precisa do número para a mensagem citá-lo: grava, e as mensagens vão no segundo commit.
        await db.SaveChangesAsync(ct);

        var texto = LiberacaoDeTravaRegras.TextoParaGerencia(pedido);
        var comWhatsApp = 0;
        foreach (var gerente in aprovadores)
        {
            await GravacaoDoChat.AdicionarAsync(db,
                GravacaoDoChat.Mensagem(ChatEquipeRegras.Sistema(gerente.Id), gerente.Id, gerente.Nome, texto, null, true, momento,
                    paciente.Id, pedido.PacienteNome), ct);
            if (gerente.TelefoneWhatsApp is { Length: > 0 } celular)
            {
                db.AvisosDeLiberacao.Add(new AvisoDeLiberacao
                {
                    LiberacaoDeTravaId = pedido.Id, Destino = AvisoDeLiberacao.ParaEquipe, Telefone = celular,
                    Texto = Cortar(texto, 2000), CriadoEm = momento,
                });
                comWhatsApp++;
            }
        }
        await db.SaveChangesAsync(ct);
        Soltar();
        return new(pedido, aprovadores.Count, comWhatsApp);
    }

    // ================================================================ decidir

    /// <summary>
    /// A mensagem da gerência é uma decisão? Devolve a resposta a dar — ou nulo, e a mensagem
    /// segue para as perguntas de sempre. <paramref name="canal"/>: "whatsapp" ou "app".
    /// </summary>
    public async Task<string?> DecidirPorTextoAsync(UsuarioSistema eu, string texto, string canal, CancellationToken ct = default)
    {
        if (!eu.PodeAprovarLiberacao || LiberacaoDeTravaRegras.Ler(texto) is not { } decisao) return null;

        var momento = agora();
        var pendentes = await db.LiberacoesDeTrava.AsNoTracking()
            .Where(l => l.Estado == LiberacaoDeTrava.Pendente && l.ExpiraEm > momento)
            .OrderBy(l => l.Id).Take(20).ToListAsync(ct);

        if (decisao.PedidoId is { } id)
            return pendentes.Any(l => l.Id == id)
                ? await DecidirAsync(id, eu, decisao.Liberar, decisao.Motivo, canal, ct)
                : LiberacaoDeTravaRegras.NaoAchei(id);

        // "sim"/"não" sozinhos: sem pedido aberto não são decisão de nada — a conversa segue.
        if (pendentes.Count == 0) return null;
        return pendentes.Count == 1
            ? await DecidirAsync(pendentes[0].Id, eu, decisao.Liberar, decisao.Motivo, canal, ct)
            : LiberacaoDeTravaRegras.TextoVarios(pendentes);
    }

    /// <summary>Só "isto é uma decisão que eu trataria?", sem gravar — para o CRM não mandar a frase à IA.</summary>
    public async Task<bool> EhDecisaoAsync(UsuarioSistema eu, string texto, CancellationToken ct = default)
    {
        if (!eu.PodeAprovarLiberacao || LiberacaoDeTravaRegras.Ler(texto) is not { } decisao) return false;
        if (decisao.PedidoId is not null) return true;
        var momento = agora();
        return await db.LiberacoesDeTrava.AsNoTracking().AnyAsync(l => l.Estado == LiberacaoDeTrava.Pendente && l.ExpiraEm > momento, ct);
    }

    public async Task<string> DecidirAsync(int pedidoId, UsuarioSistema eu, bool liberar, string? motivo, string canal,
        CancellationToken ct = default)
    {
        if (!eu.PodeAprovarLiberacao)
            throw new UnauthorizedAccessException("Seu acesso não aprova liberações de trava de consulta.");

        var momento = agora();
        var estado = liberar ? LiberacaoDeTrava.Liberada : LiberacaoDeTrava.Negada;
        var nome = Cortar(eu.Nome, 120);
        var porOnde = Cortar(canal, 20);
        // A reivindicação: só UM decide. Quem chegar depois lê o que o outro decidiu.
        var meu = await db.LiberacoesDeTrava
            .Where(l => l.Id == pedidoId && l.Estado == LiberacaoDeTrava.Pendente && l.ExpiraEm > momento)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.Estado, estado)
                .SetProperty(l => l.DecididoPorId, eu.Id).SetProperty(l => l.DecididoPorNome, nome)
                .SetProperty(l => l.DecididoEm, momento).SetProperty(l => l.DecididoPor, porOnde), ct);
        Soltar();
        var pedido = await db.LiberacoesDeTrava.SingleOrDefaultAsync(l => l.Id == pedidoId, ct);
        if (pedido is null) return LiberacaoDeTravaRegras.NaoAchei(pedidoId);
        if (meu == 0)
            return pedido.Estado == LiberacaoDeTrava.Pendente
                ? $"O pedido nº {pedidoId} expirou em {pedido.ExpiraEm:dd/MM HH:mm} — nada foi marcado. A recepção pode pedir de novo."
                : $"O pedido nº {pedidoId} já foi decidido por {pedido.DecididoPorNome} ({pedido.Estado}).";

        if (liberar)
        {
            // Marca pela agenda de sempre: a trava já enxerga a liberação (o estado acima), e as
            // outras recusas (agenda protegida, vaga de preventivo, profissional) continuam valendo.
            try
            {
                var modalidade = CatalogoModalidades.Base(pedido.ModalidadeCodigo);
                // Pedido de REMARCAÇÃO: é o horário que já existe que muda — marcar outro
                // deixaria o paciente com dois. O particular fica como estava (nulo preserva).
                var marcado = pedido.AgendamentoARemarcarId is { } existente
                    ? await agenda.RemarcarAsync(
                        existente, pedido.DataHora, pedido.Observacoes, pedido.ModalidadeCodigo, pedido.EspecialidadeConsultaCodigo,
                        pedido.PedidoPorNome, ct, pedido.ProfissionalId, pedido.SalaId, pedido.DuracaoMinutos,
                        manterRecursos: false, encaixe: pedido.Encaixe, coletaPreventivo: pedido.ColetaPreventivo, particular: false)
                    : await agenda.AgendarAsync(
                        pedido.PacienteId, pedido.DataHora, modalidade, pedido.Observacoes, ct: ct,
                        modalidadeCodigo: pedido.ModalidadeCodigo, especialidadeConsultaCodigo: pedido.EspecialidadeConsultaCodigo,
                        profissionalId: pedido.ProfissionalId, salaId: pedido.SalaId, duracaoMinutos: pedido.DuracaoMinutos,
                        encaixe: pedido.Encaixe, operador: pedido.PedidoPorNome, coletaPreventivo: pedido.ColetaPreventivo);
                pedido.AgendamentoId = marcado.Id;
                pedido.Desfecho = "marcado";
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Clinica.Application.Diagnostico.Registrar("Liberação de trava — o horário liberado não pôde ser marcado", ex);
                Soltar();
                pedido = await db.LiberacoesDeTrava.SingleAsync(l => l.Id == pedidoId, ct);
                pedido.Desfecho = Cortar(ex is InvalidOperationException ? ex.Message : "a agenda não respondeu.", 1000);
            }
        }
        else pedido.Desfecho = string.IsNullOrWhiteSpace(motivo) ? null : Cortar(motivo.Trim(), 1000);

        db.Auditoria.Add(new EventoAuditoria
        {
            DataHora = momento, Operador = eu.Nome, PacienteId = pedido.PacienteId,
            Acao = liberar ? "LiberacaoDeTravaConcedida" : "LiberacaoDeTravaNegada",
            Detalhe = Cortar($"Pedido nº {pedido.Id} de {pedido.PedidoPorNome}: consulta de {pedido.DataHora:dd/MM/yyyy HH:mm} "
                             + $"({pedido.ConvenioNome}, prazo de {pedido.PrazoDias} dias). Decidido pelo {canal}."
                             + (pedido.AgendamentoId is { } ag ? $" Horário {ag} marcado." : liberar ? $" Não marcado: {pedido.Desfecho}" : ""), 1000),
        });
        await AvisarDesfechoAsync(pedido, momento, ct);
        await db.SaveChangesAsync(ct);
        var resposta = LiberacaoDeTravaRegras.TextoDaDecisao(pedido);
        Soltar();
        return resposta;
    }

    /// <summary>Quem pediu fica sabendo (chat e WhatsApp, se tiver); liberado e marcado, o paciente recebe a confirmação.</summary>
    private async Task AvisarDesfechoAsync(LiberacaoDeTrava pedido, DateTime momento, CancellationToken ct)
    {
        var quemPediu = await db.Usuarios.AsNoTracking().SingleOrDefaultAsync(u => u.Id == pedido.PedidoPorId, ct);
        var texto = LiberacaoDeTravaRegras.TextoParaQuemPediu(pedido);
        if (quemPediu is not null)
        {
            await GravacaoDoChat.AdicionarAsync(db,
                GravacaoDoChat.Mensagem(ChatEquipeRegras.Sistema(quemPediu.Id), quemPediu.Id, quemPediu.Nome, texto, null, true, momento,
                    pedido.PacienteId, pedido.PacienteNome), ct);
            if (quemPediu is { Ativo: true, TelefoneWhatsApp: { Length: > 0 } celular })
                db.AvisosDeLiberacao.Add(new AvisoDeLiberacao
                {
                    LiberacaoDeTravaId = pedido.Id, Destino = AvisoDeLiberacao.ParaEquipe, Telefone = celular,
                    Texto = Cortar(texto, 2000), CriadoEm = momento,
                });
        }

        if (pedido is not { Estado: LiberacaoDeTrava.Liberada, AgendamentoId: not null }) return;
        var telefone = await db.Pacientes.AsNoTracking().Where(p => p.Id == pedido.PacienteId).Select(p => p.Telefone).SingleOrDefaultAsync(ct);
        if (TelefoneDaEquipe.Chave(telefone) is not { } doPaciente) return;
        var clinica = (await new ParametrosService(new ClinicaRepositorio(db)).ObterPrestadorAsync(ct)).NomeFantasia;
        db.AvisosDeLiberacao.Add(new AvisoDeLiberacao
        {
            LiberacaoDeTravaId = pedido.Id, Destino = AvisoDeLiberacao.ParaPaciente, Telefone = doPaciente,
            Texto = Cortar(LiberacaoDeTravaRegras.TextoParaPaciente(pedido, clinica), 2000), CriadoEm = momento,
        });
    }

    // ================================================================ as filas que o CRM lê

    /// <summary>
    /// As mensagens à EQUIPE que ainda não saíram (o pedido, à gerência; o desfecho, a quem pediu).
    /// É também o pulso que EXPIRA os pedidos sem resposta — o CRM lê esta fila a cada minuto.
    /// </summary>
    public async Task<IReadOnlyList<AvisoDaLiberacao>> AvisosParaEquipeAsync(CancellationToken ct = default)
    {
        await ExpirarAsync(ct);
        return await Fila(AvisoDeLiberacao.ParaEquipe).ToListAsync(ct);
    }

    /// <summary>A confirmação ao PACIENTE que ainda não saiu.</summary>
    public async Task<IReadOnlyList<AvisoDaLiberacao>> AvisosParaPacienteAsync(CancellationToken ct = default)
        => await Fila(AvisoDeLiberacao.ParaPaciente).ToListAsync(ct);

    /// <summary>A fila de um destino: três dias para trás, cinquenta por leitura (a mesma do desfecho dos pedidos).</summary>
    internal IQueryable<AvisoDaLiberacao> Fila(string destino)
    {
        var desde = agora().AddDays(-3);
        return db.AvisosDeLiberacao.AsNoTracking()
            .Where(a => a.Destino == destino && a.TratadoEm == null && a.CriadoEm >= desde)
            .OrderBy(a => a.Id).Take(50)
            .Select(a => new AvisoDaLiberacao(a.Id, a.Telefone, a.Texto));
    }

    /// <summary>
    /// O CRM tratou a mensagem. Entregue ou não, ela sai da fila: fora da janela de 24 h a Meta
    /// recusa mensagem livre e repetir não muda isso. O pedido continua na conversa com o
    /// sistema, no app — é por lá que a gerência decide quando o WhatsApp não chega.
    /// </summary>
    public async Task ConfirmarAvisoAsync(int avisoId, bool entregue, string? motivo, CancellationToken ct = default)
    {
        var momento = agora();
        var porQue = entregue ? null : Cortar(motivo ?? "sem motivo", 500);
        await db.AvisosDeLiberacao.Where(a => a.Id == avisoId && a.TratadoEm == null)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.TratadoEm, momento).SetProperty(a => a.Entregue, entregue)
                .SetProperty(a => a.Motivo, porQue), ct);
    }

    /// <summary>Pedido sem resposta até o prazo: vira "expirado" e quem pediu é avisado. Um por vez, com reivindicação.</summary>
    public async Task<int> ExpirarAsync(CancellationToken ct = default)
    {
        var momento = agora();
        var vencidos = await db.LiberacoesDeTrava.AsNoTracking()
            .Where(l => l.Estado == LiberacaoDeTrava.Pendente && l.ExpiraEm <= momento).Select(l => l.Id).Take(20).ToListAsync(ct);
        var expirados = 0;
        foreach (var id in vencidos)
        {
            var meu = await db.LiberacoesDeTrava.Where(l => l.Id == id && l.Estado == LiberacaoDeTrava.Pendente)
                .ExecuteUpdateAsync(s => s.SetProperty(l => l.Estado, LiberacaoDeTrava.Expirada), ct);
            if (meu == 0) continue;
            var pedido = await db.LiberacoesDeTrava.AsNoTracking().SingleAsync(l => l.Id == id, ct);
            await AvisarDesfechoAsync(pedido, momento, ct);
            await db.SaveChangesAsync(ct);
            Soltar();
            expirados++;
        }
        return expirados;
    }

    private static string Cortar(string texto, int maximo) => texto.Length <= maximo ? texto : texto[..maximo];

    /// <summary>O contexto é do escopo de quem chamou (a ponte responde várias mensagens): nada fica rastreado.</summary>
    private void Soltar() => db.ChangeTracker.Clear();
}
