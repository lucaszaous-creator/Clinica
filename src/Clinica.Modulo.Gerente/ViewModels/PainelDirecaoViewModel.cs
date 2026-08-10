using System.Collections.ObjectModel;
using System.Globalization;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Clinica.Desktop.Shell.Modulos;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Gerente.ViewModels;

/// <summary>Uma meta do mês no painel: o alvo, o realizado e o quanto falta.</summary>
public sealed class LinhaMetaPainel
{
    /// <summary>Largura interna do cartão de meta no XAML (290 menos o padding do Card).</summary>
    private const double LarguraUtilDoCartao = 258;

    public required string Rotulo { get; init; }
    public required string Alvo { get; init; }
    public required string Realizado { get; init; }
    public required string Situacao { get; init; }

    /// <summary>
    /// Largura da barra EM PIXELS, já resolvida aqui.
    ///
    /// É a regra do design system para gráfico: a fração vem normalizada do ViewModel,
    /// porque o DataTemplate não enxerga os irmãos da série nem a largura do pai. Sem base
    /// de cálculo a barra fica em ZERO — e quem diz que não houve apuração é o texto ao
    /// lado, não uma barra cheia por engano.
    /// </summary>
    public double LarguraBarra { get; init; }

    public bool Atingida { get; init; }
    public bool EmRisco { get; init; }

    public static LinhaMetaPainel De(MetaApurada m, Func<IndicadorMeta, decimal, string> formatar)
    {
        // Realizado NULO é "não apurado", nunca zero: escrever "R$ 0,00" onde a leitura
        // falhou faria a direção concluir que o mês não teve movimento.
        var realizado = m.Realizado is { } r ? formatar(m.Indicador, r) : "—";

        var situacao = m.Atingida
            ? "meta batida"
            : m.EmRisco
                ? m.DesvioProjetado is { } d
                    ? $"no ritmo atual fecha {d:0.#}% abaixo"
                    : "no ritmo atual não alcança"
                : m.PercentualAtingido is { } pct
                    ? $"{pct:0.#}% do alvo"
                    : "sem base para apurar";

        return new LinhaMetaPainel
        {
            Rotulo = m.Rotulo + (m.Profissional is { } prof ? $" · {prof}" : string.Empty),
            Alvo = formatar(m.Indicador, m.Alvo),
            Realizado = realizado,
            Situacao = situacao,
            // A barra satura em 100%: passar do alvo é bom, e uma barra de 180% sairia
            // do cartão sem dizer nada que o texto ao lado já não diga.
            LarguraBarra = m.PercentualAtingido is { } p
                ? Math.Min(p / 100.0, 1.0) * LarguraUtilDoCartao
                : 0,
            Atingida = m.Atingida,
            EmRisco = m.EmRisco
        };
    }
}

/// <summary>
/// Um alerta como a tela mostra: título, explicação, cor e para onde ir.
/// </summary>
public sealed partial class LinhaAlertaDirecao : ObservableObject
{
    public required string Titulo { get; init; }
    public required string Detalhe { get; init; }

    /// <summary>
    /// Peso do alerta. Vai como ENUM e a cor é decidida por <c>DataTrigger</c> no XAML —
    /// mandar daqui a chave do estilo ("AlertaPerigo") não funciona: <c>StaticResource</c>
    /// resolve em tempo de compilação do XAML, não a partir de uma string do ViewModel.
    /// </summary>
    public required GravidadeDirecao Gravidade { get; init; }

    /// <summary>Rótulo do botão ("Ver contas a pagar/receber").</summary>
    public required string DestinoRotulo { get; init; }

    private string _destino = string.Empty;

    public required string Destino
    {
        get => _destino;
        init
        {
            _destino = value;
            // O destino pode não existir para quem está usando: no executável do
            // Financeiro o faturamento consolidado não é carregado, e uma pessoa sem
            // VerFinanceiro não vê as telas de dinheiro. Botão que não faz nada é pior
            // do que botão ausente.
            PodeIr = NavegacaoSuite.Existe(value);
        }
    }

    public bool PodeIr { get; private set; }

    [RelayCommand]
    private void Ir() => NavegacaoSuite.Ir(Destino);
}

/// <summary>
/// O painel da direção (parcela 22): a primeira tela do Gerente Geral.
///
/// Antes, o Gerente — que carrega os três módulos — abria no primeiro item do primeiro
/// deles: o painel da RECEPÇÃO. Quem manda na clínica entrava no sistema e via a fila do
/// balcão; informação correta, dona errada. O que a direção precisa saber estava
/// espalhado por sete telas, e ninguém abre sete telas toda manhã — então o caixa não
/// conferido, o depósito que não caiu e a conta vencida ficavam invisíveis exatamente
/// porque cada um morava numa tela que só se abre quando já se desconfia do problema.
///
/// A tela **não calcula nada**: cada número vem do serviço dono dele, e o
/// <see cref="PainelDirecaoService"/> só os junta. Painel que recalcula por conta própria
/// vira a segunda verdade sobre o mesmo dinheiro.
///
/// Cada alerta LEVA ao assunto (<see cref="NavegacaoSuite"/>). Um painel que aponta o
/// problema e não leva até ele faz a pessoa decorar em qual seção mora cada coisa — e
/// resolver o que está à mão em vez do que é urgente.
/// </summary>
public sealed partial class PainelDirecaoViewModel : ObservableObject
{
    private static readonly CultureInfo Brasil = new("pt-BR");

    private readonly IServiceScopeFactory _escopos;

    public ObservableCollection<LinhaAlertaDirecao> Alertas { get; } = [];

    /// <summary>
    /// As metas do mês com o realizado ao lado (parcela 54).
    ///
    /// <b>O serviço entregava isto desde a parcela 28 e nenhuma tela lia.</b> O painel só
    /// mostrava a meta quando ela estava EM RISCO, por um alerta — ou seja, meta no ritmo
    /// certo era invisível fora da tela de Metas, e a própria tela de Metas prometia por
    /// escrito que "o painel da direção já compara com ela". Promessa que o código não
    /// cumpria.
    ///
    /// Ver a meta batida importa tanto quanto ver a que falhou: é ela que responde
    /// "chegamos onde dissemos que íamos chegar?", que é a pergunta que a meta existe para
    /// responder — e um painel que só fala de meta quando há problema treina a direção a
    /// ler meta como má notícia.
    /// </summary>
    public ObservableCollection<LinhaMetaPainel> MetasDoMes { get; } = [];

    [ObservableProperty] private bool _temMetas;

    [ObservableProperty] private string _saudacao = string.Empty;
    [ObservableProperty] private string _dataFormatada = string.Empty;

    // ---- Dinheiro do mês ----
    [ObservableProperty] private string _entradasMes = "—";
    [ObservableProperty] private string _saidasMes = "—";
    [ObservableProperty] private string _saldoMes = "—";
    [ObservableProperty] private bool _saldoNegativo;

    /// <summary>"▲ 33% vs. o mesmo trecho de jul" — vazio sem base de comparação.</summary>
    [ObservableProperty] private string _variacaoEntradas = string.Empty;
    [ObservableProperty] private bool _temVariacao;
    [ObservableProperty] private bool _variacaoPositiva;

    // ---- Os quatro números que a direção cobra ----
    [ObservableProperty] private string _contasVencidas = "—";
    [ObservableProperty] private string _contasVencidasDetalhe = string.Empty;
    [ObservableProperty] private string _depositoAtrasado = "—";
    [ObservableProperty] private string _depositoAtrasadoDetalhe = string.Empty;
    [ObservableProperty] private string _pendenciasFaturamento = "—";
    [ObservableProperty] private string _pendenciasDetalhe = string.Empty;
    [ObservableProperty] private string _aReceberPrevisto = "—";
    [ObservableProperty] private string _aReceberDetalhe = string.Empty;

    [ObservableProperty] private bool _semAlerta;
    [ObservableProperty] private bool _carregando;

    /// <summary>
    /// Blocos que não puderam ser lidos. A tela mostra QUAIS: um painel que diz "nenhuma
    /// conta vencida" porque a consulta quebrou é pior do que um painel que não abre.
    /// </summary>
    [ObservableProperty] private string _naoVerificados = string.Empty;
    [ObservableProperty] private bool _temNaoVerificado;

    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    public PainelDirecaoViewModel(IServiceScopeFactory escopos)
    {
        _escopos = escopos;
        _ = CarregarAsync();
    }

    [RelayCommand]
    public async Task CarregarAsync()
    {
        if (Carregando) return;
        Carregando = true;
        try
        {
            Mensagem = null;
            MensagemEhErro = false;

            var hoje = DateOnly.FromDateTime(DateTime.Today);
            Saudacao = SaudacaoDaHora(DateTime.Now.Hour);
            DataFormatada = DateTime.Today.ToString("dddd, dd 'de' MMMM 'de' yyyy", Brasil);

            using var scope = _escopos.CreateScope();
            var servico = scope.ServiceProvider.GetRequiredService<PainelDirecaoService>();
            var p = await servico.MontarAsync(hoje);

            EntradasMes = Moeda(p.EntradasMes);
            SaidasMes = Moeda(p.SaidasMes);
            SaldoMes = Moeda(p.SaldoMes);
            SaldoNegativo = p.SaldoMes < 0m;

            // Sem base de comparação, nada de seta — "—" continua sendo estado legítimo.
            if (p.VariacaoEntradas is { } variacao)
            {
                TemVariacao = true;
                VariacaoPositiva = variacao >= 0d;
                var mesAnterior = new DateOnly(hoje.Year, hoje.Month, 1).AddMonths(-1);
                VariacaoEntradas = string.Format(
                    Brasil, "{0} {1:P0} vs. o mesmo trecho de {2}",
                    variacao >= 0d ? "▲" : "▼", Math.Abs(variacao),
                    mesAnterior.ToString("MMM", Brasil));
            }
            else
            {
                TemVariacao = false;
                VariacaoEntradas = string.Empty;
            }

            ContasVencidas = p.Contas.QuantidadeVencida.ToString(Brasil);
            ContasVencidasDetalhe = p.Contas.TemVencido
                ? $"{Moeda(p.Contas.TotalVencido)} em aberto"
                : "nada vencido";

            DepositoAtrasado = p.Recebiveis.QuantidadeAtrasada.ToString(Brasil);
            DepositoAtrasadoDetalhe = p.Recebiveis.TemAtraso
                ? $"{Moeda(p.Recebiveis.LiquidoAtrasado)} líquidos não creditados"
                : "adquirentes em dia";

            PendenciasFaturamento = p.PendenciasVencidas.ToString(Brasil);
            PendenciasDetalhe = p.PendenciasEmAberto > 0
                ? $"de {p.PendenciasEmAberto} pendente(s) no total"
                : "nenhuma guia pendente";

            AReceberPrevisto = Moeda(p.Recebiveis.LiquidoAVencer + p.Contas.AReceberAVencer);
            AReceberDetalhe = p.Recebiveis.ProximoDeposito is { } proximo
                ? $"próximo crédito em {proximo:dd/MM}"
                : "sem crédito de cartão previsto";

            Alertas.Clear();
            MetasDoMes.Clear();
            foreach (var m in p.Metas)
                MetasDoMes.Add(LinhaMetaPainel.De(m, MetaMensal.Formatar));
            TemMetas = MetasDoMes.Count > 0;

            foreach (var a in p.Alertas) Alertas.Add(Montar(a));
            SemAlerta = p.SemAlerta;

            TemNaoVerificado = p.TemNaoVerificado;
            NaoVerificados = p.TemNaoVerificado
                ? "Não foi possível ler: " + string.Join(", ", p.NaoVerificados) +
                  ". Estes blocos estão em branco por falha de leitura, não por estarem em ordem."
                : string.Empty;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Gerente — painel da direção não pôde ser montado", ex);
            Mensagem = $"Não foi possível montar o painel: {ex.Message}";
            MensagemEhErro = true;
        }
        finally
        {
            Carregando = false;
        }
    }

    private static LinhaAlertaDirecao Montar(AlertaDirecao a)
    {
        var (destino, rotulo) = Destino(a.Assunto);
        return new LinhaAlertaDirecao
        {
            Titulo = a.Titulo,
            Detalhe = a.Detalhe,
            Gravidade = a.Gravidade,
            Destino = destino,
            DestinoRotulo = rotulo
        };
    }

    /// <summary>
    /// De que assunto para qual seção. Aqui, e não no serviço: a camada de aplicação não
    /// conhece a sidebar — e não deve.
    /// </summary>
    private static (string Chave, string Rotulo) Destino(AssuntoDirecao assunto) => assunto switch
    {
        AssuntoDirecao.ContasVencidas => (ChavesSuite.Contas, "Ver contas"),
        AssuntoDirecao.PacientesDevendo => (ChavesSuite.Inadimplencia, "Ver quem deve"),
        AssuntoDirecao.DepositoAtrasado => (ChavesSuite.Recebiveis, "Ver recebíveis"),
        AssuntoDirecao.CaixaNaoConferido => (ChavesSuite.FechamentoCaixa, "Conferir caixa"),
        AssuntoDirecao.GuiasSemReceita => (ChavesSuite.Conciliacao, "Ver conciliação"),
        // Mesmo destino, rótulo diferente: a Conciliação tem as duas abas, e o botão
        // precisa dizer QUAL delas resolve o alerta — "ver conciliação" duas vezes na
        // mesma tela faria a direção clicar no primeiro e achar que já viu.
        AssuntoDirecao.ReceitaGlosada => (ChavesSuite.Conciliacao, "Ver glosadas"),
        // Chave local do proprio modulo: Metas nao atravessa modulo, entao continua
        // sendo const do dono em vez de virar contrato em ChavesSuite.
        AssuntoDirecao.MetaEmRisco => (Modulo.ModuloGerente.ChaveMetas, "Ver metas"),
        // Chave do Financeiro: atravessa modulo, entao vem de ChavesSuite.
        AssuntoDirecao.OrcamentoEstourado => (ChavesSuite.Resultado, "Ver tetos"),
        // O prontuário em aberto se resolve no Consultório, que é onde as sessões sem
        // evolução estão listadas uma a uma. No Gerente o módulo está carregado; num
        // executável que não o carregue o botão nasce desabilitado sozinho.
        AssuntoDirecao.ProntuarioEmAberto => (ChavesSuite.ConsultorioMeuDia, "Ver sessões"),
        _ => (ChavesSuite.FaturamentoTiss, "Ver faturamento")
    };

    /// <summary>
    /// Bom dia / boa tarde / boa noite. Detalhe pequeno e proposital: esta é a primeira
    /// tela que a direção vê no dia, e um painel que começa com seis números vermelhos
    /// sem uma linha humana é lido como cobrança.
    /// </summary>
    private static string SaudacaoDaHora(int hora) => hora switch
    {
        < 12 => "Bom dia",
        < 18 => "Boa tarde",
        _ => "Boa noite"
    };

    private static string Moeda(decimal valor) => valor.ToString("C2", Brasil);
}
