using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Clinica.Application.Servicos;
using Clinica.Clinico.Modulo;
using Clinica.Clinico.ViewModels;
using Clinica.Clinico.Views;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Componentes;
static class Program
{
    static string Saida = "artifacts/layout-windows";
    static int falhas;
    static bool completo;
    [STAThread]
    static int Main(string[] args)
    {
        completo = args.Contains("--completo"); Directory.CreateDirectory(Saida);
        using var log = new StreamWriter(Saida + "/bindings.log"); PresentationTraceSources.DataBindingSource.Listeners.Add(new TextWriterTraceListener(log)); PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
        var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown }; app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/Clinica.Desktop.Shell;component/Styles/Suite.xaml") });
        int code = 0; app.Dispatcher.BeginInvoke(async () => { try { await Executar(); if (falhas > 0) throw new Exception($"{falhas} cortes encontrados. Consulte artifacts/layout-windows."); Console.WriteLine("TELAS CONFERIDAS"); } catch (Exception e) { Console.WriteLine(e); code = 1; } finally { PresentationTraceSources.DataBindingSource.Flush(); app.Shutdown(); } }); app.Run(); return code;
    }
    static async Task Executar()
    {
        using var conexao = new SqliteConnection("Data Source=:memory:"); conexao.Open(); var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(conexao).Options;
        IModuloApp[] modulos = [new Clinica.Recepcao.Modulo.ModuloRecepcao(), new ModuloClinico(), new Clinica.Financeiro.Modulo.ModuloFinanceiro(), new Clinica.Gerente.Modulo.ModuloGerente()];
        var services = new ServiceCollection(); services.AddClinica("Host=127.0.0.1;Database=nao_usado;Username=nao_usado"); services.AddScoped(_ => new ClinicaDbContext(options)); services.AddSingleton<SessaoUsuario>(); services.AddSingleton<SnackbarService>(); services.AddSingleton<ISnackbarService>(s => s.GetRequiredService<SnackbarService>()); services.AddSingleton<IDialogoService, DialogoTeste>(); foreach (var m in modulos) m.Registrar(services);
        using var sp = services.BuildServiceProvider(); using var scope = sp.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<ClinicaDbContext>(); db.Database.EnsureCreated();
        var prof = new Profissional { Nome = "Profissional demonstrativo de nome comprido", RegistroConselho = "CRM-RJ 123456", Ativo = true }; var pac = new Paciente { Nome = "Paciente fictício com nome completo e sobrenomes para validar leitura", Documento = "12345678909", Telefone = "22999990000", Convenio = Convenio.UnimedIntercambio }; db.AddRange(prof, pac); await db.SaveChangesAsync();
        var usuario = new UsuarioSistema { Nome = prof.Nome, Login = "qa", Perfil = PerfilAcesso.Gerente, ProfissionalId = prof.Id, Profissional = prof }; db.Add(usuario); await db.SaveChangesAsync(); sp.GetRequiredService<SessaoUsuario>().Entrar(usuario);
        for (var i = 0; i < 12; i++) db.Add(new Agendamento { PacienteId = pac.Id, ProfissionalId = prof.Id, DataHora = DateTime.Today.AddHours(8 + i / 2.0), ModalidadePrevista = ModalidadeAtendimento.AcupunturaComEletro }); await db.SaveChangesAsync();
        sp.GetRequiredService<PacienteEmFoco>().Definir(pac.Id, pac.Nome, 1, null, DateOnly.FromDateTime(DateTime.Today));
        var vm = new ShellViewModel("Gerente", modulos, sp); var win = new ShellWindow { DataContext = vm, ShowInTaskbar = false, ShowActivated = false, WindowStartupLocation = WindowStartupLocation.Manual, Left = -30000, Top = -30000, Width = 960, Height = 600 }; win.Show();
        foreach (var item in vm.Itens.Where(i => !i.Oculto && (completo || new[] { "agenda", "consultorio-agenda", "receituario", "consultorio-prescricoes" }.Contains(i.Chave))))
        {
            vm.NavegarCommand.Execute(item); await Task.Delay(350);
            var abas = vm.TelaAtual is TelaComAbas t ? Descendentes(t).OfType<TabControl>().First() : null;
            int n = abas?.Items.Count ?? 1;
            for (var a = 0; a < n; a++)
            {
                if (abas != null) abas.SelectedIndex = a; await Task.Delay(200);
                foreach (int largura in new[] { 1920, 1366, 880, 960, 1024 })
                {
                    win.Width = largura; win.UpdateLayout(); await Task.Delay(100); win.UpdateLayout();
                    string nome = item.Chave + "-" + a + "-" + largura; Console.WriteLine("TELA " + nome);
                    foreach (var el in Descendentes(win).OfType<FrameworkElement>().Where(e => e.IsVisible && (e is Button || e is TextBox || e is ComboBox || e is DataGrid)))
                    {
                        var pt = el.TranslatePoint(new Point(), win); if (pt.X < -1 || pt.X + el.ActualWidth > win.ActualWidth - 15 + 1) { falhas++; Console.WriteLine($"VAZA {nome} {el.GetType().Name} {(el as Button)?.Content} x={pt.X:0} w={el.ActualWidth:0}"); }
                    }
                    foreach (var grade in Descendentes(win).OfType<DataGrid>())
                    {
                        var sv = Descendentes(grade).OfType<ScrollViewer>().FirstOrDefault();
                        if (sv != null && sv.ScrollableWidth > 0.01) { falhas++; Console.WriteLine($"COLUNAS {nome} excedente={sv.ScrollableWidth:0}"); }
                        if (grade.Name is "TabelaAgenda" or "TabelaMeuDia")
                        {
                            var ocupada = grade.Columns.Where(c => c.Visibility == Visibility.Visible).Sum(c => c.ActualWidth);
                            if (ocupada < grade.ActualWidth - 30) { falhas++; Console.WriteLine($"ESPAÇO PERDIDO {nome}: {ocupada}/{grade.ActualWidth}"); }
                            foreach (var botao in Descendentes(grade).OfType<Button>().Where(b => b.IsVisible && b.Content is string texto && texto is not ""))
                            {
                                DependencyObject? pai = botao; while (pai is not null && pai is not DataGridCell) pai = VisualTreeHelper.GetParent(pai);
                                if (pai is DataGridCell celula) { var ponto = botao.TranslatePoint(new Point(), celula); if (ponto.X < -1 || ponto.X + botao.ActualWidth > celula.ActualWidth + 1) { falhas++; Console.WriteLine($"AÇÃO CORTADA {nome}: {botao.Content}"); } }
                            }
                        }
                    }
                    if (largura == 1366 || largura == 960) Foto(win, nome);
                }
            }
        }
        win.Close();
    }
    static IEnumerable<DependencyObject> Descendentes(DependencyObject o) { for (int i = 0; i < VisualTreeHelper.GetChildrenCount(o); i++) { var c = VisualTreeHelper.GetChild(o, i); yield return c; foreach (var d in Descendentes(c)) yield return d; } }
    static void Foto(Window w, string nome) { var raiz = (FrameworkElement)w.Content; var bmp = new RenderTargetBitmap((int)raiz.ActualWidth, (int)raiz.ActualHeight, 96, 96, PixelFormats.Pbgra32); bmp.Render(raiz); var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bmp)); using var f = File.Create(Saida + "/" + nome + ".png"); png.Save(f); }
}
sealed class DialogoTeste : IDialogoService
{
    public string? PerguntarTexto(string titulo, string pergunta, string? textoInicial = null, bool obrigatorio = true) => null;
    public bool Confirmar(string titulo, string mensagem) => false;
    public bool ConfirmarPerigo(string titulo, string mensagem) => false;
    public void Aviso(string titulo, string mensagem) { }
}
sealed class SnackbarTeste : ISnackbarService
{
    public void Sucesso(string mensagem) { }
    public void Erro(string mensagem) { }
    public void Info(string mensagem) { }
}


