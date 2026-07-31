using System.Collections.ObjectModel;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
// ConvenioInfo (o nome de exibição do convênio) mora em Clinica.Domain.Regras.
using Clinica.Domain.Regras;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Gerente.ViewModels;

/// <summary>Uma linha da quebra por convênio, já formatada para a tela.</summary>
public sealed class LinhaConvenioGerencial
{
    public required string Convenio { get; init; }
    public required int Total { get; init; }
    public required int Baixados { get; init; }
    public required int Pendentes { get; init; }
    public required string TaxaBaixa { get; init; }
    public required string TaxaGlosa { get; init; }
    public required string TempoMedio { get; init; }
}

/// <summary>
/// A visão consolidada do faturamento para a direção — SÓ LEITURA.
///
/// É o desenho que a Fase 4 cancelada deixou como alternativa: em vez de o Gerente
/// herdar as telas do app congelado, ele lê os MESMOS serviços compartilhados
/// (<see cref="RelatorioService"/>, <see cref="PendenciaService"/>) por uma tela
/// própria. Nada aqui grava — não há baixa, nem estorno, nem lote —, então não existe
/// risco de duas máquinas disputarem a mesma guia.
///
/// A pergunta desta tela também é outra: no faturamento é "que guia vence primeiro";
/// aqui é "a clínica está perdendo faturamento, e onde".
/// </summary>
public sealed partial class FaturamentoGerencialViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;

    public ObservableCollection<LinhaConvenioGerencial> PorConvenio { get; } = [];
    public ObservableCollection<FaixaEnvelhecimento> Envelhecimento { get; } = [];

    /// <summary>
    /// Evolução da taxa de baixa nos últimos 6 meses. Fica no rodapé da tela: a pergunta
    /// "estamos melhorando?" só faz sentido depois de "como estamos".
    /// </summary>
    public ObservableCollection<ResumoMensal> Meses { get; } = [];

    /// <summary>Períodos oferecidos — a lista mora em <see cref="PeriodoGerencial"/>.</summary>
    public IReadOnlyList<string> Periodos { get; } = PeriodoGerencial.Opcoes;

    [ObservableProperty] private string _periodoSelecionado = PeriodoGerencial.EsteMes;
    [ObservableProperty] private bool _carregando;

    /// <summary>
    /// A leitura FALHOU — o terceiro estado. Sem ele, tela vazia por erro fica idêntica
    /// a tela vazia por não haver nada.
    /// </summary>
    [ObservableProperty] private bool _naoVerificado;
    [ObservableProperty] private string _mensagem = string.Empty;
    [ObservableProperty] private bool _mensagemEhErro;
    [ObservableProperty] private string _intervaloFormatado = string.Empty;

    [ObservableProperty] private int _totalGuias;
    [ObservableProperty] private int _baixadas;
    [ObservableProperty] private int _pendentes;
    [ObservableProperty] private int _naoConformidades;
    [ObservableProperty] private string _taxaBaixaFormatada = "—";
    [ObservableProperty] private string _taxaGlosaFormatada = "—";
    [ObservableProperty] private string _tempoMedioFormatado = "—";

    /// <summary>Pendências em aberto AGORA (não do período) — é o que ainda dá para salvar.</summary>
    [ObservableProperty] private int _pendenciasEmAberto;

    public FaturamentoGerencialViewModel(IServiceScopeFactory escopos)
    {
        _escopos = escopos;
        _ = CarregarAsync();
    }

    partial void OnPeriodoSelecionadoChanged(string value) => _ = CarregarAsync();

    [RelayCommand]
    public async Task CarregarAsync()
    {
        try
        {
            Carregando = true;
            NaoVerificado = false;
            Mensagem = string.Empty;
            MensagemEhErro = false;

            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var intervalo = PeriodoGerencial.Intervalo(PeriodoSelecionado, hoje);
            var (inicio, fim) = intervalo;
            IntervaloFormatado = PeriodoGerencial.Rotular(intervalo);

            using var scope = _escopos.CreateScope();
            var relatorios = scope.ServiceProvider.GetRequiredService<RelatorioService>();
            var pendencias = scope.ServiceProvider.GetRequiredService<PendenciaService>();

            var relatorio = await relatorios.GerarAsync(inicio, fim, hoje);
            var meses = await relatorios.ComparativoMensalAsync(hoje, 6);
            var emAberto = await pendencias.CodigosPendentesAsync(hoje);

            TotalGuias = relatorio.Resumo.TotalCodigos;
            Baixadas = relatorio.Resumo.Baixados;
            Pendentes = relatorio.Resumo.Pendentes;
            NaoConformidades = relatorio.Resumo.NaoConformidades;
            PendenciasEmAberto = emAberto.Count;

            TaxaBaixaFormatada = TotalGuias == 0 ? "—" : $"{relatorio.Resumo.TaxaBaixa:0.#}%";
            TaxaGlosaFormatada = relatorio.Resumo.Baixados == 0 ? "—" : $"{relatorio.Resumo.TaxaGlosa:0.#}%";
            TempoMedioFormatado = relatorio.Resumo.TempoMedioBaixaDias is { } dias
                ? $"{dias:0.#} dias"
                : "—";

            PorConvenio.Clear();
            foreach (var c in relatorio.PorConvenio)
                PorConvenio.Add(new LinhaConvenioGerencial
                {
                    Convenio = ConvenioInfo.NomeExibicao(c.Convenio),
                    Total = c.TotalCodigos,
                    Baixados = c.Baixados,
                    Pendentes = c.Pendentes,
                    TaxaBaixa = c.TotalCodigos == 0 ? "—" : $"{c.TaxaBaixa:0.#}%",
                    TaxaGlosa = c.Baixados == 0 ? "—" : $"{c.TaxaGlosa:0.#}%",
                    TempoMedio = c.TempoMedioBaixaDias is { } d ? $"{d:0.#} d" : "—"
                });

            Envelhecimento.Clear();
            foreach (var faixa in relatorio.Envelhecimento) Envelhecimento.Add(faixa);

            Meses.Clear();
            foreach (var mes in meses) Meses.Add(mes);

            if (TotalGuias == 0)
                Mensagem = "Nenhuma guia com atendimento neste período.";
        }
        catch (Exception ex)
        {
            NaoVerificado = true;
            Clinica.Application.Diagnostico.Registrar(
                "Gerente — visão do faturamento não pôde ser carregada", ex);
            Mensagem = $"Não foi possível carregar o faturamento: {ex.Message}";
            MensagemEhErro = true;
        }
        finally
        {
            Carregando = false;
        }
    }
}
