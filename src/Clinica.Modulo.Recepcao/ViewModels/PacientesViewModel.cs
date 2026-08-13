using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Recepcao.ViewModels;

/// <summary>
/// Pacientes na Recepção: LISTA de largura inteira, e a ficha do paciente atrás de um
/// clique.
///
/// O que mudou, e por quê (parcela 47)
/// -----------------------------------
/// Até aqui era mestre-detalhe numa tela só: uma coluna de 360 px com a lista, grudada à
/// esquerda para sempre, e a ficha à direita como uma PILHA de oito cartões com moldura.
/// As duas metades são o defeito que o `README.md` proíbe em letras maiúsculas — a faixa
/// lateral permanente ("metade da largura útil gasta com a mesma lista") e a colcha de
/// retalhos ("cinco molduras lado a lado leem-se como cinco telas costuradas"). A regra
/// existe porque o cliente reprovou isso em voz alta, e a saída prescrita é literalmente
/// esta: <b>tela de LISTA (largura inteira) → tela do ITEM atrás de um clique, com abas</b>.
///
/// O padrão não foi inventado aqui: é o mesmo <c>PacienteWorkspaceView</c> que o
/// Consultório usa desde a parcela 37 — cabeçalho da pessoa uma vez, e as seções em abas.
/// Ter dois desenhos diferentes para "ficha do paciente" no mesmo sistema é o que faz a
/// recepcionista achar que abriu outro programa.
///
/// A busca NÃO é reescrita aqui: usa o <see cref="SeletorPacienteViewModel"/> da suíte,
/// que já resolve limite no SQL, agrupamento das teclas e descarte de resposta fora de
/// ordem. Como esta é a tela de LISTAGEM, ela o usa com <c>limite: null</c>.
/// </summary>
public sealed partial class PacientesViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly ISnackbarService _snackbar;

    public SeletorPacienteViewModel Seletor { get; }

    public FichaPacienteViewModel Ficha { get; }

    [ObservableProperty] private string _resumo = string.Empty;

    /// <summary>
    /// Estamos na ficha de alguém? É o que troca a LISTA pela tela do paciente.
    ///
    /// Mora no ViewModel e não numa navegação do shell porque as duas telas são a mesma
    /// seção da sidebar: "Pacientes" continua sendo um item só, e voltar da ficha não
    /// pode custar reencontrar o item no menu.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostrandoLista))]
    private bool _mostrandoFicha;

    /// <summary>Estamos na lista? O par existe porque XAML não nega booleano sem conversor.</summary>
    public bool MostrandoLista => !MostrandoFicha;

    /// <summary>
    /// Habilita os botões de escrita da tela. É a metade VISÍVEL da permissão: o
    /// botão apagado explica por que não dá; a guarda no comando é que impede.
    /// Só desabilitar seria enfeite — um atalho de teclado passaria direto.
    ///
    /// O nome casa com o do XAML e com o bit (EditarPaciente = CADASTRO, parcela 49).
    /// Com o nome antigo ("PodeEditarProntuario"), o binding do "Novo paciente" não
    /// resolvia, e IsEnabled ficava no padrão true — barreira visível morta em silêncio.
    /// </summary>
    public bool PodeEditarCadastro => SessaoUsuario.Atual.Pode(Permissao.EditarPaciente);

    public PacientesViewModel(
        IServiceScopeFactory escopos, ISnackbarService snackbar, IDialogoService dialogo)
    {
        _escopos = escopos;
        _snackbar = snackbar;

        // Listagem: sem corte, é ela que precisa mostrar todo mundo.
        Seletor = new SeletorPacienteViewModel(escopos, limite: null);
        Seletor.SelecaoMudou += AoTrocarPaciente;
        Seletor.Atualizou += AtualizarResumo;

        Ficha = new FichaPacienteViewModel(escopos, snackbar, dialogo);
        // Editar o cadastro muda o nome/foto que a lista mostra.
        Ficha.Alterou += () => _ = Seletor.BuscarAsync(imediato: true);

        _ = Seletor.BuscarAsync(imediato: true);
    }

    /// <summary>
    /// Escolher alguém na lista ABRE a ficha. A ficha carrega antes de a tela trocar
    /// (não se espera o await): o cabeçalho já tem nome e foto vindos da linha clicada, e
    /// segurar a troca até o banco responder faria o clique parecer engolido num banco
    /// remoto de quinze segundos.
    /// </summary>
    private void AoTrocarPaciente(Paciente? paciente)
    {
        if (paciente is null) return;

        MostrandoFicha = true;
        _ = Ficha.AbrirAsync(paciente.Id);
    }

    /// <summary>
    /// Volta para a lista. LIMPA a seleção de propósito: sem isso, clicar de novo no mesmo
    /// paciente não dispararia `SelecaoMudou` (o item já estava selecionado) e a ficha não
    /// reabriria — botão que funciona uma vez e depois não.
    /// </summary>
    [RelayCommand]
    private void Voltar()
    {
        MostrandoFicha = false;
        Seletor.Limpar();
    }

    private void AtualizarResumo()
        => Resumo = Seletor.Resultados.Count switch
        {
            0 => "Nenhum paciente encontrado.",
            1 => "1 paciente.",
            var n => $"{n} pacientes."
        };

    [RelayCommand]
    private async Task NovoPacienteAsync()
    {
        SessaoUsuario.Atual.Exigir(Permissao.EditarPaciente, "cadastrar paciente");

        var vm = new PacienteEdicaoViewModel(_escopos);
        var janela = new Janelas.PacienteWindow(vm)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        if (janela.ShowDialog() != true) return;

        _snackbar.Sucesso("Paciente cadastrado.");
        await Seletor.BuscarAsync(imediato: true);
    }

    [RelayCommand]
    private Task AtualizarAsync() => Seletor.BuscarAsync(imediato: true);
}
