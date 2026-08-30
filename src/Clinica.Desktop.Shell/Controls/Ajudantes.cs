using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Clinica.Desktop.Controls;

/// <summary>
/// Attached properties do design system:
/// - Placeholder: texto-fantasma em TextBox (consumido pelo template em Styles/Componentes/Campos.xaml);
/// - Icone: glifo (Segoe Fluent/MDL2) exibido por templates que suportam ícone;
/// - EstaCarregando: estado de loading em botões (spinner + desabilita o clique);
/// - SomenteNumeros: bloqueia entrada não numérica (digitação e colagem) em TextBox;
/// - RodaDaPagina: devolve a roda do mouse à PÁGINA quando o cursor está sobre uma lista
///   que rola por dentro.
/// </summary>
public static class Ajudantes
{
    public static readonly DependencyProperty PlaceholderProperty =
        DependencyProperty.RegisterAttached("Placeholder", typeof(string), typeof(Ajudantes),
            new PropertyMetadata(string.Empty));

    public static string GetPlaceholder(DependencyObject obj) => (string)obj.GetValue(PlaceholderProperty);
    public static void SetPlaceholder(DependencyObject obj, string value) => obj.SetValue(PlaceholderProperty, value);

    public static readonly DependencyProperty IconeProperty =
        DependencyProperty.RegisterAttached("Icone", typeof(string), typeof(Ajudantes),
            new PropertyMetadata(string.Empty));

    public static string GetIcone(DependencyObject obj) => (string)obj.GetValue(IconeProperty);
    public static void SetIcone(DependencyObject obj, string value) => obj.SetValue(IconeProperty, value);

    public static readonly DependencyProperty EstaCarregandoProperty =
        DependencyProperty.RegisterAttached("EstaCarregando", typeof(bool), typeof(Ajudantes),
            new PropertyMetadata(false));

    public static bool GetEstaCarregando(DependencyObject obj) => (bool)obj.GetValue(EstaCarregandoProperty);
    public static void SetEstaCarregando(DependencyObject obj, bool value) => obj.SetValue(EstaCarregandoProperty, value);

    public static readonly DependencyProperty SomenteNumerosProperty =
        DependencyProperty.RegisterAttached("SomenteNumeros", typeof(bool), typeof(Ajudantes),
            new PropertyMetadata(false, OnSomenteNumerosChanged));

    public static bool GetSomenteNumeros(DependencyObject obj) => (bool)obj.GetValue(SomenteNumerosProperty);
    public static void SetSomenteNumeros(DependencyObject obj, bool value) => obj.SetValue(SomenteNumerosProperty, value);

    private static void OnSomenteNumerosChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox caixa) return;
        if ((bool)e.NewValue)
        {
            caixa.PreviewTextInput += BloquearNaoNumerico;
            DataObject.AddPastingHandler(caixa, BloquearColagemNaoNumerica);
            InputMethod.SetIsInputMethodEnabled(caixa, false);
        }
        else
        {
            caixa.PreviewTextInput -= BloquearNaoNumerico;
            DataObject.RemovePastingHandler(caixa, BloquearColagemNaoNumerica);
        }
    }

    private static void BloquearNaoNumerico(object sender, TextCompositionEventArgs e)
        => e.Handled = !e.Text.All(char.IsDigit);

    private static void BloquearColagemNaoNumerica(object sender, DataObjectPastingEventArgs e)
    {
        var texto = e.DataObject.GetData(DataFormats.Text) as string;
        if (texto is null || !texto.All(char.IsDigit))
            e.CancelCommand();
    }

    // ==================== RodaDaPagina ====================

    /// <summary>
    /// A roda do mouse chega à PÁGINA mesmo com o cursor sobre uma lista que rola por
    /// dentro.
    ///
    /// ⚠️ O DEFEITO QUE ISTO FECHA (parcela 90, achado pelo cliente no Novo atendimento).
    /// Um <see cref="ListBox"/> — ou qualquer controle com <c>ScrollViewer</c> no template —
    /// dentro de uma página que também rola COME a roda do mouse: o
    /// <c>ScrollViewer</c> interno marca o evento como tratado e ele nunca sobe para o de
    /// fora. Com a lista ocupando o meio da tela, ela vira o maior alvo do cursor, e a
    /// página simplesmente NÃO ROLA.
    ///
    /// O estrago não é "rola pouco": é a pessoa ver o conteúdo cortado embaixo, girar a
    /// roda, nada acontecer, e concluir que <b>a tela está quebrada</b>. Foi exatamente o
    /// relato — "toda cortada", e rolar não resolvia.
    ///
    /// ⚠️ E ele não aparece na máquina de quem programa: só corta quando a altura útil é
    /// menor que o conteúdo, que é o monitor de 1366×768 do balcão, ou qualquer tela com a
    /// escala do Windows em 125/150%.
    ///
    /// Compilador nenhum pega — o XAML é bem-formado, o binding é válido e nada lança;
    /// quem cobra é a <b>checagem 43</b> do <c>verificar-suite</c>, que acusa todo controle
    /// rolante dentro de página rolante sem este atributo.
    /// </summary>
    public static readonly DependencyProperty RodaDaPaginaProperty =
        DependencyProperty.RegisterAttached("RodaDaPagina", typeof(bool), typeof(Ajudantes),
            new PropertyMetadata(false, OnRodaDaPaginaChanged));

    public static bool GetRodaDaPagina(DependencyObject obj) => (bool)obj.GetValue(RodaDaPaginaProperty);
    public static void SetRodaDaPagina(DependencyObject obj, bool value) => obj.SetValue(RodaDaPaginaProperty, value);

    private static void OnRodaDaPaginaChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement elemento) return;

        if ((bool)e.NewValue) elemento.PreviewMouseWheel += DevolverRodaAPagina;
        else elemento.PreviewMouseWheel -= DevolverRodaAPagina;
    }

    /// <summary>
    /// Rola a página quando a lista interna já chegou ao fim naquela direção; senão deixa a
    /// lista rolar normalmente.
    ///
    /// ⚠️ A condição de BORDA é o que separa esta correção de um remendo: sem ela, a roda
    /// iria SEMPRE para a página e a lista deixaria de rolar — trocaríamos um defeito pelo
    /// oposto. Com ela, a lista rola até o fim e a página continua dali, que é o que
    /// qualquer pessoa espera de uma roda de mouse.
    /// </summary>
    private static void DevolverRodaAPagina(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled || sender is not DependencyObject origem) return;

        var interno = RolagemDe(origem);

        // Sem rolagem interna, ou já no limite da direção em que a pessoa girou: a roda é
        // da página. `e.Delta > 0` é girar para CIMA.
        var podeRolarDentro = interno is not null
            && interno.ScrollableHeight > 0
            && (e.Delta > 0 ? interno.VerticalOffset > 0
                            : interno.VerticalOffset < interno.ScrollableHeight);

        if (podeRolarDentro) return;

        if (PaginaDe(origem) is not { } pagina) return;

        e.Handled = true;
        pagina.ScrollToVerticalOffset(pagina.VerticalOffset - e.Delta);
    }

    /// <summary>O <c>ScrollViewer</c> que o próprio controle traz no template.</summary>
    private static ScrollViewer? RolagemDe(DependencyObject raiz)
    {
        if (raiz is ScrollViewer proprio) return proprio;

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(raiz); i++)
        {
            var filho = VisualTreeHelper.GetChild(raiz, i);
            if (filho is ScrollViewer achado) return achado;
            if (RolagemDe(filho) is { } fundo) return fundo;
        }

        return null;
    }

    /// <summary>O <c>ScrollViewer</c> da PÁGINA — o primeiro ancestral, pulando o próprio.</summary>
    private static ScrollViewer? PaginaDe(DependencyObject no)
    {
        for (var pai = VisualTreeHelper.GetParent(no); pai is not null; pai = VisualTreeHelper.GetParent(pai))
            if (pai is ScrollViewer pagina) return pagina;

        return null;
    }
}
