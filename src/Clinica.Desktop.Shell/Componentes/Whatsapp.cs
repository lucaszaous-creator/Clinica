using System.Diagnostics;
using Clinica.Domain;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>
/// Abrir o WhatsApp do paciente num lugar só, para a suíte inteira: a regra do DDI e a
/// validação do telefone não se repetem por tela (foi assim que o faturamento acabou
/// com três cópias da mesma coisa).
/// </summary>
public static class Whatsapp
{
    /// <summary>
    /// Abre a conversa com a mensagem pronta. Devolve null quando abriu; caso contrário,
    /// a explicação para a tela mostrar — nunca lança e nunca falha em silêncio.
    /// </summary>
    public static string? Abrir(string? telefone, string nomePaciente, string mensagem)
    {
        var fone = Telefone.Normalizar(telefone);
        if (fone.Length is < 10 or > 13)
            return $"{nomePaciente}: telefone ausente ou inválido no cadastro.";

        if (fone.Length is 10 or 11)
            fone = "55" + fone; // wa.me exige DDI

        try
        {
            Process.Start(new ProcessStartInfo(
                $"https://wa.me/{fone}?text={Uri.EscapeDataString(mensagem)}")
            {
                UseShellExecute = true
            });
            return null;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Suíte — WhatsApp não pôde ser aberto", ex);
            return $"Não foi possível abrir o WhatsApp: {ex.Message}";
        }
    }

    /// <summary>Mensagem padrão de confirmação de sessão.</summary>
    public static string ConfirmacaoDeSessao(string nomePaciente, DateTime quando, string? nomeClinica = null)
    {
        var primeiroNome = nomePaciente
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault() ?? nomePaciente;

        var dia = quando.Date == DateTime.Today.AddDays(1) ? "amanhã" : quando.ToString("dd/MM");

        return $"Olá, {primeiroNome}! Estamos confirmando sua sessão {dia} às {quando:HH:mm}."
             + " Se tiver algum imprevisto, é só responder por aqui."
             + (string.IsNullOrWhiteSpace(nomeClinica) ? string.Empty : $" — {nomeClinica}");
    }

    /// <summary>
    /// Mensagem padrão da pesquisa de satisfação (parcela 5). A pergunta é a do NPS
    /// clássico, com a escala explícita: sem "de 0 a 10" a resposta vem em texto e a
    /// nota não existe.
    /// </summary>
    public static string PesquisaDeSatisfacao(string nomePaciente, string? nomeClinica = null)
    {
        var primeiroNome = PrimeiroNome(nomePaciente);

        return $"Olá, {primeiroNome}! De 0 a 10, o quanto você recomendaria a nossa clínica"
             + " para um amigo ou familiar? Sua resposta leva 10 segundos e nos ajuda muito."
             + (string.IsNullOrWhiteSpace(nomeClinica) ? string.Empty : $" — {nomeClinica}");
    }

    /// <summary>Mensagem padrão de recall: o paciente parou de vir e a clínica está chamando de volta.</summary>
    public static string Recall(string nomePaciente, int? diasSemVir = null, string? nomeClinica = null)
    {
        var primeiroNome = PrimeiroNome(nomePaciente);
        var tempo = diasSemVir is >= 60 ? " Faz um tempinho desde a sua última sessão." : string.Empty;

        return $"Olá, {primeiroNome}! Tudo bem?{tempo}"
             + " Estamos com horários abertos e podemos reservar um para você — quer que eu marque?"
             + (string.IsNullOrWhiteSpace(nomeClinica) ? string.Empty : $" — {nomeClinica}");
    }

    private static string PrimeiroNome(string nomePaciente)
        => nomePaciente.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? nomePaciente;

    /// <summary>Mensagem padrão de cobrança do documento que falta para a guia.</summary>
    public static string CobrancaDeGuia(string nomePaciente, string? nomeClinica = null)
    {
        var primeiroNome = nomePaciente
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault() ?? nomePaciente;

        return $"Olá, {primeiroNome}! Precisamos do documento/guia da sua última sessão para"
             + " concluir o faturamento junto ao convênio. Consegue nos enviar?"
             + (string.IsNullOrWhiteSpace(nomeClinica) ? string.Empty : $" — {nomeClinica}");
    }
}
