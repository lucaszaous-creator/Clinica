using System.IO;
using System.Windows;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

// A NAVEGAÇÃO dos cinco apps para as videoaulas (out/2026). Uso:
//   dotnet run --project tools/treinamento/captura/CapturaTreinamento.csproj -- <saída>
// Monta os MESMOS módulos e o MESMO ShellViewModel dos executáveis, sobre um SQLite em
// memória (nenhuma conexão com produção), e grava <saída>/navegacao.json: o menu do topo de
// cada app, as abas das telas compostas, o componente Razor de cada chave e o recorte do
// Treinamento. As telas são Blazor desde out/2026 e são GRAVADAS na prévia
// (tools/PreviaBlazor, Linux) — a foto WPF desta ferramenta não alcançava o navegador e saiu.
static class Program
{
    static ServiceProvider? services;
    static SqliteConnection? connection;
    static string output = "";

    [STAThread]
    static void Main(string[] args)
    {
        output = Path.GetFullPath(args.Length > 0 ? args[0] : "saida-navegacao");
        Directory.CreateDirectory(output);
        var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/Clinica.Desktop.Shell;component/Styles/Suite.xaml") });
        app.DispatcherUnhandledException += (_, e) => { File.WriteAllText(Path.Combine(output, "erro.txt"), e.Exception.ToString()); e.Handled = true; };
        app.Startup += async (_, _) =>
        {
            try { await GravarAsync(); app.Shutdown(0); }
            catch (Exception e) { File.WriteAllText(Path.Combine(output, "erro.txt"), e.ToString()); app.Shutdown(1); }
        };
        app.Exit += (_, _) => { services?.Dispose(); connection?.Dispose(); };
        app.Run();
    }

    static async Task GravarAsync()
    {
        connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(connection).Options;
        IModuloApp[] modules =
        [
            new Clinica.Recepcao.Modulo.ModuloRecepcao(), new Clinica.Clinico.Modulo.ModuloClinico(),
            new Clinica.Financeiro.Modulo.ModuloFinanceiro(), new Clinica.Faturamento.Modulo.ModuloFaturamento(),
            new Clinica.Gerente.Modulo.ModuloGerente(),
        ];
        var collection = new ServiceCollection();
        collection.AddClinica("Host=127.0.0.1;Port=1;Database=NAO_USAR;Username=NAO_USAR;Timeout=1");
        collection.AddScoped(_ => new ClinicaDbContext(options));
        collection.AddSingleton<SessaoUsuario>();
        collection.AddSingleton<SnackbarService>();
        collection.AddSingleton<ISnackbarService>(s => s.GetRequiredService<SnackbarService>());
        collection.AddSingleton<IDialogoService, DialogosDemo>();
        foreach (var module in modules) module.Registrar(collection);
        services = collection.BuildServiceProvider();

        var db = services.GetRequiredService<ClinicaDbContext>();
        await db.Database.EnsureCreatedAsync();
        var medico = new Profissional { Nome = "Dr. Gustavo · demonstração", RegistroConselho = "CRM DEMO", Ativo = true };
        var coren = new Profissional { Nome = "Joana · demonstração", RegistroConselho = "COREN-DEMO 000", Ativo = true };
        db.AddRange(medico, coren);
        await db.SaveChangesAsync();
        var gerente = new UsuarioSistema { Nome = "Ana · demonstração", Login = "treinamento.local", Perfil = PerfilAcesso.Gerente, ProfissionalId = medico.Id };
        var enfermeira = new UsuarioSistema { Nome = "Joana · demonstração", Login = "enfermagem.local", Perfil = PerfilAcesso.Enfermagem, ProfissionalId = coren.Id };
        db.AddRange(gerente, enfermeira);
        await db.SaveChangesAsync();

        Navegacao.Gravar(services, gerente, enfermeira, output);
    }
}

sealed class DialogosDemo : IDialogoService
{
    public string? PerguntarTexto(string titulo, string pergunta, string? textoInicial = null, bool obrigatorio = true) => null;
    public bool Confirmar(string titulo, string mensagem) => false;
    public bool ConfirmarPerigo(string titulo, string mensagem) => false;
    public void Aviso(string titulo, string mensagem) { }
}
