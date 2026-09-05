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

    private static string Assinatura(string? nomeClinica)
        => string.IsNullOrWhiteSpace(nomeClinica) ? string.Empty : $" — {nomeClinica}";
}
