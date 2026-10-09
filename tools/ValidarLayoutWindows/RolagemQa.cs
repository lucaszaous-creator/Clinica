using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

/// <summary>Exercita o template real de rolagem com setas modernas, nas duas orientações.</summary>
internal static class RolagemQa
{
    public static async Task Executar()
    {
        var rolagem = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new Border { Width = 1200, Height = 1400, Background = Brushes.White,
                Child = new TextBlock { Text = "Conteúdo fictício para validar rolagem vertical e horizontal.", Margin = new Thickness(20) } }
        };
        var janela = new Window { Content = rolagem, Width = 420, Height = 340, Left = -30000, Top = -30000,
            WindowStartupLocation = WindowStartupLocation.Manual, ShowActivated = false, ShowInTaskbar = false };
        var principalAnterior = System.Windows.Application.Current.MainWindow;
        janela.Show();
        try
        {
            async Task Estabilizar() { await Task.Delay(50); janela.UpdateLayout(); }
            await Estabilizar();
            foreach (var orientacao in new[] { Orientation.Vertical, Orientation.Horizontal })
            {
                var barra = Desc<ScrollBar>(rolagem).Single(b => b.Orientation == orientacao && b.IsVisible);
                var trilho = barra.Template.FindName("PART_Track", barra) as Track
                    ?? throw new Exception("Rolagem sem trilho nativo.");
                if (trilho.Thumb is null || !trilho.Thumb.IsVisible)
                    throw new Exception("Rolagem sem indicador arrastável.");
                var avancar = Desc<RepeatButton>(barra).Single(b => b.Command == (orientacao == Orientation.Vertical ? ScrollBar.LineDownCommand : ScrollBar.LineRightCommand));
                var voltar = Desc<RepeatButton>(barra).Single(b => b.Command == (orientacao == Orientation.Vertical ? ScrollBar.LineUpCommand : ScrollBar.LineLeftCommand));
                foreach (var seta in new[] { avancar, voltar })
                    if (!seta.IsVisible || seta.ActualWidth < 24 || seta.ActualHeight < 24 || !Desc<System.Windows.Shapes.Path>(seta).Any()
                        || string.IsNullOrWhiteSpace(System.Windows.Automation.AutomationProperties.GetName(seta)) || seta.Interval <= 0)
                        throw new Exception($"Seta {orientacao} perdeu desenho vetorial, alvo ou descrição acessível.");
                double Deslocamento() => orientacao == Orientation.Vertical ? rolagem.VerticalOffset : rolagem.HorizontalOffset;
                void Clicar(RepeatButton b) => ((RoutedCommand)b.Command).Execute(b.CommandParameter, b.CommandTarget);
                Clicar(avancar); await Estabilizar();
                var primeiroPasso = Deslocamento();
                if (primeiroPasso <= 0) throw new Exception($"Seta não avançou {orientacao}.");
                for (var repeticao = 0; repeticao < 3; repeticao++) { Clicar(avancar); await Estabilizar(); }
                var repetido = Deslocamento();
                if (repetido <= primeiroPasso) throw new Exception($"Repetição da seta não avançou {orientacao}.");
                Clicar(voltar); await Estabilizar();
                if (Deslocamento() >= repetido) throw new Exception($"Seta não voltou {orientacao}.");
                rolagem.ScrollToHorizontalOffset(0); rolagem.ScrollToVerticalOffset(0); await Estabilizar();
                var pagina = trilho.IncreaseRepeatButton;
                if (pagina.Command is not RoutedCommand comando || !comando.CanExecute(pagina.CommandParameter, pagina.CommandTarget))
                    throw new Exception("Trilho não permite avançar uma página.");
                comando.Execute(pagina.CommandParameter, pagina.CommandTarget);
                await Estabilizar();
                if (Deslocamento() <= 0) throw new Exception($"Clique no trilho não rolou {orientacao}.");
                rolagem.ScrollToHorizontalOffset(0); rolagem.ScrollToVerticalOffset(0); await Estabilizar();
                trilho.Thumb.RaiseEvent(new DragStartedEventArgs(0, 0) { RoutedEvent = Thumb.DragStartedEvent });
                trilho.Thumb.RaiseEvent(new DragDeltaEventArgs(orientacao == Orientation.Horizontal ? 25 : 0,
                    orientacao == Orientation.Vertical ? 25 : 0) { RoutedEvent = Thumb.DragDeltaEvent });
                trilho.Thumb.RaiseEvent(new DragCompletedEventArgs(0, 0, false) { RoutedEvent = Thumb.DragCompletedEvent });
                await Estabilizar();
                if (Deslocamento() <= 0) throw new Exception($"Arraste não rolou {orientacao}.");
                rolagem.ScrollToHorizontalOffset(0); rolagem.ScrollToVerticalOffset(0); await Estabilizar();
            }
            rolagem.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, -120)
                { RoutedEvent = Mouse.MouseWheelEvent });
            await Estabilizar();
            if (rolagem.VerticalOffset <= 0) throw new Exception("Roda do mouse não rolou o conteúdo.");
            rolagem.ScrollToTop(); await Estabilizar();
            rolagem.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(janela), Environment.TickCount, Key.PageDown)
                { RoutedEvent = Keyboard.KeyDownEvent });
            await Estabilizar();
            if (rolagem.VerticalOffset <= 0) throw new Exception("PageDown não rolou o conteúdo.");
            Console.WriteLine("OK ROLAGEM: setas vetoriais nas quatro direções, alvos 24px e repetição; trilho e arraste vertical/horizontal, roda e PageDown preservados.");
        }
        finally { janela.Close(); System.Windows.Application.Current.MainWindow = principalAnterior; }
    }

    private static IEnumerable<T> Desc<T>(DependencyObject pai) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(pai); i++)
        {
            var filho = VisualTreeHelper.GetChild(pai, i);
            if (filho is T tipo) yield return tipo;
            foreach (var descendente in Desc<T>(filho)) yield return descendente;
        }
    }
}
