using System.Collections.ObjectModel;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
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

    /// <summary>Atendimentos mes a mes — a linha de volume do painel executivo.</summary>
    public ObservableCollection<PontoGrafico> SerieAtendidos { get; } = [];

    /// <summary>
    /// No-show mes a mes. Mes SEM horario fechado devolve null e a linha se interrompe:
    /// desenhar 0% num mes em que a clinica nao abriu seria inventar um mes perfeito.
    /// </summary>
    public ObservableCollection<PontoGrafico> SerieNoShow { get; } = [];

    /// <summary>Ocupacao por profissional em barras — o "quem esta cheio" de relance.</summary>
    public ObservableCollection<BarraOcupacao> BarrasOcupacao { get; } = [];

    /// <summary>Ha serie suficiente para desenhar (um ponto so nao e evolucao).</summary>
    [ObservableProperty]
    private bool _temSerie;

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

    /// <summary>
    /// Traduz as listas em series de grafico. A NORMALIZACAO das barras acontece aqui, e
    /// nao no template: a fracao depende do MAIOR valor da serie, e o DataTemplate nao
    /// enxerga os irmaos.
    ///
    /// O maximo da barra e a maior ocupacao do periodo, nao 100%: numa clinica que nunca
    /// passa de 60%, todas as barras ficariam curtas e iguais, e a comparacao entre
    /// profissionais — que e a pergunta — sumiria.
    /// </summary>
    private void MontarGraficos()
    {
        SerieAtendidos.Clear();
        SerieNoShow.Clear();
        foreach (var m in Serie)
        {
            SerieAtendidos.Add(new PontoGrafico(m.Rotulo, m.Atendidos));
            SerieNoShow.Add(new PontoGrafico(m.Rotulo, m.Fechados == 0 ? null : m.TaxaNoShow));
        }
        TemSerie = Serie.Count > 1;

        BarrasOcupacao.Clear();
        var comOcupacao = Produtividade
            .Where(p => p.OcupacaoPercentual is not null)
            .OrderByDescending(p => p.OcupacaoPercentual)
            .ToList();

        var maior = comOcupacao.Count == 0 ? 0 : comOcupacao.Max(p => p.OcupacaoPercentual!.Value);
        foreach (var p in comOcupacao)
        {
            var valor = p.OcupacaoPercentual!.Value;
            BarrasOcupacao.Add(new BarraOcupacao(
                p.Nome,
                $"{valor:0.#}% · {p.Atendidos} atendido(s)",
                maior <= 0 ? 0 : valor / maior));
        }
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

            MontarGraficos();

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

/// <summary>
/// Uma barra do gráfico de ocupação. A fração já vem normalizada pelo ViewModel —
/// o template não enxerga os irmãos da série para descobrir o máximo sozinho.
/// </summary>
public sealed record BarraOcupacao(string Rotulo, string ValorRotulo, double Fracao);
