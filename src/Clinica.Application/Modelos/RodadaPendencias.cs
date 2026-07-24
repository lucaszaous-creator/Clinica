using Clinica.Domain;

namespace Clinica.Application.Modelos;

/// <summary>
/// Situação da rodada de pendências ("rodar as pendências" — o fechamento de ciclo periódico,
/// à moda do fechamento de diárias hoteleiro). Alimenta o banner do painel e o bloqueio ao vencer.
/// </summary>
public sealed record RodadaPendenciasStatus(
    int IntervaloDias,
    /// <summary>Última rodada concluída (null = ciclo ainda não ancorado).</summary>
    DateOnly? UltimaRodada,
    /// <summary>Quando a próxima rodada vence (null enquanto não houver âncora).</summary>
    DateOnly? ProximaRodada,
    bool Vencida,
    int DiasEmAtraso,
    /// <summary>Guias pendentes que exigem decisão (baixa ou não conformidade) nesta rodada.</summary>
    int GuiasParaDecisao,
    bool AplicaConsultas,
    int ConsultasParaRevisar,
    bool AplicaCarteirinhas,
    int CarteirinhasParaRevisar)
{
    /// <summary>Há guias aguardando decisão na rodada.</summary>
    public bool TemGuiasParaDecisao => GuiasParaDecisao > 0;

    /// <summary>A rodada venceu e ainda há guias sem decisão — é o gatilho do aviso bloqueante.</summary>
    public bool ExigeDecisao => Vencida && GuiasParaDecisao > 0;
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
