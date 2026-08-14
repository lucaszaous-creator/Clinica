using Clinica.Domain.Entities;
using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell.Componentes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Gerente.ViewModels;

/// <summary>Uma adquirente na quebra do período — vira barra na tela.</summary>
public sealed class LinhaAdquirente
{
    public required string Rotulo { get; init; }
    public required string ValorRotulo { get; init; }
    public required double Fracao { get; init; }

    /// <summary>Efetiva contra tabela, quando há taxa cadastrada para comparar.</summary>
    public required string Comparacao { get; init; }

    /// <summary>O efetivo passou do teto da tabela — o argumento da renegociação.</summary>
    public required bool AcimaDaTabela { get; init; }
}

/// <summary>Um mês da tabela de custo.</summary>
public sealed class LinhaMesCusto
{
    public required string Mes { get; init; }
    public required string Bruto { get; init; }
    public required string Taxa { get; init; }
    public required string Imposto { get; init; }
    public required string Liquido { get; init; }
    public required string Percentual { get; init; }

    public static LinhaMesCusto De(MesCusto m) => new()
    {
        Mes = m.Rotulo,
        Bruto = m.Bruto.ToString("C"),
        Taxa = m.Taxa.ToString("C"),
        Imposto = m.Imposto.ToString("C"),
        Liquido = m.Liquido.ToString("C"),
        // "—" e não "0%": mês sem faturamento não teve dedução de 0%, não teve base.
        Percentual = m.PercentualDeducoes is { } p ? $"{p:0.##}%" : "—"
    };
}

/// <summary>
/// Taxas e impostos na visão da DIREÇÃO (parcela 17).
///
/// A cliente pediu taxas e impostos "no Financeiro e no Gerente", e as duas telas
/// respondem perguntas diferentes: o Financeiro **cadastra** (qual é a taxa da Cielo, qual
/// é a alíquota do ISS); a direção quer saber **quanto isso custou**.
///
/// Duas leituras que só existem aqui:
///
/// 1. **A taxa EFETIVA contra a de tabela.** A tabela diz 3,1%; se a clínica parcela mais
///    do que imagina, o efetivo do ano é 3,9%. É a diferença entre as duas que dá
///    argumento na renegociação — e ninguém a calculava.
/// 2. **A dedução como percentual do faturamento.** "Foram R$ 14.200 em taxa" é um número
///    grande sem referência; "9,4% de tudo o que entrou" é uma decisão.
/// </summary>
public sealed partial class CustoTransacaoViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;

    public ObservableCollection<LinhaAdquirente> Adquirentes { get; } = [];
    public ObservableCollection<LinhaMesCusto> Meses { get; } = [];

    /// <summary>Série do gráfico: quanto por cento do faturamento foi embora, mês a mês.</summary>
    public ObservableCollection<PontoGrafico> SeriePercentual { get; } = [];

    /// <summary>Série do gráfico: as deduções em dinheiro.</summary>
    public ObservableCollection<PontoGrafico> SerieDeducoes { get; } = [];

    public IReadOnlyList<int> Janelas { get; } = [3, 6, 12, 24];

    [ObservableProperty] private int _mesesJanela = 12;

    [ObservableProperty] private string _bruto = "—";
    [ObservableProperty] private string _taxa = "—";
    [ObservableProperty] private string _imposto = "—";
    [ObservableProperty] private string _liquido = "—";
    [ObservableProperty] private string _percentualTaxa = "—";
    [ObservableProperty] private string _percentualImposto = "—";
    [ObservableProperty] private string _percentualDeducoes = "—";
    [ObservableProperty] private string _periodo = string.Empty;
    [ObservableProperty] private string? _maisCara;
    [ObservableProperty] private string _resumo = string.Empty;

    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    public CustoTransacaoViewModel(IServiceScopeFactory escopos)
    {
        _escopos = escopos;
        _ = CarregarAsync();
    }

    partial void OnMesesJanelaChanged(int value) => _ = CarregarAsync();

    /// <summary>
    /// Número da carga mais recente pedida — descarte de resposta fora de ordem (parcela 50).
    /// Trocar a janela de meses dispara outra leitura; a resposta velha chegando por último
    /// deixaria a série e as barras de um período que não é o escrito no cabeçalho.
    /// </summary>
    private int _geracaoCarga;

    [RelayCommand]
    public async Task CarregarAsync()
    {
        var geracao = ++_geracaoCarga;

        try
        {
            Mensagem = null;
            MensagemEhErro = false;

            using var scope = _escopos.CreateScope();
            var custo = scope.ServiceProvider.GetRequiredService<CustoTransacaoService>();

            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var inicio = new DateOnly(hoje.Year, hoje.Month, 1).AddMonths(-(MesesJanela - 1));
            var fim = new DateOnly(hoje.Year, hoje.Month, 1).AddMonths(1).AddDays(-1);
            Periodo = $"{inicio:MM/yyyy} a {fim:MM/yyyy}";

            var serie = await custo.SerieAsync(hoje, MesesJanela);
            var resumo = await custo.ResumoAsync(inicio, fim);

            var linhasAdquirente = new List<LinhaAdquirente>();
            foreach (var a in await custo.PorAdquirenteAsync(inicio, fim))
            {
                var faixa = await custo.FaixaDeTabelaAsync(a.Adquirente, hoje);
                var efetiva = a.TaxaEfetiva;

                // A comparação só aparece quando há os dois lados. Escrever "efetiva 3,9%"
                // sozinha não diz se é caro; e inventar uma tabela para comparar seria
                // pior que não comparar.
                var comparacao = (faixa, efetiva) switch
                {
                    (null, { } e) => $"efetiva {e:0.##}% · sem taxa cadastrada para comparar",
                    ({ } f, { } e) when f.Menor == f.Maior => $"efetiva {e:0.##}% · tabela {f.Menor:0.##}%",
                    ({ } f, { } e) => $"efetiva {e:0.##}% · tabela {f.Menor:0.##}% a {f.Maior:0.##}%",
                    _ => "sem base para medir"
                };

                linhasAdquirente.Add(new LinhaAdquirente
                {
                    Rotulo = a.Adquirente,
                    ValorRotulo = $"{a.Taxa:C} de {a.Bruto:C} ({a.Quantidade})",
                    Fracao = a.Fracao,
                    Comparacao = comparacao,
                    AcimaDaTabela = faixa is { } f2 && efetiva is { } e2 && e2 > f2.Maior
                });
            }

            // Chegou tarde: outra carga mais nova já foi pedida.
            if (geracao != _geracaoCarga) return;

            Meses.Clear();
            SeriePercentual.Clear();
            SerieDeducoes.Clear();
            foreach (var m in serie)
            {
                Meses.Add(LinhaMesCusto.De(m));
                // Mês sem faturamento vira NULL na série, e a linha se interrompe: um mês
                // sem base desenhado como 0% inventaria um mês perfeito, que é justamente
                // o erro contra o qual o gráfico foi construído.
                SeriePercentual.Add(new PontoGrafico(
                    m.Rotulo, m.PercentualDeducoes is { } p ? (double)p : null));
                SerieDeducoes.Add(new PontoGrafico(m.Rotulo, (double)m.Deducoes));
            }

            Bruto = resumo.Bruto.ToString("C");
            Taxa = resumo.Taxa.ToString("C");
            Imposto = resumo.Imposto.ToString("C");
            Liquido = resumo.Liquido.ToString("C");
            PercentualTaxa = Percentual(resumo.PercentualTaxa);
            PercentualImposto = Percentual(resumo.PercentualImposto);
            PercentualDeducoes = Percentual(resumo.PercentualDeducoes);

            MaisCara = resumo.MaisCara is { } m2
                ? $"Adquirente que mais descontou: {m2.Adquirente} ({m2.Taxa:C})"
                : null;

            Adquirentes.Clear();
            foreach (var linha in linhasAdquirente)
                Adquirentes.Add(linha);

            var acima = Adquirentes.Count(a => a.AcimaDaTabela);
            Resumo = Adquirentes.Count == 0
                ? "Nenhum recebimento no período."
                : acima == 0
                    ? $"{Adquirentes.Count} origem(ns) de recebimento no período."
                    : $"{Adquirentes.Count} origem(ns) · {acima} com taxa efetiva ACIMA do teto de tabela.";
        }
        catch (Exception ex)
        {
            // Chegou tarde: outra carga mais nova já foi pedida.
            if (geracao != _geracaoCarga) return;

            Clinica.Application.Diagnostico.Registrar("Gerente — custo de transação não pôde ser lido", ex);
            Mensagem = $"Não foi possível medir o custo: {ex.Message}";
            MensagemEhErro = true;
        }
    }

    /// <summary>
    /// Exporta a série e a quebra em CSV — a direção leva o número para a planilha dela,
    /// e é com ele que negocia com a adquirente.
    /// </summary>
    [RelayCommand]
    private async Task ExportarAsync()
    {
        // Saída de dado conta como ato, e ato pede a segunda barreira (parcela 64).
        // O item da sidebar já exige o bit, mas item de menu é UMA barreira — foi por
        // confiar só nela que a tela de Acessos ficou sem guarda até a parcela 51.
        SessaoUsuario.Atual.Exigir(Permissao.VerFinanceiro, "exportar o custo de transação");

        try
        {
            var linhas = new List<IReadOnlyList<string>>();

            foreach (var m in Meses)
                linhas.Add(new[] { "Mes", m.Mes, m.Bruto, m.Taxa, m.Imposto, m.Liquido, m.Percentual });

            foreach (var a in Adquirentes)
                linhas.Add(new[] { "Adquirente", a.Rotulo, a.ValorRotulo, "", "", "", a.Comparacao });

            var csv = ExportacaoCsv.Montar(
                ["Tipo", "Nome", "Bruto", "Taxa", "Imposto", "Liquido", "Deducoes %"],
                linhas);

            var erro = await ImpressaoPdf.SalvarEAbrirAsync(
                csv, $"custo-transacao-{DateTime.Today:yyyy-MM-dd}.csv",
                "CSV (*.csv)|*.csv", ".csv");

            Mensagem = erro;
            MensagemEhErro = erro is not null;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Gerente — custo não pôde ser exportado", ex);
            Mensagem = $"Não foi possível exportar: {ex.Message}";
            MensagemEhErro = true;
        }
    }

    /// <summary>Null vira "—", nunca 0%: não medido e zero são coisas diferentes.</summary>
    private static string Percentual(decimal? valor) => valor is { } v ? $"{v:0.##}%" : "—";
}
