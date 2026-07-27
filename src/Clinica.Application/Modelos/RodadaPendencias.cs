using Clinica.Domain;

namespace Clinica.Application.Modelos;

/// <summary>
/// Situação da rodada de pendências ("rodar as pendências"). O prazo é POR GUIA: cada pendência
/// exige decisão (baixa ou não conformidade) quando fica pendente há mais do que o intervalo
/// configurado (contado da data prevista de faturamento). Alimenta o banner do painel e o bloqueio.
/// </summary>
public sealed record RodadaPendenciasStatus(
    int IntervaloDias,
    /// <summary>Há ao menos uma guia que passou do prazo (intervalo) sem decisão.</summary>
    bool Vencida,
    /// <summary>Quantas guias já passaram do prazo e exigem decisão (baixa ou não conformidade).</summary>
    int GuiasParaDecisao,
    /// <summary>Atraso (em dias além do prazo) da guia mais antiga que exige decisão — para o banner.</summary>
    int MaiorAtrasoDias,
    bool AplicaConsultas,
    int ConsultasParaRevisar,
    bool AplicaCarteirinhas,
    int CarteirinhasParaRevisar)
{
    /// <summary>Há guias que passaram do prazo, aguardando decisão.</summary>
    public bool TemGuiasParaDecisao => GuiasParaDecisao > 0;

    /// <summary>Gatilho do aviso bloqueante: alguma guia passou do prazo sem decisão.</summary>
    public bool ExigeDecisao => Vencida;
}

/// <summary>Uma guia marcada como não conformidade (backlog documentado, exibido no relatório e reabrível).</summary>
public sealed record NaoConformidadeItem(
    int CodigoId,
    string PacienteNome,
    Convenio Convenio,
    TipoCodigo Tipo,
    OrdemCodigo Ordem,
    DateOnly DataPrevista,
    string Justificativa,
    DateTime? Em);
