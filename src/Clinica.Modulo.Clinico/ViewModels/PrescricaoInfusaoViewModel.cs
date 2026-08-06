using Clinica.Application.Servicos;
using Clinica.Clinico.Janelas;
using Clinica.Clinico.Modulo;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Desktop.Shell;
using Clinica.Domain.Entities;
using Clinica.Domain;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace Clinica.Clinico.ViewModels;

/// <summary>Uma folha de infusão na lista do paciente.</summary>
public sealed class LinhaPrescricaoInterna
{
    public required int PrescricaoId { get; init; }
    public required string Numero { get; init; }
    public required string Data { get; init; }
    public required string Situacao { get; init; }
    public required string Resumo { get; init; }
    public required string Execucao { get; init; }
    public required string Codigo { get; init; }
    public required bool Cancelada { get; init; }
    public required bool TemAssinatura { get; init; }

    /// <summary>Já houve execução registrada — a folha de registro tem o que mostrar.</summary>
    public required bool TemRegistroExecucao { get; init; }

    /// <summary>Só rascunho se cancela — depois de executada a folha é registro de um fato.</summary>
    public required bool PodeCancelar { get; init; }

    public static LinhaPrescricaoInterna De(PrescricaoInterna p)
    {
        var itens = p.Itens.Count;
        var resumo = itens == 1 ? "1 item" : $"{itens} itens";

        var execucao = p.Situacao switch
        {
            SituacaoPrescricao.Rascunho => "ainda não assinada",
            SituacaoPrescricao.Cancelada => "cancelada",
            _ => $"{p.Realizados} realizados · {p.NaoRealizados} não realizados · {p.Pendentes} aguardando"
        };

        return new LinhaPrescricaoInterna
        {
            PrescricaoId = p.Id,
            Numero = p.Numero,
            Data = $"{p.Data:dd/MM/yyyy} às {p.Hora:HH\\:mm}",
            Situacao = RotulosEnum.De(p.Situacao),
            Resumo = resumo,
            Execucao = execucao,
            Codigo = p.CodigoVerificacao,
            Cancelada = p.Cancelada,
            TemAssinatura = p.AssinaturaDoPrescritor is not null,
            TemRegistroExecucao = p.Realizados + p.NaoRealizados > 0,
            PodeCancelar = p.Situacao is SituacaoPrescricao.Rascunho or SituacaoPrescricao.Assinada
        };
    }
}

/// <summary>
/// PRESCRIÇÃO DE INFUSÃO no consultório (parcela 42) — a folha multi-item que a equipe
/// executa aqui dentro, com checagem de enfermagem.
///
/// Por que não é a tela de "Prescrições" ao lado
/// ---------------------------------------------
/// Aquela emite os quatro papéis que o paciente LEVA (receita, atestado, comparecimento,
/// pedido de exame). Esta é a folha que FICA: destinada ao próprio consultório, executada
/// pela enfermagem e checada item a item. São fluxos diferentes e documentos diferentes —
/// juntá-los num seletor de tipo faria a decisão mais frequente do dia (dar um atestado)
/// dividir espaço com a mais rara.
///
/// O paciente já vem escolhido, como no resto do Consultório: quem prescreve acabou de
/// atender. A busca fica como atalho.
/// </summary>
public sealed partial class PrescricaoInfusaoViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly PacienteEmFoco _foco;
    private readonly ISnackbarService _snackbar;
    private readonly IDialogoService _dialogo;

    public SeletorPacienteViewModel Seletor { get; }

    public ObservableCollection<LinhaPrescricaoInterna> Prescricoes { get; } = [];

    [ObservableProperty] private bool _carregando;
    [ObservableProperty] private bool _naoVerificado;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;
    [ObservableProperty] private bool _semPaciente = true;
    [ObservableProperty] private string _paciente = string.Empty;

    private int _pacienteId;

    public bool TemPaciente => !SemPaciente;

    /// <summary>Metade visível da permissão; a que impede é o <c>Exigir</c> no comando.</summary>
    public bool PodePrescrever => SessaoUsuario.Atual.Pode(Permissao.Prescrever);

    /// <summary>
    /// Sem paciente escolhido não há o que prescrever, e o botão diz isso apagado — a tela
    /// abre pela sidebar sem ninguém em foco, e botão aceso que não faz nada faz quem clica
    /// concluir que o sistema quebrou.
    /// </summary>
    public bool PodeCriarPrescricao => TemPaciente && PodePrescrever;

    partial void OnSemPacienteChanged(bool value)
    {
        OnPropertyChanged(nameof(TemPaciente));
        OnPropertyChanged(nameof(PodeCriarPrescricao));
    }

    public PrescricaoInfusaoViewModel(
        IServiceScopeFactory escopos, PacienteEmFoco foco,
        ISnackbarService snackbar, IDialogoService dialogo)
    {
        _escopos = escopos;
        _foco = foco;
        _snackbar = snackbar;
        _dialogo = dialogo;

        Seletor = new SeletorPacienteViewModel(escopos);
        Seletor.SelecaoMudou += paciente =>
        {
            if (paciente is null) return;

            _foco.Definir(paciente.Id, paciente.Nome);
            _pacienteId = paciente.Id;
            Paciente = paciente.Nome;
            _ = CarregarAsync();
        };

        if (_foco.PacienteId is { } id)
        {
            _pacienteId = id;
            Paciente = _foco.Nome;
        }

        _ = CarregarAsync();
    }

    [RelayCommand]
    public async Task CarregarAsync()
    {
        SemPaciente = _pacienteId == 0;
        Prescricoes.Clear();

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

            using var scope = _escopos.CreateScope();
            var servico = scope.ServiceProvider.GetRequiredService<PrescricaoInternaService>();

            foreach (var prescricao in await servico.DoPacienteAsync(_pacienteId))
                Prescricoes.Add(LinhaPrescricaoInterna.De(prescricao));
        }
        catch (Exception ex)
        {
            NaoVerificado = true;
            Application.Diagnostico.Registrar(
                "Consultório — prescrições de infusão não puderam ser carregadas", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
        finally
        {
            Carregando = false;
        }
    }

    [RelayCommand]
    private async Task NovaAsync()
    {
        if (_pacienteId == 0)
        {
            Mensagem = "Escolha um paciente antes de prescrever: a tela abre no paciente "
                     + "que você está atendendo, ou use a busca ao lado.";
            MensagemEhErro = true;
            return;
        }

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.Prescrever, "prescrever");

            var vm = new PrescricaoInternaEdicaoViewModel(
                _escopos, _dialogo, _pacienteId, Paciente,
                SessaoUsuario.Atual.ProfissionalId);

            var janela = new PrescricaoInternaWindow(vm)
            {
                Owner = System.Windows.Application.Current?.MainWindow
            };
            janela.ShowDialog();

            // Recarrega dos dois jeitos: fechar sem assinar não significa que nada
            // aconteceu — o rascunho pode ter sido criado e salvo.
            await CarregarAsync();

            if (janela.Assinou)
                _snackbar.Sucesso("Prescrição assinada e enviada à sala de infusão.");
        }
        catch (Exception ex)
        {
            Application.Diagnostico.Registrar(
                "Consultório — prescrição de infusão não pôde ser criada", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    /// <summary>Abre a folha para acompanhar a execução (e checar, se tiver permissão).</summary>
    [RelayCommand]
    private async Task AbrirAsync(LinhaPrescricaoInterna? linha)
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

    /// <summary>
    /// Imprime a folha. Quando há assinatura eletrônica, sai o ARQUIVO GUARDADO — a
    /// assinatura cobre bytes, e um PDF "igual" regerado agora abriria como inválido.
    /// </summary>
    [RelayCommand]
    private async Task ImprimirAsync(LinhaPrescricaoInterna? linha)
    {
        if (linha is null) return;
        await ImprimirFolhaAsync(linha, FolhaPrescricao.Prescricao);
    }

    [RelayCommand]
    private async Task ImprimirExecucaoAsync(LinhaPrescricaoInterna? linha)
    {
        if (linha is null) return;
        await ImprimirFolhaAsync(linha, FolhaPrescricao.RegistroExecucao);
    }

    private async Task ImprimirFolhaAsync(LinhaPrescricaoInterna linha, FolhaPrescricao folhaPedida)
    {
        try
        {
            FolhaAssinada folha;
            using (var scope = _escopos.CreateScope())
            {
                var assinaturas = scope.ServiceProvider
                    .GetRequiredService<AssinaturaDePrescricaoService>();
                folha = await assinaturas.FolhaAsync(linha.PrescricaoId, folhaPedida);
            }

            var erro = await ImpressaoPdf.SalvarEAbrirAsync(
                folha.Pdf, ImpressaoPdf.NomeSeguro(folha.NomeArquivo));

            // A conferência da assinatura é DITA, nas três respostas possíveis: íntegra,
            // alterada, ou não foi possível conferir. Abrir o PDF em silêncio faria o
            // terceiro caso passar por sucesso.
            Mensagem = erro ?? folha.Conferencia?.Frase;
            MensagemEhErro = erro is not null || folha.Conferencia is { Integra: false };
        }
        catch (Exception ex)
        {
            Application.Diagnostico.Registrar(
                "Consultório — folha de infusão não pôde ser impressa", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    [RelayCommand]
    private async Task CancelarAsync(LinhaPrescricaoInterna? linha)
    {
        if (linha is null || !linha.PodeCancelar) return;

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.Prescrever, "prescrever");

            var motivo = _dialogo.PerguntarTexto(
                "Cancelar prescrição",
                $"Por que a prescrição {linha.Numero} está sendo cancelada? Ela continua na "
                + "lista, marcada — a folha impressa não desaparece por ser apagada do sistema.");
            if (string.IsNullOrWhiteSpace(motivo)) return;

            using var scope = _escopos.CreateScope();
            var servico = scope.ServiceProvider.GetRequiredService<PrescricaoInternaService>();
            await servico.CancelarAsync(linha.PrescricaoId, motivo, SessaoUsuario.Atual.Operador);

            _snackbar.Info("Prescrição cancelada.");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Application.Diagnostico.Registrar(
                "Consultório — prescrição de infusão não pôde ser cancelada", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }
}
