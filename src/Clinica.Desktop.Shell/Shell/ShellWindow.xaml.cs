using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Clinica.Desktop.Shell.Modulos;

namespace Clinica.Desktop.Shell;

/// <summary>
/// Janela genérica da suíte: rail de categorias montado a partir dos módulos carregados,
/// painel da categoria escolhida e área de conteúdo com a tela ativa. Não conhece nenhuma
/// tela em particular.
/// </summary>
public partial class ShellWindow : Window
{
    /// <summary>Abaixo desta largura o painel ancorado come a tela — ele solta sozinho.</summary>
    private const double LarguraSoltarPainel = 1100;

    /// <summary>
    /// Espera antes de ESPIAR uma categoria.
    ///
    /// Sem ela, atravessar o rail para chegar ao conteúdo abriria as quatro categorias em
    /// sequência, cada uma piscando por cima da tela. 180ms é curto o bastante para quem
    /// mirou não sentir atraso e longo o bastante para quem só passou não ver nada.
    /// </summary>
    private static readonly TimeSpan AtrasoParaEspiar = TimeSpan.FromMilliseconds(180);

    /// <summary>
    /// Espera antes de FECHAR o painel espiado.
    ///
    /// É a folga do "corredor": para ir do ícone até um item o mouse atravessa a borda
    /// entre os dois, e num percurso diagonal ele sai da zona por um instante. Fechar na
    /// hora é o que torna o flyout um alvo móvel — o defeito clássico deste modelo, e a
    /// razão de o clique FIXAR o painel.
    /// </summary>
    private static readonly TimeSpan AtrasoParaFechar = TimeSpan.FromMilliseconds(320);

    private readonly DispatcherTimer _paraEspiar;
    private readonly DispatcherTimer _paraFechar;
    private GrupoMenuModulo? _categoriaEspiada;

    public ShellWindow()
    {
        InitializeComponent();

        // Ctrl+F cai no campo de pesquisa. É atalho de janela e não de TextBox porque o
        // foco, na hora do atalho, está em qualquer lugar da tela ativa.
        InputBindings.Add(new KeyBinding(
            new RelayFoco(() => PesquisaGlobal.Focus()), Key.F, ModifierKeys.Control));

        _paraEspiar = new DispatcherTimer { Interval = AtrasoParaEspiar };
        _paraEspiar.Tick += (_, _) =>
        {
            _paraEspiar.Stop();
            if (DataContext is ShellViewModel vm) vm.EspiarCategoria(_categoriaEspiada);
        };

        _paraFechar = new DispatcherTimer { Interval = AtrasoParaFechar };
        _paraFechar.Tick += (_, _) =>
        {
            _paraFechar.Stop();
            if (DataContext is ShellViewModel { PainelFixado: false } vm) vm.EspiarCategoria(null);
        };

        SizeChanged += AoRedimensionar;
    }

    /// <summary>Mouse parou sobre um ícone do rail: arma o "espiar".</summary>
    private void AoEntrarNaCategoria(object sender, MouseEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: GrupoMenuModulo categoria }) return;
        if (DataContext is not ShellViewModel { PainelFixado: false }) return;

        _paraFechar.Stop();
        _categoriaEspiada = categoria;
        _paraEspiar.Stop();
        _paraEspiar.Start();
    }

    /// <summary>Mouse entrou no painel espiado: ele fica enquanto o mouse estiver ali.</summary>
    private void AoEntrarNoPainel(object sender, MouseEventArgs e) => _paraFechar.Stop();

    /// <summary>
    /// Mouse saiu do rail ou do painel: arma o fechamento, sem executá-lo.
    ///
    /// Armar em vez de fechar é o que dá a folga do corredor — se o mouse reaparecer na
    /// outra metade da zona dentro do prazo, o temporizador é cancelado e nada acontece.
    /// </summary>
    private void AoSairDaZonaDoMenu(object sender, MouseEventArgs e)
    {
        _paraEspiar.Stop();
        if (DataContext is not ShellViewModel { PainelFixado: false }) return;

        _paraFechar.Stop();
        _paraFechar.Start();
    }

    /// <summary>
    /// Em 1366×768 (o monitor típico do balcão) o painel ancorado tira 250px da tela. Ele
    /// solta sozinho quando a janela encolhe, e o Ctrl+B continua mandando — quem fixar de
    /// propósito não é contrariado enquanto não mexer na janela.
    /// </summary>
    private void AoRedimensionar(object sender, SizeChangedEventArgs e)
    {
        if (DataContext is not ShellViewModel vm) return;

        if (e.NewSize.Width < LarguraSoltarPainel && vm.PainelFixado)
        {
            vm.PainelFixado = false;
            vm.CategoriaAberta = null;
        }
    }

    /// <summary>Comando mínimo para ligar uma tecla a uma ação da própria janela.</summary>
    private sealed class RelayFoco(Action acao) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => acao();
    }
}
