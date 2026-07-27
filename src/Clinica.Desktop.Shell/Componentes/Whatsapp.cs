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
