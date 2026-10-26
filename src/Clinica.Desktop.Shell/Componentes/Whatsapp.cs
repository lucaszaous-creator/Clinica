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
        var fone = Telefone.SemPrefixoDeDiscagem(Telefone.Normalizar(telefone));
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

    /// <summary>
    /// Mensagem padrão de confirmação de sessão. O TEXTO mora em
    /// <see cref="Clinica.Application.Modelos.MensagensDeContato"/> desde que o e-mail
    /// automático passou a mandar a MESMA confirmação (set/2026): duas redações divergiriam
    /// na primeira correção, e o paciente receberia frases diferentes pelo mesmo motivo.
    /// Aqui fica só a porta do WhatsApp.
    /// </summary>
    public static string ConfirmacaoDeSessao(string nomePaciente, DateTime quando, string? nomeClinica = null)
        => Clinica.Application.Modelos.MensagensDeContato.ConfirmacaoDeSessao(
            nomePaciente, quando, DateOnly.FromDateTime(DateTime.Today), nomeClinica);

    /// <summary>O convite para marcar o retorno pedido por quem atendeu (a fila "Retornos a marcar").</summary>
    public static string ConviteDeRetorno(string nomePaciente, DateOnly retornoEm, string profissional, string? nomeClinica = null)
        => Clinica.Application.Modelos.MensagensDeContato.ConviteDeRetorno(
            nomePaciente, retornoEm, profissional, nomeClinica);

    /// <summary>Mensagem padrão da pesquisa de satisfação — o TEXTO mora em <see cref="Clinica.Application.Modelos.MensagensDeContato.PesquisaDeSatisfacao"/>.</summary>
    public static string PesquisaDeSatisfacao(string nomePaciente, string? nomeClinica = null)
        => Clinica.Application.Modelos.MensagensDeContato.PesquisaDeSatisfacao(nomePaciente, nomeClinica);

    /// <summary>Mensagem padrão de recall — o TEXTO mora em <see cref="Clinica.Application.Modelos.MensagensDeContato.Recall"/>.</summary>
    public static string Recall(string nomePaciente, int? diasSemVir = null, string? nomeClinica = null)
        => Clinica.Application.Modelos.MensagensDeContato.Recall(nomePaciente, diasSemVir, nomeClinica);

    /// <summary>
    /// Mensagem padrão de cobrança do documento que falta para a guia. O TEXTO mora em
    /// <see cref="Clinica.Application.Modelos.InicioRecepcaoTela.CobrancaDeGuia"/> desde que o
    /// Início da Recepção virou Blazor (out/2026): duas redações divergiriam na primeira correção.
    /// </summary>
    public static string CobrancaDeGuia(string nomePaciente, string? nomeClinica = null)
        => Clinica.Application.Modelos.InicioRecepcaoTela.CobrancaDeGuia(nomePaciente, nomeClinica);
}
