using System.Globalization;
using Clinica.Application.Servicos;

namespace Clinica.Application.Formatacao;

/// <summary>
/// As frases do formulário de ESTORNO (parcela 94; out/2026, migrado do
/// <c>EstornoAtendimentoViewModel</c> WPF). Só é oferecido o que aquele atendimento de fato
/// produziu: caixinha para desfazer o que não existe é botão que não faz nada (parcela 41).
/// </summary>
public static class EstornoNaTela
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static string Titulo(PreviaDoEstorno p) =>
        // Sem número gravado o serviço devolve "#<id>": "nº #412" diria o número duas vezes.
        p.Numero.StartsWith('#') ? $"Estornar o atendimento {p.Numero}" : $"Estornar o atendimento nº {p.Numero}";

    public static string Subtitulo(PreviaDoEstorno p) => $"{p.Paciente} · {p.Modalidade} · {p.Data.ToString("dd/MM/yyyy", PtBr)}";

    /// <summary>As GUIAS saem sempre: são a razão do estorno.</summary>
    public static string Guias(PreviaDoEstorno p) => p.GuiasAnulaveis switch
    {
        0 => "Não há guia em aberto para anular.",
        1 => "1 guia será anulada (fica no histórico, marcada como estornada).",
        var n => $"{n} guias serão anuladas (ficam no histórico, marcadas como estornadas)."
    };

    public static string? RotuloCaixa(PreviaDoEstorno p)
        => p.EntradaNoCaixa is { } v ? $"Cancelar a entrada no caixa ({v.ToString("C", PtBr)})" : null;

    public const string RotuloPacote = "Devolver a sessão ao saldo do pacote";

    public static string? RotuloInsumo(PreviaDoEstorno p) => !p.TemInsumo ? null
        : p.InsumosBaixados == 1
            ? "Devolver 1 insumo ao estoque (entrada compensatória)"
            : $"Devolver {p.InsumosBaixados} insumos ao estoque (entrada compensatória)";

    /// <summary>
    /// A consulta renovada é INFORMAÇÃO, sem caixinha: desfazê-la exigiria ressuscitar a
    /// anterior, e receita emitida sob a nova ficaria inválida.
    /// </summary>
    public static string? ConsultaRenovada(PreviaDoEstorno p) => p.ConsultaRenovadaEm is not { } em ? null
        : $"A consulta do convênio foi renovada em {em.ToString("dd/MM/yyyy", PtBr)} por esta sessão. Ela NÃO é desfeita aqui — "
          + "desfazer exigiria ressuscitar a anterior, e receita já emitida sob a nova ficaria inválida. Se for o caso, "
          + "resolva pela aba Consultas.";

    public static string? Horario(PreviaDoEstorno p) => p.AgendamentoId is null ? null
        : "O horário deste atendimento volta para \"Agendado\" e poderá ser lançado de novo.";

    /// <summary>O motivo é obrigatório — é o que fica na trilha. Sem ele o botão nem acende.</summary>
    public static bool PodeEstornar(PreviaDoEstorno? p, string? motivo, bool temPermissao)
        => p is not null && p.Pode && temPermissao && !string.IsNullOrWhiteSpace(motivo);
}
