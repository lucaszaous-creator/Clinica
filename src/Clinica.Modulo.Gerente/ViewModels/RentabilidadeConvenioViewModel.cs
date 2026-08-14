using Clinica.Domain.Entities;
using Clinica.Domain;
using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Desktop.Shell.Componentes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Gerente.ViewModels;

/// <summary>Um convênio na tabela de rentabilidade.</summary>
public sealed class LinhaConvenio
{
    public required string Rotulo { get; init; }
    public required string ValorRotulo { get; init; }
    public required double Fracao { get; init; }
    public required string Guias { get; init; }
    public required string Bruto { get; init; }
    public required string Retido { get; init; }
    public required string Liquido { get; init; }
    public required string PorGuia { get; init; }
    public required string Prazo { get; init; }
    public required string Pendencias { get; init; }

    /// <summary>Tem guia efetivada sem receita lançada — trabalho feito e não cobrado.</summary>
    public required bool TemPendencia { get; init; }

    public static LinhaConvenio De(RentabilidadeConvenio c) => new()
    {
        // `Convenio` JÁ vem resolvido pelo serviço (CatalogoConvenios.Nome(codigo)); o
        // record carrega `Codigo` separado para quem precisa da identidade. Chamar Nome()
        // de novo aqui devolveria "Unimed" para TODA operadora, porque um nome não é chave
        // do catálogo e a busca cai na família padrão.
        Rotulo = c.Convenio,
        ValorRotulo = $"{c.Liquido:C} líquido em {c.Guias} guia(s)",
        Fracao = c.Fracao,
        Guias = c.Guias.ToString(),
        Bruto = c.Bruto.ToString("C"),
        // Zero retido vira "—": a operadora que não retém e a que não pagou nada
        // apareceriam iguais escritas como "R$ 0,00".
        Retido = c.Retido > 0m ? c.Retido.ToString("C") : "—",
        Liquido = c.Liquido.ToString("C"),
        // "—" e não zero: média sobre nenhuma guia recebida é invenção.
        PorGuia = c.LiquidoPorGuia is { } m ? m.ToString("C") : "—",
        Prazo = c.PrazoMedioDias is { } d ? $"{d:0.#} dias" : "—",
        Pendencias = c.SemReceita == 0
            ? (c.Glosadas == 0 ? "—" : $"{c.Glosadas} glosada(s)")
            : c.Glosadas == 0
                ? $"{c.SemReceita} sem receita"
                : $"{c.SemReceita} sem receita · {c.Glosadas} glosada(s)",
        TemPendencia = c.SemReceita > 0
    };
}

/// <summary>
/// Rentabilidade por convênio (parcela 19).
///
/// A clínica fatura muito convênio e não tinha como responder à pergunta mais básica da
/// direção: **qual operadora vale a pena?**. O faturamento sabe quantas guias saíram, o
/// financeiro sabe quanto entrou — e os dois números nunca se encontravam por convênio.
///
/// A tela mostra três coisas que nenhuma outra mostra:
///
/// 1. **Líquido por guia.** O único número comparável entre operadoras que pagam valores e
///    volumes diferentes. "A Unimed faturou mais" não diz nada se ela precisou de três
///    vezes mais atendimentos para isso.
/// 2. **Prazo médio de pagamento.** Uma operadora que paga 10% a mais em 90 dias pode ser
///    pior que outra que paga menos em 30.
/// 3. **Guias sem receita.** Trabalho já feito e não cobrado, com nome de quem está
///    devendo — a fila da conciliação vista por convênio.
/// </summary>
public sealed partial class RentabilidadeConvenioViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;

    public ObservableCollection<LinhaConvenio> Convenios { get; } = [];

    public IReadOnlyList<int> Janelas { get; } = [1, 3, 6, 12];

    [ObservableProperty] private int _mesesJanela = 6;

    [ObservableProperty] private string _bruto = "—";
    [ObservableProperty] private string _retido = "—";
    [ObservableProperty] private string _liquido = "—";
    [ObservableProperty] private string _percentualRetido = "—";
    [ObservableProperty] private string _taxaGlosa = "—";
    [ObservableProperty] private string _guias = "—";
    [ObservableProperty] private string _periodo = string.Empty;
    [ObservableProperty] private string? _melhorEPior;
    [ObservableProperty] private string _resumo = string.Empty;
    [ObservableProperty] private bool _temPendencia;

    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    public RentabilidadeConvenioViewModel(IServiceScopeFactory escopos)
    {
        _escopos = escopos;
        _ = CarregarAsync();
    }

    partial void OnMesesJanelaChanged(int value) => _ = CarregarAsync();

    /// <summary>
    /// Número da carga mais recente pedida — descarte de resposta fora de ordem (parcela 50).
    /// Trocar a janela de meses dispara outra leitura; a resposta velha chegando por último
    /// faria a direção comparar operadoras sobre um período que não é o do cabeçalho.
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
            var servico = scope.ServiceProvider.GetRequiredService<RentabilidadeConvenioService>();

            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var inicio = new DateOnly(hoje.Year, hoje.Month, 1).AddMonths(-(MesesJanela - 1));
            var fim = new DateOnly(hoje.Year, hoje.Month, 1).AddMonths(1).AddDays(-1);
            Periodo = $"{inicio:MM/yyyy} a {fim:MM/yyyy}";

            var porConvenio = await servico.PorConvenioAsync(inicio, fim);
            var r = await servico.ResumoAsync(inicio, fim);

            // Chegou tarde: outra carga mais nova já foi pedida.
            if (geracao != _geracaoCarga) return;

            Convenios.Clear();
            foreach (var c in porConvenio)
                Convenios.Add(LinhaConvenio.De(c));

            Guias = r.Guias.ToString();
            Bruto = r.Bruto.ToString("C");
            Retido = r.Retido.ToString("C");
            Liquido = r.Liquido.ToString("C");
            PercentualRetido = r.PercentualRetido is { } p ? $"{p:0.##}%" : "—";
            TaxaGlosa = r.TaxaGlosa is { } g ? $"{g:0.#}%" : "—";

            // A comparação só aparece com DUAS operadoras medidas: comparar uma consigo
            // mesma não diz nada, e escrever "a melhor é a única" seria ruído.
            MelhorEPior = r.MaisRentavel is { } melhor && r.MenosRentavel is { } pior
                ? $"Rende mais por atendimento: {melhor.Convenio} "
                  + $"({melhor.LiquidoPorGuia:C}/guia) · rende menos: "
                  + $"{pior.Convenio} ({pior.LiquidoPorGuia:C}/guia)"
                : null;

            var pendentes = Convenios.Count(c => c.TemPendencia);
            TemPendencia = pendentes > 0;
            Resumo = Convenios.Count == 0
                ? "Nenhuma guia no período."
                : pendentes == 0
                    ? $"{Convenios.Count} convênio(s) no período."
                    : $"{Convenios.Count} convênio(s) · {pendentes} com guia efetivada e SEM receita lançada.";
        }
        catch (Exception ex)
        {
            // Chegou tarde: outra carga mais nova já foi pedida.
            if (geracao != _geracaoCarga) return;

            Clinica.Application.Diagnostico.Registrar("Gerente — rentabilidade por convênio falhou", ex);
            Mensagem = $"Não foi possível medir a rentabilidade: {ex.Message}";
            MensagemEhErro = true;
        }
    }

    /// <summary>
    /// Exporta em CSV — é com esta planilha que a clínica senta para renegociar tabela com
    /// a operadora.
    /// </summary>
    [RelayCommand]
    private async Task ExportarAsync()
    {
        // Saída de dado conta como ato, e ato pede a segunda barreira (parcela 64).
        // O item da sidebar já exige o bit, mas item de menu é UMA barreira — foi por
        // confiar só nela que a tela de Acessos ficou sem guarda até a parcela 51.
        SessaoUsuario.Atual.Exigir(Permissao.VerFinanceiro, "exportar a rentabilidade por convênio");

        try
        {
            var linhas = Convenios
                .Select(c => (IReadOnlyList<string>)new[]
                {
                    c.Rotulo, c.Guias, c.Bruto, c.Retido, c.Liquido, c.PorGuia, c.Prazo, c.Pendencias
                })
                .ToList();

            var csv = ExportacaoCsv.Montar(
                ["Convenio", "Guias", "Bruto", "Retido", "Liquido", "Liquido/guia",
                 "Prazo medio", "Pendencias"],
                linhas);

            var erro = await ImpressaoPdf.SalvarEAbrirAsync(
                csv, $"rentabilidade-convenios-{DateTime.Today:yyyy-MM-dd}.csv",
                "CSV (*.csv)|*.csv", ".csv");

            Mensagem = erro;
            MensagemEhErro = erro is not null;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Gerente — rentabilidade não pôde ser exportada", ex);
            Mensagem = $"Não foi possível exportar: {ex.Message}";
            MensagemEhErro = true;
        }
    }
}
