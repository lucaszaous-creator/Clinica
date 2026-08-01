using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Clinica.Desktop.Controls;

/// <summary>
/// Movimento de ENTRADA: a seção aparece subindo alguns pixels, em vez de estar
/// simplesmente lá.
///
/// O design system já tinha movimento, mas só de microinteração (`Duracao.Rapida` e
/// `Duracao.Normal`, ≤150 ms): hover de botão, chevron que gira, sidebar que recolhe —
/// resposta ao dedo de quem clicou. Não havia nada para a outra pergunta, que é de
/// ORIENTAÇÃO: quando a tela troca inteira, o olho perde o ponto de leitura e tem de
/// procurar de novo onde as coisas ficaram. Um deslocamento curto na direção certa
/// devolve esse ponto de graça.
///
/// Três decisões que valem a pena escrever:
///
/// 1. **Entrada não é microinteração, e por isso não cabe em 150 ms.** O teto de 150 ms
///    existe porque resposta ao clique precisa parecer instantânea. Aqui o movimento
///    está CONTANDO alguma coisa (de onde a seção veio), e abaixo de ~200 ms ele não é
///    percebido como movimento — é percebido como um piscar, que é pior que nada.
///    Por outro lado, acima de ~300 ms ele passa a atrasar quem já sabe o que quer, e o
///    balcão sabe: o teto aqui é o começo do incômodo, não o começo da elegância.
/// 2. **Quem desligou animação no Windows não vê animação nenhuma.**
///    <see cref="SystemParameters.ClientAreaAnimation"/> é a preferência do sistema
///    operacional, e ignorá-la seria decidir pelo usuário sobre uma coisa que ele já
///    decidiu — inclusive por enjoo de movimento, que é motivo médico.
/// 3. **A animação se REMOVE quando termina.** Propriedade animada em WPF continua sob
///    controle da animação para sempre, e um binding que escrevesse ali depois seria
///    silenciosamente ignorado — o clássico "mudei a propriedade e a tela não mexeu".
///    No fim, o valor local é fixado e a animação sai da frente.
///
/// Uso: <c>ctrl:Movimento.Entrada="True"</c> e, para escalonar irmãos,
/// <c>ctrl:Movimento.Atraso="60"</c> (em ms).
///
/// ⚠️ **Não use dentro do ItemTemplate de lista virtualizada nem de lista que recarrega
/// sozinha.** O contêiner é reciclado e o `Loaded` volta a disparar: o quadro da fila,
/// que volta ao banco a cada 2 minutos, piscaria inteiro na cara de quem está no balcão.
/// O lugar disto é a SEÇÃO — o cartão, a coluna, o painel.
/// </summary>
public static class Movimento
{
    /// <summary>
    /// Duração da entrada, em ms. Ver decisão 1 no resumo da classe: acima do teto das
    /// microinterações de propósito, e ainda assim curta.
    /// </summary>
    public const int DuracaoEntradaMs = 220;

    /// <summary>De quantos pixels a seção sobe. Curto: é uma pista, não uma viagem.</summary>
    public const double DeslocamentoEntrada = 10;

    // ==================== Entrada ====================

    public static readonly DependencyProperty EntradaProperty =
        DependencyProperty.RegisterAttached(
            "Entrada", typeof(bool), typeof(Movimento),
            new PropertyMetadata(false, AoMudarEntrada));

    public static void SetEntrada(DependencyObject alvo, bool valor)
        => alvo.SetValue(EntradaProperty, valor);

    public static bool GetEntrada(DependencyObject alvo)
        => (bool)alvo.GetValue(EntradaProperty);

    // ==================== Atraso (escalonamento) ====================

    /// <summary>
    /// Atraso da entrada, em ms. É o que transforma quatro seções que aparecem juntas
    /// numa leitura da esquerda para a direita — o olho segue a ordem em que a tela quer
    /// ser lida. Passar de ~150 ms no último irmão faz a tela parecer lenta.
    /// </summary>
    public static readonly DependencyProperty AtrasoProperty =
        DependencyProperty.RegisterAttached(
            "Atraso", typeof(int), typeof(Movimento), new PropertyMetadata(0));

    public static void SetAtraso(DependencyObject alvo, int valor)
        => alvo.SetValue(AtrasoProperty, valor);

    public static int GetAtraso(DependencyObject alvo)
        => (int)alvo.GetValue(AtrasoProperty);

    // ==================== Implementação ====================

    /// <summary>Elementos que já entraram — a entrada é de uma vez só, por elemento.</summary>
    private static readonly DependencyProperty JaEntrouProperty =
        DependencyProperty.RegisterAttached(
            "JaEntrou", typeof(bool), typeof(Movimento), new PropertyMetadata(false));

    private static void AoMudarEntrada(DependencyObject alvo, DependencyPropertyChangedEventArgs e)
    {
        if (alvo is not FrameworkElement elemento) return;

        if (e.NewValue is not true)
        {
            elemento.Loaded -= AoCarregar;
            return;
        }

        // Já está na árvore (o XAML pode ser aplicado depois do Loaded): anima agora.
        if (elemento.IsLoaded) Animar(elemento);
        else elemento.Loaded += AoCarregar;
    }

    private static void AoCarregar(object remetente, RoutedEventArgs e)
    {
        if (remetente is FrameworkElement elemento) Animar(elemento);
    }

    private static void Animar(FrameworkElement elemento)
    {
        // O sistema operacional já respondeu por quem prefere a tela parada.
        if (!SystemParameters.ClientAreaAnimation) return;

        // Uma vez por elemento: contêiner reciclado por virtualização dispara Loaded de
        // novo, e a lista inteira piscaria a cada rolagem.
        if ((bool)elemento.GetValue(JaEntrouProperty)) return;
        elemento.SetValue(JaEntrouProperty, true);

        var suavizacao = new CubicEase { EasingMode = EasingMode.EaseOut };
        var atraso = TimeSpan.FromMilliseconds(Math.Max(0, GetAtraso(elemento)));
        var duracao = new Duration(TimeSpan.FromMilliseconds(DuracaoEntradaMs));

        var surgir = new DoubleAnimation(0, 1, duracao)
        {
            BeginTime = atraso,
            EasingFunction = suavizacao
        };
        surgir.Completed += (_, _) =>
        {
            // Solta a propriedade: animada para sempre, ela ignoraria em silêncio
            // qualquer binding que escrevesse ali depois.
            elemento.BeginAnimation(UIElement.OpacityProperty, null);
            elemento.Opacity = 1;
        };

        // Elemento que já tem transformação própria (um giro, uma escala) só recebe o
        // fade: sobrescrever a transformação dele quebraria o que já estava desenhado.
        if (elemento.RenderTransform is null or MatrixTransform { Matrix.IsIdentity: true })
        {
            var subida = new TranslateTransform(0, DeslocamentoEntrada);
            elemento.RenderTransform = subida;

            var subir = new DoubleAnimation(DeslocamentoEntrada, 0, duracao)
            {
                BeginTime = atraso,
                EasingFunction = suavizacao
            };
            subir.Completed += (_, _) =>
            {
                subida.BeginAnimation(TranslateTransform.YProperty, null);
                subida.Y = 0;
            };

            subida.BeginAnimation(TranslateTransform.YProperty, subir);
        }

        elemento.Opacity = 0;
        elemento.BeginAnimation(UIElement.OpacityProperty, surgir);
    }
}
