using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Clinica.Atualizacao;
using Clinica.Desktop.Shell.Configuracao;

namespace Clinica.Desktop.Shell;

/// <summary>
/// A TELA "ATUALIZANDO…" ANTES DO LOGIN (nov/2026 — "como nos jogos"). Só aparece para a versão
/// OBRIGATÓRIA (<see cref="AtualizacaoNaAbertura"/>): logo, a versão, a barra e uma frase. Sem botão — o
/// tempo limite (2 min) é da regra, que fecha a janela e libera o login sozinha.
///
/// <para><b>WPF nativo, de propósito.</b> Ela aparece ANTES de existir o navegador da janela do app
/// (<see cref="JanelaDoApp"/>): um WebView2 aqui traria de volta a janela preta que o CLAUDE.md conta
/// (a janela nativa do WebView2 nasce preta antes do navegador existir dentro dela). Uma janela WPF pinta o
/// fundo do token no primeiro quadro.</para>
///
/// <para><b>O ENCAIXE DA ANIMAÇÃO DE ABERTURA</b> (o squad de design desenha em paralelo): o
/// <see cref="Animacao"/> — um <see cref="ContentControl"/> de 120×120 acima da versão, hoje com o símbolo
/// estático. A animação entra como um <see cref="UIElement"/> WPF (Storyboard/XAML vetorial), atribuído em
/// <see cref="AnimacaoDeAbertura.Criar"/>. Regras para não atrasar a abertura: ela roda ENQUANTO o trabalho
/// acontece (nunca antes dele), começa no primeiro quadro, é interrompível a qualquer instante (a janela fecha
/// quando o download termina) e, se tiver introdução, a introdução cabe em ~1 s e o resto é laço.</para>
/// </summary>
public sealed class JanelaDeAtualizacao : Window, ITelaDeAtualizacao
{
    private readonly TextBlock _versao;
    private readonly TextBlock _frase;
    private readonly ProgressBar _barra;

    /// <summary>Onde a animação da logo entra (ver o resumo da classe).</summary>
    public ContentControl Animacao { get; }

    public JanelaDeAtualizacao(string nomeApp)
    {
        Title = $"{nomeApp} — atualizando";
        Width = 440;
        Height = 300;
        ResizeMode = ResizeMode.NoResize;
        WindowStyle = WindowStyle.None;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ShowInTaskbar = true;
        Topmost = false;
        UseLayoutRounding = true;
        SetResourceReference(BackgroundProperty, "Brush.Superficie");
        SetResourceReference(BorderBrushProperty, "Brush.Borda");
        BorderThickness = new Thickness(1);
        try
        {
            Icon = BitmapFrame.Create(new Uri("pack://application:,,,/Clinica.Desktop.Shell;component/Assets/Marca/icone.png"));
        }
        catch (Exception ex) { LogSuite.Registrar("Atualização — ícone da janela não carregou", ex); }

        Animacao = new ContentControl { Width = 120, Height = 120, HorizontalAlignment = HorizontalAlignment.Center, Content = AnimacaoDeAbertura.Criar() };

        _versao = new TextBlock { FontSize = 18, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 16, 0, 4) };
        _versao.SetResourceReference(TextBlock.ForegroundProperty, "Brush.Texto.Primario");
        _frase = new TextBlock
        {
            Text = "Baixando a versão nova. O sistema abre sozinho em seguida.",
            HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(24, 0, 24, 16)
        };
        _frase.SetResourceReference(TextBlock.ForegroundProperty, "Brush.Texto.Secundario");
        _barra = new ProgressBar { Height = 6, Minimum = 0, Maximum = 100, Margin = new Thickness(40, 0, 40, 0), IsIndeterminate = true };
        _barra.SetResourceReference(ProgressBar.ForegroundProperty, "Brush.Acento");
        _barra.SetResourceReference(ProgressBar.BackgroundProperty, "Brush.Acento.Suave");

        var pilha = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        pilha.Children.Add(Animacao);
        pilha.Children.Add(_versao);
        pilha.Children.Add(_frase);
        pilha.Children.Add(_barra);
        Content = pilha;
    }

    // ITelaDeAtualizacao — a regra chama de qualquer thread; a janela só se mexe na dela.
    public void Mostrar(string versao) => NaJanela(() =>
    {
        _versao.Text = $"Atualizando para a versão {versao}…";
        if (!IsVisible) Show();
    });

    public void Progresso(int porcento) => NaJanela(() =>
    {
        _barra.IsIndeterminate = false;
        _barra.Value = Math.Clamp(porcento, 0, 100);
    });

    public void Fechar() => NaJanela(() => { if (IsVisible) Close(); });

    private void NaJanela(Action acao)
    {
        try
        {
            if (Dispatcher.CheckAccess()) acao();
            else Dispatcher.Invoke(acao);
        }
        catch (Exception ex) { LogSuite.Registrar("Atualização — janela de progresso", ex); }
    }
}

/// <summary>
/// O PONTO DE ENCAIXE DA ANIMAÇÃO DA LOGO (nov/2026). Hoje devolve o símbolo estático; quando o design entregar
/// a animação, ela nasce aqui — um elemento WPF (XAML vetorial + Storyboard), não HTML: a janela de abertura
/// existe antes do navegador e não pode depender dele. Tamanho 120×120; começa no primeiro quadro; pode ser
/// cortada a qualquer instante; introdução ≤ ~1 s e o resto em laço.
/// </summary>
public static class AnimacaoDeAbertura
{
    public static UIElement Criar()
    {
        try
        {
            return new Image
            {
                Source = new BitmapImage(new Uri("pack://application:,,,/Clinica.Desktop.Shell;component/Assets/Marca/simbolo-cor.png")),
                Stretch = Stretch.Uniform
            };
        }
        catch (Exception ex)
        {
            LogSuite.Registrar("Atualização — símbolo da marca não carregou", ex);
            return new Border();
        }
    }
}
