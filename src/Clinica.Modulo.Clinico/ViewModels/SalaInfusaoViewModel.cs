using Clinica.Application.Servicos;
using Clinica.Clinico.Janelas;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Domain.Entities;
using Clinica.Domain;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace Clinica.Clinico.ViewModels;

/// <summary>Uma folha na fila da sala.</summary>
public sealed class LinhaSalaInfusao
{
    public required int PrescricaoId { get; init; }
    public required string Paciente { get; init; }
    public required string Numero { get; init; }
    public required string Hora { get; init; }
    public required string Prescritor { get; init; }
    public required string Progresso { get; init; }
    public required string Itens { get; init; }
    public required bool TemPendencia { get; init; }
    public required bool Encerrada { get; init; }

    public static LinhaSalaInfusao De(PrescricaoInterna p) => new()
    {
        PrescricaoId = p.Id,
        Paciente = p.Paciente?.Nome ?? "—",
        Numero = p.Numero,
        Hora = p.Hora.ToString("HH\\:mm"),
        Prescritor = p.Profissional?.Rotulo ?? "—",
        Progresso = p.Situacao == SituacaoPrescricao.Encerrada
            ? $"encerrada · {p.Realizados} realizados, {p.NaoRealizados} não realizados"
            : $"{p.Realizados} de {p.Itens.Count} realizados · {p.Pendentes} aguardando",
        Itens = string.Join(" · ", p.Itens
            .OrderBy(i => i.Ordem)
            .Take(3)
            .Select(i => i.Descricao)) + (p.Itens.Count > 3 ? " · …" : string.Empty),
        TemPendencia = p.Pendentes > 0,
        Encerrada = p.Situacao == SituacaoPrescricao.Encerrada
    };
}

/// <summary>
/// A SALA DE INFUSÃO (parcela 42): as folhas assinadas do dia, esperando execução.
///
/// Por que uma tela de LISTA, e a folha atrás de um clique
/// -------------------------------------------------------
/// É a regra de leiaute que o cliente cobrou cinco vezes: seção que só existe COM um item
/// escolhido não divide a tela com a lista. Aqui a lista responde "o que falta fazer hoje"
/// com a largura inteira, e a folha — que é onde se checa item a item — abre em janela.
///
/// Rascunho NÃO aparece
/// --------------------
/// Só entram folhas ASSINADAS. Mostrar rascunho na sala convidaria a técnica a administrar
/// o que ninguém mandou, que é o buraco clássico do papel — e é justamente o que a
/// assinatura existe para fechar.
///
/// A releitura periódica é o que faz o recado CHEGAR
/// -------------------------------------------------
/// Como na fila do balcão e no quadro do consultório (parcelas 26 e 38): quem prescreve
/// está noutra máquina, e sem releitura a folha nova só apareceria no próximo clique. Ela
/// é SILENCIOSA — não acende "Carregando" nem escreve erro —, porque quem está com um
/// paciente na cadeira não pode ver a lista piscar em branco a cada minuto.
/// </summary>
public sealed partial class SalaInfusaoViewModel : ObservableObject, IDisposable
{
    private readonly IServiceScopeFactory _escopos;
    private readonly IDialogoService _dialogo;
    private readonly System.Windows.Threading.DispatcherTimer _releitura;

    public ObservableCollection<LinhaSalaInfusao> Folhas { get; } = [];

    [ObservableProperty] private bool _carregando;
    [ObservableProperty] private bool _naoVerificado;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;
    [ObservableProperty] private string _resumo = string.Empty;

    /// <summary>Mostra também as já encerradas — a conferência do fim do dia.</summary>
    [ObservableProperty] private bool _incluirEncerradas;

    /// <summary>Metade visível da permissão; a que impede é o <c>Exigir</c> no comando.</summary>
    public bool PodeChecar => SessaoUsuario.Atual.Pode(Permissao.ChecarPrescricao);

    public SalaInfusaoViewModel(IServiceScopeFactory escopos, IDialogoService dialogo)
    {
        _escopos = escopos;
        _dialogo = dialogo;

        _releitura = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(1)
        };
        _releitura.Tick += async (_, _) => await ReleituraSilenciosaAsync();

        _ = CarregarAsync();
    }

    /// <summary>Ligada e desligada pelo Loaded/Unloaded da View — timer de tela fechada é vazamento.</summary>
    public void Acompanhar(bool ligado)
    {
        if (ligado) _releitura.Start();
        else _releitura.Stop();
    }

    partial void OnIncluirEncerradasChanged(bool value) => _ = CarregarAsync();

    [RelayCommand]
    public async Task CarregarAsync() => await BuscarAsync(silenciosa: false);

    private Task ReleituraSilenciosaAsync() => BuscarAsync(silenciosa: true);

    private async Task BuscarAsync(bool silenciosa)
    {
        try
        {
            if (!silenciosa)
            {
                Carregando = true;
                NaoVerificado = false;
                Mensagem = null;
                MensagemEhErro = false;
            }

            using var scope = _escopos.CreateScope();
            var servico = scope.ServiceProvider.GetRequiredService<ChecagemPrescricaoService>();

            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var folhas = await servico.DoDiaAsync(hoje, incluirEncerradas: IncluirEncerradas);

            Folhas.Clear();
            foreach (var folha in folhas)
                Folhas.Add(LinhaSalaInfusao.De(folha));

            var pendentes = Folhas.Count(f => f.TemPendencia);
            Resumo = Folhas.Count == 0
                ? "Nenhuma prescrição de infusão para hoje."
                : $"{Folhas.Count} folha(s) hoje · {pendentes} com item aguardando.";
        }
        catch (Exception ex)
        {
            // A releitura periódica falha CALADA na tela e RUIDOSA no log: quem está com o
            // paciente na cadeira não pode ver a lista virar erro a cada minuto por causa
            // de uma oscilação de rede — mas o problema não pode sumir.
            Application.Diagnostico.Registrar("Consultório — sala de infusão", ex);

            if (silenciosa) return;

            NaoVerificado = true;
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
        finally
        {
            if (!silenciosa) Carregando = false;
        }
    }

    /// <summary>Abre a folha para checar item a item.</summary>
    [RelayCommand]
    private async Task AbrirAsync(LinhaSalaInfusao? linha)
    {
        if (linha is null) return;

        var vm = new FolhaExecucaoViewModel(_escopos, _dialogo, linha.PrescricaoId);
        var janela = new FolhaExecucaoWindow(vm)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };
        janela.ShowDialog();

        await CarregarAsync();
    }

    public void Dispose() => _releitura.Stop();
}
