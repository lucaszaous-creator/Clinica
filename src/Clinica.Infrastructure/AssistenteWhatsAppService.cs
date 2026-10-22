using System.Security.Cryptography;
using System.Text;
using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Infrastructure;

/// <summary>A resposta a quem escreveu ao WhatsApp da clínica. <paramref name="DaEquipe"/> falso: é paciente, siga o fluxo do CRM.</summary>
/// <param name="Entendeu">As regras reconheceram a pergunta (falso = a resposta é a ajuda de "não entendi").</param>
public sealed record RespostaWhatsAppEquipe(bool DaEquipe, string? Texto = null, string? Nome = null, bool Entendeu = true);

/// <summary>O desfecho de um pedido à recepção que ainda precisa chegar pelo WhatsApp a quem pediu.</summary>
public sealed record AvisoWhatsAppEquipe(int PedidoId, string Telefone, string Texto);

/// <summary>
/// O sistema da clínica respondendo pelo WhatsApp a quem é da equipe (out/2026). O médico manda
/// ao número da clínica "qual a minha agenda de hoje?", o CRM pergunta à ponte quem é aquele
/// celular (<see cref="UsuarioSistema.TelefoneWhatsApp"/>) e esta classe responde EM NOME DAQUELE
/// LOGIN — com as mesmas leituras e as mesmas barreiras do Sistema SemDor do chat
/// (<see cref="SistemaEquipeService"/>): nunca alcança mais do que a pessoa alcançaria no app.
///
/// A conversa fica registrada na conversa da pessoa com o sistema (<c>sistema:{id}</c>): quem abre
/// o chat no app vê o que perguntou pelo celular, e o pedido à recepção nasce pelo MESMO
/// <see cref="PedidosRecepcaoService"/> do botão do app — cartão no # Recepção, dono e desfecho.
/// </summary>
public sealed class AssistenteWhatsAppService
{
    private readonly ClinicaDbContext db;
    private readonly Func<DateTime> agora;

    public AssistenteWhatsAppService(ClinicaDbContext db) : this(db, () => DateTime.Now) { }

    public AssistenteWhatsAppService(ClinicaDbContext db, Func<DateTime> agora)
    {
        this.db = db;
        this.agora = agora;
    }

    /// <summary>Os celulares da equipe (chave canônica): o CRM consulta para não abrir conversa de paciente com eles.</summary>
    public async Task<IReadOnlyList<string>> NumerosAsync(CancellationToken ct = default)
        => await db.Usuarios.AsNoTracking().Where(u => u.Ativo && u.TelefoneWhatsApp != null)
            .Select(u => u.TelefoneWhatsApp!).ToListAsync(ct);

    /// <summary>
    /// Responde à mensagem. <paramref name="idMensagem"/> é o id da Meta: o mesmo pedido escrito não
    /// vira dois pedidos se a mensagem chegar duas vezes.
    /// </summary>
    /// <summary>
    /// Só pergunta se as regras ENTENDEM o texto, sem gravar nada (@SemDor fase 2): o CRM chama a IA
    /// apenas quando a resposta é não — e então manda a pergunta reescrita para <see cref="ResponderAsync"/>.
    /// </summary>
    public async Task<RespostaWhatsAppEquipe> EntendeAsync(string telefone, string texto, CancellationToken ct = default)
    {
        if (TelefoneDaEquipe.Chave(telefone) is not { } chave) return new(false);
        var eu = await db.Usuarios.AsNoTracking().SingleOrDefaultAsync(u => u.TelefoneWhatsApp == chave && u.Ativo, ct);
        if (eu is null) return new(false);
        texto = (texto ?? "").Trim();
        var hoje = DateOnly.FromDateTime(agora());
        if (texto.Length == 0 || AssistenteWhatsAppRegras.LerPedido(texto, hoje) is not null) return new(true, Nome: eu.Nome);
        var profissionais = await db.Profissionais.AsNoTracking().Where(p => p.Ativo).Select(p => new { p.Id, p.Nome }).ToListAsync(ct);
        var intencao = SistemaEquipeRegras.Interpretar(texto, hoje, profissionais.Select(p => (p.Id, p.Nome)).ToList()).Intencao;
        return new(true, Nome: eu.Nome, Entendeu: intencao != IntencaoDoSistema.NaoEntendi);
    }

    public async Task<RespostaWhatsAppEquipe> ResponderAsync(string telefone, string texto, string idMensagem, CancellationToken ct = default)
    {
        if (TelefoneDaEquipe.Chave(telefone) is not { } chave) return new(false);
        var eu = await db.Usuarios.AsNoTracking().SingleOrDefaultAsync(u => u.TelefoneWhatsApp == chave && u.Ativo, ct);
        if (eu is null) return new(false);

        texto = (texto ?? "").Replace("\r\n", "\n").Trim();
        if (texto.Length > 1000) texto = texto[..1000];
        if (texto.Length == 0)
            return new(true, "Por aqui eu respondo texto. Mande, por exemplo, \"minha agenda de hoje\".", eu.Nome);

        var conversa = ChatEquipeRegras.Sistema(eu.Id);
        var momento = agora();
        await GravarAsync(GravacaoDoChat.Mensagem(conversa, eu.Id, eu.Nome, texto, null, false, momento), ct);

        string resposta;
        var entendeu = true;
        string? textoNoChat = null;
        CartaoChat? cartaoNoChat = null;
        var gravarResposta = true;
        try
        {
            if (AssistenteWhatsAppRegras.LerPedido(texto, DateOnly.FromDateTime(momento)) is { } pedido)
                (resposta, gravarResposta) = await PedirAsync(eu, pedido, idMensagem, momento, ct);
            else
            {
                var (t, c) = await new SistemaEquipeService(db, agora).ResponderAsync(eu, texto, ct);
                (textoNoChat, cartaoNoChat) = (t, c);
                entendeu = c is not { Tipo: "ajuda", Titulo: "Não entendi a pergunta" };
                // A ajuda do app fala de botões que o WhatsApp não tem: aqui ela ensina a ESCREVER.
                resposta = c is { Tipo: "ajuda", Titulo: "Não entendi a pergunta" or "O que posso responder" or "Pedir à recepção" }
                    ? AssistenteWhatsAppRegras.Ajuda(c.Titulo == "Não entendi a pergunta", PedidosRecepcaoRegras.PodeTratar(eu))
                    : AssistenteWhatsAppRegras.Texto(t, c);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Clinica.Application.Diagnostico.Registrar("WhatsApp da equipe — resposta não montada", ex);
            resposta = ex is UnauthorizedAccessException or InvalidOperationException
                ? ex.Message
                : "Não consegui ler os dados da agenda agora. Tente de novo em instantes.";
        }

        if (gravarResposta)
        {
            var resp = GravacaoDoChat.Mensagem(conversa, eu.Id, eu.Nome, textoNoChat ?? resposta, cartaoNoChat, true, agora());
            await GravarAsync(resp, ct);
            await MarcarLidaAsync(eu.Id, conversa, resp.Id, ct);
        }
        return new(true, resposta, eu.Nome, entendeu);
    }

    /// <summary>
    /// O pedido escrito: acha o horário citado na agenda que a pessoa alcança. Um só → o pedido
    /// nasce. Vários → a resposta lista e diz como escrever para escolher. Nenhum → diz onde procurou.
    /// O segundo valor diz se a resposta precisa ser gravada no chat (o pedido criado já grava o cartão).
    /// </summary>
    private async Task<(string Resposta, bool Gravar)> PedirAsync(UsuarioSistema eu, PedidoPorTexto pedido, string idMensagem,
        DateTime momento, CancellationToken ct)
    {
        var rotulo = PedidosRecepcaoRegras.RotuloDoTipo(pedido.Tipo);
        int? agendamentoId = null, pacienteId = null;
        string? achado = null;

        if (pedido.Nome.Length > 0)
        {
            var candidatos = await HorariosCandidatosAsync(eu, pedido, momento, ct);
            if (candidatos.Count > 1)
            {
                var sb = new StringBuilder($"Achei {candidatos.Count} horários de \"{pedido.Nome}\". Mande de novo com o dia e a hora, por exemplo:");
                foreach (var h in candidatos.Take(8))
                    sb.Append($"\n• {rotulo} {h.Paciente} {h.DataHora:dd/MM} {h.DataHora:HH:mm}");
                return (sb.ToString(), true);
            }
            if (candidatos.Count == 1)
            {
                agendamentoId = candidatos[0].Id;
                pacienteId = candidatos[0].PacienteId;
                achado = $"{candidatos[0].Paciente} · {PedidosRecepcaoRegras.Horario(candidatos[0].DataHora)}";
            }
            else if (PedidosRecepcaoRegras.PedeHorario(pedido.Tipo))
                return ($"Não achei horário marcado de \"{pedido.Nome}\" {OndeProcurei(eu, pedido)}. Confira o nome ou mande com o dia, por exemplo: \"{rotulo} {pedido.Nome} 24/10 10h\".", true);
            else if (await PacienteUnicoAsync(eu, pedido.Nome, ct) is { } p)
            {
                pacienteId = p.Id;
                achado = p.Nome;
            }
        }
        else if (PedidosRecepcaoRegras.PedeHorario(pedido.Tipo))
            return ($"Diga de quem é o horário, por exemplo: \"{rotulo} Maria Silva 24/10 10h\".", true);

        var criado = await new PedidosRecepcaoService(db, agora).CriarAsync(eu.Id,
            new NovoPedidoRecepcao(Idempotencia(idMensagem), pedido.Tipo, pacienteId, agendamentoId, pedido.Texto), ct);
        var sobre = achado ?? (pedido.Nome.Length > 0 ? $"\"{pedido.Nome}\" (não achei a ficha — a recepção confere pelo texto)" : "recado");
        return ($"*Pedido enviado à recepção* (nº {criado.Id})\n{rotulo} · {sobre}\n\nQuando ela concluir, a resposta chega aqui. Nada muda na agenda até a recepção fazer.", false);
    }

    private static string OndeProcurei(UsuarioSistema eu, PedidoPorTexto p)
        => (eu.ProfissionalId is null ? "na agenda da clínica" : "na sua agenda")
           + (p.Dia is { } d ? $" em {d:dd/MM}" : " nos próximos 30 dias");

    /// <summary>
    /// Os horários marcados e por vir que batem com o nome (e com a hora, quando citada), na agenda
    /// que a pessoa alcança: a dela, ou a da clínica para quem vê a agenda e não atende.
    /// </summary>
    private async Task<IReadOnlyList<SistemaEquipeService.LinhaDoHorario>> HorariosCandidatosAsync(UsuarioSistema eu, PedidoPorTexto pedido,
        DateTime momento, CancellationToken ct)
    {
        int? profissionalId;
        if (eu.ProfissionalId is { } meu) profissionalId = meu;
        else if (eu.Pode(Permissao.VerAgenda)) profissionalId = null;
        else return [];

        var de = pedido.Dia is { } d ? d.ToDateTime(TimeOnly.MinValue) : momento.Date;
        var ate = pedido.Dia is { } d2 ? d2.ToDateTime(TimeOnly.MinValue).AddDays(1) : momento.Date.AddDays(31);
        var linhas = await SistemaEquipeService.ConsultaHorarios(db, de, ate, profissionalId).ToListAsync(ct);
        var bate = linhas.Where(l => l.Status == StatusAgendamento.Agendado && l.ChegadaEm is null && l.DataHora > momento
                                     && AssistenteWhatsAppRegras.NomeBate(pedido.Nome, l.Paciente)).ToList();
        if (pedido.Hora is { } h && bate.Count > 1)
        {
            var naHora = bate.Where(l => TimeOnly.FromDateTime(l.DataHora) == h).ToList();
            if (naHora.Count == 0) naHora = bate.Where(l => l.DataHora.Hour == h.Hour).ToList();
            if (naHora.Count > 0) bate = naHora;
        }
        else if (pedido.Hora is { } h1 && bate.Count == 1 && bate[0].DataHora.Hour != h1.Hour)
            bate = [];
        return bate;
    }

    /// <summary>O paciente citado, quando o nome acha UMA ficha só (encaixe e aviso não precisam de horário).</summary>
    private async Task<(int Id, string Nome)?> PacienteUnicoAsync(UsuarioSistema eu, string nome, CancellationToken ct)
    {
        if (!eu.Pode(Permissao.VerFichaPaciente)) return null;
        var primeira = nome.Split(' ')[0];
        var achados = await db.Pacientes.AsNoTracking().Where(p => p.Nome.ToLower().Contains(primeira))
            .OrderBy(p => p.Nome).Take(50).Select(p => new { p.Id, p.Nome }).ToListAsync(ct);
        var bate = achados.Where(p => AssistenteWhatsAppRegras.NomeBate(nome, p.Nome)).ToList();
        return bate.Count == 1 ? (bate[0].Id, bate[0].Nome) : null;
    }

    /// <summary>A chave do comando a partir do id da mensagem da Meta: a mesma mensagem, o mesmo pedido.</summary>
    public static Guid Idempotencia(string idMensagem)
        => new(MD5.HashData(Encoding.UTF8.GetBytes("whatsapp-equipe:" + (idMensagem ?? ""))));

    private async Task GravarAsync(MensagemEquipe m, CancellationToken ct)
    {
        await GravacaoDoChat.AdicionarAsync(db, m, ct);
        await db.SaveChangesAsync(ct);
        foreach (var e in db.ChangeTracker.Entries().Where(e => e.Entity is MensagemEquipe or ConversaEquipe).ToList())
            e.State = EntityState.Detached;
    }

    /// <summary>A pessoa leu no celular: a conversa no app não aparece como não lida por causa disso.</summary>
    private async Task MarcarLidaAsync(int usuarioId, string conversa, int ultimaId, CancellationToken ct)
    {
        var momento = agora();
        var mudou = await db.LeiturasEquipe.Where(l => l.UsuarioId == usuarioId && l.Conversa == conversa)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.UltimaLidaId, l => l.UltimaLidaId < ultimaId ? ultimaId : l.UltimaLidaId)
                .SetProperty(l => l.LidaEm, momento), ct);
        if (mudou > 0) return;
        var nova = db.LeiturasEquipe.Add(new LeituraEquipe { UsuarioId = usuarioId, Conversa = conversa, UltimaLidaId = ultimaId, LidaEm = momento });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { /* o app marcou ao mesmo tempo */ }
        finally { nova.State = EntityState.Detached; }
    }

    // ================================================================ o desfecho de volta pelo WhatsApp

    /// <summary>
    /// Os desfechos que ainda não foram ao WhatsApp de quem pediu: pedido encerrado nos últimos 3
    /// dias, de quem tem o número ligado, criado DEPOIS de o número ser ligado.
    /// </summary>
    public async Task<IReadOnlyList<AvisoWhatsAppEquipe>> AvisosPendentesAsync(CancellationToken ct = default)
    {
        var desde = agora().AddDays(-3);
        var linhas = await ConsultaAvisos(db, desde).ToListAsync(ct);
        return linhas.Select(x => new AvisoWhatsAppEquipe(x.Pedido.Id, x.Telefone, PedidosRecepcaoRegras.Desfecho(x.Pedido))).ToList();
    }

    public sealed record LinhaDoAviso(PedidoRecepcao Pedido, string Telefone);

    /// <summary>A fila dos avisos (uma ida, com o celular de quem pediu).</summary>
    public static IQueryable<LinhaDoAviso> ConsultaAvisos(ClinicaDbContext db, DateTime desde)
        => (from p in db.PedidosRecepcao.AsNoTracking()
            join u in db.Usuarios.AsNoTracking() on p.CriadoPorId equals u.Id
            where p.AvisoWhatsAppEm == null && p.EncerradoEm != null && p.EncerradoEm >= desde
                  && u.Ativo && u.TelefoneWhatsApp != null && u.WhatsAppLigadoEm != null && p.CriadoEm >= u.WhatsAppLigadoEm
            orderby p.EncerradoEm
            select new LinhaDoAviso(p, u.TelefoneWhatsApp!)).Take(50);

    /// <summary>
    /// O CRM tratou o aviso. Entregue ou não, ele sai da fila (o WhatsApp recusa mensagem livre fora
    /// da janela de 24 h, e repetir não muda isso); o resultado fica nos eventos do pedido, e o
    /// desfecho continua na conversa da pessoa com o sistema, no app.
    /// </summary>
    public async Task ConfirmarAvisoAsync(int pedidoId, bool entregue, string? motivo, CancellationToken ct = default)
    {
        var momento = agora();
        var mudou = await db.PedidosRecepcao.Where(p => p.Id == pedidoId && p.AvisoWhatsAppEm == null)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.AvisoWhatsAppEm, momento), ct);
        if (mudou == 0) return;
        var obs = entregue ? "desfecho enviado pelo WhatsApp" : "desfecho NÃO foi pelo WhatsApp: " + (motivo ?? "sem motivo");
        db.EventosPedidoRecepcao.Add(new EventoPedidoRecepcao
        {
            PedidoRecepcaoId = pedidoId, UsuarioId = await db.PedidosRecepcao.Where(p => p.Id == pedidoId).Select(p => p.CriadoPorId).SingleAsync(ct),
            Autor = ChatEquipeRegras.NomeDoSistema, Em = momento, Acao = "aviso-whatsapp",
            Observacao = obs.Length > 2000 ? obs[..2000] : obs,
        });
        await db.SaveChangesAsync(ct);
        foreach (var e in db.ChangeTracker.Entries<EventoPedidoRecepcao>().ToList()) e.State = EntityState.Detached;
    }
}
