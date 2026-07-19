using Clinica.Domain;

namespace Clinica.Application.Modelos;

/// <summary>Resumo de faturamento de um período: a métrica-chave é a % de baixa.</summary>
public sealed record ResumoFaturamento(
    int TotalCodigos,
    int Baixados,
    int Pendentes,
    double TaxaBaixa,               // 0..100
    int Glosadas = 0,               // guias baixadas que sofreram glosa (qualquer situação)
    double TaxaGlosa = 0,           // % das baixadas que foram glosadas
    double? TempoMedioBaixaDias = null); // média atendimento → baixa (nulo sem baixas)

/// <summary>Quebra do faturamento por convênio no período.</summary>
public sealed record FaturamentoPorConvenio(
    Convenio Convenio,
    int TotalCodigos,
    int Baixados,
    int Pendentes,
    double TaxaBaixa,
    int Glosadas = 0,
    double TaxaGlosa = 0,
    double? TempoMedioBaixaDias = null);

/// <summary>Envelhecimento das pendências em aberto (por faixa de atraso).</summary>
public sealed record FaixaEnvelhecimento(
    string Faixa,
    int Quantidade);

/// <summary>Resumo de um mês no comparativo mensal (evolução da taxa de baixa).</summary>
public sealed record ResumoMensal(
    int Ano,
    int Mes,
    string Rotulo,        // ex.: "mar/2026"
    int TotalCodigos,
    int Baixados,
    int Pendentes,
    double TaxaBaixa);

/// <summary>Relatório completo exibido na tela.</summary>
public sealed record RelatorioFaturamento(
    DateOnly Inicio,
    DateOnly Fim,
    ResumoFaturamento Resumo,
    IReadOnlyList<FaturamentoPorConvenio> PorConvenio,
    IReadOnlyList<FaixaEnvelhecimento> Envelhecimento);
