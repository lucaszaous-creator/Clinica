using Clinica.Application.Modelos;

namespace Clinica.Ui.Blazor.Hospedagem;

/// <summary>
/// O que a tela de Exames pede ao aplicativo e que continua fora dela (out/2026): a emissão do
/// pedido (a MESMA janela de documento de todas as portas) e o paciente na seção de exames (a
/// navegação do posto). Escolher o paciente, registrar o laudo e ver os resultados são formulários
/// Blazor da própria tela. A prévia responde com <see cref="JanelasDosExamesIndisponiveis"/>.
/// </summary>
public interface IJanelasDosExames
{
    /// <summary>Abre a emissão do pedido de exame para este paciente; verdadeiro quando o pedido saiu.</summary>
    Task<bool> NovoPedidoAsync(int pacienteId);

    /// <summary>Abre o paciente na seção "Exames e anexos"; falso quando o posto não alcança.</summary>
    Task<bool> AbrirExamesDoPacienteAsync(PedidoDeExameLinha pedido);
}

/// <summary>Sem hospedeiro WPF (prévia, testes): nada abre, e a tela diz isso com uma frase.</summary>
public sealed class JanelasDosExamesIndisponiveis : IJanelasDosExames
{
    public static readonly JanelasDosExamesIndisponiveis Instancia = new();

    public const string Frase = "Esta ação abre no aplicativo do Consultório.";

    public Task<bool> NovoPedidoAsync(int pacienteId) => Task.FromResult(false);
    public Task<bool> AbrirExamesDoPacienteAsync(PedidoDeExameLinha pedido) => Task.FromResult(false);
}
