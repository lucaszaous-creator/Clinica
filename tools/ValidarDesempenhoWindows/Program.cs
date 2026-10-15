using System.Collections.Concurrent;
using System.IO;
using System.Data.Common;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

internal static class Program
{
    static readonly string Saida = Path.GetFullPath("artifacts/desempenho-global");
    [STAThread]
    static int Main()
    {
        Directory.CreateDirectory(Saida);
        var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/Clinica.Desktop.Shell;component/Styles/Suite.xaml") });
        int code = 0;
        app.Dispatcher.BeginInvoke(async () =>
        {
            try { await Conferir(); Console.WriteLine("QA WPF desempenho aprovado."); }
            catch (Exception ex) { Console.WriteLine(ex); code = 1; }
            finally { app.Shutdown(); }
        });
        app.Run();
        return code;
    }

    static void Exigir(bool valor, string mensagem) { if (!valor) throw new InvalidOperationException(mensagem); }
    static bool Criado(PacienteWorkspaceViewModel vm, string nome)
    {
        var lazy = typeof(PacienteWorkspaceViewModel).GetField("_secao" + nome, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(vm)!;
        return (bool)lazy.GetType().GetProperty("IsValueCreated")!.GetValue(lazy)!;
    }
    static async Task Estabilizar(Medidor m)
    {
        var limite = Stopwatch.StartNew();
        do
        {
            int n = m.Consultas.Count;
            await Task.Delay(250);
            if (n == m.Consultas.Count && m.Pendentes == 0) return;
        } while (limite.Elapsed < TimeSpan.FromSeconds(30));
        throw new TimeoutException("Consultas sintéticas não estabilizaram.");
    }
    static void Capturar(Window w, string nome)
    {
        var bmp = new RenderTargetBitmap((int)w.ActualWidth, (int)w.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(w);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bmp));
        using var arquivo = File.Create(Path.Combine(Saida, nome)); encoder.Save(arquivo);
    }

    static async Task Conferir()
    {
        var cs = $"Data Source=qa-desempenho-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        using var manter = new SqliteConnection(cs); manter.Open();
        var medidor = new Medidor();
        var services = new ServiceCollection();
        services.AddClinica("Host=127.0.0.1;Database=nao_usado;Username=nao_usado");
        services.AddScoped(_ => new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(cs).AddInterceptors(medidor).Options));
        services.AddSingleton<IDialogoService, DialogoTeste>();
        services.AddSingleton<ISnackbarService, SnackbarService>();
        new ModuloClinico().Registrar(services);
        using var sp = services.BuildServiceProvider();
        int pacienteId;
        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ClinicaDbContext>(); db.Database.EnsureCreated();
            var paciente = new Paciente { Nome = "PACIENTE FICTÍCIO · TESTE DE DESEMPENHO", Convenio = Convenio.UnimedIntercambio };
            db.Pacientes.Add(paciente); db.SaveChanges(); pacienteId = paciente.Id;
            db.Evolucoes.Add(new Evolucao { PacienteId = pacienteId, Data = DateOnly.FromDateTime(DateTime.Today).AddDays(-1), QueixaPrincipal = "Registro fictício anterior", EvaAntes = 8, EvaDepois = 3 });
            var usuario = new UsuarioSistema { Nome = "Profissional fictício", Login = "qa", Perfil = PerfilAcesso.Profissional };
            db.Usuarios.Add(usuario); db.SaveChanges(); new SessaoUsuario().Entrar(usuario);
        }
        var foco = new PacienteEmFoco(); foco.Definir(pacienteId, "PACIENTE FICTÍCIO · TESTE DE DESEMPENHO");
        var resultados = new List<object>();
        int anteriores = 0;
        foreach (var antecipar in new[] { true, false })
        {
            medidor.Consultas.Clear();
            var tempo = Stopwatch.StartNew();
            var vm = new PacienteWorkspaceViewModel(sp, foco);
            if (antecipar)
                foreach (var nome in new[] { "Enfermagem", "Anamnese", "Capa", "Prontuario", "Prescricoes", "Infusoes", "Anexos", "Dor", "Medidas", "Avaliacoes", "Administrativo" })
                    typeof(PacienteWorkspaceViewModel).GetProperty(nome)!.GetValue(vm);
            var window = new Window { Title = "Validação sintética de desempenho", Width = 1366, Height = 768, ShowInTaskbar = false, Left = -10000, Top = -10000,
                Content = new PacienteWorkspaceView { DataContext = vm } };
            window.Show();
            try
            {
                await Estabilizar(medidor);
                var quantidade = medidor.Consultas.Count;
                resultados.Add(new { percurso = antecipar ? "abas_antecipadas_reproduzido" : "sob_demanda", consultas = quantidade, milissegundos = tempo.ElapsedMilliseconds });
                if (antecipar) { anteriores = quantidade; continue; }
                Exigir(quantidade < anteriores, "A abertura não reduziu consultas.");
                foreach (var nome in new[] { "Prontuario", "Anamnese", "Enfermagem", "Dor", "Medidas", "Avaliacoes", "Infusoes", "Anexos" })
                    Exigir(!Criado(vm, nome), $"A seção oculta {nome} foi criada na abertura.");
                var atendimento = vm.Atendimento;
                atendimento.QueixaPrincipal = "Rascunho sintético preservado";
                vm.AbaAtual = ModuloClinico.AbaDe(ModuloClinico.ChaveProntuario);
                await Estabilizar(medidor);
                Exigir(Criado(vm, "Prontuario"), "Histórico não materializado ao abrir.");
                Exigir(vm.Prontuario.Sessoes.Count == 1, "Histórico não carregou registro sintético.");
                vm.AbaAtual = 0; await Estabilizar(medidor);
                Exigir(ReferenceEquals(atendimento, vm.Atendimento) && atendimento.QueixaPrincipal == "Rascunho sintético preservado", "Rascunho mudou ao trocar de aba.");
                Capturar(window, "atendimento.png");
                vm.AbaAtual = ModuloClinico.AbaDe(ModuloClinico.ChaveEvolucaoDor); await Estabilizar(medidor);
                Exigir(Criado(vm, "Dor") && !Criado(vm, "Medidas") && !Criado(vm, "Avaliacoes"), "Acompanhamento carregou abas ocultas.");
                vm.SubAbaAcompanhamento = 1; await Estabilizar(medidor);
                Exigir(Criado(vm, "Medidas"), "Medidas não abriu.");
                vm.SubAbaAcompanhamento = 2; await Estabilizar(medidor);
                Exigir(Criado(vm, "Avaliacoes"), "Avaliações não abriu.");
                Capturar(window, "acompanhamento.png");
                foco.Definir(pacienteId + 100, "Outro foco fictício");
                Exigir(vm.Paciente.Contains("PACIENTE FICTÍCIO"), "Workspace perdeu identidade com outro foco.");
            }
            finally { window.Close(); await Estabilizar(medidor); }
        }
        await File.WriteAllTextAsync(Path.Combine(Saida, "workspace.json"), JsonSerializer.Serialize(new { banco = "SQLite sintético, atraso de 20 ms por consulta", resultados, rascunho_preservado = true, abas_sob_demanda = true }, new JsonSerializerOptions { WriteIndented = true }));
        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();
            db.Pacientes.AddRange(Enumerable.Range(1, 240).Select(i => new Paciente { Nome = $"PACIENTE FICTÍCIO {i:000}", Documento = $"{i:00000000000}" }));
            await db.SaveChangesAsync();
        }
        var lista = new Clinica.Recepcao.ViewModels.PacientesViewModel(sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<ISnackbarService>(), sp.GetRequiredService<IDialogoService>(), sp.GetRequiredService<IFabricaFichaPaciente>());
        await lista.Seletor.DesligarSugestaoCommand.ExecuteAsync(null);
        var listaWindow = new Window { Width = 1100, Height = 700, Left = -10000, Top = -10000, ShowInTaskbar = false,
            Content = new Clinica.Recepcao.Views.PacientesView { DataContext = lista } };
        listaWindow.Show();
        try
        {
            await Estabilizar(medidor);
            Exigir(lista.Seletor.Resultados.Count == 100, "Primeira página não foi limitada.");
            await lista.Seletor.ProximaPaginaCommand.ExecuteAsync(null);
            await lista.Seletor.ProximaPaginaCommand.ExecuteAsync(null);
            Exigir(lista.Seletor.Resultados.Count == 41 && lista.Seletor.PaginaAtual == 3, "Última página não alcançada.");
            await Estabilizar(medidor);
            Capturar(listaWindow, "pacientes-pagina-3.png");
            lista.Seletor.Termo = "00000000240";
            await lista.Seletor.BuscarAsync(true);
            Exigir(lista.Seletor.Resultados.Count == 1 && lista.Seletor.PaginaAtual == 1, "Busca global não encontrou paciente fora da primeira página.");
            await Estabilizar(medidor);
            Capturar(listaWindow, "busca-paciente.png");
        }
        finally { listaWindow.Close(); }
    }
}

sealed class Medidor : DbCommandInterceptor
{
    public ConcurrentQueue<string> Consultas { get; } = new();
    public int Pendentes;
    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref Pendentes);
        try { Consultas.Enqueue(command.CommandText); await Task.Delay(20, cancellationToken); return result; }
        finally { Interlocked.Decrement(ref Pendentes); }
    }
}
sealed class DialogoTeste : IDialogoService
{
    public string? PerguntarTexto(string titulo, string pergunta, string? textoInicial = null, bool obrigatorio = true) => null;
    public bool Confirmar(string titulo, string mensagem) => false;
    public bool ConfirmarPerigo(string titulo, string mensagem) => false;
    public void Aviso(string titulo, string mensagem) => Console.WriteLine($"AVISO sintético: {titulo}: {mensagem}");
}
