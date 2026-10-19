using System.Globalization;
using Clinica.Application.Servicos;
using Clinica.Domain.Avaliacoes;
using Clinica.Domain.Entities;

namespace Clinica.Application.Modelos;

/// <summary>
/// O que a aba "Avaliações" do Acompanhamento afirma (out/2026, subido da AvaliacoesViewModel e
/// da AplicarAvaliacaoViewModel WPF na migração da tela do paciente para Blazor). Puro, com
/// teste — a tela só desenha.
/// </summary>
public static class AvaliacoesNaTela
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>
    /// O lembrete que acompanha toda aplicação: o sistema pontua e registra; a interpretação e a
    /// conduta são de quem assina o prontuário.
    /// </summary>
    public const string Ressalva =
        "O sistema apenas pontua e registra o que foi respondido. A faixa é a leitura "
        + "publicada da escala — a interpretação e a conduta são de quem assina o prontuário.";

    public sealed record Linha(int AvaliacaoId, string Data, string Instrumento, string Pontuacao, string Faixa,
        string Observacoes, bool TemAlerta, string Alerta, bool FaixaAlerta);

    /// <summary>A leitura do escore do instrumento escolhido.</summary>
    public sealed record Leitura(string Primeira, string Atual, string Ganho, string Faixa, string Frase,
        bool TemCurva, IReadOnlyList<PontoDaSerie> Curva);

    public static Linha LinhaDe(AvaliacaoClinica a) => new(
        a.Id, a.Data.ToString("dd/MM/yyyy", PtBr), a.InstrumentoNome, a.PontuacaoFormatada,
        string.IsNullOrWhiteSpace(a.FaixaNome) ? "—" : a.FaixaNome!,
        a.Observacoes?.Trim() ?? string.Empty, a.TemAlertaDeItem, a.AlertaItem ?? string.Empty,
        a.FaixaGravidade == GravidadeFaixa.Alerta);

    public static string Resumo(int aplicacoes)
        => aplicacoes == 0
            ? "Nenhuma escala aplicada a este paciente ainda."
            : $"{aplicacoes} aplicação(ões) registradas no prontuário.";

    public static Leitura Ler(EvolucaoDoEscore evolucao, string instrumentoNome)
    {
        var curva = evolucao.Pontos.Select(p => new PontoDaSerie(p.Data.ToString("dd/MM", PtBr), p.Pontuacao)).ToList();
        if (evolucao.Aplicacoes == 0)
            return new("—", "—", "—", "—",
                $"{instrumentoNome} ainda não foi aplicado a este paciente. A primeira aplicação é a linha de base do tratamento.",
                false, curva);

        var unidade = evolucao.Unidade == "%" ? " %" : $" de {evolucao.PontuacaoMaxima}";
        var ganho = evolucao.Ganho is { } g
            ? g switch { > 0 => $"melhorou {g}", < 0 => $"piorou {-g}", _ => "sem mudança" }
            : "—";
        var frase = evolucao.Aplicacoes == 1
            ? "Uma aplicação só é a linha de base — não há evolução para ler ainda."
            : $"{evolucao.Aplicacoes} aplicações desde {evolucao.Pontos[0].Data.ToString("dd/MM/yyyy", PtBr)}.";
        return new($"{evolucao.Primeiro}{unidade}", $"{evolucao.Atual}{unidade}", ganho,
            evolucao.Pontos[^1].Faixa ?? "—", frase, true, curva);
    }

    /// <summary>A linha que acompanha o preenchimento: quantas faltam e a soma parcial.</summary>
    public static string Parcial(int total, IReadOnlyCollection<int> valoresRespondidos)
    {
        var respondidas = valoresRespondidos.Count;
        if (respondidas == 0) return $"{total} item(ns) a responder.";
        var soma = valoresRespondidos.Sum();
        var faltam = total - respondidas;
        return faltam > 0
            ? $"{respondidas} de {total} respondidas — faltam {faltam}. Soma parcial: {soma}."
            : $"Escala completa. Soma: {soma}.";
    }

    /// <summary>As escalas oferecidas: as da especialidade de quem atende, ou todas.</summary>
    public static IReadOnlyList<IInstrumentoAvaliacao> Oferecidos(bool todos, string? especialidadeCodigo)
        => todos ? RegistroInstrumentos.Todos : AvaliacaoClinicaService.Disponiveis(especialidadeCodigo);
}
