namespace Clinica.Application.Modelos;

/// <summary>
/// Os textos que a clínica manda ao paciente sobre a agenda dele — UMA definição, lida
/// pelo WhatsApp de um clique (shell) e pelo e-mail automático (set/2026).
///
/// Moram na Application e não no shell porque o e-mail sai de um serviço sem tela, e a
/// Application não enxerga o WPF. Duas redações da mesma confirmação — uma no botão, outra
/// no e-mail — divergiriam na primeira correção, e o paciente receberia frases diferentes
/// pelo mesmo motivo.
///
/// Nenhuma mensagem leva dado clínico: notificação aparece em tela bloqueada, e o telefone
/// ou o e-mail podem não ser só do paciente (a regra da cobrança, parcela 23).
/// </summary>
public static class MensagensDeContato
{
    /// <summary>"Maria" de "Maria da Silva" — como se fala com alguém pelo telefone.</summary>
    public static string PrimeiroNome(string nome)
        => nome.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? nome;

    /// <summary>
    /// A confirmação da sessão marcada. "hoje"/"amanhã" em relação a <paramref name="hoje"/>
    /// — o relógio vem de fora para a frase ser testável — e, mais longe, o dia da semana com
    /// a data ("segunda-feira, 08/09"): o lembrete que sai na sexta para a segunda precisa
    /// dizer QUAL dia, e "08/09" sozinho obriga a pessoa a abrir o calendário.
    /// </summary>
    public static string ConfirmacaoDeSessao(
        string nomePaciente, DateTime quando, DateOnly hoje, string? nomeClinica = null)
        => $"Olá, {PrimeiroNome(nomePaciente)}! Estamos confirmando sua sessão {QuandoPorExtenso(quando, hoje)}."
         + " Se tiver algum imprevisto, é só responder por aqui."
         + Assinatura(nomeClinica);

    /// <summary>O assunto do e-mail da confirmação — a mesma referência de dia do corpo.</summary>
    public static string AssuntoConfirmacao(DateTime quando, DateOnly hoje, string? nomeClinica = null)
        => $"Confirmação da sua sessão {QuandoPorExtenso(quando, hoje)}"
         + (string.IsNullOrWhiteSpace(nomeClinica) ? string.Empty : $" · {nomeClinica}");

    /// <summary>
    /// "hoje às 14:00" · "amanhã às 14:00" · "segunda-feira, 08/09 às 14:00". Cultura FIXA
    /// em pt-BR: a frase é ENVIADA, e dois postos com culturas diferentes mandariam
    /// "Monday" e "segunda-feira" pelo mesmo motivo.
    /// </summary>
    public static string QuandoPorExtenso(DateTime quando, DateOnly hoje)
    {
        var dia = DateOnly.FromDateTime(quando);
        var quandoTexto = dia == hoje ? "hoje"
            : dia == hoje.AddDays(1) ? "amanhã"
            : $"{quando.ToString("dddd", PtBr)}, {quando:dd/MM}";
        return $"{quandoTexto} às {quando:HH:mm}";
    }

    private static readonly System.Globalization.CultureInfo PtBr = new("pt-BR");

    /// <summary>
    /// O convite para marcar o retorno que quem atendeu pediu (a fila "Retornos a
    /// marcar"). Diz QUEM pediu e PARA QUANDO; não diz por quê — o motivo é registro
    /// clínico e não sai numa mensagem.
    /// </summary>
    public static string ConviteDeRetorno(
        string nomePaciente, DateOnly retornoEm, string profissional, string? nomeClinica = null)
        => $"Olá, {PrimeiroNome(nomePaciente)}! {profissional} pediu seu retorno por volta de "
           + $"{retornoEm:dd/MM}. Podemos marcar um horário? Responda por aqui com o melhor dia para você."
           + Assinatura(nomeClinica);

    /// <summary>
    /// O convite de RECALL: o paciente parou de vir e a clínica chama de volta. Morava no
    /// shell (`Whatsapp.Recall`); desceu para cá quando a fila de Acompanhamento virou tela
    /// Blazor (out/2026) — duas redações divergiriam na primeira correção.
    /// </summary>
    public static string Recall(string nomePaciente, int? diasSemVir = null, string? nomeClinica = null)
    {
        var tempo = diasSemVir is >= 60 ? " Faz um tempinho desde a sua última sessão." : string.Empty;
        return $"Olá, {PrimeiroNome(nomePaciente)}! Tudo bem?{tempo}"
             + " Estamos com horários abertos e podemos reservar um para você — quer que eu marque?"
             + Assinatura(nomeClinica);
    }

    /// <summary>O primeiro contato com quem o profissional indicou para o BSV.</summary>
    public static string IndicacaoBsv(string nomePaciente, string? nomeClinica = null)
        => $"Olá, {PrimeiroNome(nomePaciente)}! Aqui é da clínica. Podemos conversar sobre o agendamento do seu atendimento?"
           + Assinatura(nomeClinica);

    /// <summary>
    /// O SMS da confirmação (out/2026). CURTO de propósito: tem de caber em UM segmento de
    /// 160 caracteres com o link — cada segmento a mais é cobrado em toda sessão da clínica.
    /// Sai SEM ACENTO (<see cref="Clinica.Domain.TextoSms.ParaGsm"/>), e quem chama não
    /// precisa lembrar disso. Sem link (publicação não configurada), pede resposta por
    /// telefone em vez de prometer um botão que não existe.
    /// </summary>
    public static string ConfirmacaoPorSms(
        string nomePaciente, DateTime quando, DateOnly hoje, string? nomeClinica, string? link)
    {
        var quem = string.IsNullOrWhiteSpace(nomeClinica) ? string.Empty : $"{nomeClinica.Trim()}: ";
        var corpo = $"{quem}{PrimeiroNome(nomePaciente)}, sua sessao e {QuandoPorExtenso(quando, hoje)}.";
        corpo += link is null
            ? " Se nao puder vir, ligue para a clinica."
            : $" Confirme ou desmarque: {link}";
        var texto = Clinica.Domain.TextoSms.ParaGsm(corpo);

        // O nome da clínica é o que mais cresce; se estourar um segmento, ele sai primeiro.
        if (texto.Length > Clinica.Domain.TextoSms.CaracteresPorSegmentoGsm && quem.Length > 0)
            return ConfirmacaoPorSms(nomePaciente, quando, hoje, null, link);
        return texto;
    }

    private static string Assinatura(string? nomeClinica)
        => string.IsNullOrWhiteSpace(nomeClinica) ? string.Empty : $" — {nomeClinica}";
}
