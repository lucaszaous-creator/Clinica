using System.Globalization;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Clinica.Domain.Avaliacoes;
using Clinica.Domain.Medidas;

namespace Clinica.Application.Modelos;

/// <summary>
/// O que a aba "Medidas" do Acompanhamento afirma (out/2026, subido da MedidasViewModel WPF na
/// migração da tela do paciente para Blazor): os cartões das últimas medidas (o IMC primeiro,
/// com a procedência da altura), a leitura da série do tipo acompanhado e as linhas do
/// histórico. Puro, com teste — a tela só desenha.
/// </summary>
public static class MedidasNaTela
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>Um cartão da fileira de cima.</summary>
    public sealed record Cartao(string Rotulo, string Valor, string Detalhe, bool Alerta, bool Derivado = false);

    /// <summary>Uma colheita do histórico (só as feitas no consultório — a enfermagem se corrige onde escreveu).</summary>
    public sealed record Linha(int MedidaId, string Data, string Tipo, string Valor, string Faixa, string Observacoes,
        bool FaixaAlerta, bool TemFaixa);

    /// <summary>A leitura da série do tipo acompanhado.</summary>
    public sealed record Leitura(
        string Primeiro, string Atual, string Variacao, string Frase, bool Preocupa, bool TemSerie,
        string? HistoricoSoDoConsultorio, IReadOnlyList<PontoDaSerie> Curva);

    public static IReadOnlyList<Cartao> Cartoes(ResumoMedidas resumo)
    {
        var cartoes = new List<Cartao>
        {
            // O IMC abre a fileira, com a PROCEDÊNCIA da altura: um IMC de uma altura de três
            // anos atrás continua sendo a melhor leitura disponível, desde que quem lê saiba.
            new("IMC (calculado)",
                resumo.TemImc ? $"{resumo.Imc!.Value.ToString("0.#", PtBr)} kg/m²" : "—",
                resumo.TemImc
                    ? $"{resumo.ImcFaixa} · peso de {resumo.ImcEm:dd/MM/yy} com a altura de {resumo.AlturaUsadaEm:dd/MM/yy}"
                    : resumo.Ultimas.Any(m => m.TipoCodigo == CatalogoMedidas.Peso)
                        ? "falta a ALTURA — o peso sozinho não se interpreta"
                        : "registre peso e altura",
                resumo.TemImc && resumo.ImcGravidade == GravidadeFaixa.Alerta,
                Derivado: true)
        };
        cartoes.AddRange(resumo.Ultimas.Select(m => new Cartao(
            m.TipoNome, m.ValorFormatado,
            m.TemFaixa ? $"{m.FaixaNome} · {m.Data:dd/MM/yy}" : $"colhido em {m.Data:dd/MM/yy}",
            m.FaixaGravidade == GravidadeFaixa.Alerta)));
        return cartoes;
    }

    public static string ResumoAtual(ResumoMedidas resumo)
        => resumo.Ultimas.Count == 0
            ? "Nenhuma medida registrada para este paciente ainda."
            : $"{resumo.Ultimas.Count} medida(s) acompanhada(s) · última colheita em "
              + $"{resumo.Ultimas.Max(m => m.Data):dd/MM/yyyy}";

    public static Linha LinhaDe(MedidaClinica m) => new(
        m.Id, m.Data.ToString("dd/MM/yyyy", PtBr), m.TipoNome, m.ValorFormatado, m.FaixaNome ?? string.Empty,
        m.Observacoes ?? string.Empty, m.FaixaGravidade == GravidadeFaixa.Alerta, m.TemFaixa);

    public static Leitura Ler(SerieMedida serie)
    {
        // Com duas fontes no mesmo dia, o rótulo precisa da HORA: dois "03/08" colados na
        // curva não se distinguem, e é a diferença entre eles que interessa.
        var curva = serie.Pontos.Select(p => new PontoDaSerie(
            p.Hora is { } h ? $"{p.Data:dd/MM} {h:HH\\:mm}" : p.Data.ToString("dd/MM", PtBr),
            (double)p.Valor)).ToList();

        if (!serie.TemDados)
            return new("—", "—", "—", $"Nenhuma colheita de {serie.TipoNome} para este paciente ainda.",
                false, false, null, curva);

        var primeiro = Formatar(serie.Primeiro, serie);
        var atual = Formatar(serie.Atual, serie);
        if (serie.Variacao is not { } v)
            // Uma medida só não é variação nenhuma: 0 diria que o paciente estacionou.
            return new(primeiro, atual, "—",
                $"Uma colheita só de {serie.TipoNome}: é a linha de base. A leitura começa na segunda.",
                false, true, Enfermagem(serie), curva);

        var sinal = v > 0 ? "+" : "−";
        var variacao = $"{sinal}{Math.Abs(v).ToString("0.#", PtBr)} {serie.Unidade}";
        var de = serie.Pontos[0].Data.ToString("dd/MM/yyyy", PtBr);
        var ate = serie.Pontos[^1].Data.ToString("dd/MM/yyyy", PtBr);
        var frase = serie.Melhorou switch
        {
            true => $"{serie.TipoNome} caminhou na direção esperada entre {de} e {ate}, em {serie.Pontos.Count} colheita(s).",
            false => $"{serie.TipoNome} piorou entre {de} e {ate}. Vale revisar a conduta.",
            _ => $"{serie.TipoNome} não mudou entre {de} e {ate}."
        };
        // A PROCEDÊNCIA vai na leitura: a curva de pressão mescla a colheita do consultório e
        // a aferição da enfermagem, e a diferença entre dois pontos do mesmo dia é a leitura.
        var fontes = serie.Pontos.Select(p => p.Procedencia).Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct().OrderBy(f => f).ToList();
        if (fontes.Count > 1) frase += " A curva junta duas procedências: " + string.Join(" e ", fontes) + ".";

        return new(primeiro, atual, variacao, frase, serie.Melhorou == false, true, Enfermagem(serie), curva);
    }

    /// <summary>
    /// A tabela lista só as colheitas desta tela; as aferições da enfermagem aparecem na curva
    /// e se corrigem na evolução de enfermagem. Gráfico com pontos sobre uma tabela vazia se lê
    /// como defeito — por isso a frase.
    /// </summary>
    private static string? Enfermagem(SerieMedida serie)
    {
        var daEnfermagem = serie.Pontos.Count(p => p.Procedencia == "enfermagem");
        return daEnfermagem > 0
            ? $"A tabela abaixo lista só as colheitas feitas aqui. As {daEnfermagem} aferição(ões) da ENFERMAGEM "
              + "aparecem na curva acima e no prontuário — elas se corrigem na evolução de enfermagem, onde foram escritas."
            : null;
    }

    private static string Formatar(decimal? valor, SerieMedida serie)
        => valor is { } v ? $"{v.ToString("0.#", PtBr)} {serie.Unidade}" : "—";

    /// <summary>
    /// O número digitado na colheita: vírgula ou ponto como decimal ("72,5" e "72.5" são o
    /// mesmo peso) — a MESMA leitura do formulário WPF.
    /// </summary>
    public static decimal? LerNumero(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var limpo = texto.Trim().Replace(',', '.');
        return decimal.TryParse(limpo, NumberStyles.Number, CultureInfo.InvariantCulture, out var valor) ? valor : null;
    }
}
