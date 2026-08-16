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

    /// <summary>Rascunho se edita — é o que <c>PrescricaoInterna.PodeEditar</c> já dizia.</summary>
    public required bool PodeEditar { get; init; }

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
            // O estado da linha COMPÕE com a permissão (parcela 61): sem o bit, o botão
            // ficava aceso e o clique estourava no Exigir — botão aceso que só explode é
            // o defeito da parcela 41. O Exigir do comando continua sendo a barreira que
            // impede; esta é a metade que explica.
            PodeCancelar = (p.Situacao is SituacaoPrescricao.Rascunho or SituacaoPrescricao.Assinada)
                           && SessaoUsuario.Atual.Pode(Permissao.Prescrever),
            PodeEditar = p.PodeEditar && SessaoUsuario.Atual.Pode(Permissao.Prescrever)
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

    /// <summary>
    /// Descarte de resposta fora de ordem (parcela 50): a troca de paciente dispara uma
    /// carga por seleção, e a resposta atrasada do paciente anterior chegando por último
    /// exibiria a folha de infusão dele sob o nome do paciente novo.
    /// </summary>
    private int _geracaoCarga;

    /// <summary>Último paciente cujo acesso já entrou na trilha — a tela recarrega mais do que troca.</summary>
    private int _acessoRegistradoDe;

    [RelayCommand]
    public async Task CarregarAsync()
    {
        var geracao = ++_geracaoCarga;

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

            // Trilha de LEITURA (parcela 52), na troca de paciente: a folha de infusão é
            // prescrição — dado de saúde —, e esta porta ficava fora da trilha.
            if (_acessoRegistradoDe != _pacienteId)
            {
                _acessoRegistradoDe = _pacienteId;
                await scope.ServiceProvider.GetRequiredService<AcessoProntuarioService>()
                    .RegistrarAsync(_pacienteId, SessaoUsuario.Atual.Operador,
                        OrigemAcessoProntuario.Documento);
            }

            var prescricoes = await servico.DoPacienteAsync(_pacienteId);

            // Chegou tarde: outra seleção já pediu uma carga mais nova.
            if (geracao != _geracaoCarga) return;

            foreach (var prescricao in prescricoes)
                Prescricoes.Add(LinhaPrescricaoInterna.De(prescricao));
        }
        catch (Exception ex)
        {
            if (geracao != _geracaoCarga) return;

            NaoVerificado = true;
            Application.Diagnostico.Registrar(
                "Consultório — prescrições de infusão não puderam ser carregadas", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
        finally
        {
            // A carga superada não apaga o "Carregando" da que ainda está no ar.
            if (geracao == _geracaoCarga) Carregando = false;
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
                Owner = JanelaDona.Atual()
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

    /// <summary>
    /// Reabre um RASCUNHO para continuar escrevendo.
    ///
    /// Faltava, e o efeito era o pior possível: salvar o rascunho e fechar a janela deixava
    /// a prescrição inalcançável. "Abrir" leva à folha de EXECUÇÃO — a tela da enfermagem —,
    /// então quem tentasse voltar para corrigir a dose achava um quadro de checagem e
    /// concluía que teria de começar de novo.
    /// </summary>
    [RelayCommand]
    private async Task EditarAsync(LinhaPrescricaoInterna? linha)
    {
        if (linha is null) return;

        if (!linha.PodeEditar)
        {
            // A guarda DIZ por que não dá, em vez de voltar calada.
            Mensagem = $"A prescrição {linha.Numero} já foi assinada e não pode mais ser "
                     + "editada. Cancele e emita outra, se for o caso.";
            MensagemEhErro = true;
            return;
        }

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.Prescrever, "editar prescrição");

            var vm = new PrescricaoInternaEdicaoViewModel(
                _escopos, _dialogo, _pacienteId, Paciente,
                SessaoUsuario.Atual.ProfissionalId, prescricaoId: linha.PrescricaoId);

            var janela = new PrescricaoInternaWindow(vm)
            {
                Owner = JanelaDona.Atual()
            };
            janela.ShowDialog();

            await CarregarAsync();

            if (janela.Assinou)
                _snackbar.Sucesso("Prescrição assinada e enviada à sala de infusão.");
        }
        catch (Exception ex)
        {
            Application.Diagnostico.Registrar(
                "Consultório — rascunho de prescrição não pôde ser reaberto", ex);
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
            Owner = JanelaDona.Atual()
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
