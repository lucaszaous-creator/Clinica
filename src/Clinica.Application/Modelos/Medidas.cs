using Clinica.Domain.Avaliacoes;
using Clinica.Domain.Entities;
using Clinica.Domain.Medidas;

namespace Clinica.Application.Modelos;

/// <summary>
/// Um ponto da série de uma medida (parcela 37).
///
/// <paramref name="Derivado"/> marca o que o sistema CALCULOU em vez de ter colhido — hoje
/// só o IMC. A tela precisa dizer isso: número calculado apresentado do mesmo jeito que
/// número medido faz o profissional procurar no papel uma aferição que nunca existiu.
/// </summary>
/// <param name="Procedencia">
/// De ONDE o ponto veio, escrito por extenso (parcela 72).
///
/// ⚠️ Nasce porque a curva de PRESSÃO ARTERIAL passou a mesclar duas fontes: a
/// <c>MedidaClinica</c>, colhida no consultório, e os SINAIS VITAIS da evolução de
/// enfermagem, aferidos na sala. Sem a procedência na linha, a curva teria dois pontos do
/// mesmo dia sem dizer que um é de antes da consulta e o outro de meia hora depois da
/// bomba — e a diferença entre os dois é justamente a leitura clínica.
///
/// Nulo = colhido pela porta padrão do tipo; a tela não escreve nada.
/// </param>
public sealed record PontoMedida(
    DateOnly Data,
    decimal Valor,
    decimal? ValorSecundario,
    string? FaixaNome,
    GravidadeFaixa Gravidade,
    bool Derivado = false,
    string? Procedencia = null,
    TimeOnly? Hora = null);

/// <summary>
/// A série de uma medida ao longo do tratamento, com a leitura de ponta a ponta.
///
/// A variação é do PRIMEIRO ao ÚLTIMO ponto, e o sinal é lido pelo <see cref="TipoMedida.
/// PiorQuandoMaior"/> — não se pode dizer "melhorou" olhando só se o número caiu, porque a
/// próxima medida do catálogo pode ser das que sobem quando o paciente melhora (é o que o
/// Katz já é, do lado das escalas).
/// </summary>
public sealed record SerieMedida(
    string TipoCodigo,
    string TipoNome,
    string Unidade,
    IReadOnlyList<PontoMedida> Pontos)
{
    public bool TemDados => Pontos.Count > 0;

    /// <summary>Primeiro valor colhido. Null sem nenhum ponto.</summary>
    public decimal? Primeiro => Pontos.Count == 0 ? null : Pontos[0].Valor;

    /// <summary>Valor mais recente.</summary>
    public decimal? Atual => Pontos.Count == 0 ? null : Pontos[^1].Valor;

    /// <summary>
    /// Quanto mudou do primeiro ao último. Null com menos de dois pontos: uma medida só
    /// não é variação nenhuma, e mostrar 0 diria que o paciente estacionou.
    /// </summary>
    public decimal? Variacao
        => Pontos.Count < 2 ? null : Pontos[^1].Valor - Pontos[0].Valor;

    /// <summary>
    /// A variação é uma MELHORA. Null quando não há variação apurável ou quando o tipo
    /// não está mais no catálogo — sem saber para que lado é bom, afirmar seria chutar.
    /// </summary>
    public bool? Melhorou
    {
        get
        {
            if (Variacao is not { } v || v == 0) return null;
            if (CatalogoMedidas.Obter(TipoCodigo) is not { } tipo) return null;
            return tipo.PiorQuandoMaior ? v < 0 : v > 0;
        }
    }
}

/// <summary>
/// A última colheita de cada medida do paciente — o cartão que abre com ele.
///
/// <paramref name="AlturaUsadaEm"/> existe por honestidade: o IMC é derivado do peso de
/// hoje com a altura mais recente que houver, e essa altura pode ser de três anos atrás. A
/// PROCEDÊNCIA do número vai junto pelo mesmo motivo que a proposta de valor do preço por
/// convênio a leva — campo que se preenche sozinho sem explicar faz a pessoa confirmar sem
/// conferir.
/// </summary>
public sealed record ResumoMedidas(
    IReadOnlyList<MedidaClinica> Ultimas,
    decimal? Imc,
    string? ImcFaixa,
    GravidadeFaixa ImcGravidade,
    DateOnly? ImcEm,
    DateOnly? AlturaUsadaEm)
{
    public bool TemImc => Imc is not null;
}
