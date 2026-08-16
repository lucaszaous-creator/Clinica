using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Clinica.Clinico.ViewModels;
using Clinica.Domain.Entities;

namespace Clinica.Clinico.Views;

/// <summary>
/// Abertura do consultório: a agenda do profissional e o que ficou sem registro.
///
/// A View liga e desliga a releitura periódica do quadro (parcela 38). Quem faz o
/// check-in é o balcão, e sem ela "Chamar próximo" continuaria dizendo "ninguém
/// aguardando" com o paciente já sentado na sala de espera. Ligar aqui, e não no
/// ViewModel, é o que impede um <c>DispatcherTimer</c> de manter vivo cada tela já
/// trocada — o shell constrói uma nova a cada navegação.
///
/// O ARRASTAR mora aqui, em código e não em XAML, pela razão da fila do balcão
/// (parcela 58): binding para dentro de um <c>Popup</c> falha em RUNTIME, calado — a
/// categoria que nenhuma das três redes locais pega. Em código, o compilar-sombra pega.
/// </summary>
public partial class MeuDiaView : UserControl
{
    /// <summary>Onde o botão foi pressionado — o arrasto só começa depois de ANDAR.</summary>
    private Point _inicioArraste;

    /// <summary>Cartão sob o cursor quando o botão desceu. Nulo = nada a arrastar.</summary>
    private LinhaSessao? _candidato;

    public MeuDiaView()
    {
        InitializeComponent();

        Loaded += (_, _) => (DataContext as MeuDiaViewModel)?.IniciarRelogio();
        Unloaded += (_, _) => (DataContext as MeuDiaViewModel)?.PararRelogio();
    }

    // ==================== Arrastar o cartão entre as raias ====================

    /// <summary>
    /// Anota o ponto de partida. NÃO marca o evento como tratado: se marcasse, o clique
    /// nunca chegaria aos botões do cartão, e o quadro perderia a metade que funciona
    /// para quem não arrasta nada.
    /// </summary>
    private void AoPressionarCartao(object sender, MouseButtonEventArgs e)
    {
        // O arrasto é porta de escrita como os botões: quem não pode movimentar a fila não
        // começa a arrastar. Os comandos de etapa já recusariam, mas deixar o cartão
        // "pegável" é a barreira invisível da parcela 41 em forma de gesto — e o quadro do
        // balcão já fazia esta conferência (FilaView.AoPressionarCartao).
        if (DataContext is MeuDiaViewModel vm && !vm.PodeMovimentarFila) return;

        _inicioArraste = e.GetPosition(null);
        _candidato = (sender as FrameworkElement)?.DataContext as LinhaSessao;
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
            Clinica.Application.Diagnostico.Registrar("Consultório — arrasto no quadro falhou", ex);
        }
    }

    private void AoArrastarSobreRaia(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(LinhaSessao))
            ? DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    /// <summary>
    /// A raia acende ao receber o cartão. Alvo de soltar sem realce é alvo que a pessoa
    /// erra — e errar aqui manda o paciente para a coluna do lado.
    /// </summary>
    private void AoEntrarNaRaia(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(LinhaSessao))) return;
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

        if (e.Data.GetData(typeof(LinhaSessao)) is not LinhaSessao cartao) return;
        if (DataContext is not MeuDiaViewModel vm) return;

        try
        {
            await vm.MoverParaAsync(cartao, alvo);
        }
        catch (Exception ex)
        {
            // `async void` de handler não tem quem pegue a exceção: sem isto, um erro
            // aqui derruba o app inteiro pela rede de última instância.
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — cartão solto no quadro não pôde ser movido", ex);
        }
    }
}
