using Clinica.Application.Modelos;

namespace Clinica.Ui.Blazor.Hospedagem;

/// <summary>
/// As janelas que a tela de Exames abre e que continuam WPF (out/2026): a emissão do pedido
/// (a MESMA janela de documento de todas as portas), o registro do laudo amarrado ao pedido,
/// os resultados do pedido e o paciente na seção de exames. A tela decide e mostra; quem
/// conhece as janelas é o hospedeiro — a prévia responde com <see cref="JanelasDosExamesIndisponiveis"/>.
/// </summary>
public interface IJanelasDosExames
{
    /// <summary>Pergunta QUEM e abre a emissão; devolve o nome do paciente quando o pedido saiu.</summary>
    Task<string?> NovoPedidoAsync();

    /// <summary>Registra o laudo já amarrado a este pedido; verdadeiro quando gravou.</summary>
    Task<bool> RegistrarResultadoAsync(PedidoDeExameLinha pedido);

    /// <summary>Os resultados que respondem a este pedido, num modal.</summary>
    Task VerResultadosAsync(PedidoDeExameLinha pedido);

    /// <summary>Abre o paciente na seção "Exames e anexos"; falso quando o posto não alcança.</summary>
    Task<bool> AbrirExamesDoPacienteAsync(PedidoDeExameLinha pedido);
}

/// <summary>Sem hospedeiro WPF (prévia, testes): nada abre, e a tela diz isso com uma frase.</summary>
public sealed class JanelasDosExamesIndisponiveis : IJanelasDosExames
{
    public static readonly JanelasDosExamesIndisponiveis Instancia = new();

    public const string Frase = "Esta ação abre no aplicativo do Consultório.";

    public Task<string?> NovoPedidoAsync() => Task.FromResult<string?>(null);
    public Task<bool> RegistrarResultadoAsync(PedidoDeExameLinha pedido) => Task.FromResult(false);
    public Task VerResultadosAsync(PedidoDeExameLinha pedido) => Task.CompletedTask;
    public Task<bool> AbrirExamesDoPacienteAsync(PedidoDeExameLinha pedido) => Task.FromResult(false);
}
