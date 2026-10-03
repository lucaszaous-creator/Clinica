using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Clinica.Application.Servicos;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

internal static class Program
{
    static string saida = "";
    static bool barras;
    [STAThread]
    static int Main(string[] args)
    {
        saida = Path.GetFullPath(args.FirstOrDefault(a => !a.StartsWith("--")) ?? "artifacts/documentos-modelo-b"); Directory.CreateDirectory(saida);
        barras = args.Contains("--barras");
        using var log = new StreamWriter(Path.Combine(saida, "bindings.log"));
        PresentationTraceSources.DataBindingSource.Listeners.Add(new TextWriterTraceListener(log));
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
        var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Clinica.Desktop.Shell;component/Styles/Suite.xaml", UriKind.Relative) });
        app.Startup += async (_, _) =>
        {
            try { await Capturar(); app.Shutdown(0); }
            catch (Exception ex) { Console.WriteLine(ex); app.Shutdown(1); }
        };
        return app.Run();
    }

    static async Task Capturar()
    {
        using var conn = new SqliteConnection("Data Source=:memory:"); conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(conn).Options;
        var servicos = new ServiceCollection();
        servicos.AddClinica("Host=127.0.0.1;Port=1;Database=never_used");
        servicos.AddScoped(_ => new ClinicaDbContext(options)); servicos.AddSingleton<SessaoUsuario>();
        servicos.AddSingleton<Clinica.Desktop.Controls.SnackbarService>();
        servicos.AddSingleton<Clinica.Desktop.Controls.ISnackbarService>(s => s.GetRequiredService<Clinica.Desktop.Controls.SnackbarService>());
        servicos.AddSingleton<Clinica.Desktop.Controls.IDialogoService, DialogoTeste>();
        var modulo = new Clinica.Clinico.Modulo.ModuloClinico(); modulo.Registrar(servicos);
        using var provider = servicos.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClinicaDbContext>(); db.Database.EnsureCreated();
        var paciente = new Paciente { Nome = "Paciente de demonstração", Endereco = "Rua de demonstração, 100 — Centro" };
        var profissional = new Profissional { Nome = "Médica de demonstração", RegistroConselho = "CRM-RJ 000000", Ativo = true };
        db.Pacientes.Add(paciente); db.Profissionais.Add(profissional); await db.SaveChangesAsync();
        var repo = new ClinicaRepositorio(db);
        var usuario = await new AcessoService(repo).CriarAsync(profissional.Nome, "demo.documentos", "Teste#Documento2026", PerfilAcesso.Gerente);
        usuario.ProfissionalId = profissional.Id; usuario.Profissional = profissional; await db.SaveChangesAsync();
        provider.GetRequiredService<SessaoUsuario>().Entrar(usuario);
        var escopos = provider.GetRequiredService<IServiceScopeFactory>();
        if (barras)
        {
            var vm = new Clinica.Desktop.Shell.ShellViewModel("Consultório", [modulo], provider);
            var w = new Clinica.Desktop.Shell.ShellWindow { DataContext = vm, Width = 1366, Height = 690, ShowActivated = false, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual, Left = -30000, Top = -30000 };
            w.Show(); await Task.Delay(300);
            var nav = (Menu)w.FindName("Categorias");
            var group = vm.Grupos.Single(g => g.Grupo == Clinica.Desktop.Shell.Modulos.GrupoSidebar.Atendimento);
            var category = (MenuItem)nav.ItemContainerGenerator.ContainerFromItem(group);
            category.IsSubmenuOpen = true; await Task.Delay(200); w.UpdateLayout();
            var entry = group.Itens.Single(i => i.Rotulo == "Prescrições");
            var leaf = (MenuItem)category.ItemContainerGenerator.ContainerFromItem(entry);
            leaf.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, leaf));
            await Task.Delay(200);
            if (!entry.EstaAtivo || !group.EstaAtivo) throw new Exception("O menu não navegou para Prescrições.");
            foreach (var width in new[] { 1366, 1280, 1024, 880 })
            {
                w.Width = width; w.UpdateLayout(); await Task.Delay(150);
                var bar = (FrameworkElement)w.FindName("NavegacaoSuperior"); var p = bar.TranslatePoint(new Point(), w);
                if (p.X + bar.ActualWidth > w.ActualWidth - 15) throw new Exception($"Barra cortada em {width}px");
                await Render(w, $"barra-real-{width}.png");
            }
            w.Width = 1366; w.UpdateLayout(); category.IsSubmenuOpen = true; await Task.Delay(150); w.UpdateLayout();
            leaf.Focus(); await Task.Delay(100);
            var popup = (System.Windows.Controls.Primitives.Popup)category.Template.FindName("PART_Popup", category);
            var menu = (FrameworkElement)popup.Child; menu.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(menu.ActualWidth), (int)Math.Ceiling(menu.ActualHeight), 96, 96, PixelFormats.Pbgra32); bitmap.Render(menu);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = File.Create(Path.Combine(saida, "menu-real.png"))) encoder.Save(stream);
            var combined = new DrawingVisual();
            using (var drawing = combined.RenderOpen())
            {
                var content = (FrameworkElement)w.Content;
                drawing.DrawRectangle(new VisualBrush(w), null, new Rect(0, 0, content.ActualWidth, content.ActualHeight));
                var anchor = category.TranslatePoint(new Point(-10, category.ActualHeight), w);
                drawing.DrawImage(bitmap, new Rect(anchor.X, anchor.Y, menu.ActualWidth, menu.ActualHeight));
            }
            var full = new RenderTargetBitmap((int)((FrameworkElement)w.Content).ActualWidth, (int)((FrameworkElement)w.Content).ActualHeight, 96, 96, PixelFormats.Pbgra32); full.Render(combined);
            var fullEncoder = new PngBitmapEncoder(); fullEncoder.Frames.Add(BitmapFrame.Create(full));
            using (var stream = File.Create(Path.Combine(saida, "barra-real-menu-aberto.png"))) fullEncoder.Save(stream);
            category.IsSubmenuOpen = false; w.Close(); Console.WriteLine("Barra real: navegação e larguras 1366, 1280, 1024 e 880 conferidas."); return;
        }
        {
            foreach (var tipo in new[] { TipoDocumentoClinico.Receita, TipoDocumentoClinico.Atestado, TipoDocumentoClinico.Comparecimento, TipoDocumentoClinico.PedidoExame })
            {
                var vm = new DocumentoEdicaoViewModel(escopos, paciente.Id, tipo);
                for (var i = 0; vm.Carregando && i < 200; i++) await Task.Delay(25);
                if (vm.Carregando || vm.MensagemEhErro) throw new Exception(vm.Mensagem);
                vm.Data = new DateTime(2026, 10, 2); vm.PeriodoInicio = vm.Data; vm.DiasAfastamentoTexto = "2";
                vm.HoraChegadaTexto = "09:00"; vm.HoraSaidaTexto = "09:40";
                vm.Corpo = tipo switch
                {
                    TipoDocumentoClinico.Receita => "1. Medicamento / apresentação\nQuantidade: informar\nModo de usar: preencher a orientação.\n\n2. Medicamento / apresentação\nQuantidade: informar\nModo de usar: preencher a orientação.",
                    TipoDocumentoClinico.Atestado => "Texto complementar do profissional, quando necessário.\n\nOs dias e a data de início são informados nos campos acima.",
                    TipoDocumentoClinico.Comparecimento => "Declaro, para os devidos fins, que o paciente de demonstração\ncompareceu ao atendimento no período informado acima.",
                    _ => "Orientações adicionais para a realização dos exames, quando necessárias."
                };
                if (tipo == TipoDocumentoClinico.PedidoExame)
                {
                    vm.Itens[0].Descricao = "Exame de demonstração 1"; vm.Itens[0].Quantidade = "1"; vm.Itens[0].Detalhe = "Indicação do profissional";
                    vm.AdicionarItemCommand.Execute(null); vm.Itens[1].Descricao = "Exame de demonstração 2"; vm.Itens[1].Quantidade = "1"; vm.Itens[1].Detalhe = "Indicação do profissional";
                }
                Window window = new DocumentoWindow(vm);
                window.Width = 1280; window.Height = 800; window.ShowActivated = false; window.ShowInTaskbar = false; window.WindowStartupLocation = WindowStartupLocation.Manual; window.Left = -30000; window.Top = -30000;
                window.Show(); await Render(window, $"modelo-B-{tipo}.png");
                {
                    var documento = new DocumentoNaTela { DocumentoId = 1, Numero = "QA", Tipo = tipo, Rotulo = tipo.ToString(), Cancelado = false, Assinado = false };
                    if (documento.PodeAssinar || documento.OferecerAssinar) throw new Exception("Assinatura ainda oferecida neste documento.");
                    if (!(documento with { Assinado = true }).PodeEnviar) throw new Exception("A entrega de documentos históricos assinados foi bloqueada.");
                    window.Width = 900; window.Height = 680; await Render(window, $"modelo-B-{tipo}-900.png");
                    var editor = Descendentes(window).OfType<Clinica.Desktop.Controls.EditorTextoClinico>().Single();
                    var campo = Descendentes(editor).OfType<RichTextBox>().Single();
                    campo.Document.Blocks.Clear(); campo.Document.Blocks.Add(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("Texto editado na folha") { FontWeight = FontWeights.Bold }));
                    if (!vm.Corpo!.Contains("Texto editado na folha") || string.IsNullOrEmpty(vm.CorpoFormatado)) throw new Exception("A folha perdeu o texto ou o negrito.");
                    await vm.SalvarComoModeloCommand.ExecuteAsync("Modelo QA " + tipo);
                    if (vm.MensagemEhErro || vm.ModeloSelecionado is null) throw new Exception(vm.Mensagem);
                    if(tipo==TipoDocumentoClinico.Receita)
                    {
                        for(var aba=0;aba<3;aba++)
                        {
                            var opcoes=new DocumentoOpcoesWindow(vm,aba){ShowActivated=false,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-30000,Top=-30000};
                            opcoes.Show();await Render(opcoes,$"opcoes-{aba}.png");opcoes.Close();
                        }
                    }
                    vm.Corpo = ""; vm.AplicarModeloCommand.Execute(null);
                    if (!vm.Corpo!.Contains("Texto editado na folha")) throw new Exception("O modelo não recuperou o texto.");
                    await vm.ExcluirModeloCommand.ExecuteAsync(null);
                    if (vm.MensagemEhErro || vm.ModeloSelecionado is not null) throw new Exception(vm.Mensagem);
                    Console.WriteLine($"{tipo}: edição, negrito, salvar, aplicar e excluir modelo conferidos.");
                }
                window.Close(); Console.WriteLine($"Modelo B: {tipo} capturado.");
            }
            return;
        }
    }

    static IEnumerable<DependencyObject> Descendentes(DependencyObject node)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
        { var child = VisualTreeHelper.GetChild(node, i); yield return child; foreach (var item in Descendentes(child)) yield return item; }
    }
    static async Task Render(Window w, string nome)
    {
        await Task.Delay(120); w.UpdateLayout();
        var content = (FrameworkElement)w.Content;
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(content.ActualWidth + content.Margin.Left + content.Margin.Right), (int)Math.Ceiling(content.ActualHeight + content.Margin.Top + content.Margin.Bottom), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(w); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(saida, nome)); encoder.Save(stream);
    }
}

sealed class DialogoTeste : Clinica.Desktop.Controls.IDialogoService
{
    public string? PerguntarTexto(string titulo, string pergunta, string? textoInicial = null, bool obrigatorio = true) => null;
    public bool Confirmar(string titulo, string mensagem) => true;
    public bool ConfirmarPerigo(string titulo, string mensagem) => true;
    public void Aviso(string titulo, string mensagem) { }
}
