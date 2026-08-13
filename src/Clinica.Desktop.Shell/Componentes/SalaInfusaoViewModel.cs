using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Domain.Entities;
using Clinica.Domain;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace Clinica.Desktop.Shell.Componentes;

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

    /// <summary>
    /// O código de conferência impresso na folha (ex.: "K7Q2…"): quem está com o papel na
    /// mão acha a folha por ele, de qualquer dia — a lista só mostra hoje.
    /// </summary>
    [ObservableProperty] private string _codigoBusca = string.Empty;

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

    /// <summary>
    /// Descarte de resposta fora de ordem (parcela 50): a batida do relógio e a caixa
    /// "incluir encerradas" concorrem SEM coordenação nenhuma — a resposta velha chegando
    /// por último deixava a lista do estado oposto ao da caixa marcada, numa tela de
    /// checagem de medicação.
    /// </summary>
    private int _geracaoCarga;

    private async Task BuscarAsync(bool silenciosa)
    {
        var geracao = ++_geracaoCarga;

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

            // Chegou tarde: outra carga mais nova já foi pedida.
            if (geracao != _geracaoCarga) return;

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

            if (silenciosa || geracao != _geracaoCarga) return;

            NaoVerificado = true;
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
        finally
        {
            // A carga superada não apaga o "Carregando" da que ainda está no ar.
            if (!silenciosa && geracao == _geracaoCarga) Carregando = false;
        }
    }

    /// <summary>Abre a folha para checar item a item.</summary>
    [RelayCommand]
    private async Task AbrirAsync(LinhaSalaInfusao? linha)
    {
        if (linha is null) return;
        await AbrirFolhaAsync(linha.PrescricaoId);
    }

    /// <summary>
    /// Acha a folha pelo CÓDIGO impresso nela — a conferência que este sistema oferece no
    /// lugar do ICP-Brasil, e que até aqui só a Recepção alcançava (para documento
    /// clínico). A técnica está com o papel na mão; o código é a ponte de volta.
    /// </summary>
    [RelayCommand]
    private async Task AbrirPorCodigoAsync()
    {
        var codigo = CodigoBusca.Trim();
        if (codigo.Length == 0)
        {
            Mensagem = "Digite o código de conferência impresso na folha (ex.: K7Q2M3).";
            MensagemEhErro = true;
            return;
        }

        try
        {
            int prescricaoId;
            using (var scope = _escopos.CreateScope())
            {
                var servico = scope.ServiceProvider.GetRequiredService<PrescricaoInternaService>();
                var prescricao = await servico.PorCodigoAsync(codigo);
                if (prescricao is null)
                {
                    Mensagem = $"Nenhuma prescrição com o código \"{codigo}\". Confira o "
                             + "papel — o código não usa as letras I e O nem os dígitos 0 e 1.";
                    MensagemEhErro = true;
                    return;
                }
                prescricaoId = prescricao.Id;
            }

            Mensagem = null;
            MensagemEhErro = false;
            CodigoBusca = string.Empty;
            await AbrirFolhaAsync(prescricaoId);
        }
        catch (Exception ex)
        {
            Application.Diagnostico.Registrar(
                "Sala de infusão — busca pelo código não pôde ser feita", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    /// <summary>
    /// Imprime a via de PRESCRIÇÃO — a que a enfermagem confere e assina à caneta. É a
    /// porta que faltava na sala: a impressão só existia na tela de quem prescreve, num
    /// app que a máquina da enfermagem não instala.
    /// </summary>
    [RelayCommand]
    private async Task ImprimirAsync(LinhaSalaInfusao? linha)
    {
        if (linha is null) return;

        try
        {
            FolhaAssinada folha;
            using (var scope = _escopos.CreateScope())
            {
                var assinaturas = scope.ServiceProvider
                    .GetRequiredService<AssinaturaDePrescricaoService>();
                folha = await assinaturas.FolhaAsync(
                    linha.PrescricaoId, FolhaPrescricao.Prescricao);
            }

            var erro = await ImpressaoPdf.SalvarEAbrirAsync(
                folha.Pdf, ImpressaoPdf.NomeSeguro(folha.NomeArquivo));

            Mensagem = erro ?? folha.Conferencia?.Frase;
            MensagemEhErro = erro is not null || folha.Conferencia is { Integra: false };
        }
        catch (Exception ex)
        {
            Application.Diagnostico.Registrar(
                "Sala de infusão — folha não pôde ser impressa", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    private async Task AbrirFolhaAsync(int prescricaoId)
    {
        var vm = new FolhaExecucaoViewModel(_escopos, _dialogo, prescricaoId);
        var janela = new FolhaExecucaoWindow(vm)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };
        janela.ShowDialog();

        await CarregarAsync();
    }

    public void Dispose() => _releitura.Stop();
}
