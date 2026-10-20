using Clinica.Domain.Entities;

namespace Clinica.Application.Modelos;

/// <summary>
/// O que a tela de Exames do Consultório AFIRMA (out/2026, migrado do <c>ExamesViewModel</c>
/// WPF para morar onde o teste alcança). A situação de cada pedido continua derivada de FATO
/// em <see cref="PedidoDeExameLinha"/>; aqui ficam a janela da lista e a frase do resumo.
/// </summary>
public static class PedidosDeExameNaTela
{
    /// <summary>A janela da lista. 90 dias cobrem o ciclo real de um laudo; o resumo DIZ o recorte.</summary>
    public const int JanelaDias = 90;

    /// <summary>"12 pedido(s) nos últimos 90 dias · 3 aguardando resultado".</summary>
    public static string Resumo(IReadOnlyCollection<PedidoDeExameLinha> pedidos)
    {
        if (pedidos.Count == 0) return $"Nenhum pedido nos últimos {JanelaDias} dias";
        var aguardando = pedidos.Count(p => p.Situacao == SituacaoPedidoExame.AguardandoResultado);
        return $"{pedidos.Count} pedido(s) nos últimos {JanelaDias} dias · {aguardando} aguardando resultado";
    }

    /// <summary>
    /// Um resultado no modal "Resultado — exame" (out/2026: subido do <c>ResultadosDoPedidoViewModel</c>
    /// WPF quando o modal virou Blazor): os campos do laudo como foram registrados.
    /// </summary>
    public sealed record ResultadoDoPedido(
        int Id, string Data, string? Laboratorio, string Nome, string Valor,
        string? Referencia, string? Observacoes, string? ArquivoNome)
    {
        /// <summary>Só a linha que TEM arquivo oferece "Abrir laudo": aceso sobre um resultado sem laudo, ele não faria nada.</summary>
        public bool TemArquivo => !string.IsNullOrWhiteSpace(ArquivoNome);
    }

    /// <summary>"Pedido 2026/0012 · 03/10/2026" — o cabeçalho do modal.</summary>
    public static string PedidoTexto(PedidoDeExameLinha pedido)
        => $"Pedido {pedido.Numero} · {pedido.Data.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture)}";

    /// <summary>
    /// Os resultados que respondem a ESTE pedido. O recorte é em memória: os resultados do paciente são
    /// poucos e o vínculo já veio na linha — uma consulta própria seria peça a mais. Cancelado não entra
    /// (o serviço já devolve só os vigentes; a guarda aqui é a segunda).
    /// </summary>
    public static IReadOnlyList<ResultadoDoPedido> ResultadosDoPedido(IEnumerable<ResultadoExame> doPaciente, int pedidoDocumentoId)
        => doPaciente
            .Where(r => r.PedidoDocumentoId == pedidoDocumentoId && r.CanceladoEm is null)
            .Select(r => new ResultadoDoPedido(
                r.Id, r.Data.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture),
                string.IsNullOrWhiteSpace(r.Laboratorio) ? null : r.Laboratorio,
                r.Nome, r.ResumoDoResultado,
                string.IsNullOrWhiteSpace(r.Referencia) ? null : r.Referencia,
                string.IsNullOrWhiteSpace(r.Observacoes) ? null : r.Observacoes,
                r.ArquivoNome))
            .ToList();

    /// <summary>O tom do selo da situação: aguardar é aviso, resultado é sucesso, cancelado é neutro.</summary>
    public static string Tom(PedidoDeExameLinha pedido) => pedido.Situacao switch
    {
        SituacaoPedidoExame.AguardandoResultado => "aviso",
        SituacaoPedidoExame.ResultadoDisponivel => "sucesso",
        _ => string.Empty
    };
}
