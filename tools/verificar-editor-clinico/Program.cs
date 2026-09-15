using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Interop;
using System.Windows.Controls;
using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
using Clinica.Clinico.Janelas;
using Clinica.Clinico.ViewModels;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

static class Program
{
    static readonly string Saida = Path.GetFullPath("artifacts/qa-prescricao");
    [STAThread]
    static int Main()
    {
        Directory.CreateDirectory(Saida);
        using var bindings = new StreamWriter(Path.Combine(Saida, "bindings.log"));
        PresentationTraceSources.DataBindingSource.Listeners.Add(new TextWriterTraceListener(bindings));
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
        var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/Clinica.Desktop.Shell;component/Styles/Suite.xaml") });
        var code = 0;
        app.Dispatcher.BeginInvoke(async () =>
        {
            try { await ConferirAsync(); Console.WriteLine("QA OK: sugestões, cancelamento, endereço, texto anterior, infusão e PDFs."); }
            catch (Exception ex) { Console.WriteLine(ex); code = 1; }
            finally { PresentationTraceSources.DataBindingSource.Flush(); app.Shutdown(); }
        });
        app.Run();
        return code;
    }

    static void Exigir(bool valor, string erro) { if (!valor) throw new InvalidOperationException(erro); }

    static async Task ConferirAsync()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(conn).Options;
        var dialogo = new DialogoTeste();
        var services = new ServiceCollection();
        // O último registro substitui a fábrica da aplicação. Toda operação usa somente
        // o SQLite em memória criado acima; a suíte de produção não é iniciada.
        services.AddClinica("Host=127.0.0.1;Database=nao_usado;Username=nao_usado");
        services.AddScoped(_ => new ClinicaDbContext(options));
        services.AddSingleton<IDialogoService>(dialogo);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();
        db.Database.EnsureCreated();
        var paciente = new Paciente { Nome = "Paciente demonstrativo", Convenio = Convenio.UnimedIntercambio };
        var profissional = new Profissional { Nome = "Profissional demonstrativo", RegistroConselho = "CRM-SP 123456" };
        db.AddRange(paciente, profissional);
        await db.SaveChangesAsync();
        var usuario = new UsuarioSistema { Nome = profissional.Nome, Login = "qa.sintetico", Perfil = PerfilAcesso.Profissional,
            ProfissionalId = profissional.Id, Profissional = profissional };
        db.Add(usuario); await db.SaveChangesAsync(); new SessaoUsuario().Entrar(usuario);
        var documentos = scope.ServiceProvider.GetRequiredService<DocumentoClinicoService>();
        await documentos.SalvarModeloAsync(new ModeloDocumento { Nome = "Orientações do consultório", Tipo = TipoDocumentoClinico.Receita,
            Corpo = "Texto demonstrativo preparado pela clínica.\nO profissional pode ajustar este conteúdo livremente." });
        await documentos.SalvarModeloAsync(new ModeloDocumento { Nome = "Modelo anterior da clínica", Tipo = TipoDocumentoClinico.Receita,
            Itens = [new ItemModelo { Descricao = "Item demonstrativo", Quantidade = "Quantidade informada", Detalhe = "Modo de usar informado pelo profissional." }] });
        var factory = provider.GetRequiredService<IServiceScopeFactory>();
        var vm = new DocumentoEdicaoViewModel(factory, paciente.Id);
        await Esperar(() => !vm.Carregando);
        Exigir(vm.Sugestoes.Count == 2, "Sugestões não carregadas.");
        vm.Corpo = "Receituário digitado pelo profissional.";
        vm.Sugestoes[0].Selecionada = true;
        Exigir(vm.Corpo.StartsWith("Receituário digitado"), "Sugestão apagou texto.");
        Exigir(vm.Corpo.Contains(vm.Sugestoes[0].Texto), "Sugestão não inserida.");
        var antes = vm.Corpo; vm.Sugestoes[0].Selecionada = false;
        Exigir(vm.Corpo == antes, "Desmarcar apagou conteúdo.");
        var window = new DocumentoWindow(vm);
        await Desenhar(window, "receituario.png", 860, 680);
        await vm.EmitirCommand.ExecuteAsync(null);
        Exigir(dialogo.Perguntas == 1 && vm.DocumentoEmitidoId == 0, "Cancelar endereço emitiu documento.");
        Exigir(vm.Corpo == antes, "Cancelar endereço perdeu texto.");
        Exigir(await db.DocumentosClinicos.CountAsync() == 0, "Cancelar consumiu um documento.");
        dialogo.Resposta = "Rua Demonstrativa, 120, Centro, Cidade";
        var completar = typeof(DocumentoEdicaoViewModel).GetMethod("CompletarEnderecoDaReceitaAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Exigir(await (Task<bool>)completar.Invoke(vm, null)!, "Endereço não foi completado.");
        Exigir(await (Task<bool>)completar.Invoke(vm, null)!, "Releitura do endereço falhou.");
        Exigir(dialogo.Perguntas == 2, "Perguntou endereço já existente.");
        await Desenhar(window, "receituario-endereco.png", 860, 680);
        await Desenhar(window, "receituario-compacto.png", 700, 520);
        var longo = string.Join("\n\n", Enumerable.Range(1, 70).Select(i => $"Parágrafo {i:00}: texto demonstrativo para verificar impressão. Preserva acentuação, orientação escrita e espaço entre os parágrafos."));
        db.ChangeTracker.Clear();
        var documento = await documentos.EmitirAsync(new DocumentoClinico { Tipo = TipoDocumentoClinico.Receita,
            PacienteId = paciente.Id, ProfissionalId = profissional.Id, Corpo = longo }, "qa.sintetico");
        File.WriteAllBytes(Path.Combine(Saida, "receituario-longo.pdf"), await scope.ServiceProvider.GetRequiredService<DocumentosClinicosPdfService>().GerarAsync(documento.Id, null));

        var infusao = new PrescricaoInternaEdicaoViewModel(factory, dialogo, paciente.Id, paciente.Nome, profissional.Id);
        await Esperar(() => !infusao.Ocupado);
        Exigir(infusao.Itens.Single().Diluente == "SF 0,9%" && infusao.Itens.Single().TempoInfusao == "1 hora", "Padrões ausentes.");
        infusao.Itens[0].Descricao = "Prescrição demonstrativa da infusão.\nTexto livre escrito pelo profissional, com as orientações necessárias.";
        await Desenhar(new PrescricaoInternaWindow(infusao), "infusao.png", 980, 720);
        var servico = scope.ServiceProvider.GetRequiredService<PrescricaoInternaService>();
        var prescricao = await servico.CriarAsync(paciente.Id, profissional.Id);
        await servico.SalvarRascunhoAsync(prescricao.Id, "Documento de demonstração", null,
            [new ItemPrescricaoInterna { Descricao = longo, Diluente = "SF 0,9%", TempoInfusao = "1 hora" }]);
        File.WriteAllBytes(Path.Combine(Saida, "infusao-longa.pdf"), await scope.ServiceProvider.GetRequiredService<PrescricaoInternaPdfService>().GerarPrescricaoAsync(prescricao.Id));
        var linhaAntiga = LinhaItemPrescricao.De(new ItemPrescricaoInterna { Descricao = "Anterior", Diluente = null, TempoInfusao = "45 minutos" });
        Exigir(linhaAntiga.Diluente is null && linhaAntiga.TempoInfusao == "45 minutos", "Reabertura alterou prescrição antiga.");

        var folha = new FolhaDaSessaoViewModel(factory) { Paciente = paciente.Nome, SemPaciente = false };
        folha.Anteriores.Add(Clinica.Application.Modelos.ResumoSessaoAnterior.De(new Evolucao { Id = 91, PacienteId = paciente.Id, Data = new DateOnly(2026,9,14), TextoEvolucao = "Evolução anterior demonstrativa." }));
        folha.SessaoParaReutilizar = folha.Anteriores[0];
        folha.ReutilizarSessaoCommand.Execute(null);
        Exigir(folha.TextoEvolucao == "Evolução anterior demonstrativa.", "Não reutilizou evolução.");
        folha.TextoEvolucao = "Texto atual preservado."; folha.ReutilizarSessaoCommand.Execute(null);
        Exigir(folha.TextoEvolucao == "Texto atual preservado.", "Reutilizar sobrescreveu texto atual.");
        folha.Anteriores.Clear(); Exigir(folha.SessaoParaReutilizar is null, "Seleção atravessou troca de contexto.");
        var folhaView = new FolhaDaSessaoView { DataContext = folha };
        await Desenhar(new Window { Content = folhaView }, "evolucao.png", 1050, 760);
    }

    static async Task Esperar(Func<bool> condicao)
    {
        for (var i=0; i<500 && !condicao(); i++) await Task.Delay(20);
        Exigir(condicao(), "Tempo excedido ao carregar a tela.");
    }

    static async Task Desenhar(Window window, string nome, int w, int h)
    {
        var root = (FrameworkElement)window.Content;
        if (window.DataContext is not null) root.DataContext = window.DataContext;
        window.Content = null;
        var border = new Border { Background = window.Background ?? Brushes.White, Child = root, Width = w, Height = h };
        using var source = new HwndSource(new HwndSourceParameters("QA isolado") { Width = w, Height = h,
            WindowStyle = unchecked((int)0x80000000), PositionX = -30000, PositionY = -30000 });
        source.RootVisual = border;
        border.Measure(new Size(w, h)); border.Arrange(new Rect(0, 0, w, h)); border.UpdateLayout();
        await Task.Delay(350);
        var bmp = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(border); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bmp));
        using var output = File.Create(Path.Combine(Saida, nome)); encoder.Save(output);
        border.Child = null; window.Content = root;
    }
}

sealed class DialogoTeste : IDialogoService
{
    public int Perguntas; public string? Resposta;
    public string? PerguntarTexto(string titulo, string pergunta, string? textoInicial = null, bool obrigatorio = true) { Perguntas++; return Resposta; }
    public bool Confirmar(string titulo, string mensagem) => false;
    public bool ConfirmarPerigo(string titulo, string mensagem) => false;
    public void Aviso(string titulo, string mensagem) { }
}
