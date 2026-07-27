using Clinica.Domain;

namespace Clinica.Application.Modelos;

/// <summary>
/// Situação da rodada de pendências. O prazo é contado POR ATENDIMENTO: cada guia pendente vence
/// <see cref="PrazoDias"/> dias (padrão 10) depois do atendimento do paciente. Ao vencer sem baixa,
/// a guia EXIGE uma decisão (baixa ou não conformidade) e bloqueia o uso até a resolução.
/// Alimenta o banner do painel e o gatilho do aviso bloqueante.
/// </summary>
public sealed record RodadaPendenciasStatus(
    /// <summary>Prazo (em dias, desde o atendimento) para exigir decisão em cada guia.</summary>
    int PrazoDias,
    /// <summary>Guias pendentes cujo prazo já venceu — exigem decisão (baixa ou não conformidade) e bloqueiam.</summary>
    int GuiasParaDecisao,
    /// <summary>Total de guias pendentes (inclui as que ainda estão dentro do prazo).</summary>
    int GuiasPendentes,
    bool AplicaConsultas,
    int ConsultasParaRevisar,
    bool AplicaCarteirinhas,
    int CarteirinhasParaRevisar)
{
    /// <summary>Há guias com prazo vencido aguardando decisão.</summary>
    public bool TemGuiasParaDecisao => GuiasParaDecisao > 0;

    /// <summary>Há guias vencidas sem decisão — gatilho do aviso bloqueante.</summary>
    public bool ExigeDecisao => GuiasParaDecisao > 0;
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
