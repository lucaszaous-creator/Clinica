using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Clinica.Desktop.Controls;
using Clinica.Domain.Entities;
using Clinica.Financeiro.Janelas;
using Clinica.Financeiro.ViewModels;
using Clinica.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Abre diálogos reais sem executar comandos de gravação ou serviços externos.</summary>
public static class DialogosQa
{
    public static async Task Executar(IServiceProvider services, string saida)
    {
        Directory.CreateDirectory(saida);
        var relatorio = new List<string>();
        var falhas = new List<string>();
        var escopos = services.GetRequiredService<IServiceScopeFactory>();
        var snackbar = services.GetRequiredService<ISnackbarService>();
        var dialogo = services.GetRequiredService<IDialogoService>();
        int itemId;
        string nomeItem;
        using (var scope = escopos.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();
            if (!db.Database.IsSqlite() || !db.Database.GetConnectionString()!.Contains(":memory:", StringComparison.Ordinal))
                throw new InvalidOperationException("QA de diálogos exige SQLite em memória, sem produção.");
            var item = await db.ItensEstoque.FirstOrDefaultAsync();
            if (item is null)
            {
                item = new ItemEstoque { Nome = "Material fictício para demonstração", Unidade = "un", EstoqueMinimo = 5 };
                db.Add(item);
            }
            if (!await db.Set<Profissional>().AnyAsync())
                db.Add(new Profissional { Nome = "Profissional fictício · demonstração", Ativo = true });
            await db.SaveChangesAsync();
            itemId = item.Id;
            nomeItem = item.Nome;
        }

        (string Nome, Func<Window> Criar)[] casos =
        [
            ("categoria", () => new CategoriaWindow(new CategoriaEdicaoViewModel(escopos, 1))),
            ("cobranca-pix", () => new CobrancaPixWindow(new CobrancaPixViewModel(escopos, 250m, "DEMONSTRAÇÃO · sem emissão"))),
            ("contas-fixas", () => new ContasFixasWindow(new ContasViewModel(escopos, snackbar, dialogo))),
            ("conta", () => new ContaWindow(new ContaEdicaoViewModel(escopos))),
            ("extrato-estoque", () => new ExtratoEstoqueWindow(new ExtratoEstoqueViewModel(escopos, itemId, nomeItem))),
            ("lancamento", () => new LancamentoWindow(new LancamentoEdicaoViewModel(escopos)
                { Descricao = "Material de demonstração", Valor = "250,00", Observacoes = "Dados fictícios para validar o formulário." })),
            ("item-estoque", () => new ItemEstoqueWindow(new ItemEstoqueEdicaoViewModel(escopos, itemId))),
            ("orcamento", () => new OrcamentoWindow(new OrcamentoEdicaoViewModel(escopos, DateTime.Today.Year, DateTime.Today.Month))),
            ("recorrente", () => new RecorrenteWindow(new RecorrenteEdicaoViewModel(escopos, 0))),
            ("movimento-estoque", () => new MovimentoEstoqueWindow(new MovimentoEstoqueViewModel(escopos, itemId, nomeItem))),
            ("regra-repasse", () => new RegraRepasseWindow(new RegraRepasseViewModel(escopos))),
            ("taxa", () => new TaxaWindow(new TaxaEdicaoViewModel(escopos, 0))),
            ("tributo", () => new TributoWindow(new TributoEdicaoViewModel(escopos, 0))),
            ("regras-repasse", () => new RegrasRepasseWindow(new RepassesViewModel(escopos, snackbar, dialogo))),
            ("validades-estoque", () => new ValidadesEstoqueWindow(new EstoqueViewModel(escopos, snackbar, dialogo)))
        ];

        foreach (var (nome, criar) in casos)
        {
            Window? janela = null;
            try
            {
                janela = criar();
                janela.ShowInTaskbar = false;
                janela.ShowActivated = false;
                janela.WindowStartupLocation = WindowStartupLocation.Manual;
                janela.Left = -30000;
                janela.Top = -30000;
                // Limita o espaço disponível sem substituir o layout ou o conteúdo real.
                janela.MaxWidth = 900;
                janela.MaxHeight = 600;
                if (!double.IsNaN(janela.Width)) janela.Width = Math.Min(janela.Width, 900);
                if (!double.IsNaN(janela.Height)) janela.Height = Math.Min(janela.Height, 600);
                janela.Show();
                await Estabilizar(janela);
                for (var tentativa = 0; tentativa < 100 && Carregando(janela.DataContext); tentativa++)
                {
                    await Task.Delay(50);
                    await Estabilizar(janela);
                }
                if (Carregando(janela.DataContext)) throw new InvalidOperationException("Carga não terminou em 5 segundos.");
                if (janela.DataContext?.GetType().GetProperty("NaoVerificado")?.GetValue(janela.DataContext) is true)
                    throw new InvalidOperationException("ViewModel informou falha de leitura.");

                var conteudo = (FrameworkElement)janela.Content;
                var botoes = Desc<Button>(conteudo).Where(b => b.IsDefault || b.IsCancel).ToArray();
                foreach (var botao in botoes)
                    ExigirVisivel(botao, conteudo, $"Ação fixa {botao.Content}");
                Capturar(conteudo, Path.Combine(saida, $"dialogo-{nome}.png"));

                var rolagens = Desc<ScrollViewer>(conteudo).Where(s => s.IsVisible && s.ScrollableHeight > 0).ToArray();
                foreach (var scroll in rolagens)
                {
                    if (!Desc<ScrollBar>(scroll).Any(b => b.IsVisible && b.Orientation == Orientation.Vertical
                            && b.Template.FindName("PART_Track", b) is Track { Thumb.IsVisible: true }))
                        throw new InvalidOperationException("Área rolável sem indicador vertical arrastável.");
                    scroll.ScrollToEnd();
                    await Estabilizar(janela);
                    if (scroll.VerticalOffset <= 0) throw new InvalidOperationException("Rolagem não alcança o conteúdo inferior.");
                    foreach (var botao in botoes) ExigirVisivel(botao, conteudo, $"Ação após rolagem {botao.Content}");
                    scroll.ScrollToHome();
                }

                var campos = Desc<FrameworkElement>(conteudo)
                    .Where(c => c.IsVisible && c is TextBox or ComboBox or DatePicker).ToArray();
                foreach (var campo in campos)
                {
                    campo.BringIntoView();
                    await Estabilizar(janela);
                    ExigirVisivel(campo, conteudo, $"Campo {campo.GetType().Name}");
                }
                if (rolagens.Length > 0)
                    Capturar(conteudo, Path.Combine(saida, $"dialogo-{nome}-rolagem.png"));
                var cancelamento = botoes.Any(b => b.IsCancel) ? "cancelar/fechar acessível" : "sem IsCancel declarado; fechamento pela moldura";
                var resultado = $"OK {nome}: {janela.ActualWidth:0}x{janela.ActualHeight:0}; {botoes.Length} ações verificadas; {campos.Length} campos alcançáveis; {rolagens.Length} áreas roláveis; {cancelamento}.";
                relatorio.Add(resultado);
                Console.WriteLine(resultado);
            }
            catch (Exception ex)
            {
                var erro = $"FALHA {nome}: {ex.GetBaseException().Message}";
                relatorio.Add(erro);
                falhas.Add(erro);
                Console.WriteLine(erro);
            }
            finally { janela?.Close(); }
        }
        await File.WriteAllLinesAsync(Path.Combine(saida, "resultado-dialogos.txt"), relatorio);
        if (falhas.Count > 0) throw new InvalidOperationException(string.Join(Environment.NewLine, falhas));
    }

    static bool Carregando(object? vm) => vm?.GetType().GetProperty("Carregando")?.GetValue(vm) is true
        || vm?.GetType().GetProperty("Ocupado")?.GetValue(vm) is true;

    static async Task Estabilizar(Window janela)
    {
        await janela.Dispatcher.InvokeAsync(janela.UpdateLayout, DispatcherPriority.ApplicationIdle);
        await janela.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
    }

    static void ExigirVisivel(FrameworkElement elemento, FrameworkElement conteudo, string descricao)
    {
        if (!elemento.IsVisible || elemento.ActualWidth <= 0 || elemento.ActualHeight <= 0)
            throw new InvalidOperationException(descricao + " invisível ou sem tamanho.");
        DependencyObject? ancestral = elemento;
        while ((ancestral = VisualTreeHelper.GetParent(ancestral)) is not null)
        {
            if (ancestral is not FrameworkElement limite || (ancestral != conteudo && ancestral is not ScrollContentPresenter)) continue;
            var retangulo = elemento.TransformToAncestor(limite).TransformBounds(new Rect(elemento.RenderSize));
            var area = new Rect(limite.RenderSize);
            area.Inflate(1, 1);
            if (!area.Contains(retangulo)) throw new InvalidOperationException(descricao + " cortado pela janela/rolagem.");
            if (ancestral == conteudo) break;
        }
    }

    static void Capturar(FrameworkElement conteudo, string arquivo)
    {
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(conteudo.ActualWidth), (int)Math.Ceiling(conteudo.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(conteudo);
        var png = new PngBitmapEncoder();
        png.Frames.Add(BitmapFrame.Create(bitmap));
        using var destino = File.Create(arquivo);
        png.Save(destino);
    }

    static IEnumerable<T> Desc<T>(DependencyObject raiz) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(raiz); i++)
        {
            var filho = VisualTreeHelper.GetChild(raiz, i);
            if (filho is T alvo) yield return alvo;
            foreach (var descendente in Desc<T>(filho)) yield return descendente;
        }
    }
}
