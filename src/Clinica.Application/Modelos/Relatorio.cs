using Clinica.Domain;
using Clinica.Domain.Regras;

namespace Clinica.Application.Modelos;

/// <summary>Resumo de faturamento de um período: a métrica-chave é a % de baixa.</summary>
public sealed record ResumoFaturamento(
    int TotalCodigos,
    int Baixados,
    int Pendentes,
    double TaxaBaixa,               // 0..100
    int Glosadas = 0,               // guias baixadas que sofreram glosa (qualquer situação)
    double TaxaGlosa = 0,           // % das baixadas que foram glosadas
    double? TempoMedioBaixaDias = null, // média atendimento → baixa (nulo sem baixas)
    int NaoConformidades = 0);      // guias justificadas como não conformidade numa rodada

/// <summary>
/// Quebra do faturamento por convênio no período.
///
/// ⚠️ A quebra é por OPERADORA (<see cref="ConvenioCodigo"/>), nunca pela família de
/// regra. Agrupar pela família fundia numa linha só todas as operadoras que a clínica
/// cadastrou — elas compartilham <see cref="Convenio.Personalizado"/> —, e o rótulo dessa
/// linha era o nome de UMA delas. A direção lia "Porto Saúde: 143 guias" e eram Porto
/// Saúde mais Sul América somadas. É a mesma razão pela qual o lote TISS agrupa por
/// operadora (parcela 60), aqui aplicada ao número que a direção usa para negociar.
/// </summary>
public sealed record FaturamentoPorConvenio(
    Convenio Convenio,
    int TotalCodigos,
    int Baixados,
    int Pendentes,
    double TaxaBaixa,
    int Glosadas = 0,
    double TaxaGlosa = 0,
    double? TempoMedioBaixaDias = null,
    int NaoConformidades = 0)
{
    /// <inheritdoc cref="PendenciaCodigo.ConvenioCodigo"/>
    public string? ConvenioCodigo { get; init; }

    /// <inheritdoc cref="PendenciaCodigo.ConvenioNome"/>
    public string ConvenioNome => CatalogoConvenios.Nome(ConvenioCodigo, Convenio);
}

/// <summary>Quantas consultas de cada especialidade a clínica fez no período.</summary>
public sealed record ConsultasPorEspecialidade(
    string Especialidade,
    int Quantidade,
    int Baixadas);

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
    IReadOnlyList<FaixaEnvelhecimento> Envelhecimento,
    IReadOnlyList<ConsultasPorEspecialidade> ConsultasEspecialidades,
    IReadOnlyList<NaoConformidadeItem> NaoConformidades);
