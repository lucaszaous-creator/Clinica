using System.Globalization;
using Clinica.Domain.Entities;

namespace Clinica.Application.Modelos;

/// <summary>O que a resposta (ou a falta dela) pede da recepção.</summary>
public enum TipoAvisoConfirmacao
{
    /// <summary>O paciente confirmou pelo link do SMS ou pelo WhatsApp do CRM. É notícia, não pede ação.</summary>
    Confirmou,

    /// <summary>
    /// O paciente avisou que não vai — pelo link, pelo CRM ou pelo balcão ("pediu para
    /// remarcar"). Pede a ligação que remarca e libera o horário.
    /// </summary>
    NaoVem,

    /// <summary>O lembrete com link saiu e o paciente não clicou. Ver <see cref="AvisosDeConfirmacao.EsperaPorResposta"/>.</summary>
    SemResposta,

    /// <summary>
    /// O paciente pediu pelo WhatsApp para cancelar (out/2026). Pede que a recepção aceite o
    /// cancelamento na agenda. No fim do enum: o sino grava a leitura pelo número do tipo.
    /// </summary>
    PediuCancelar
}

/// <summary>
/// O contato de confirmação de UM horário futuro, com o que a decisão precisa — a projeção
/// de <c>IClinicaRepositorio.ContatosParaAvisosDeConfirmacaoAsync</c>. Sem a ficha, sem o
/// agendamento inteiro: são nove colunas por linha, a cada minuto, em todo posto.
/// </summary>
public sealed record ContatoParaAvisoDeConfirmacao(
    int AgendamentoId,
    string Paciente,
    DateTime Sessao,
    string? Profissional,
    StatusContato Status,
    bool TemLink,
    DateTime? EnviadoEm,
    DateTime? RespondidoEm,
    string? Comentario);

/// <summary>Um aviso para a recepção: quem, quando é a sessão, o que aconteceu e desde quando.</summary>
public sealed record AvisoDeConfirmacao(
    int AgendamentoId,
    string Paciente,
    DateTime Sessao,
    string? Profissional,
    TipoAvisoConfirmacao Tipo,
    DateTime Momento)
{
    /// <summary>Um aviso por horário E por tipo: o "sem resposta" que vira "não vem" é notícia nova.</summary>
    public (int AgendamentoId, TipoAvisoConfirmacao Tipo) Chave => (AgendamentoId, Tipo);

    /// <summary>Não vem e sem resposta pedem uma ligação; confirmou é só notícia.</summary>
    public bool PedeAcao => Tipo != TipoAvisoConfirmacao.Confirmou;
}

/// <summary>
/// AS RESPOSTAS DOS PACIENTES VIRAM AVISO (out/2026 — pedido da clínica: <i>"quando o
/// paciente confirma, cancela ou não responde o SMS, a secretária precisa ser NOTIFICADA,
/// e a mesma informação tem de chegar ao Gerente"</i>). Até aqui a resposta só aparecia
/// quando alguém abria Relacionamento → Confirmações.
///
/// A decisão mora AQUI, pura, e é a ÚNICA: o sino do shell (Recepção e Gerente) e o painel
/// da direção leem a mesma lista (<see cref="Servicos.AvisosDeConfirmacaoService"/>). Dois
/// cálculos do "quem não vem" dariam dois números para a mesma agenda.
///
/// A classificação continua sendo a de <see cref="RodadaDeConfirmacao.Classificar"/> — o
/// "não vem" é o mesmo selo vermelho das listas do dia.
/// </summary>
public static class AvisosDeConfirmacao
{
    /// <summary>
    /// Quanto o lembrete espera antes de virar "sem resposta": <b>3 horas</b>. O SMS sai ~24 h
    /// antes (<see cref="Clinica.Domain.ConfirmacaoPorLink.AntecedenciaPadraoHoras"/>), e quem
    /// vai responder responde na primeira hora; menos que isso acusaria de silêncio quem
    /// ainda nem leu, e a recepção ligaria para quem ia clicar. Três horas ainda deixam o
    /// resto do expediente para a ligação.
    /// </summary>
    public static readonly TimeSpan EsperaPorResposta = TimeSpan.FromHours(3);

    /// <summary>O horizonte mínimo: as sessões das próximas 36 horas.</summary>
    public static readonly TimeSpan HorizonteMinimo = TimeSpan.FromHours(36);

    /// <summary>
    /// Até quando a recepção precisa saber: <b>o maior entre agora + 36 h e o fim do PRÓXIMO
    /// DIA ÚTIL</b>. Só 36 h esconderia, na sexta, a segunda-feira — e o lembrete da segunda
    /// sai justamente na sexta (<see cref="Clinica.Domain.ConfirmacaoPorLink.MomentoDoLembrete"/>
    /// adianta o fim de semana). Sem esta regra, "Ana não respondeu" da sessão de segunda às
    /// 8h só apareceria no domingo à noite, com ninguém na clínica para ligar.
    /// </summary>
    public static DateTime FimDoHorizonte(DateTime agora)
    {
        var proximoUtil = agora.Date.AddDays(1);
        while (proximoUtil.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) proximoUtil = proximoUtil.AddDays(1);
        var fimDoProximoUtil = proximoUtil.AddDays(1);
        var minimo = agora + HorizonteMinimo;
        return fimDoProximoUtil > minimo ? fimDoProximoUtil : minimo;
    }

    /// <summary>
    /// Os avisos, dados os contatos de confirmação das sessões e o momento. As regras:
    /// <list type="bullet">
    /// <item><b>Só sessão FUTURA dentro do horizonte</b> (<see cref="FimDoHorizonte"/>). A que
    /// já passou não tem mais o que remarcar; a de daqui a uma semana não tem lembrete ainda.
    /// O agendamento cancelado ou remarcado já saiu na consulta (é o "resolvido" natural: a
    /// recepção ligou e remarcou, o aviso some).</item>
    /// <item><b>Confirmou</b> só quando quem respondeu foi o PACIENTE (link do SMS ou WhatsApp
    /// do CRM) e a resposta chegou desde <paramref name="confirmacoesDesde"/>. A confirmação que
    /// a própria recepção marcou depois de ligar não é notícia para ela — e trinta delas
    /// enterrariam o "não vem" que pede ação.</item>
    /// <item><b>Não vem</b> sempre, de qualquer origem, enquanto o horário seguir marcado: é a
    /// pendência que só a ligação resolve.</item>
    /// <item><b>Sem resposta</b> — conservador de propósito: o contato está <c>Enviado</c> (nem
    /// respondido nem dispensado), saiu COM LINK (só o SMS tem link: no WhatsApp manual e na
    /// ligação a resposta é a recepção quem registra, e "não respondeu" ali é julgamento dela),
    /// foi enviado há pelo menos <see cref="EsperaPorResposta"/> e a sessão ainda está no
    /// horizonte. O momento do aviso é o envio + a espera.</item>
    /// </list>
    /// Ordem: o que pede ação primeiro (não vem, sem resposta), e dentro de cada tipo pela
    /// hora da sessão — a mais próxima é a que tem menos tempo para a ligação.
    /// </summary>
    public static IReadOnlyList<AvisoDeConfirmacao> Decidir(
        IEnumerable<ContatoParaAvisoDeConfirmacao> contatos, DateTime agora, DateTime confirmacoesDesde)
    {
        var fim = FimDoHorizonte(agora);
        var avisos = new List<AvisoDeConfirmacao>();
        // Um aviso por horário: a origem "AGD:{id}" é única, mas dado anterior à idempotência
        // pode ter dois contatos — o mais recente (o respondido vence o enviado) decide.
        foreach (var c in contatos
                     .Where(c => c.Sessao > agora && c.Sessao < fim)
                     .GroupBy(c => c.AgendamentoId)
                     .Select(g => g.OrderByDescending(c => c.Status == StatusContato.Respondido)
                                   .ThenByDescending(c => c.RespondidoEm ?? c.EnviadoEm).First()))
        {
            var situacao = RodadaDeConfirmacao.Classificar(c.Status, c.EnviadoEm, c.Comentario);
            // Resposta sem carimbo (dado antigo) conta como velha: nunca vira notícia "agora".
            var respondido = c.RespondidoEm ?? c.EnviadoEm ?? DateTime.MinValue;
            switch (situacao)
            {
                case SituacaoConfirmacao.PediuRemarcar:
                    avisos.Add(Aviso(c, TipoAvisoConfirmacao.NaoVem, respondido));
                    break;
                case SituacaoConfirmacao.PediuCancelar:
                    avisos.Add(Aviso(c, TipoAvisoConfirmacao.PediuCancelar, respondido));
                    break;
                case SituacaoConfirmacao.Confirmou when RespondeuOPaciente(c.Comentario) && respondido >= confirmacoesDesde:
                    avisos.Add(Aviso(c, TipoAvisoConfirmacao.Confirmou, respondido));
                    break;
                case SituacaoConfirmacao.AvisadoSemResposta
                    when c.Status == StatusContato.Enviado && c.TemLink
                         && c.EnviadoEm is { } enviado && agora - enviado >= EsperaPorResposta:
                    avisos.Add(Aviso(c, TipoAvisoConfirmacao.SemResposta, enviado + EsperaPorResposta));
                    break;
            }
        }

        return avisos
            .OrderBy(a => a.Tipo switch
            {
                TipoAvisoConfirmacao.NaoVem or TipoAvisoConfirmacao.PediuCancelar => 0,
                TipoAvisoConfirmacao.SemResposta => 1,
                _ => 2
            })
            .ThenBy(a => a.Sessao)
            .ThenBy(a => a.AgendamentoId)
            .ToList();
    }

    private static AvisoDeConfirmacao Aviso(ContatoParaAvisoDeConfirmacao c, TipoAvisoConfirmacao tipo, DateTime momento)
        => new(c.AgendamentoId, c.Paciente, c.Sessao, c.Profissional, tipo, momento);

    /// <summary>A confirmação veio do próprio paciente — pelo link do SMS ou pelo WhatsApp do CRM.</summary>
    public static bool RespondeuOPaciente(string? comentario)
        => string.Equals(comentario?.Trim(), RodadaDeConfirmacao.RespostaConfirmouPeloLink, StringComparison.OrdinalIgnoreCase)
           || string.Equals(comentario?.Trim(), RodadaDeConfirmacao.RespostaConfirmouPeloWhatsApp, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// O número que pede ação — o do sino e o do painel da direção, pela MESMA conta. Quem
    /// pediu para cancelar também não vem: conta junto do "não vem".
    /// </summary>
    public static (int NaoVem, int SemResposta) Contar(IEnumerable<AvisoDeConfirmacao> avisos)
    {
        int naoVem = 0, semResposta = 0;
        foreach (var a in avisos)
        {
            if (a.Tipo is TipoAvisoConfirmacao.NaoVem or TipoAvisoConfirmacao.PediuCancelar) naoVem++;
            else if (a.Tipo == TipoAvisoConfirmacao.SemResposta) semResposta++;
        }
        return (naoVem, semResposta);
    }

    /// <summary>
    /// Quais avisos viram NOTIFICAÇÃO agora (o aviso passageiro + a linha no histórico do
    /// sino). Cada um UMA vez por sessão do app — a batida relê a cada minuto, e repetir a
    /// mesma frase ensinaria a ignorá-la.
    ///
    /// ⚠️ <b>Reabrir o app não pode repetir o dia inteiro.</b> O conjunto de já anunciados
    /// vive em memória (gravá-lo em disco seria mais uma peça para quem divide a máquina e
    /// para a atualização). Por isso a PRIMEIRA leitura semeia: o que aconteceu antes de
    /// <paramref name="semearAte"/> (a abertura do app menos uma folga) entra no conjunto em
    /// silêncio — continua na LISTA do sino e no número, só não pisca de novo. Na leituras
    /// seguintes <paramref name="semearAte"/> é nulo, e tudo o que for novo no conjunto é
    /// anunciado, mesmo com o carimbo antigo (a resposta que o relógio do SMS só trouxe agora).
    /// </summary>
    public static IReadOnlyList<AvisoDeConfirmacao> ParaAnunciar(
        IReadOnlyList<AvisoDeConfirmacao> avisos,
        ISet<(int AgendamentoId, TipoAvisoConfirmacao Tipo)> jaAnunciados,
        DateTime? semearAte)
    {
        var novos = new List<AvisoDeConfirmacao>();
        foreach (var a in avisos)
        {
            if (semearAte is { } corte && a.Momento < corte) { jaAnunciados.Add(a.Chave); continue; }
            if (jaAnunciados.Add(a.Chave)) novos.Add(a);
        }
        return novos;
    }

    /// <summary>A folga da semeadura: o relógio do SMS lê o balde a cada 5 min, então o que chegou logo antes de abrir ainda é notícia.</summary>
    public static readonly TimeSpan FolgaDaAbertura = TimeSpan.FromMinutes(15);

    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>"hoje 14:00", "amanhã 08:30", "seg, 26/10 09:00" — como a recepção fala da sessão.</summary>
    public static string QuandoDaSessao(DateTime sessao, DateTime agora)
    {
        var dias = DateOnly.FromDateTime(sessao).DayNumber - DateOnly.FromDateTime(agora).DayNumber;
        var hora = sessao.ToString("HH:mm", PtBr);
        return dias switch
        {
            0 => $"hoje {hora}",
            1 => $"amanhã {hora}",
            _ => $"{DiaDaSemana(sessao.DayOfWeek)}, {sessao.ToString("dd/MM", PtBr)} {hora}"
        };
    }

    // Abreviação escrita à mão, como em RodadaDeConfirmacao.TituloDoDia: o "ddd" do pt-BR do .NET põe ponto.
    // Interna: a janela de 24 h do WhatsApp (ConversasNaTela) fala do dia com a mesma abreviação.
    internal static string DiaDaSemana(DayOfWeek d) => d switch
    {
        DayOfWeek.Monday => "seg", DayOfWeek.Tuesday => "ter", DayOfWeek.Wednesday => "qua",
        DayOfWeek.Thursday => "qui", DayOfWeek.Friday => "sex", DayOfWeek.Saturday => "sáb", _ => "dom"
    };

    /// <summary>A frase da notificação — o que aconteceu e, quando pede, o que fazer.</summary>
    public static string Frase(AvisoDeConfirmacao a, DateTime agora)
    {
        var quando = QuandoDaSessao(a.Sessao, agora);
        var com = string.IsNullOrWhiteSpace(a.Profissional) ? "" : $" ({a.Profissional})";
        return a.Tipo switch
        {
            TipoAvisoConfirmacao.Confirmou => $"{a.Paciente} confirmou a sessão de {quando}{com}",
            TipoAvisoConfirmacao.NaoVem => $"{a.Paciente} avisou que NÃO VEM — {quando}{com}: ligue para remarcar",
            TipoAvisoConfirmacao.PediuCancelar => $"{a.Paciente} pediu para CANCELAR — {quando}{com}: aceite o cancelamento na agenda",
            _ => $"{a.Paciente} não respondeu ao lembrete de {quando}{com}: vale uma ligação"
        };
    }

    /// <summary>A primeira linha do aviso no sino.</summary>
    public static string Titulo(AvisoDeConfirmacao a) => a.Tipo switch
    {
        TipoAvisoConfirmacao.Confirmou => $"{a.Paciente} confirmou",
        TipoAvisoConfirmacao.NaoVem => $"{a.Paciente} NÃO VEM — ligar para remarcar",
        TipoAvisoConfirmacao.PediuCancelar => $"{a.Paciente} pediu para CANCELAR — aceitar na agenda",
        _ => $"{a.Paciente} não respondeu ao lembrete"
    };

    /// <summary>A sessão de que se fala: "amanhã 14:00 · Dra. Ana".</summary>
    public static string Sessao(AvisoDeConfirmacao a, DateTime agora)
        => QuandoDaSessao(a.Sessao, agora) + (string.IsNullOrWhiteSpace(a.Profissional) ? "" : $" · {a.Profissional}");

    /// <summary>Desde quando: "respondeu às 10:42", "avisou ontem às 18:40", "SMS sem resposta desde 12:00".</summary>
    public static string Detalhe(AvisoDeConfirmacao a, DateTime agora)
    {
        if (a.Momento == DateTime.MinValue) return a.Tipo switch
        {
            TipoAvisoConfirmacao.NaoVem => "avisou que não vai",
            TipoAvisoConfirmacao.PediuCancelar => "pediu para cancelar",
            _ => ""
        };
        var dias = DateOnly.FromDateTime(agora).DayNumber - DateOnly.FromDateTime(a.Momento).DayNumber;
        var hora = a.Momento.ToString("HH:mm", PtBr);
        var quando = dias switch
        {
            0 => $"às {hora}",
            1 => $"ontem às {hora}",
            _ => $"em {a.Momento.ToString("dd/MM", PtBr)} às {hora}"
        };
        var desde = dias switch
        {
            0 => hora,
            1 => $"ontem {hora}",
            _ => $"{a.Momento.ToString("dd/MM", PtBr)} {hora}"
        };
        return a.Tipo switch
        {
            TipoAvisoConfirmacao.Confirmou => $"respondeu {quando}",
            TipoAvisoConfirmacao.NaoVem => $"avisou {quando}",
            TipoAvisoConfirmacao.PediuCancelar => $"pediu {quando}",
            _ => $"SMS sem resposta desde {desde}"
        };
    }
}

/// <summary>
/// O dia que a tela de Confirmações deve abrir quando a pessoa clica num aviso do sino
/// (out/2026). Estático pela mesma razão de <c>NavegacaoSuite</c>: o shell e a tela Blazor
/// não se conhecem, e há uma janela de shell por processo. A tela CONSOME o pedido ao
/// carregar — sem pedido, ela abre em "amanhã", como sempre.
/// </summary>
public static class AberturaDasConfirmacoes
{
    private static DateOnly? _dia;
    public static void PedirDia(DateOnly dia) => _dia = dia;
    public static DateOnly? ConsumirDia() { var d = _dia; _dia = null; return d; }
}
