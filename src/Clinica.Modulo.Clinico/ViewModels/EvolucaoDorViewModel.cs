using System.Collections.ObjectModel;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Clinico.Modulo;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell.Componentes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Clinico.ViewModels;

/// <summary>Uma sessão medida, como a tabela da curva a mostra.</summary>
public sealed class LinhaDor
{
    public required string Data { get; init; }
    public required int Antes { get; init; }
    public required int Depois { get; init; }
    public required string Variacao { get; init; }

    /// <summary>A sessão aliviou (a dor caiu do início ao fim dela).</summary>
    public required bool Aliviou { get; init; }

    public static LinhaDor De(PontoEva p) => new()
    {
        Data = p.Data.ToString("dd/MM/yyyy"),
        Antes = p.Antes,
        Depois = p.Depois,
        Variacao = p.Variacao switch
        {
            > 0 => $"−{p.Variacao}",
            < 0 => $"+{-p.Variacao}",
            _ => "sem mudança"
        },
        Aliviou = p.Variacao > 0
    };
}

/// <summary>
/// O PAINEL DE EVOLUÇÃO DA DOR — a leitura que o profissional mostra ao paciente.
///
/// A EVA já era medida desde a parcela 2 e já aparecia como quatro números no prontuário
/// da recepção. O que faltava era a CURVA: quinze sessões de números numa tabela não
/// respondem "estamos melhorando?", e é essa a pergunta que se faz na sétima semana de
/// tratamento, quando o paciente pergunta se vale a pena continuar.
///
/// Duas linhas, não uma
/// --------------------
/// A curva ANTES mede o tratamento; a curva DEPOIS mede a sessão. Um paciente pode sair
/// de toda sessão com 2 e voltar na semana seguinte com 8 — a dor alivia e retorna, e o
/// tratamento não está sustentando. Uma linha só esconderia exatamente esse caso, que é o
/// que muda conduta.
///
/// O que a tela NÃO faz é inventar leitura: sem o par completo não há ponto, sem pontos
/// não há curva, e o texto diz por quê. É a mesma regra do resto do projeto — "não medido"
/// e "zero" são coisas diferentes.
/// </summary>
public sealed partial class EvolucaoDorViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly PacienteEmFoco _foco;

    /// <summary>
    /// Sessões olhadas para dizer se o tratamento está estacionado. Três é o mínimo que
    /// distingue tendência de oscilação — com duas, uma semana ruim já viraria diagnóstico
    /// de fracasso.
    /// </summary>
    public const int SessoesParaTendencia = 3;

    public ObservableCollection<LinhaDor> Sessoes { get; } = [];

    /// <summary>Série da dor ANTES de cada sessão — a curva do tratamento.</summary>
    public ObservableCollection<PontoGrafico> CurvaAntes { get; } = [];

    /// <summary>Série da dor DEPOIS de cada sessão — a curva do alívio imediato.</summary>
    public ObservableCollection<PontoGrafico> CurvaDepois { get; } = [];

    [ObservableProperty] private string _paciente = string.Empty;
    [ObservableProperty] private bool _semPaciente = true;

    [ObservableProperty] private string _dorInicial = "—";
    [ObservableProperty] private string _dorAtual = "—";
    [ObservableProperty] private string _ganhoAcumulado = "—";
    [ObservableProperty] private string _alivioMedio = "—";
    [ObservableProperty] private string _reducao = "—";

    [ObservableProperty] private string _resumo = string.Empty;

    /// <summary>Leitura da tendência das últimas sessões, em uma frase.</summary>
    [ObservableProperty] private string _tendencia = string.Empty;

    /// <summary>A tendência é de piora ou de estagnação — a tela destaca.</summary>
    [ObservableProperty] private bool _tendenciaPreocupa;

    /// <summary>
    /// Há pelo menos uma sessão com o par EVA completo — ou seja, há curva.
    ///
    /// É o que faz o gráfico SUMIR quando não há o que desenhar: sem isso ficavam 200 px
    /// de área vazia dizendo, com o desenho, a mesma coisa que a frase logo acima já dizia
    /// com palavras.
    /// </summary>
    [ObservableProperty] private bool _temCurva;

    [ObservableProperty] private bool _carregando;
    [ObservableProperty] private bool _naoVerificado;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    public EvolucaoDorViewModel(IServiceScopeFactory escopos, PacienteEmFoco foco)
    {
        _escopos = escopos;
        _foco = foco;

        if (_foco.Definido) _ = CarregarAsync();
    }

    private int PacienteId => _foco.PacienteId ?? 0;

    [RelayCommand]
    public async Task CarregarAsync()
    {
        SemPaciente = PacienteId == 0;
        Sessoes.Clear();
        CurvaAntes.Clear();
        CurvaDepois.Clear();

        if (SemPaciente)
        {
            Paciente = string.Empty;
            return;
        }

        try
        {
            Carregando = true;
            NaoVerificado = false;
            Mensagem = null;
            MensagemEhErro = false;
            Paciente = _foco.Nome;

            using var scope = _escopos.CreateScope();
            var prontuario = scope.ServiceProvider.GetRequiredService<ProntuarioService>();

            Aplicar(await prontuario.EvolucaoDaDorAsync(PacienteId));
        }
        catch (Exception ex)
        {
            NaoVerificado = true;
            Clinica.Application.Diagnostico.Registrar("Consultório — evolução da dor não pôde ser lida", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
        finally
        {
            Carregando = false;
        }
    }

    private void Aplicar(EvolucaoDaDor dor)
    {
        foreach (var p in dor.Pontos)
        {
            Sessoes.Add(LinhaDor.De(p));

            var rotulo = p.Data.ToString("dd/MM");
            CurvaAntes.Add(new PontoGrafico(rotulo, p.Antes));
            CurvaDepois.Add(new PontoGrafico(rotulo, p.Depois));
        }

        TemCurva = dor.SessoesComMedida > 0;

        if (dor.SessoesComMedida == 0)
        {
            DorInicial = DorAtual = GanhoAcumulado = AlivioMedio = Reducao = "—";
            Tendencia = string.Empty;
            TendenciaPreocupa = false;
            Resumo = dor.SessoesRegistradas == 0
                ? "Nenhuma sessão registrada no prontuário ainda."
                : $"{dor.SessoesRegistradas} sessão(ões) registradas e nenhuma com o par EVA "
                  + "(antes e depois). Sem o par não há curva: uma medida solta não diz se a dor melhorou.";
            return;
        }

        DorInicial = $"{dor.DorInicial}/10";
        DorAtual = $"{dor.DorAtual}/10";
        GanhoAcumulado = dor.GanhoAcumulado is { } ganho
            ? (ganho >= 0 ? $"−{ganho} pontos" : $"+{-ganho} pontos")
            : "—";
        AlivioMedio = $"{dor.AlivioMedioPorSessao:0.#} por sessão";

        // A redução percentual é a frase que o paciente entende ("caiu 60%"). Só existe
        // com dor inicial maior que zero: quem começou em 0 não tem de quanto cair, e
        // dividir por zero produziria um número que a tela mostraria com cara de exato.
        Reducao = dor.DorInicial is { } inicial and > 0 && dor.GanhoAcumulado is { } g
            ? $"{100.0 * g / inicial:0}%"
            : "—";

        Resumo = $"{dor.SessoesComMedida} de {dor.SessoesRegistradas} sessão(ões) com EVA medida.";

        AplicarTendencia(dor);
    }

    /// <summary>
    /// A leitura das últimas sessões, que é a que muda conduta.
    ///
    /// Compara a dor ANTES da primeira e da última das <see cref="SessoesParaTendencia"/>
    /// mais recentes — de propósito a "antes", não a "depois": alívio dentro da sessão que
    /// não se sustenta até a próxima é justamente o padrão que a leitura precisa expor.
    ///
    /// Com menos sessões que o mínimo a tela diz que ainda não dá para afirmar, em vez de
    /// arriscar uma tendência sobre dois pontos.
    /// </summary>
    private void AplicarTendencia(EvolucaoDaDor dor)
    {
        if (dor.Pontos.Count < SessoesParaTendencia)
        {
            TendenciaPreocupa = false;
            Tendencia = $"São necessárias ao menos {SessoesParaTendencia} sessões medidas "
                        + "para falar em tendência; até lá, cada ponto é só um dia.";
            return;
        }

        var ultimas = dor.Pontos.TakeLast(SessoesParaTendencia).ToList();
        var variacao = ultimas[0].Antes - ultimas[^1].Antes;

        TendenciaPreocupa = variacao <= 0;
        Tendencia = variacao switch
        {
            > 0 => $"Nas últimas {SessoesParaTendencia} sessões a dor de CHEGADA caiu "
                   + $"{variacao} ponto(s) — o alívio está se sustentando entre as sessões.",
            0 => $"Nas últimas {SessoesParaTendencia} sessões a dor de CHEGADA não mudou: "
                 + "a sessão alivia, e o alívio não chega à seguinte. Vale revisar a conduta.",
            _ => $"Nas últimas {SessoesParaTendencia} sessões a dor de CHEGADA subiu "
                 + $"{-variacao} ponto(s). Vale revisar a conduta e investigar o que mudou."
        };
    }

    /// <summary>
    /// A curva em planilha. Sai com o mesmo "—" da tela nas colunas sem base — trocar por
    /// zero faria o Excel calcular média sobre um número que não existe.
    /// </summary>
    [RelayCommand]
    private async Task ExportarAsync()
    {
        try
        {
            if (Sessoes.Count == 0)
            {
                Mensagem = "Não há sessão medida para exportar.";
                MensagemEhErro = true;
                return;
            }

            var csv = ExportacaoCsv.Montar(
                ["Data", "EVA antes", "EVA depois", "Variação"],
                Sessoes.Select(s => new[]
                {
                    s.Data,
                    s.Antes.ToString(),
                    s.Depois.ToString(),
                    s.Variacao
                }));

            var erro = await ImpressaoPdf.SalvarEAbrirAsync(
                csv,
                ImpressaoPdf.NomeSeguro($"evolucao-da-dor-{Paciente}-{DateTime.Today:yyyy-MM-dd}.csv"),
                "CSV (*.csv)|*.csv", ".csv");

            if (erro is null) return;
            Mensagem = erro;
            MensagemEhErro = true;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Consultório — curva de dor não pôde ser exportada", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }
}
