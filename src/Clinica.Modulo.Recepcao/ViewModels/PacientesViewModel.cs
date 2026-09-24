using Clinica.Desktop.Shell.Componentes.Cadastro;
using Clinica.Desktop.Shell.Modulos;
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

    private readonly IFabricaFichaPaciente _fabrica;
    [ObservableProperty] private object? _fichaUnica;
    public IReadOnlyList<string> Filtros { get; } = SessaoUsuario.Atual.Pode(Permissao.VerProntuario)
        ? new[] { "Todos", "Em tratamento", "Cadastro incompleto" }
        : new[] { "Todos", "Cadastro incompleto" };
    [ObservableProperty] private int _filtro;
    partial void OnFiltroChanged(int value) { if (Seletor is not null) { if (string.IsNullOrWhiteSpace(Seletor.Termo)) Seletor.DesligarSugestaoCommand.Execute(null); else _ = Seletor.BuscarAsync(imediato: true); } }
    private async Task<IReadOnlyList<Paciente>> BuscarListaAsync(string? termo, CancellationToken ct)
    {
        using var scope = _escopos.CreateScope();
        var pacientes = await scope.ServiceProvider.GetRequiredService<PacienteService>().BuscarAsync(termo, null, ct);
        var filtro = Filtros[Math.Clamp(Filtro, 0, Filtros.Count - 1)];
        if (filtro == "Cadastro incompleto") return pacientes.Where(p => string.IsNullOrWhiteSpace(p.Documento) || string.IsNullOrWhiteSpace(p.Endereco)).ToList();
        if (filtro != "Em tratamento") return pacientes;
        SessaoUsuario.Atual.Exigir(Permissao.VerProntuario, "consultar pacientes em tratamento");
        var sessoes = await scope.ServiceProvider.GetRequiredService<Clinica.Application.Abstracoes.IClinicaRepositorio>()
            .SessoesDosPacientesAsync(pacientes.Select(p => p.Id).ToList(), ct);
        return pacientes.Where(p => sessoes.TryGetValue(p.Id, out var resumo) && resumo.Sessoes > 0).ToList();
    }
    public int SecaoInicial { get; set; } = 2;
    public bool MostrarVoltar { get; init; } = true;

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
        IServiceScopeFactory escopos, ISnackbarService snackbar, IDialogoService dialogo, IFabricaFichaPaciente fabrica)
    {
        _escopos = escopos;
        _snackbar = snackbar;
        _fabrica = fabrica;

        // Listagem: sem corte, é ela que precisa mostrar todo mundo QUANDO alguém pedir.
        //
        // ⚠️ `SemBuscaInicial` (set/2026 — o cliente: "a tela de pacientes ainda carrega
        // todos os pacientes"). Esta era a consulta mais cara do sistema: `limite: null`
        // com o termo VAZIO traz o cadastro INTEIRO — 2.284 fichas — a cada abertura da
        // tela, num banco remoto.
        //
        // Eu tinha deixado esta tela de fora com o argumento de que "numa tela de LISTAGEM
        // a lista É a resposta". O argumento continua valendo para o que a tela OFERECE, e
        // não para o que ela faz sozinha: a listagem completa continua a um clique
        // ("Ver todos"), e é justamente por ser cara que ela precisa ser PEDIDA.
        Seletor = new SeletorPacienteViewModel(escopos, limite: null) { SemBuscaInicial = true, ConsultaPersonalizada = BuscarListaAsync };
        Seletor.SelecaoMudou += AoTrocarPaciente;
        Seletor.Atualizou += AtualizarResumo;


        AtualizarResumo();
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
        var foco = new PacienteEmFoco();
        foco.Definir(paciente.Id, paciente.Nome);
        FichaUnica = _fabrica.Criar(foco, Voltar, SecaoInicial);
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
        FichaUnica = null;
        Seletor.Selecionado = null;
        if (!Seletor.Ocioso) _ = Seletor.BuscarAsync(imediato: true);
    }

    /// <summary>
    /// ⚠️ O OCIOSO responde primeiro, e é a correção que anda junto de `SemBuscaInicial`:
    /// "Nenhum paciente encontrado" numa tela recém-aberta seria uma afirmação FALSA sobre
    /// uma clínica de 2.284 fichas — e é a leitura que leva a cadastrar de novo quem já
    /// tem ficha, partindo o histórico em dois (parcela 57).
    /// </summary>
    private void AtualizarResumo()
    {
        Resumo = Seletor.Ocioso
            ? "Busque por nome ou CPF — ou veja todos os pacientes."
            : Seletor.Resultados.Count switch
            {
                0 => "Nenhum paciente encontrado.",
                1 => "1 paciente.",
                var n => $"{n} pacientes."
            };

        OnPropertyChanged(nameof(ListandoTudo));
    }

    /// <summary>
    /// A lista completa está no ar. É o que apaga o botão "Ver todos" quando ele já foi
    /// clicado: botão que continua aceso depois de fazer o que faz manda clicar de novo.
    /// </summary>
    public bool ListandoTudo => Seletor.ListandoTodos;

    [RelayCommand]
    private void NovoPaciente()
    {
        SessaoUsuario.Atual.Exigir(Permissao.EditarPaciente, "cadastrar paciente");

        var vm = new CadastroPacienteViewModel(_escopos);
        var janela = new CadastroPacienteWindow(vm)
        {
            Owner = JanelaDona.Atual()
        };

        if (janela.ShowDialog() != true) return;

        _snackbar.Sucesso("Paciente cadastrado.");

        // ⚠️ Busca pelo NOME de quem acabou de ser cadastrado, e não "recarrega a lista".
        //
        // Com a tela OCIOSA (set/2026) recarregar não faria nada — `BuscarAsync` sai cedo
        // sem consultar —, e a pessoa veria "Paciente cadastrado." com a tela em branco,
        // que se lê como "não salvou". Buscar pelo nome sai do ocioso E mostra exatamente
        // a ficha nova, em vez de trazer as 2.284 para ela se perder no meio.
        Seletor.Termo = vm.Nome;
    }
}
