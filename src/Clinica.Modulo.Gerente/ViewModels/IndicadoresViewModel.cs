using System.Collections.ObjectModel;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell.Componentes;
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

    // ===== Variacao contra o mes anterior (parcela 11) =====
    //
    // Null = SEM BASE DE COMPARACAO, e a linha nao aparece. Seta sem base seria pior que
    // nada: "0% vs mes anterior" num periodo que nao tem mes anterior e invencao com
    // aparencia de medida — o mesmo motivo pelo qual as metricas mostram "—" e nao 0%.

    [ObservableProperty] private string? _variacaoAtendidos;
    [ObservableProperty] private bool _variacaoAtendidosBoa;

    [ObservableProperty] private string? _variacaoNoShow;
    [ObservableProperty] private bool _variacaoNoShowBoa;

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

        MontarVariacoes();

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

    /// <summary>
    /// Exporta o que esta tela mostra, em CSV. CSV e nao PDF porque a direcao leva o
    /// numero para a planilha dela — gerar PDF exigiria montar layout para um dado que
    /// vai ser reprocessado de qualquer jeito.
    ///
    /// Metrica sem base de calculo sai como "—", igual a tela: trocar por 0 na exportacao
    /// faria a planilha calcular media em cima de um numero que nao existe, e o erro
    /// sairia da nossa mao no primeiro copiar-e-colar.
    /// </summary>
    [RelayCommand]
    private async Task ExportarAsync()
    {
        try
        {
            var linhas = new List<IReadOnlyList<string>>();

            foreach (var m in Serie)
                linhas.Add(new[]
                {
                    "Mes", m.Rotulo,
                    m.Atendidos.ToString(),
                    m.Faltas.ToString(),
                    m.Cancelados.ToString(),
                    m.Fechados == 0 ? "\u2014" : $"{m.TaxaNoShow:0.#}",
                    m.OcupacaoPercentual is { } o ? $"{o:0.#}" : "\u2014",
                    // A serie mensal nao tem completude por profissional; o travessao e a
                    // resposta honesta, e nao um zero que a planilha entraria na media.
                    "\u2014"
                });

            foreach (var p in Produtividade)
                linhas.Add(new[]
                {
                    "Profissional", p.Nome,
                    p.Atendidos.ToString(),
                    p.Faltas.ToString(),
                    p.Cancelados.ToString(),
                    p.Fechados == 0 ? "\u2014" : $"{p.TaxaNoShow:0.#}",
                    p.OcupacaoPercentual is { } o ? $"{o:0.#}" : "\u2014",
                    p.CompletudeProntuario is { } c ? $"{c:0.#}" : "\u2014"
                });

            var csv = ExportacaoCsv.Montar(
                ["Tipo", "Nome", "Atendidos", "Faltas", "Cancelados", "No-show %", "Ocupacao %", "Prontuario %"],
                linhas);

            var erro = await ImpressaoPdf.SalvarEAbrirAsync(
                csv, $"indicadores-{DateTime.Today:yyyy-MM-dd}.csv",
                "CSV (*.csv)|*.csv", ".csv");

            Mensagem = erro ?? string.Empty;
            MensagemEhErro = erro is not null;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Gerente — indicadores nao puderam ser exportados", ex);
            Mensagem = $"Nao foi possivel exportar: {ex.Message}";
            MensagemEhErro = true;
        }
    }

    /// <summary>
    /// Compara o ultimo mes fechado com o anterior. Quem decide se subir e BOM e cada
    /// metrica, nao o componente: mais atendimento e bom, mais falta e ruim, e o mesmo
    /// "subiu" pinta de verde num caso e de vermelho no outro.
    /// </summary>
    private void MontarVariacoes()
    {
        VariacaoAtendidos = VariacaoNoShow = null;

        if (Serie.Count < 2) return;

        var atual = Serie[^1];
        var anterior = Serie[^2];

        if (anterior.Atendidos > 0)
        {
            var delta = (atual.Atendidos - anterior.Atendidos) * 100.0 / anterior.Atendidos;
            VariacaoAtendidos = Descrever(delta, anterior.Rotulo);
            VariacaoAtendidosBoa = delta >= 0;
        }

        // No-show ja e percentual: a variacao dele e em PONTOS, nao em porcentagem de
        // porcentagem. "Subiu 50%" sobre 4% confunde; "subiu 2 pontos" nao.
        if (anterior.Fechados > 0 && atual.Fechados > 0)
        {
            var pontos = atual.TaxaNoShow - anterior.TaxaNoShow;
            var seta = pontos > 0 ? "\u25B2" : pontos < 0 ? "\u25BC" : "\u2192";
            VariacaoNoShow = $"{seta} {Math.Abs(pontos):0.#} ponto(s) vs {anterior.Rotulo}";
            // Aqui subir e RUIM.
            VariacaoNoShowBoa = pontos <= 0;
        }
    }

    private static string Descrever(double delta, string referencia)
    {
        var seta = delta > 0 ? "\u25B2" : delta < 0 ? "\u25BC" : "\u2192";
        return $"{seta} {Math.Abs(delta):0.#}% vs {referencia}";
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
            NaoVerificado = true;
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
