using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Clinica.Domain.Entities;
using Clinica.Recepcao.ViewModels;

namespace Clinica.Recepcao.Views;

/// <summary>
/// Fila de hoje em kanban.
///
/// A View liga e desliga o relógio que envelhece os tempos de espera e relê o dia — é
/// por ele que a chamada feita no consultório chega ao balcão (parcela 38). O shell
/// constrói uma tela nova a cada navegação, e um <c>DispatcherTimer</c> rodando manteria
/// vivo cada ViewModel já trocado (e faria vários irem ao banco pela mesma coisa).
///
/// O ARRASTAR e o menu "⋯" moram aqui, em código, e não em XAML, de propósito: os dois
/// dependeriam de binding para dentro de um <c>Popup</c> (que não tem o UserControl como
/// ancestral) ou de truques com <c>PlacementTarget.Tag</c>, e binding que não resolve
/// falha em RUNTIME, calado — a categoria de defeito que nenhuma das três redes locais
/// pega. Em código, o <c>compilar-sombra</c> pega.
/// </summary>
public partial class FilaView : UserControl
{
    /// <summary>Onde o botão foi pressionado — o arrasto só começa depois de ANDAR.</summary>
    private Point _inicioArraste;

    /// <summary>Cartão sob o cursor quando o botão desceu. Nulo = nada a arrastar.</summary>
    private CartaoFila? _candidato;

    public FilaView()
    {
        InitializeComponent();

        Loaded += (_, _) => (DataContext as FilaViewModel)?.IniciarRelogio();
        Unloaded += (_, _) => (DataContext as FilaViewModel)?.PararRelogio();
    }

    // ==================== Arrastar o cartão entre as raias ====================

    /// <summary>
    /// Anota o ponto de partida. NÃO marca o evento como tratado: se marcasse, o clique
    /// nunca chegaria aos botões do cartão, e o quadro perderia a metade que funciona
    /// para quem não arrasta nada.
    /// </summary>
    private void AoPressionarCartao(object sender, MouseButtonEventArgs e)
    {
        // O arrasto é uma porta de escrita como os botões: quem não tem EditarAgenda
        // não começa a arrastar — os comandos de etapa já recusariam, mas deixar o
        // cartão "pegável" seria a barreira invisível da parcela 41 em forma de gesto.
        if (DataContext is FilaViewModel vm && !vm.PodeEditarAgenda) return;

        _inicioArraste = e.GetPosition(null);
        _candidato = (sender as FrameworkElement)?.DataContext as CartaoFila;
    }

    /// <summary>
    /// Começa o arrasto só depois do limiar do sistema. Sem ele, o tremor da mão em cima
    /// de um botão viraria um arrasto e o clique se perderia.
    /// </summary>
    private void AoMoverSobreCartao(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _candidato is null) return;

        var agora = e.GetPosition(null);
        if (Math.Abs(agora.X - _inicioArraste.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(agora.Y - _inicioArraste.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        var cartao = _candidato;
        _candidato = null;

        if (sender is not DependencyObject origem) return;

        try
        {
            DragDrop.DoDragDrop(origem, cartao, DragDropEffects.Move);
        }
        catch (Exception ex)
        {
            // Arrasto é conforto: se o Windows recusar a operação (área de transferência
            // ocupada, sessão remota), o quadro continua andando pelos botões.
            Clinica.Application.Diagnostico.Registrar("Recepção — arrasto na fila falhou", ex);
        }
    }

    private void AoArrastarSobreRaia(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(CartaoFila))
            ? DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    /// <summary>
    /// A raia acende ao receber o cartão. Alvo de soltar sem realce é alvo que a pessoa
    /// erra — e num quadro de cinco colunas errar significa mandar o paciente para a
    /// coluna do lado.
    /// </summary>
    private void AoEntrarNaRaia(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(CartaoFila))) return;
        if (sender is Border raia) raia.Background = (Brush)FindResource("Brush.Acento.Suave");
    }

    /// <summary>
    /// Apaga o realce devolvendo a propriedade ao ESTILO (<c>ClearValue</c>), nunca
    /// regravando a cor à mão: um valor local a mais aqui é o que faz a raia parar de
    /// seguir o design system na próxima troca de tema.
    /// </summary>
    private void AoSairDaRaia(object sender, DragEventArgs e)
    {
        if (sender is Border raia) raia.ClearValue(BackgroundProperty);
    }

    private async void AoSoltarNaRaia(object sender, DragEventArgs e)
    {
        if (sender is Border raia) raia.ClearValue(BackgroundProperty);
        e.Handled = true;

        if (sender is not FrameworkElement destino
            || destino.Tag as string is not { } nome
            || !Enum.TryParse<EtapaFila>(nome, out var alvo)) return;

        if (e.Data.GetData(typeof(CartaoFila)) is not CartaoFila cartao) return;
        if (DataContext is not FilaViewModel vm) return;

        try
        {
            await vm.MoverParaAsync(cartao, alvo);
        }
        catch (Exception ex)
        {
            // `async void` de handler não tem quem pegue a exceção: sem isto, um erro
            // aqui derruba o app inteiro pela rede de última instância.
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — cartão solto na fila não pôde ser movido", ex);
        }
    }

    // ==================== O "⋯" do cartão ====================

    /// <summary>
    /// As ações de exceção: voltar etapa, falta e cancelamento.
    ///
    /// O menu é montado em código porque um <c>ContextMenu</c> declarado em XAML vive num
    /// <c>Popup</c>, fora da árvore visual desta tela — os comandos precisariam de
    /// <c>PlacementTarget.Tag</c> para chegar ao ViewModel, e binding que erra o caminho
    /// falha calado na hora do clique.
    /// </summary>
    private void AoAbrirMenuDoCartao(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement botao) return;
        if (botao.DataContext is not CartaoFila cartao) return;
        if (DataContext is not FilaViewModel vm) return;

        var menu = new ContextMenu
        {
            PlacementTarget = botao,
            Placement = PlacementMode.Bottom
        };

        void Acrescentar(string rotulo, System.Windows.Input.ICommand comando, bool visivel)
        {
            if (!visivel) return;
            var item = new MenuItem { Header = rotulo, Command = comando, CommandParameter = cartao };
            menu.Items.Add(item);
        }

        // A PORTA do termo (parcela 66). Vem primeira quando há termo pendente: o alerta do
        // check-in diz que falta assinar, e alerta sem porta no mesmo app é pior que alerta
        // nenhum — ele ensina a pessoa a ignorá-lo (a lição da parcela 48).
        // ⚠️ A visibilidade é ESTADO **e** PERMISSÃO. O bloco não segue um `IsEnabled` só
        // porque os três atos pedem bits diferentes — colher o termo é
        // `ColherAssinaturaPaciente` (a técnica de enfermagem o tem e não tem o da
        // agenda), mover a fila é `EditarAgenda` OU `MovimentarFila`, e falta/cancelamento
        // são do balcão, `EditarAgenda` estrito. Sem esta metade, o item aparecia aceso e
        // a recusa só chegava depois do clique.
        Acrescentar("Colher o termo do procedimento…", vm.ColherTermoCommand,
            cartao.TemTermoPendente && vm.PodeColherTermo);

        if (menu.Items.Count > 0) menu.Items.Add(new Separator());

        Acrescentar("Voltar uma etapa", vm.VoltarEtapaCommand,
            cartao.PodeVoltar && vm.PodeEditarAgenda);
        Acrescentar("Entrou (pular a chamada)", vm.IniciarAtendimentoCommand,
            cartao.PodeIniciar && cartao.Etapa != EtapaFila.Chamado && vm.PodeEditarAgenda);

        if (menu.Items.Count > 0 && cartao.EmAberto && vm.PodeConcluirSessao)
            menu.Items.Add(new Separator());

        Acrescentar("Marcar falta", vm.MarcarFaltaCommand,
            cartao.EmAberto && vm.PodeConcluirSessao);
        Acrescentar("Cancelar o horário", vm.CancelarCommand,
            cartao.EmAberto && vm.PodeConcluirSessao);

        if (menu.Items.Count == 0)
        {
            // Menu vazio que abre e fecha é o "botão que não faz nada" da parcela 41.
            menu.Items.Add(new MenuItem
            {
                Header = "Nada a fazer com este cartão",
                IsEnabled = false
            });
        }

        menu.IsOpen = true;
    }
}
