using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Linq;

// Teste WPF real dos recursos dos cinco apps, sem bootstrap, configuração ou banco.
internal static class Program
{
    private static readonly List<string> Resultados = [];
    private static Window Janela = null!;
    [STAThread]
    private static int Main()
    {
        var raiz = Directory.GetCurrentDirectory();
        var saida = Path.Combine(raiz, "artifacts", "qa-rolagem-suite");
        Directory.CreateDirectory(saida);
        var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        try
        {
            var arquivos = Directory.GetFiles(Path.Combine(raiz, "src"), "*.xaml", SearchOption.AllDirectories)
                .Where(p => !p.Contains("\\obj\\") && !p.Contains("\\bin\\")).Order().ToArray();
            var docs = arquivos.Select(p => (Caminho: Path.GetRelativePath(raiz, p), Xml: XDocument.Load(p))).ToArray();
            var barras = docs.SelectMany(d => d.Xml.Descendants().Where(e => e.Name.LocalName is "Style" or "ControlTemplate")
                .Where(e => ((string?)e.Attribute("TargetType"))?.Contains("ScrollBar") == true)
                .Select(e => d.Caminho)).Distinct().ToArray();
            Exigir(barras.Length == 1 && barras[0].EndsWith("Sobreposicao.xaml"), "Um único template compartilhado de ScrollBar");
            var apps = docs.Where(d => d.Xml.Root?.Name.LocalName == "Application").ToArray();
            Exigir(apps.Length == 5, "Cinco aplicativos desktop encontrados");
            var inventario = docs.Select(d => new { arquivo = d.Caminho,
                controles = d.Xml.Descendants().Where(e => e.Name.LocalName is "ScrollViewer" or "DataGrid" or "ListBox" or "TextBox" or "RichTextBox")
                .Select(e => new { tipo = e.Name.LocalName, vertical = (string?)e.Attribute("VerticalScrollBarVisibility") ?? (string?)e.Attribute("ScrollViewer.VerticalScrollBarVisibility"),
                    horizontal = (string?)e.Attribute("HorizontalScrollBarVisibility") ?? (string?)e.Attribute("ScrollViewer.HorizontalScrollBarVisibility"),
                    multilinha = (string?)e.Attribute("AcceptsReturn"), estilo = (string?)e.Attribute("Style") }).ToArray() }).ToArray();
            File.WriteAllText(Path.Combine(saida, "inventario.json"), JsonSerializer.Serialize(new { xaml = arquivos.Length, apps = apps.Select(a => a.Caminho), templates = barras, inventario }, new JsonSerializerOptions { WriteIndented = true }));
            foreach (var entrada in apps)
            {
                app.Resources.MergedDictionaries.Clear();
                foreach (var fonte in entrada.Xml.Descendants().Where(e => e.Name.LocalName == "ResourceDictionary" && e.Attribute("Source") != null))
                    app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri((string)fonte.Attribute("Source")!, UriKind.RelativeOrAbsolute) });
                Janela = new Window { Width = 540, Height = 340, Left = -30000, Top = -30000, ShowInTaskbar = false, ShowActivated = false };
                Janela.Show();
                ConferirApp(entrada.Caminho);
                Janela.Close();
            }
            ConferirLacunas(raiz);
            File.WriteAllLines(Path.Combine(saida, "resultado.txt"), Resultados.Append($"APROVADO: {Resultados.Count} verificações; {arquivos.Length} XAML inventariados; 5 apps."));
            Console.WriteLine($"APROVADO: {Resultados.Count} verificações; {arquivos.Length} XAML; cinco apps. Evidências: {saida}");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { app.Shutdown(); }
    }

    private static void ConferirApp(string app)
    {
        Console.WriteLine($"Conferindo {app}");
        var conteudo = new Border { Width = 1600, Height = 2400, Background = Brushes.White };
        var scroll = new ScrollViewer { Content = conteudo, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Montar(scroll);
        ConferirSetas(scroll, true, app + " / página");
        conteudo.Width = 80; conteudo.Height = 80; Atualizar();
        Exigir(scroll.ComputedVerticalScrollBarVisibility != Visibility.Visible && scroll.ComputedHorizontalScrollBarVisibility != Visibility.Visible, app + " / Auto oculta barras quando cabe");
        conteudo.Height = 100000; conteudo.Width = 100000; Atualizar();
        foreach (var barra in Descendentes<ScrollBar>(scroll).Where(b => b.IsVisible))
        {
            var track = (Track)barra.Template.FindName("PART_Track", barra);
            Exigir(track.Thumb.ActualWidth >= 24 && track.Thumb.ActualHeight >= 24, app + " / polegar alcançável em conteúdo extenso");
        }
        var lista = new ListBox { ItemsSource = Enumerable.Range(1, 500).Select(n => $"Item {n:000} " + new string('x', 180)).ToArray() };
        Montar(lista);
        var listaScroll = Descendentes<ScrollViewer>(lista).First();
        ConferirSetas(listaScroll, false, app + " / ListBox");
        Exigir(Descendentes<ListBoxItem>(lista).Count() < 500, app + " / virtualização preservada");
        var tabela = new DataGrid { ItemsSource = Enumerable.Range(1, 200).Select(n => new Linha(n, "Registro sintético")).ToArray() };
        tabela.Columns.Add(new DataGridTextColumn { Header = "Número", Binding = new System.Windows.Data.Binding("Numero"), Width = 700 });
        tabela.Columns.Add(new DataGridTextColumn { Header = "Texto", Binding = new System.Windows.Data.Binding("Texto"), Width = 700 });
        Montar(tabela);
        ConferirSetas(Descendentes<ScrollViewer>(tabela).First(), true, app + " / DataGrid");
        var texto = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 160, Text = string.Join('\n', Enumerable.Range(1, 100).Select(n => $"Linha {n}")) };
        Montar(texto);
        ConferirSetas(Descendentes<ScrollViewer>(texto).First(), false, app + " / TextBox multilinha sem opção local");
        var rico = new RichTextBox { Height = 160, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        rico.Document.Blocks.Add(new Paragraph(new Run(texto.Text)));
        Montar(rico);
        ConferirSetas(Descendentes<ScrollViewer>(rico).First(), false, app + " / RichTextBox");
    }

    private static void ConferirSetas(ScrollViewer scroll, bool horizontal, string contexto)
    {
        scroll.ScrollToTop(); scroll.ScrollToLeftEnd(); Atualizar();
        Exigir(scroll.ComputedVerticalScrollBarVisibility == Visibility.Visible, contexto + " / barra vertical visível no overflow");
        foreach (var orientacao in horizontal ? new[] { Orientation.Vertical, Orientation.Horizontal } : new[] { Orientation.Vertical })
        {
            var barra = Descendentes<ScrollBar>(scroll).First(b => b.Orientation == orientacao && b.IsVisible);
            var anterior = (RepeatButton?)barra.Template.FindName("anterior", barra);
            var proximo = (RepeatButton?)barra.Template.FindName("proximo", barra);
            Exigir(anterior != null && proximo != null && anterior.IsVisible && proximo.IsVisible, contexto + " / setas " + orientacao);
            Exigir(anterior!.ActualWidth >= 24 && proximo!.ActualHeight >= 24, contexto + " / alvo 24px " + orientacao);
            Exigir(!string.IsNullOrWhiteSpace(AutomationProperties.GetName(proximo!)), contexto + " / nome acessível " + orientacao);
            var antes = orientacao == Orientation.Vertical ? scroll.VerticalOffset : scroll.HorizontalOffset;
            Invocar(proximo!); Atualizar();
            var depois = orientacao == Orientation.Vertical ? scroll.VerticalOffset : scroll.HorizontalOffset;
            Exigir(depois > antes, contexto + " / seta avança " + orientacao);
            Invocar(anterior); Atualizar();
            Exigir((orientacao == Orientation.Vertical ? scroll.VerticalOffset : scroll.HorizontalOffset) < depois, contexto + " / seta retorna " + orientacao);
            Exigir(proximo!.Delay == 350 && proximo.Interval == 60, contexto + " / repetição configurada " + orientacao);
            var pagina = (RepeatButton)barra.Template.FindName("paginaProxima", barra);
            Invocar(pagina); Atualizar();
            Exigir((orientacao == Orientation.Vertical ? scroll.VerticalOffset : scroll.HorizontalOffset) > depois, contexto + " / trilho avança página " + orientacao);
            if (orientacao == Orientation.Vertical) scroll.ScrollToBottom(); else scroll.ScrollToRightEnd();
            Atualizar();
            Invocar(proximo); Atualizar();
            Exigir((orientacao == Orientation.Vertical ? scroll.VerticalOffset <= scroll.ScrollableHeight : scroll.HorizontalOffset <= scroll.ScrollableWidth), contexto + " / limite final " + orientacao);
            var fim = orientacao == Orientation.Vertical ? scroll.VerticalOffset : scroll.HorizontalOffset;
            var track = (Track)barra.Template.FindName("PART_Track", barra);
            track.Thumb.RaiseEvent(new DragStartedEventArgs(0, 0) { RoutedEvent = Thumb.DragStartedEvent });
            track.Thumb.RaiseEvent(new DragDeltaEventArgs(orientacao == Orientation.Horizontal ? -10 : 0, orientacao == Orientation.Vertical ? -10 : 0) { RoutedEvent = Thumb.DragDeltaEvent });
            track.Thumb.RaiseEvent(new DragCompletedEventArgs(0, 0, false) { RoutedEvent = Thumb.DragCompletedEvent });
            Atualizar();
            Exigir((orientacao == Orientation.Vertical ? scroll.VerticalOffset : scroll.HorizontalOffset) < fim, contexto + " / arrastar polegar " + orientacao);
        }
    }

    private static void ConferirLacunas(string raiz)
    {
        Janela = new Window { Width = 900, Height = 600, Left = -30000, Top = -30000, ShowInTaskbar = false, ShowActivated = false };
        Janela.Show();
        var convenios = new Clinica.Recepcao.Views.ConvenioPacienteView { DataContext = new
        {
            ValidadeConsulta = "Dados sintéticos de validação", Mensagem = "", UsaConsultaRenovavel = false, AutorizacoesNaoVerificadas = false, PodeEditarCadastro = true,
            Autorizacoes = Enumerable.Range(1, 85).Select(n => new { Autorizacao = new { Numero = $"Autorização {n:000}", DataEmissao = DateTime.Today, DataValidade = DateTime.Today.AddDays(30) }, Resumo = "Autorização demonstrativa", Utilizavel = true, NaUltima = false }).ToArray()
        } };
        Montar(convenios);
        ConferirSetas(Descendentes<ScrollViewer>(convenios).First(), false, "Recepção / 85 autorizações");
        Janela.Close();
        Janela = new Clinica.Financeiro.Janelas.RegrasRepasseWindow(null!) { Left = -30000, Top = -30000, ShowInTaskbar = false, ShowActivated = false,
            DataContext = new { Regras = Enumerable.Range(1, 85).Select(n => new { Profissional = $"Regra {n:000}", Regra = "Regra sintética", Vigencia = "Demonstração" }).ToArray(),
                Apuracoes = Enumerable.Range(1, 85).Select(n => new { Profissional = $"Apuração {n:000}", Valor = "Dados fictícios", Periodo = "Demonstração", Situacao = "Teste", PodeCancelar = false }).ToArray() } };
        Janela.Show(); Atualizar();
        ConferirSetas(Descendentes<ScrollViewer>(Janela).First(), false, "Financeiro / 85 regras e 85 apurações");
        Janela.Height = Janela.MinHeight; Atualizar();
        ConferirSetas(Descendentes<ScrollViewer>(Janela).First(), false, "Financeiro / janela na altura mínima");
        Janela.Close();
        Janela = new Window { Width = 900, Height = 600, Left = -30000, Top = -30000, ShowInTaskbar = false, ShowActivated = false };
        Janela.Show();
        foreach (var arquivo in new[] { "src/Clinica.Modulo.Clinico/Janelas/PrescricaoInternaWindow.xaml", "src/Clinica.Desktop.Shell/Componentes/FolhaExecucaoWindow.xaml", "src/Clinica.Desktop.Shell/Shell/ShellWindow.xaml" })
        {
            var propriedade = arquivo.EndsWith("ShellWindow.xaml") ? "ResultadosPesquisa" : "Alertas";
            var xml = XDocument.Load(Path.Combine(raiz, arquivo));
            if (propriedade == "ResultadosPesquisa")
            {
                var estilo = xml.Descendants().Single(e => e.Name.LocalName == "Style" && e.Attributes().Any(a => a.Name.LocalName == "Key" && a.Value == "ItemSidebar"));
                System.Windows.Application.Current.Resources["ItemSidebar"] = (Style)XamlReader.Parse(estilo.ToString());
            }
            var regiao = xml.Descendants().Single(e => e.Name.LocalName == "ScrollViewer" && e.Descendants().Any(i => (string?)i.Attribute("ItemsSource") == "{Binding " + propriedade + "}"));
            var copia = new XElement(regiao);
            // O DataTemplate já é explícito no ItemsControl; removemos somente a referência
            // de tipo do módulo para carregar a mesma região isolada, sem iniciar o shell.
            foreach (var tipo in copia.Descendants().Attributes("DataType").ToArray()) tipo.Remove();
            var controle = (ScrollViewer)XamlReader.Parse(copia.ToString());
            controle.VerticalAlignment = VerticalAlignment.Top;
            controle.DataContext = new { Alertas = Enumerable.Range(1, 85).Select(n => $"Alerta distinto {n:000} para teste de rolagem").ToArray(),
                ResultadosPesquisa = Enumerable.Range(1, 85).Select(n => new { Item = new { Glifo = "A" }, Rotulo = $"Resultado {n:000}", Caminho = "Demonstração" }).ToArray() };
            Montar(controle);
            Exigir(controle.ActualHeight <= (propriedade == "Alertas" ? 160 : 380), arquivo + " / altura limitada preserva restante da tela");
            ConferirSetas(controle, false, arquivo + " / 85 " + propriedade);
        }
        Janela.Close();
    }

    private static void Invocar(RepeatButton botao)
    {
        var peer = new RepeatButtonAutomationPeer(botao);
        ((IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)!).Invoke();
    }
    private static void Montar(UIElement elemento) { Janela.Content = elemento; Atualizar(); }
    private static void Atualizar() { Janela.UpdateLayout(); Janela.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle); Janela.UpdateLayout(); }
    private static void Exigir(bool condicao, string mensagem) { if (!condicao) throw new InvalidOperationException(mensagem); Resultados.Add("OK " + mensagem); }
    private static IEnumerable<T> Descendentes<T>(DependencyObject raiz) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(raiz); i++)
        {
            var filho = VisualTreeHelper.GetChild(raiz, i);
            if (filho is T valor) yield return valor;
            foreach (var descendente in Descendentes<T>(filho)) yield return descendente;
        }
    }
    public record Linha(int Numero, string Texto);
}

