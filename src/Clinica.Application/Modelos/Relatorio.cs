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
    double? TempoMedioBaixaDias = null, // média atendimento → baixa (nulo sem baixas)
    int NaoConformidades = 0);      // guias justificadas como não conformidade numa rodada

/// <summary>
/// Quebra do faturamento por convênio no período.
///
/// ⚠️ <b>A quebra é por OPERADORA, não por família de regra.</b> Até a parcela 68 o
/// agrupamento era pelo enum <see cref="Domain.Entities.Convenio"/>, e todas as operadoras
/// que a clínica cadastrou em Configurações — Sul América, Unimed Costa do Sol, qualquer
/// uma fora dos cinco embutidos — resolvem para <c>Convenio.Personalizado</c>. Elas caíam
/// numa linha só, com o nome literal do enum: <b>"Personalizado"</b>.
///
/// A tabela existe para responder "a clínica está perdendo faturamento, e ONDE" — e era
/// justamente o ONDE que ela fundia. É a mesma razão pela qual o
/// <c>RentabilidadeConvenioService</c> agrupa pelo CÓDIGO desde a parcela 19, com o motivo
/// escrito ao lado; aqui a lição não tinha sido aplicada.
///
/// <c>ConvenioCodigo</c> é ADITIVO com padrão nulo, pelo padrão do projeto para record
/// compartilhado com o app de faturamento em produção (parcela 50).
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
    int NaoConformidades = 0,
    string? ConvenioCodigo = null)
{
    /// <summary>
    /// O nome da OPERADORA, resolvido no ponto único: o código vence, a família é o
    /// caminho de baixo. Resolvido AQUI, e não em cada tela, porque são duas — o Gerente e
    /// o app de faturamento — e duas resoluções divergiriam na primeira correção.
    /// </summary>
    public string Nome => Clinica.Domain.Regras.CatalogoConvenios.Nome(ConvenioCodigo, Convenio);
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
