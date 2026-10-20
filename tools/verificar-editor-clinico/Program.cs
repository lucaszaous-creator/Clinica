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
using Clinica.Clinico.Modulo;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Desktop.Shell.Modulos;
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
            try { await ConferirAsync(); Console.WriteLine("QA OK: texto anterior, infusão e PDFs."); }
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
        services.AddSingleton<ISnackbarService, SnackbarService>();
        new ModuloClinico().Registrar(services);
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
        // A folha de emissão de documento virou Blazor (DocumentoForm, out/2026): as conferências
        // dela (sugestões, endereço, alergia, modelos) estão nos testes bUnit do dotnet test.
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

        await ConferirMapaEFinalizacao(provider, scope.ServiceProvider, factory, paciente, profissional);
    }

    static async Task ConferirMapaEFinalizacao(IServiceProvider provider, IServiceProvider servicos,
        IServiceScopeFactory factory, Paciente paciente, Profissional profissional)
    {
        var prontuario = servicos.GetRequiredService<ProntuarioService>();
        var mapas = servicos.GetRequiredService<MapaCorporalService>();
        var dia = DateOnly.FromDateTime(DateTime.Today);
        var anterior = await prontuario.SalvarAsync(new Evolucao { PacienteId = paciente.Id,
            ProfissionalId = profissional.Id, Data = dia.AddDays(-2), TextoEvolucao = "Sessão anterior demonstrativa." });
        await mapas.SalvarAsync(anterior.Id,
            [new PontoMapa { Face = FaceCorpo.Frente, X = .3, Y = .3, Nome = "P1" },
             new PontoMapa { Face = FaceCorpo.Costas, X = .6, Y = .45, Nome = "P2", Tecnica = TecnicaPonto.Moxa }],
            "Observações demonstrativas da equipe.");
        var mapa = new MapaCorporalViewModel(factory, paciente.Id, null, dia);
        await mapa.CarregarAsync();
        Exigir(mapa.PodeEditar && mapa.SessoesAnteriores.Count == 1, "Histórico do mapa não carregado.");
        await Desenhar(new MapaCorporalWindow(mapa, "Mapa corporal — paciente demonstrativo"), "mapa-vazio.png", 1140, 720);
        await mapa.CopiarSessaoCommand.ExecuteAsync(null);
        Exigir(mapa.Pontos.Count == 2 && mapa.Observacoes == "Observações demonstrativas da equipe.", "Cópia incompleta do mapa.");
        mapa.Pontos[0].Nome = "P1 ajustado";
        mapa.NomeProximoPonto = "P3"; mapa.Marcar(FaceCorpo.Frente, .55, .7);
        mapa.NomeDoModelo = "Modelo demonstrativo da equipe"; mapa.ProtocoloDaClinica = true;
        await mapa.SalvarComoProtocoloCommand.ExecuteAsync(null);
        Exigir(mapa.Protocolos.Count == 1, "Modelo com pontos não foi cadastrado.");
        mapa.LimparCommand.Execute(null); mapa.DesfazerCommand.Execute(null);
        Exigir(mapa.Pontos.Count == 3, "Desfazer não recuperou os pontos.");
        var window = new MapaCorporalWindow(mapa, "Mapa corporal — paciente demonstrativo");
        await Desenhar(window, "mapa-preenchido.png", 1140, 720);
        await Desenhar(window, "mapa-compacto.png", 920, 620);
        foreach (var expander in Descendentes<Expander>((DependencyObject)window.Content))
            expander.IsExpanded = expander.Header?.ToString()?.Contains("modelo") == true;
        await Desenhar(window, "mapa-modelos.png", 1140, 720);
        mapa.LimparCommand.Execute(null); mapa.Observacoes = "Alteração descartada";
        typeof(MapaCorporalWindow).GetMethod("OnClosing", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(window, [new System.ComponentModel.CancelEventArgs()]);
        Exigir(mapa.Pontos.Count == 3 && mapa.Observacoes == "Observações demonstrativas da equipe.", "Descartar não restaurou o mapa.");
        Exigir((await mapas.DaEvolucaoAsync(anterior.Id))!.Pontos.Count == 2, "Editar cópia alterou a sessão anterior.");

        // A finalização da sessão é da tela do paciente Blazor (out/2026), conferida no bUnit.
        Console.WriteLine("QA MAPA OK: modelo, cópia, desfazer e descarte.");
    }

    static IEnumerable<T> Descendentes<T>(DependencyObject raiz) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(raiz); i++)
        {
            var filho = VisualTreeHelper.GetChild(raiz, i);
            if (filho is T encontrado) yield return encontrado;
            foreach (var descendente in Descendentes<T>(filho)) yield return descendente;
        }
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
