using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Financeiro.ViewModels;

/// <summary>Uma linha da tabela mês a mês.</summary>
public sealed class LinhaMesFluxo
{
    public required string Mes { get; init; }
    public required string Entradas { get; init; }
    public required string Saidas { get; init; }
    public required string Resultado { get; init; }
    public required string Previsto { get; init; }
    public required string Acumulado { get; init; }
    public required bool NoVermelho { get; init; }

    public static LinhaMesFluxo De(MesFluxo m, decimal acumulado) => new()
    {
        Mes = m.Rotulo,
        Entradas = m.EntradasRealizadas.ToString("C"),
        Saidas = m.SaidasRealizadas.ToString("C"),
        Resultado = m.Realizado.ToString("C"),
        // Previsto só aparece quando existe: "R$ 0,00" num mês fechado sugeriria que
        // ainda há algo por vir, e não há.
        Previsto = m.Previsto == 0m ? "—" : m.Previsto.ToString("C"),
        Acumulado = acumulado.ToString("C"),
        NoVermelho = m.Realizado < 0m
    };
}

/// <summary>Uma categoria na quebra do período.</summary>
public sealed class LinhaCategoriaFluxo
{
    public required string Rotulo { get; init; }
    public required string ValorRotulo { get; init; }
    public required double Fracao { get; init; }
    public required bool EhSaida { get; init; }

    public static LinhaCategoriaFluxo De(ResultadoCategoria c) => new()
    {
        Rotulo = c.Categoria,
        // O número vem escrito ao lado da barra: barra sozinha responde "quem é maior",
        // e a direção também precisa de "quanto".
        ValorRotulo = $"{c.Total:C} · {c.Fracao:P0} ({c.Quantidade})",
        Fracao = c.Fracao,
        EhSaida = c.Tipo == TipoLancamento.Saida
    };
}

/// <summary>
/// Fluxo de caixa: a série dos meses e para onde o dinheiro está indo (parcela 13).
///
/// O Caixa mostra o extrato do período e a Produção mostra o volume do faturamento —
/// nenhum dos dois mostra a SÉRIE, e é a série que revela tendência. Um mês ruim pode ser
/// azar; três meses caindo é um problema, e até aqui não havia tela em que isso
/// aparecesse.
///
/// Duas honestidades que custam número bonito:
///
/// 1. **Realizado e previsto ficam em colunas separadas.** Somados, o mês que fechou e o
///    mês que ainda vai vencer teriam a mesma cara, e a direção decidiria sobre
///    expectativa achando que era medida.
/// 2. **A coluna acumulada é VARIAÇÃO, não saldo em conta.** A clínica nunca cadastrou
///    saldo inicial. Chamar isso de saldo daria um número que ela conferiria com o
///    extrato do banco e que não bateria nunca — e o estrago não seria um rótulo errado,
///    seria desconfiança no sistema inteiro.
/// </summary>
public sealed partial class FluxoCaixaViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;

    public ObservableCollection<LinhaMesFluxo> Meses { get; } = [];
    public ObservableCollection<LinhaCategoriaFluxo> Entradas { get; } = [];
    public ObservableCollection<LinhaCategoriaFluxo> Saidas { get; } = [];

    /// <summary>Série do gráfico: o resultado realizado de cada mês.</summary>
    public ObservableCollection<PontoGrafico> SerieResultado { get; } = [];

    /// <summary>Série do gráfico: o que entrou, mês a mês.</summary>
    public ObservableCollection<PontoGrafico> SerieEntradas { get; } = [];

    public IReadOnlyList<int> Janelas { get; } = [3, 6, 12, 24];

    [ObservableProperty] private int _mesesJanela = 12;

    /// <summary>
    /// Incluir o previsto na quebra por categoria. Desligado por padrão: a pergunta
    /// "para onde foi o dinheiro" é sobre o que JÁ foi, e misturar o que ainda pode não
    /// acontecer responderia outra coisa.
    /// </summary>
    [ObservableProperty] private bool _incluirPrevisto;

    // ---- Faixa do período ----
    [ObservableProperty] private string _totalEntradas = "—";
    [ObservableProperty] private string _totalSaidas = "—";
    [ObservableProperty] private string _resultado = "—";
    [ObservableProperty] private string _margem = "—";
    [ObservableProperty] private string? _maiorSaida;
    [ObservableProperty] private string _periodo = string.Empty;
    [ObservableProperty] private bool _resultadoNegativo;

    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    public FluxoCaixaViewModel(IServiceScopeFactory escopos)
    {
        _escopos = escopos;
        _ = CarregarAsync();
    }

    partial void OnMesesJanelaChanged(int value) => _ = CarregarAsync();
    partial void OnIncluirPrevistoChanged(bool value) => _ = CarregarAsync();

    [RelayCommand]
    public async Task CarregarAsync()
    {
        try
        {
            Mensagem = null;
            MensagemEhErro = false;

            using var scope = _escopos.CreateScope();
            var fluxo = scope.ServiceProvider.GetRequiredService<FluxoCaixaService>();

            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var serie = await fluxo.ProjecaoAsync(hoje, MesesJanela);

            Meses.Clear();
            SerieResultado.Clear();
            SerieEntradas.Clear();
            foreach (var (m, acumulado) in FluxoCaixaService.Acumular(serie))
            {
                Meses.Add(LinhaMesFluxo.De(m, acumulado));
                // Mês sem movimento entra ZERADO na linha, não como buraco: buraco no
                // gráfico significa "não medido", e aqui seria falso — o mês foi medido
                // e não teve nada. (O null da série existe para o outro caso.)
                SerieResultado.Add(new PontoGrafico(m.Rotulo, (double)m.Realizado));
                SerieEntradas.Add(new PontoGrafico(m.Rotulo, (double)m.EntradasRealizadas));
            }

            // A quebra por categoria é do período INTEIRO da janela: uma despesa que só
            // aparece uma vez por trimestre sumiria de uma quebra mensal, e é justamente
            // ela que costuma explicar o mês ruim.
            var inicio = new DateOnly(hoje.Year, hoje.Month, 1).AddMonths(-(MesesJanela - 1));
            var fim = new DateOnly(hoje.Year, hoje.Month, 1).AddMonths(1).AddDays(-1);
            Periodo = $"{inicio:MM/yyyy} a {fim:MM/yyyy}";

            var categorias = await fluxo.PorCategoriaAsync(inicio, fim, IncluirPrevisto);
            Entradas.Clear();
            Saidas.Clear();
            foreach (var c in categorias)
                (c.Tipo == TipoLancamento.Saida ? Saidas : Entradas).Add(LinhaCategoriaFluxo.De(c));

            var resumo = await fluxo.ResumoAsync(inicio, fim, IncluirPrevisto);
            TotalEntradas = resumo.Entradas.ToString("C");
            TotalSaidas = resumo.Saidas.ToString("C");
            Resultado = resumo.Resultado.ToString("C");
            ResultadoNegativo = resumo.Resultado < 0m;
            // Sem receita no período, a margem não é 0% — ela não existe. Mostrar 0%
            // diria "não sobrou nada", que é diagnóstico diferente de "não houve receita".
            Margem = resumo.Margem is { } m2 ? m2.ToString("P0") : "—";
            MaiorSaida = resumo.MaiorSaida is { } s
                ? $"Maior despesa: {s.Categoria} ({s.Total:C}, {s.Fracao:P0} das saídas)"
                : null;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Financeiro — fluxo de caixa não pôde ser lido", ex);
            Mensagem = $"Não foi possível montar o fluxo: {ex.Message}";
            MensagemEhErro = true;
        }
    }

    /// <summary>
    /// Exporta a série e a quebra em CSV — a direção leva o número para a planilha dela.
    /// O previsto sai como coluna própria, nunca somado ao realizado: a planilha que
    /// recebesse os dois juntos calcularia média sobre dinheiro que talvez não exista.
    /// </summary>
    [RelayCommand]
    private async Task ExportarAsync()
    {
        try
        {
            var linhas = new List<IReadOnlyList<string>>();

            foreach (var m in Meses)
                linhas.Add(new[]
                {
                    "Mes", m.Mes, m.Entradas, m.Saidas, m.Resultado, m.Previsto, m.Acumulado
                });

            foreach (var c in Entradas)
                linhas.Add(new[] { "Entrada", c.Rotulo, c.ValorRotulo, "", "", "", "" });

            foreach (var c in Saidas)
                linhas.Add(new[] { "Saida", c.Rotulo, c.ValorRotulo, "", "", "", "" });

            var csv = ExportacaoCsv.Montar(
                ["Tipo", "Rotulo", "Entradas", "Saidas", "Resultado", "Previsto", "Acumulado"],
                linhas);

            var erro = await ImpressaoPdf.SalvarEAbrirAsync(
                csv, $"fluxo-caixa-{DateTime.Today:yyyy-MM-dd}.csv",
                "CSV (*.csv)|*.csv", ".csv");

            Mensagem = erro;
            MensagemEhErro = erro is not null;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Financeiro — fluxo não pôde ser exportado", ex);
            Mensagem = $"Não foi possível exportar: {ex.Message}";
            MensagemEhErro = true;
        }
    }
}
