using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Diagnostics;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain.Regras;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Desktop.Controls;
using Clinica.Domain.Entities;
using Clinica.Domain;
using Clinica.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using R = Clinica.Recepcao.Modulo.ModuloRecepcao;
using C = Clinica.Clinico.Modulo.ModuloClinico;
using F = Clinica.Financeiro.Modulo.ModuloFinanceiro;
using B = Clinica.Faturamento.Modulo.ModuloFaturamento;
using G = Clinica.Gerente.Modulo.ModuloGerente;

internal static partial class Program
{
    static readonly List<object> Evidencias = [];
    static readonly List<string> Falhas = [];
    static readonly List<ServiceProvider> Provedores = [];
    static string Saida = "";
    [STAThread] public static int Main(string[] args)
    {
        Saida = Path.GetFullPath(args.Length > 0 ? args[0] : "evidencias-desktop");
        Directory.CreateDirectory(Saida);
        using var bindings = new StreamWriter(Path.Combine(Saida,"bindings.log"));
        PresentationTraceSources.DataBindingSource.Listeners.Add(new TextWriterTraceListener(bindings));
        PresentationTraceSources.DataBindingSource.Switch.Level=SourceLevels.Error;
        var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Clinica.Desktop.Shell;component/Styles/Suite.xaml", UriKind.Relative) });
        app.DispatcherUnhandledException += (_, e) => { Falhas.Add("Dispatcher: " + e.Exception); e.Handled = true; };
        app.Startup += async (_, _) =>
        {
            try { await VerificarAsync(); } catch(Exception ex) { Falhas.Add(ex.ToString()); }
            // Deixa os navegadores das janelas fechadas terminarem antes de descartar os provedores.
            await Task.Delay(1000); await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            foreach (var p in Provedores) await p.DisposeAsync();
            File.WriteAllText(Path.Combine(Saida,"resultado.json"), JsonSerializer.Serialize(new { evidencias = Evidencias, falhas = Falhas }, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"VERIFICACOES={Evidencias.Count(e => e.GetType().GetProperty("teste") is not null)} FALHAS={Falhas.Count}");
            foreach(var f in Falhas) Console.WriteLine(f);
            PresentationTraceSources.DataBindingSource.Flush();
            app.Shutdown(Falhas.Count == 0 ? 0 : 1);
        };
        return app.Run();
    }
    static void Conferir(bool condicao, string caso) { if (!condicao) Falhas.Add(caso); else Evidencias.Add(new { teste=caso, passou=true }); }
    static async Task VerificarAsync()
    {
        using var conn = new SqliteConnection("Data Source=:memory:"); conn.Open();
        var sentinela=new SentinelaProntuario();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(conn).AddInterceptors(sentinela).Options;
        using(var db = new ClinicaDbContext(options))
        {
            db.Database.EnsureCreated();
            db.Pacientes.Add(new Paciente { Id=1001, Nome="Paciente de demonstração", Documento="52998224725", Endereco="Rua de demonstração, 100 — Macaé/RJ", Sexo=Sexo.Feminino, Convenio=Convenio.UnimedPadrao });
            db.SaveChanges();
        }
        var composicoes = new (string Nome, PerfilAcesso Perfil, IModuloApp[] Modulos)[] {
            ("Recepção", PerfilAcesso.Recepcao, [new R(), new ModuloContextual(new C())]),
            ("Consultório", PerfilAcesso.Profissional, [new C(),new ModuloContextual(new R())]),
            ("Enfermagem", PerfilAcesso.Enfermagem, [new C(),new ModuloContextual(new R())]),
            ("Financeiro", PerfilAcesso.Financeiro,[new F(),new ModuloContextual(new R()),new ModuloContextual(new C())]),
            ("Faturamento", PerfilAcesso.Faturista,[new Clinica.Desktop.ModuloFaturamentoAplicativo()]),
            ("Faturamento — gerente", PerfilAcesso.Gerente,[new Clinica.Desktop.ModuloFaturamentoAplicativo()]),
            ("Gerente", PerfilAcesso.Gerente,[new R(),new C(),new F(),new B(),new G()])
        };
        foreach(var (nome,perfil,modulos) in composicoes)
        {
            var s = new ServiceCollection(); s.AddClinica("Host=127.0.0.1;Port=1;Database=never_used");
            s.AddScoped<ClinicaDbContext>(_ => new ClinicaDbContext(options));
            s.AddSingleton<SessaoUsuario>(); s.AddSingleton<SnackbarService>();
            s.AddSingleton<ISnackbarService>(sp => sp.GetRequiredService<SnackbarService>()); s.AddSingleton<IDialogoService,DialogoService>();
            foreach(var modulo in modulos) modulo.Registrar(s);
            // O provedor vive até o FIM da verificação, como vive o app inteiro na clínica: o
            // navegador de uma janela já fechada ainda pode se ligar à página depois, e descartar o
            // provedor no fim de cada perfil dava ObjectDisposedException em AttachToPageAsync.
            var provider = s.BuildServiceProvider(); Provedores.Add(provider);
            provider.GetRequiredService<SessaoUsuario>().Entrar(new UsuarioSistema { Id=1,Nome="Acesso de demonstração",Perfil=perfil });
            using(var inicializacao=provider.CreateScope())
                await inicializacao.ServiceProvider.GetRequiredService<Clinica.Application.Servicos.ConvenioCatalogoService>().RecarregarCacheAsync();
            var shell = new ShellViewModel(nome,modulos,provider);
            Evidencias.Add(new { perfil=nome, destinos=shell.Itens.Select(i=>new { i.Chave,i.Rotulo,i.Grupo,i.Abas }).ToArray() });
            var visiveis = shell.Grupos.SelectMany(g => g.Itens).ToList();
            if (nome.StartsWith("Faturamento"))
            {
                Conferir(visiveis.Count == 2 && visiveis.Any(i => i.Chave == ChavesSuite.FaturamentoTiss)
                    && visiveis.Any(i => i.Chave == ChavesSuite.RetornoPacientes),
                    $"{nome}: guias e acompanhamento compartilhado no menu");
                Conferir(shell.TituloTela == "Faturamento de guias" && shell.TelaAtual is Clinica.Desktop.Shell.Componentes.TelaComAbas,
                    $"{nome}: abertura direta no faturamento");
                var composta = (Clinica.Desktop.Shell.Componentes.TelaComAbas)shell.TelaAtual!;
                var esperadas = new[] { "Resumo", "Pendências e baixas", "Consultar guias", "Faturados", "Glosas e recursos", "Não conformidades", "Lotes e XML TISS" };
                // Tendências (out/2026) é aba do Faturamento para quem vê guias; Relatórios e Regras
                // continuam só para quem tem os bits deles. A ordem é a dos destinos do módulo.
                esperadas = perfil == PerfilAcesso.Gerente
                    ? esperadas.Concat(new[] { "Relatórios de guias", "Tendências", "Regras e catálogos TISS" }).ToArray()
                    : esperadas.Concat(new[] { "Tendências" }).ToArray();
                // A régua é desenhada pela moldura Blazor desde out/2026; o estado dela é o TelaComAbas.
                Conferir(composta.Rotulos.SequenceEqual(esperadas),
                    $"{nome}: subabas de guias preservadas conforme permissão");
                // A aba materializa o DESCRITOR da tela (o navegador é o da janela).
                Conferir(composta.Selecionada == 0 && composta.ConteudoVisivel is Clinica.Desktop.Blazor.TelaBlazor,
                    $"{nome}: Resumo materializa a tela de faturamento");
                foreach (var chave in new[] { "agenda", "fila", ChavesSuite.AgendaRecepcao, ChavesSuite.ConsultorioSemana,
                    "agenda-confirmacoes", "retornos-a-marcar", "marcar-horario", "pacientes", "acessos", "configuracoes" })
                {
                    var telaAntes = shell.TelaAtual;
                    Conferir(!NavegacaoSuite.Existe(chave) && !NavegacaoSuite.Ir(chave) && ReferenceEquals(telaAntes, shell.TelaAtual),
                        $"{nome}: rota alheia recusada {chave}");
                    Conferir(modulos[0].CriarTela(chave, provider) is null, $"{nome}: fábrica recusa {chave}");
                }
                foreach (var termo in new[] { "Agenda", "Confirmações", "Retornos", "Acessos" })
                {
                    shell.TextoPesquisa = termo;
                    Conferir(shell.ResultadosPesquisa.Count == 0, $"{nome}: busca não oferece {termo}");
                }
                shell.TextoPesquisa = "";
                if (perfil == PerfilAcesso.Faturista)
                {
                    var janelaFaturamento = new JanelaDoApp(provider, "Faturamento — Clínica SemDor") { Width = 1366, Height = 820,
                        ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual, Left = -30000, Top = -30000 };
                    janelaFaturamento.MostrarMoldura(shell);
                    janelaFaturamento.Show(); await Task.Delay(250); Render(janelaFaturamento, "faturamento-exclusivo.png"); janelaFaturamento.Close();
                }
            }
            Conferir(visiveis.Select(i=>i.Rotulo).Distinct().Count()==visiveis.Count,$"{nome}: sem rótulos principais duplicados");
            Conferir(!NavegacaoSuite.Existe("destino-inexistente"),$"{nome}: destino inexistente recusado");
            if (!SessaoUsuario.Atual.Pode(Permissao.GerenciarUsuarios)) Conferir(!NavegacaoSuite.Existe("acessos"),$"{nome}: acesso proibido não resolve como aba vizinha");
            foreach(var item in shell.Itens.Where(i=>i.Abas.Count==0).ToArray())
            {
                try
                {
                    // O treinamento pertence ao shell compartilhado, não a uma fábrica de módulo.
                    if (item.Chave == "treinamento")
                    {
                        var anterior = shell.TelaAtual;
                        Conferir(NavegacaoSuite.Ir(item.Chave), $"{nome}: treinamento acessível pela navegação");
                        // Blazor desde out/2026: o treinamento é a tela Treinamento no navegador da janela.
                        Conferir(shell.TelaAtual is ITelaDoNavegador { Componente.Name: "Treinamento" },
                            $"{nome}: treinamento materializa a biblioteca compartilhada");
                        await Dispatcher.Yield(DispatcherPriority.Background);
                        Conferir(NavegacaoSuite.Voltar() && ReferenceEquals(anterior, shell.TelaAtual),
                            $"{nome}: sair do treinamento preserva a tela de origem");
                        continue;
                    }
                    var dono = modulos.First(m=>m.Nome==item.ModuloNome);
                    var tela = dono.CriarTela(item.Chave,provider);
                    // Toda rota devolve uma tela do NAVEGADOR (out/2026: a moldura é Blazor).
                    Conferir(tela is ITelaDoNavegador,$"{nome}: rota {item.Chave} materializa componente");
                    if(tela is FrameworkElement el) { el.Measure(new Size(1366,728)); el.Arrange(new Rect(0,0,1366,728)); el.UpdateLayout(); }
                    await Dispatcher.Yield(DispatcherPriority.Background);
                }
                catch(Exception ex) { Falhas.Add($"{nome}/{item.Chave}: {ex}"); }
            }
            // A ficha do paciente é a tela Blazor (out/2026): conferida no bUnit (TelasBlazorAbremTests).
            if(nome=="Gerente")
            {
                foreach(var chave in new[]{"pacientes-recepcao","agenda","faturamento-gerencial"})
                { Conferir(NavegacaoSuite.Ir(chave), "Gerente: abre " + chave); await Dispatcher.Yield(DispatcherPriority.Background); if(shell.TelaAtual is FrameworkElement v) await FotoAsync(v,chave+".png"); }
                Conferir(NavegacaoSuite.Existe("marcar-horario"),"Gerente: Marcar atendimento preservado");
                var anterior=shell.TelaAtual;
                NavegacaoSuite.Ir("configuracoes");
                Conferir(NavegacaoSuite.Voltar() && ReferenceEquals(anterior,shell.TelaAtual),"Voltar preserva instância e filtros da origem");
                // O cadastro do paciente é o CadastroPacienteForm Blazor desde out/2026 (a
                // CadastroPacienteWindow WPF saiu): o leiaute sai da prévia (tools/PreviaBlazor) e o
                // TelasBlazorAbremTests o abre e clica. Aqui ficam as REGRAS, pelo mesmo caminho do formulário.
                await VerificarCadastroAsync(provider,options,sentinela);
                var shellWindow=new JanelaDoApp(provider,"Gerente Geral — Clínica SemDor") { ShowInTaskbar=false,ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-30000,Top=-30000,Width=1366,Height=768 };
                shellWindow.MostrarMoldura(shell);
                shellWindow.Show();
                foreach(var chave in new[]{"agenda","marcar-horario","pacientes-recepcao","faturamento-gerencial","configuracoes"}) {
                    NavegacaoSuite.Ir(chave);await Task.Delay(160);Render(shellWindow,"shell-"+chave+".png");
                }
                shellWindow.Close();
                using var scopeConfiguracao=provider.CreateScope();
                var parametros=scopeConfiguracao.ServiceProvider.GetRequiredService<Clinica.Application.Servicos.ParametrosService>();
                // Os dados da clínica são da tela Blazor de Configurações desde out/2026 (o
                // DadosClinicaViewModel WPF saiu): ela salva pelo método que relê e mescla, e a
                // conferência é a mesma — dados lidos ANTES de outro acesso mudar os TUSS.
                var clinica=Clinica.Application.Servicos.DadosDaClinica.De(await parametros.ObterPrestadorAsync());
                var prestador=await parametros.ObterPrestadorAsync();prestador.CodigosTuss[TipoCodigo.Acupuntura]="12345678";await parametros.SalvarPrestadorAsync(prestador);
                await Clinica.Application.Servicos.ConfiguracoesFaturamentoNaTela.SalvarDadosDaClinicaAsync(parametros,clinica with { NomeFantasia="Clínica demonstrativa" });
                Conferir((await parametros.ObterPrestadorAsync()).NomeFantasia=="Clínica demonstrativa" && (await parametros.ObterPrestadorAsync()).CodigoTuss(TipoCodigo.Acupuntura)=="12345678","Salvar dados da clínica preserva TUSS atualizado em outro acesso");
                // A tela Blazor de Configurações salva os TUSS pelo MESMO método (relê e mescla).
                prestador=await parametros.ObterPrestadorAsync();prestador.ChavePix="pix-ficticio";await parametros.SalvarPrestadorAsync(prestador);
                await Clinica.Application.Servicos.ConfiguracoesFaturamentoNaTela.SalvarCodigosTussAsync(parametros,
                    new Dictionary<TipoCodigo,string?>{[TipoCodigo.Acupuntura]="87654321"});
                Conferir((await parametros.ObterPrestadorAsync()).CodigoTuss(TipoCodigo.Acupuntura)=="87654321" && (await parametros.ObterPrestadorAsync()).ChavePix=="pix-ficticio","Salvar catálogo TISS preserva Pix atualizado em outro acesso");

            }
            await Dispatcher.Yield(DispatcherPriority.Background);
        }
    }
    /// <summary>
    /// As regras do cadastro do paciente pelo MESMO caminho do <c>CadastroPacienteForm</c>: a
    /// conferência do Salvar (<see cref="CadastroPacienteNaTela"/>) e a gravação pelo
    /// <see cref="PacienteService"/> (ObterAsync → Aplicar → SalvarNovoAsync/AtualizarAsync).
    /// </summary>
    static async Task VerificarCadastroAsync(ServiceProvider provider, DbContextOptions<ClinicaDbContext> options,
        SentinelaProntuario sentinela)
    {
        var convenios=CadastroPacienteNaTela.Convenios(CatalogoConvenios.Ativos);
        var modalidades=CatalogoModalidades.Ativas;
        var opcao=convenios.Single(c=>c.Codigo=="UnimedPadrao");

        // A carga do cadastro lê só a ficha: nem atendimentos, nem códigos, nem prontuário.
        sentinela.Proibir=true;sentinela.ProibirHistoricoCadastro=true;sentinela.Leituras.Clear();
        Paciente? existente;
        using(var escopo=provider.CreateScope())
            existente=await escopo.ServiceProvider.GetRequiredService<PacienteService>().ObterAsync(1001);
        Conferir(sentinela.Leituras.Count==0,"Cadastro carrega somente a ficha, sem atendimentos, códigos ou prontuário");
        sentinela.Proibir=false;sentinela.ProibirHistoricoCadastro=false;
        var dados=CadastroPacienteNaTela.Da(existente!,convenios,modalidades);
        Conferir(dados.Nome=="Paciente de demonstração","Reabrir o cadastro traz os dados existentes");

        dados.Convenio=opcao;
        dados.Documento="";
        Conferir(CadastroPacienteNaTela.Conferir(dados) is { Valido: false, ErroDocumento: not null },"Cadastro exige CPF antes de gravar");
        dados.Documento="52998224725";dados.Endereco="";
        var semEndereco=CadastroPacienteNaTela.Conferir(dados);
        Conferir(semEndereco.Valido && semEndereco.ErroEndereco is null,"Cadastro salva edição sem exigir endereço");
        await GravarCadastroAsync(provider,dados,1001);
        using(var conferirCadastro=new ClinicaDbContext(options))
            Conferir((await conferirCadastro.Pacientes.FindAsync(1001))!.Endereco is null,"Edição sem endereço persiste campo nulo");

        var novo=CadastroPacienteNaTela.Novo(modalidades);
        novo.Nome="Cadastro de teste isolado";novo.Documento="111.444.777-35";novo.Endereco=null;
        novo.Convenio=opcao;novo.Email="cadastro@example.com";novo.Telefone="22999990000";
        novo.DataNascimento=new DateOnly(1990,1,1);novo.Carteirinha="DEMO";novo.ValidadeCarteirinha=new DateOnly(2030,1,1);
        novo.Origem=OrigemPaciente.Indicacao;novo.IndicadoPor="Indicação fictícia";novo.Observacoes="Observação fictícia";
        Conferir(CadastroPacienteNaTela.Conferir(novo).Valido,"Cadastro novo completo passa na conferência do Salvar");
        var novoId=await GravarCadastroAsync(provider,novo,null);
        Conferir(novoId>0,"Cadastro novo grava e devolve a identidade do paciente");
        using(var db=new ClinicaDbContext(options)) {
            var p=await db.Pacientes.AsNoTracking().SingleOrDefaultAsync(x=>x.Documento=="11144477735");
            Conferir(p is not null && p.Endereco==novo.Endereco && p.Email==novo.Email && p.Telefone==novo.Telefone
                && p.DataNascimento==new DateOnly(1990,1,1) && p.Carteirinha==novo.Carteirinha
                && p.ValidadeCarteirinha==new DateOnly(2030,1,1) && p.Origem==novo.Origem
                && p.IndicadoPor==novo.IndicadoPor && p.Observacoes==novo.Observacoes,"Gravação preserva identificação, contato, endereço, convênio e origem");
        }
        Paciente? relido;
        using(var escopo=provider.CreateScope())
            relido=await escopo.ServiceProvider.GetRequiredService<PacienteService>().ObterAsync(novoId);
        var editar=CadastroPacienteNaTela.Da(relido!,convenios,modalidades);
        Conferir(editar.Endereco==novo.Endereco && editar.IndicadoPor==novo.IndicadoPor && editar.Observacoes==novo.Observacoes,"Reabrir cadastro recupera os campos gravados");
        editar.Observacoes="Observação alterada";
        await GravarCadastroAsync(provider,editar,novoId);
        using(var db=new ClinicaDbContext(options)) {
            var registros=await db.Pacientes.AsNoTracking().Where(x=>x.Documento=="11144477735").ToListAsync();
            Conferir(registros.Count==1 && registros[0].Observacoes=="Observação alterada" && registros[0].Carteirinha=="DEMO","Editar mantém a mesma ficha e preserva campos não alterados");
        }
    }
    /// <summary>O Salvar do <c>CadastroPacienteForm</c>: lê a ficha (ou cria), aplica o formulário e grava.</summary>
    static async Task<int> GravarCadastroAsync(ServiceProvider provider, DadosDoCadastro dados, int? id)
    {
        using var escopo=provider.CreateScope();
        var pacientes=escopo.ServiceProvider.GetRequiredService<PacienteService>();
        var paciente=id is { } existente
            ? await pacientes.ObterAsync(existente) ?? throw new InvalidOperationException("Paciente não encontrado.")
            : new Paciente();
        CadastroPacienteNaTela.Aplicar(dados,paciente);
        if(id is null) await pacientes.SalvarNovoAsync(paciente,dados.CategoriaManual);
        else await pacientes.AtualizarAsync(paciente,dados.CategoriaManual);
        return paciente.Id;
    }
    static async Task FotoAsync(FrameworkElement tela,string nome)
    {
        var janela=new Window { Content=tela,ShowInTaskbar=false,ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-30000,Top=-30000,Width=1366,Height=768 };
        janela.Show();await Task.Delay(140);Render(janela,nome);janela.Content=null;janela.Close();
    }
    static void Render(FrameworkElement tela,string nome)
    {
        tela.UpdateLayout();
        var dpi=VisualTreeHelper.GetDpi(tela);
        double largura=tela.ActualWidth,altura=tela.ActualHeight;
        if(tela is Window w && w.Content is FrameworkElement raiz) { largura=raiz.ActualWidth+raiz.Margin.Left+raiz.Margin.Right;altura=raiz.ActualHeight+raiz.Margin.Top+raiz.Margin.Bottom; }
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(largura*dpi.DpiScaleX),(int)Math.Ceiling(altura*dpi.DpiScaleY),dpi.PixelsPerInchX,dpi.PixelsPerInchY,PixelFormats.Pbgra32);
        bitmap.Render(tela);
        var png = new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using var arquivo=File.Create(Path.Combine(Saida,nome));png.Save(arquivo);
    }
}

sealed class SentinelaProntuario : DbCommandInterceptor
{
    public bool Proibir { get; set; }
    public bool FalharPaciente { get; set; }
    public bool AtrasarPaciente { get; set; }
    public bool ProibirHistoricoCadastro { get; set; }
    public List<string> Leituras { get; } = [];
    void Conferir(DbCommand c) {
        if (FalharPaciente && c.CommandText.Contains("FROM \"Pacientes\"")) throw new InvalidOperationException("Falha de leitura simulada");
        if((Proibir && new[]{"Evolucoes","ProblemasPaciente","Hipoteses"}.Any(t=>c.CommandText.Contains(t,StringComparison.OrdinalIgnoreCase)))
            || (ProibirHistoricoCadastro && new[]{"Atendimentos","Codigos"}.Any(t=>c.CommandText.Contains(t,StringComparison.OrdinalIgnoreCase)))) Leituras.Add(c.CommandText);
    }
    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand c,CommandEventData d,InterceptionResult<DbDataReader> result) { Conferir(c);return result; }
    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand c,CommandEventData d,InterceptionResult<DbDataReader> result,CancellationToken ct=default) { Conferir(c);if(AtrasarPaciente && c.CommandText.Contains("Pacientes")) await Task.Delay(150,ct);return result; }
}
