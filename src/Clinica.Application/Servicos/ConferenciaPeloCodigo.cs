using System.Globalization;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// A RESPOSTA da conferência de um papel pelo código impresso no rodapé (out/2026, quando a tela
/// Documentos emitidos do Gerente saiu do WPF). É o ato de quem RECEBE um documento de fora — a
/// empresa que ligou, a escola, o convênio —, e o que este sistema oferece no lugar da
/// assinatura ICP-Brasil no papel que não a tem.
///
/// Mora aqui porque é FRASE que a tela afirma ("válido", "CANCELADO em …", "não saiu deste
/// sistema") e porque duas telas fazem a mesma conferência (a central da Recepção e a da
/// direção): duas redações divergiriam na primeira correção.
///
/// ⚠️ "Não confere" é RESPOSTA, não erro de sistema — e é a resposta que a conferência existe
/// para dar. Por isso ela tem o mesmo destaque do cancelado: os dois pedem atenção.
/// </summary>
public static class ConferenciaPeloCodigo
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public const string CodigoEmBranco = "Digite o código impresso no rodapé do documento.";

    public static string NaoEncontrado(string codigo)
        => $"Nenhum documento com o código \"{codigo}\". Confira a digitação; se estiver certa, o papel não saiu deste sistema.";

    /// <summary>
    /// "Receituário 2026/0012 · Maria Silva · emitido em 03/10/2026 · válido" — ou a
    /// situação de cancelado com a data e o motivo.
    /// </summary>
    public static string Achado(DocumentoClinico doc)
    {
        var quem = doc.Paciente?.Nome ?? "(paciente removido)";
        var situacao = doc.Cancelado
            ? $"CANCELADO em {doc.CanceladoEm?.ToString("dd/MM/yyyy", PtBr)} — {(string.IsNullOrWhiteSpace(doc.MotivoCancelamento) ? DocumentosEmitidosNaTela.SemMotivo : doc.MotivoCancelamento)}"
            : "válido";
        return $"{CentralDocumentosService.RotularClinico(doc.Tipo)} {doc.Numero} · {quem} · "
               + $"emitido em {doc.Data.ToString("dd/MM/yyyy", PtBr)} · {situacao}";
    }
}
