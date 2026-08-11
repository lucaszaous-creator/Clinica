using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Gerente.ViewModels;

/// <summary>Um paciente que parou de vir, na lista.</summary>
public sealed class LinhaSumido
{
    public required int PacienteId { get; init; }
    public required string Paciente { get; init; }
    public required string Detalhe { get; init; }
    public required string Faixa { get; init; }
    public string? Telefone { get; init; }
    public required bool EraFrequente { get; init; }
    public required bool TemPacoteAberto { get; init; }
    public required bool JaChamado { get; init; }

    public bool TemTelefone => !string.IsNullOrWhiteSpace(Telefone);
}

/// <summary>
/// Quem parou de vir (parcela 32).
///
/// A campanha de recall dispara mensagens por regra de tempo; os indicadores medem
/// no-show e ocupação. Nenhuma das duas responde a pergunta que a direção faz quando o
/// faturamento cai: **quem sumiu?** — com nome, telefone e há quanto tempo.
///
/// A diferença para o recall é o que cada um serve: lá é uma rodada automática; aqui é a
/// LISTA, para olhar caso a caso e decidir quem vale um telefonema de verdade. Numa
/// clínica de acupuntura, o paciente de tratamento longo que some vale dez recalls
/// disparados no vazio.
/// </summary>
public sealed partial class RetencaoViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;

    public ObservableCollection<LinhaSumido> Sumidos { get; } = [];

    public IReadOnlyList<int> Janelas { get; } = [60, 90, 180, 365];

    [ObservableProperty] private int _janelaDias = RetencaoPacienteService.DiasParaConsiderarSumido;
    [ObservableProperty] private bool _carregando;
    [ObservableProperty] private string _resumo = string.Empty;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    /// <summary>Falha na leitura: lista vazia por erro, não por não haver ninguém sumido.</summary>
    [ObservableProperty] private bool _naoVerificado;

    public RetencaoViewModel(IServiceScopeFactory escopos)
    {
        _escopos = escopos;
        _ = CarregarAsync();
    }

    partial void OnJanelaDiasChanged(int value) => _ = CarregarAsync();

    /// <summary>
    /// Número da carga mais recente pedida — descarte de resposta fora de ordem (parcela 50).
    /// Trocar a janela de dias dispara outra leitura; a resposta velha chegando por último
    /// deixaria a lista de sumidos de uma janela que não é a escolhida no combo.
    /// </summary>
    private int _geracaoCarga;

    [RelayCommand]
    public async Task CarregarAsync()
    {
        var geracao = ++_geracaoCarga;

        try
        {
            Carregando = true;
            NaoVerificado = false;
            Mensagem = null;
            MensagemEhErro = false;

            using var scope = _escopos.CreateScope();
            var servico = scope.ServiceProvider.GetRequiredService<RetencaoPacienteService>();

            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var lista = await servico.SumidosAsync(hoje, JanelaDias);

            // Chegou tarde: outra carga mais nova já foi pedida.
            if (geracao != _geracaoCarga) return;

            Sumidos.Clear();
            foreach (var s in lista)
                Sumidos.Add(new LinhaSumido
                {
                    PacienteId = s.PacienteId,
                    Paciente = s.Nome,
                    Telefone = s.Telefone,
                    Faixa = s.Faixa,
                    EraFrequente = s.EraFrequente,
                    TemPacoteAberto = s.TemPacoteAberto,
                    JaChamado = s.JaChamado,
                    Detalhe = $"última sessão em {s.UltimaSessao:dd/MM/yyyy} · "
                              + $"{s.DiasSemVir} dias · {s.TotalSessoes} sessão(ões) no total"
                              + (s.TemPacoteAberto ? " · TEM PACOTE EM ABERTO" : string.Empty)
                              + (s.JaChamado ? " · já recebeu recall" : string.Empty)
                });

            var frequentes = Sumidos.Count(s => s.EraFrequente);
            var comPacote = Sumidos.Count(s => s.TemPacoteAberto);

            Resumo = Sumidos.Count == 0
                ? $"Ninguém sem vir há mais de {JanelaDias} dias."
                : $"{Sumidos.Count} paciente(s) sem vir há mais de {JanelaDias} dias · "
                  + $"{frequentes} eram de tratamento"
                  + (comPacote > 0 ? $" · {comPacote} com pacote pago em aberto." : ".");
        }
        catch (Exception ex)
        {
            // Chegou tarde: outra carga mais nova já foi pedida.
            if (geracao != _geracaoCarga) return;

            Clinica.Application.Diagnostico.Registrar(
                "Gerente — lista de pacientes sumidos não pôde ser lida", ex);
            Sumidos.Clear();
            NaoVerificado = true;
            Resumo = $"Não foi possível ler a lista: {ex.Message}";
        }
        finally
        {
            // A carga superada não apaga o "Carregando" da que ainda está no ar.
            if (geracao == _geracaoCarga) Carregando = false;
            // A lista mudou (encheu ou esvaziou pela falha): o botão de exportar precisa
            // reavaliar se tem o que exportar.
            OnPropertyChanged(nameof(TemParaExportar));
        }
    }

    /// <summary>
    /// Abre o WhatsApp para chamar o paciente de volta.
    ///
    /// A mensagem é CONVITE, não cobrança: quem sumiu costuma ter parado por motivo
    /// simples (melhorou, viajou, apertou o mês), e um texto que soa como cobrança fecha
    /// a porta que a ligação veio abrir. E não leva dado clínico — o telefone pode não
    /// ser só do paciente.
    /// </summary>
    [RelayCommand]
    private void Chamar(LinhaSumido? linha)
    {
        if (linha?.Telefone is not { } telefone) return;

        var primeiro = linha.Paciente.Split(' ').FirstOrDefault() ?? linha.Paciente;
        var erro = Whatsapp.Abrir(
            telefone, linha.Paciente,
            $"Olá, {primeiro}! Faz um tempo que a gente não se vê por aqui. "
            + "Se quiser retomar as sessões, é só responder esta mensagem que a gente "
            + "encaixa um horário.");

        if (erro is null) return;
        Mensagem = erro;
        MensagemEhErro = true;
    }

    /// <summary>Há linha para exportar — é o que acende o botão de CSV.</summary>
    public bool TemParaExportar => Sumidos.Count > 0;

    /// <summary>A lista em CSV, para a direção trabalhar fora da tela.</summary>
    [RelayCommand]
    private async Task ExportarAsync()
    {
        // Lista vazia: o botão já nasce apagado (`TemParaExportar`), mas quem chegar
        // aqui por atalho ouve o motivo em vez de ver o clique cair no vazio.
        if (Sumidos.Count == 0)
        {
            Mensagem = "Não há ninguém na lista para exportar.";
            MensagemEhErro = false;
            return;
        }

        try
        {
            var csv = ExportacaoCsv.Montar(
                ["Paciente", "Telefone", "Situação", "Faixa", "Era de tratamento", "Pacote em aberto"],
                Sumidos.Select(s => new[]
                {
                    s.Paciente,
                    s.Telefone ?? "—",
                    s.Detalhe,
                    s.Faixa,
                    s.EraFrequente ? "sim" : "não",
                    s.TemPacoteAberto ? "sim" : "não"
                }));

            var erro = await ImpressaoPdf.SalvarEAbrirAsync(
                csv,
                ImpressaoPdf.NomeSeguro($"pacientes-sumidos-{DateTime.Today:yyyy-MM-dd}.csv"),
                "CSV (*.csv)|*.csv", ".csv");

            if (erro is null) return;
            Mensagem = erro;
            MensagemEhErro = true;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Gerente — lista de sumidos não pôde ser exportada", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }
}
