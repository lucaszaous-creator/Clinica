namespace Clinica.Application.Modelos;

/// <summary>Decisão da operadora sobre uma guia, vinda do demonstrativo de análise de conta.</summary>
public sealed record RetornoGuiaDecisao(
    int CodigoId,
    bool Glosada,
    string? MotivoCodigo,   // tabela de glosas da ANS
    string? MotivoTexto);   // complemento livre
