using System.Collections.ObjectModel;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
// PontoGrafico (a série do GraficoLinha) mora em Clinica.Desktop.Controls, no shell.
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell.Componentes;
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
/// Uma faixa do envelhecimento pronta para o <c>ItemBarraRotulada</c>: rótulo, número
/// escrito e a fração da barra.
///
/// A fração é normalizada AQUI, no ViewModel, e não no template — o DataTemplate não
/// enxerga os irmãos da série, e é do total das faixas que ela sai.
///
/// Ela substitui a leitura anterior (três números soltos num WrapPanel) sem perder nada:
/// o rótulo e a quantidade continuam escritos, e o que entra é a PROPORÇÃO, que responde
/// "onde está o atraso" — a pergunta que três números lado a lado não respondem.
/// </summary>
public sealed record LinhaEnvelhecimento(string Rotulo, string ValorRotulo, double Fracao);

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
    public ObservableCollection<LinhaEnvelhecimento> Envelhecimento { get; } = [];

    /// <summary>
    /// A taxa de baixa dos últimos 6 meses como CURVA. Os números continuam escritos na
    /// lista abaixo do gráfico: a linha mostra a forma, e a direção também precisa do
    /// número exato — é o desenho que a tela de Indicadores já usa.
    ///
    /// Mês SEM guia entra como buraco (valor nulo), nunca como 0%: uma linha descendo até
    /// o eixo num mês em que a clínica não faturou inventaria uma queda que não houve.
    /// </summary>
    public ObservableCollection<PontoGrafico> SerieTaxaBaixa { get; } = [];

    /// <summary>
    /// Sem série não se desenha área de gráfico nenhuma — o gráfico SOME. 200 px de
    /// branco dizendo com o desenho o que a frase já diz com palavras é o que a regra de
    /// leiaute proíbe.
    /// </summary>
    [ObservableProperty] private bool _temSerie;

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

    /// <summary>
    /// As duas taxas como fração 0..1, para a barra do cartão de KPI.
    ///
    /// A barra só existe onde a fração é REAL — taxa de baixa e taxa de glosa são
    /// percentuais de verdade. Os outros cartões (contagem de guias, tempo médio,
    /// pendentes hoje) não ganham barra: inventar um denominador para ter o desenho
    /// daria um número errado com cara de exato, que é o defeito que este projeto recusa.
    ///
    /// E o par `Tem…` decide se a barra APARECE: sem guia no período a taxa é "—", e uma
    /// barra vazia ao lado do travessão afirmaria ZERO onde o que há é "não medido".
    /// </summary>
    [ObservableProperty] private double _taxaBaixaFracao;
    [ObservableProperty] private bool _temTaxaBaixa;
    [ObservableProperty] private double _taxaGlosaFracao;
    [ObservableProperty] private bool _temTaxaGlosa;

    /// <summary>Pendências em aberto AGORA (não do período) — é o que ainda dá para salvar.</summary>
    [ObservableProperty] private int _pendenciasEmAberto;

    public FaturamentoGerencialViewModel(IServiceScopeFactory escopos)
    {
        _escopos = escopos;
        _ = CarregarAsync();
    }

    partial void OnPeriodoSelecionadoChanged(string value) => _ = CarregarAsync();

    /// <summary>
    /// Número da carga mais recente pedida — descarte de resposta fora de ordem (parcela 50).
    /// Trocar o período dispara outra leitura; a resposta velha chegando por último faria a
    /// direção ler taxa de baixa e envelhecimento de um período que não é o do cabeçalho.
    /// </summary>
    private int _geracaoCarga;

    /// <summary>
    /// O fechamento do período em PDF — a folha que vai ao contador (parcela 54).
    ///
    /// <b>Capacidade que existia com a porta no app de quem não a usa.</b> O
    /// <c>FechamentoPdfService</c> é alcançado pela Central de Documentos, que é da
    /// RECEPÇÃO; quem manda o fechamento ao contador é a direção, e ela teria de pedir ao
    /// balcão para gerar. É a família da parcela 48 — alerta e capacidade com porta no
    /// módulo errado.
    ///
    /// Fica NESTA tela, e não em Documentos, porque o período já está escolhido aqui: um
    /// item de menu próprio abriria pedindo de novo o intervalo que a pessoa acabou de
    /// definir.
    ///
    /// Não reimplementa nada — chama o mesmo <c>CentralDocumentosService</c> que a
    /// Recepção chama. Duas gerações do mesmo papel divergiriam na primeira correção.
    /// </summary>
    [RelayCommand]
    private async Task GerarFechamentoAsync()
    {
        try
        {
            Gerando = true;
            Mensagem = string.Empty;
            MensagemEhErro = false;

            var (inicio, fim) = PeriodoGerencial.Intervalo(
                PeriodoSelecionado, DateOnly.FromDateTime(DateTime.Today));

            byte[] pdf;
            using (var scope = _escopos.CreateScope())
                pdf = await scope.ServiceProvider
                    .GetRequiredService<CentralDocumentosService>()
                    .GerarFechamentoPeriodoAsync(inicio, fim);

            var erro = await ImpressaoPdf.SalvarEAbrirAsync(
                pdf, ImpressaoPdf.NomeSeguro($"Fechamento-{inicio:yyyy-MM-dd}-a-{fim:yyyy-MM-dd}.pdf"));

            if (erro is not null)
            {
                Mensagem = erro;
                MensagemEhErro = true;
            }
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Gerente — fechamento do período não pôde ser gerado", ex);
            Mensagem = $"Não foi possível gerar o fechamento: {ex.Message}";
            MensagemEhErro = true;
        }
        finally
        {
            Gerando = false;
        }
    }

    /// <summary>O PDF varre o período inteiro e demora; o botão precisa DIZER isso.</summary>
    [ObservableProperty] private bool _gerando;

    /// <summary>Negado para o XAML — a suíte não tem conversor de booleano invertido.</summary>
    public bool PodeGerarFechamento => !Gerando;

    partial void OnGerandoChanged(bool value) => OnPropertyChanged(nameof(PodeGerarFechamento));

    [RelayCommand]
    public async Task CarregarAsync()
    {
        var geracao = ++_geracaoCarga;

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

            // Chegou tarde: outra carga mais nova já foi pedida.
            if (geracao != _geracaoCarga) return;

            TotalGuias = relatorio.Resumo.TotalCodigos;
            Baixadas = relatorio.Resumo.Baixados;
            Pendentes = relatorio.Resumo.Pendentes;
            NaoConformidades = relatorio.Resumo.NaoConformidades;
            PendenciasEmAberto = emAberto.Count;

            TaxaBaixaFormatada = TotalGuias == 0 ? "—" : $"{relatorio.Resumo.TaxaBaixa:0.#}%";
            TaxaGlosaFormatada = relatorio.Resumo.Baixados == 0 ? "—" : $"{relatorio.Resumo.TaxaGlosa:0.#}%";

            // A barra acompanha o número, e só existe quando o número existe. O teto de 1
            // é guarda de desenho: taxa acima de 100% não deveria acontecer, e se
            // acontecer a barra transborda o cartão em vez de denunciar o dado.
            TemTaxaBaixa = TotalGuias > 0;
            TaxaBaixaFracao = TemTaxaBaixa ? Math.Clamp(relatorio.Resumo.TaxaBaixa / 100.0, 0, 1) : 0;
            TemTaxaGlosa = relatorio.Resumo.Baixados > 0;
            TaxaGlosaFracao = TemTaxaGlosa ? Math.Clamp(relatorio.Resumo.TaxaGlosa / 100.0, 0, 1) : 0;
            TempoMedioFormatado = relatorio.Resumo.TempoMedioBaixaDias is { } dias
                ? $"{dias:0.#} dias"
                : "—";

            PorConvenio.Clear();
            foreach (var c in relatorio.PorConvenio)
                PorConvenio.Add(new LinhaConvenioGerencial
                {
                    Convenio = c.ConvenioNome,
                    Total = c.TotalCodigos,
                    Baixados = c.Baixados,
                    Pendentes = c.Pendentes,
                    TaxaBaixa = c.TotalCodigos == 0 ? "—" : $"{c.TaxaBaixa:0.#}%",
                    TaxaGlosa = c.Baixados == 0 ? "—" : $"{c.TaxaGlosa:0.#}%",
                    TempoMedio = c.TempoMedioBaixaDias is { } d ? $"{d:0.#} d" : "—"
                });

            // TODAS as faixas entram, inclusive as vazias: aging sem a faixa de maior
            // atraso lê-se como "não há pendência velha", que é a leitura errada mais cara
            // desta tela. Quem some da lista some da cabeça.
            var totalEnvelhecimento = relatorio.Envelhecimento.Sum(f => f.Quantidade);
            Envelhecimento.Clear();
            foreach (var faixa in relatorio.Envelhecimento)
                Envelhecimento.Add(new LinhaEnvelhecimento(
                    faixa.Faixa,
                    totalEnvelhecimento == 0
                        ? $"{faixa.Quantidade}"
                        : $"{faixa.Quantidade} · {(double)faixa.Quantidade / totalEnvelhecimento:P0}",
                    totalEnvelhecimento == 0 ? 0 : (double)faixa.Quantidade / totalEnvelhecimento));

            Meses.Clear();
            foreach (var mes in meses) Meses.Add(mes);

            SerieTaxaBaixa.Clear();
            foreach (var mes in meses)
                SerieTaxaBaixa.Add(new PontoGrafico(
                    mes.Rotulo, mes.TotalCodigos == 0 ? null : mes.TaxaBaixa));

            // Um ponto só não é curva nenhuma — é uma linha de base, e desenhá-la
            // prometeria uma evolução que o período não tem.
            TemSerie = SerieTaxaBaixa.Count(p => p.Valor is not null) >= 2;

            if (TotalGuias == 0)
                Mensagem = "Nenhuma guia com atendimento neste período.";
        }
        catch (Exception ex)
        {
            // Chegou tarde: outra carga mais nova já foi pedida.
            if (geracao != _geracaoCarga) return;

            NaoVerificado = true;
            Clinica.Application.Diagnostico.Registrar(
                "Gerente — visão do faturamento não pôde ser carregada", ex);
            Mensagem = $"Não foi possível carregar o faturamento: {ex.Message}";
            MensagemEhErro = true;
        }
        finally
        {
            // A carga superada não apaga o "Carregando" da que ainda está no ar.
            if (geracao == _geracaoCarga) Carregando = false;
        }
    }
}
