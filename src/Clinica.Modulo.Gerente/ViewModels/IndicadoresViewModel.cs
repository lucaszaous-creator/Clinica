using System.Collections.ObjectModel;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Gerente.ViewModels;

/// <summary>
/// Indicadores gerenciais (feature 12): ocupação, no-show, produtividade e a
/// satisfação medida pelo NPS.
///
/// Toda métrica que pode não ter base de cálculo mostra "—", nunca zero: 0% de
/// ocupação e "não deu para medir" são coisas diferentes, e confundi-las faria a
/// direção decidir em cima de um número que não existe.
/// </summary>
public sealed partial class IndicadoresViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;

    public ObservableCollection<ProdutividadeProfissional> Produtividade { get; } = [];
    public ObservableCollection<MesGerencial> Serie { get; } = [];

    /// <summary>Períodos oferecidos — a lista mora em <see cref="PeriodoGerencial"/>.</summary>
    public IReadOnlyList<string> Periodos { get; } = PeriodoGerencial.Opcoes;

    [ObservableProperty] private string _periodoSelecionado = PeriodoGerencial.EsteMes;
    [ObservableProperty] private bool _carregando;
    [ObservableProperty] private string _mensagem = string.Empty;
    [ObservableProperty] private bool _mensagemEhErro;

    [ObservableProperty] private string _intervaloFormatado = string.Empty;
    [ObservableProperty] private string _jornadaFormatada = string.Empty;

    // ---- KPIs ----
    [ObservableProperty] private int _agendados;
    [ObservableProperty] private int _atendidos;
    [ObservableProperty] private int _faltas;
    [ObservableProperty] private int _encaixes;
    [ObservableProperty] private int _pacientesDistintos;
    [ObservableProperty] private string _noShowFormatado = "—";
    [ObservableProperty] private string _ocupacaoFormatada = "—";
    [ObservableProperty] private string _horasOcupadas = "—";

    // ---- NPS ----
    [ObservableProperty] private string _npsFormatado = "—";
    [ObservableProperty] private string _npsDetalhe = "sem respostas no período";

    public IndicadoresViewModel(IServiceScopeFactory escopos)
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
            Mensagem = string.Empty;
            MensagemEhErro = false;

            var intervalo = PeriodoGerencial.Intervalo(PeriodoSelecionado, DateOnly.FromDateTime(DateTime.Today));
            var (inicio, fim) = intervalo;
            IntervaloFormatado = PeriodoGerencial.Rotular(intervalo);

            using var scope = _escopos.CreateScope();
            var indicadores = scope.ServiceProvider.GetRequiredService<IndicadoresService>();
            var campanhas = scope.ServiceProvider.GetRequiredService<CampanhaService>();

            var painel = await indicadores.GerarAsync(inicio, fim);
            var nps = await campanhas.NpsAsync(inicio, fim);

            Agendados = painel.Agenda.Agendados;
            Atendidos = painel.Agenda.Atendidos;
            Faltas = painel.Agenda.Faltas;
            Encaixes = painel.Agenda.Encaixes;
            PacientesDistintos = painel.Agenda.PacientesDistintos;

            NoShowFormatado = painel.Agenda.Fechados == 0
                ? "—"
                : $"{painel.Agenda.TaxaNoShow:0.#}%";
            OcupacaoFormatada = painel.Agenda.OcupacaoPercentual is { } ocupacao
                ? $"{ocupacao:0.#}%"
                : "—";
            HorasOcupadas = $"{painel.Agenda.MinutosOcupados / 60.0:0.#} h";
            JornadaFormatada =
                $"Ocupação medida sobre {painel.JornadaDiariaMinutos / 60.0:0.#} h por dia com agenda aberta.";

            NpsFormatado = nps.Medido ? nps.Pontuacao.ToString() : "—";
            NpsDetalhe = nps.Medido
                ? $"{nps.Respondidos} resposta(s): {nps.Promotores} promotor(es), "
                  + $"{nps.Neutros} neutro(s), {nps.Detratores} detrator(es)"
                : nps.Enviados > 0
                    ? $"{nps.Enviados} enviada(s), nenhuma resposta ainda"
                    : "nenhuma pesquisa no período";

            Produtividade.Clear();
            foreach (var linha in painel.Produtividade) Produtividade.Add(linha);

            Serie.Clear();
            foreach (var mes in painel.Serie) Serie.Add(mes);

            if (Produtividade.Count == 0)
                Mensagem = "Nenhum horário na agenda neste período.";
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Gerente — indicadores não puderam ser carregados", ex);
            Mensagem = $"Não foi possível carregar os indicadores: {ex.Message}";
            MensagemEhErro = true;
        }
        finally
        {
            Carregando = false;
        }
    }
}
