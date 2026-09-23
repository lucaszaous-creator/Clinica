using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Diagnostics;
using Clinica.Desktop.Shell.Componentes.Cadastro;
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

internal static class Program
{
    static readonly List<object> Evidencias = [];
    static readonly List<string> Falhas = [];
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
            ("Faturamento", PerfilAcesso.Faturista,[new B(),new ModuloContextual(new R(),"agenda","pacientes","ajuda"),new ModuloContextual(new C()),new ModuloContextual(new G(),"acessos","configuracoes")]),
            ("Gerente", PerfilAcesso.Gerente,[new R(),new C(),new F(),new B(),new G()])
        };
        foreach(var (nome,perfil,modulos) in composicoes)
        {
            var s = new ServiceCollection(); s.AddClinica("Host=127.0.0.1;Port=1;Database=never_used");
            s.AddScoped<ClinicaDbContext>(_ => new ClinicaDbContext(options));
            s.AddSingleton<SessaoUsuario>(); s.AddSingleton<SnackbarService>();
            s.AddSingleton<ISnackbarService>(sp => sp.GetRequiredService<SnackbarService>()); s.AddSingleton<IDialogoService,DialogoService>();
            foreach(var modulo in modulos) modulo.Registrar(s);
            using var provider = s.BuildServiceProvider();
            provider.GetRequiredService<SessaoUsuario>().Entrar(new UsuarioSistema { Id=1,Nome="Acesso de demonstração",Perfil=perfil });
            using(var inicializacao=provider.CreateScope())
                await inicializacao.ServiceProvider.GetRequiredService<Clinica.Application.Servicos.ConvenioCatalogoService>().RecarregarCacheAsync();
            var shell = new ShellViewModel(nome,modulos,provider);
            Evidencias.Add(new { perfil=nome, destinos=shell.Itens.Select(i=>new { i.Chave,i.Rotulo,i.Grupo,i.Abas }).ToArray() });
            var visiveis = shell.Grupos.SelectMany(g => g.Itens).ToList();
            Conferir(visiveis.Select(i=>i.Rotulo).Distinct().Count()==visiveis.Count,$"{nome}: sem rótulos principais duplicados");
            Conferir(!NavegacaoSuite.Existe("destino-inexistente"),$"{nome}: destino inexistente recusado");
            if (!SessaoUsuario.Atual.Pode(Permissao.GerenciarUsuarios)) Conferir(!NavegacaoSuite.Existe("acessos"),$"{nome}: acesso proibido não resolve como aba vizinha");
            foreach(var item in shell.Itens.Where(i=>i.Abas.Count==0).ToArray())
            {
                try
                {
                    var dono = modulos.First(m=>m.Nome==item.ModuloNome);
                    var tela = dono.CriarTela(item.Chave,provider);
                    Conferir(tela is FrameworkElement,$"{nome}: rota {item.Chave} materializa componente");
                    if(tela is FrameworkElement el) { el.Measure(new Size(1366,728)); el.Arrange(new Rect(0,0,1366,728)); el.UpdateLayout(); }
                    await Dispatcher.Yield(DispatcherPriority.Background);
                }
                catch(Exception ex) { Falhas.Add($"{nome}/{item.Chave}: {ex}"); }
            }
            if(SessaoUsuario.Atual.Pode(Permissao.VerFichaPaciente))
            {
                var foco = new PacienteEmFoco(); foco.Definir(1001,"Paciente de demonstração");
                sentinela.Proibir = !SessaoUsuario.Atual.Pode(Permissao.VerProntuario);
                sentinela.Leituras.Clear();
                var ficha = provider.GetRequiredService<IFabricaFichaPaciente>().Criar(foco);
                Conferir(ficha.GetType().Name == "PacienteWorkspaceView",$"{nome}: ficha canônica");
                var vm = (Clinica.Clinico.ViewModels.PacienteWorkspaceViewModel)ficha.DataContext;
                var outro = new PacienteEmFoco(); outro.Definir(1002,"Outro paciente fictício");
                Conferir(!vm.Corresponde(outro),$"{nome}: impede reaproveitar ficha de outro paciente");
                await vm.Capa.CarregarAsync();
                if(sentinela.Proibir) Conferir(sentinela.Leituras.Count==0,$"{nome}: ficha administrativa não consulta prontuário");
                sentinela.Proibir=false;
                if(nome=="Gerente") {
                    var editorOriginal=vm.Atendimento;
                    using(var dbAtualizacao=new ClinicaDbContext(options)) { var p=await dbAtualizacao.Pacientes.FindAsync(1001);p!.Nome="Paciente demonstração atualizado";p.Endereco="Endereço atualizado fictício";await dbAtualizacao.SaveChangesAsync(); }
                    await vm.AtualizarCadastroAsync();
                    Conferir(vm.Paciente=="Paciente demonstração atualizado" && vm.Capa.Endereco=="Endereço atualizado fictício" && ReferenceEquals(vm.Atendimento,editorOriginal),"Editar cadastro atualiza ficha sem substituir editor clínico");
                    using(var dbAtualizacao=new ClinicaDbContext(options)) { var p=await dbAtualizacao.Pacientes.FindAsync(1001);p!.Nome="Paciente de demonstração";p.Endereco="Rua de demonstração, 100 — Macaé/RJ";await dbAtualizacao.SaveChangesAsync(); }
                    await vm.AtualizarCadastroAsync();
                    await FotoAsync(ficha,"ficha-unificada.png");
                    var admin=(FrameworkElement)vm.Administrativo!.Convenio;
                    await FotoAsync(admin,"autorizacoes.png");
                }
            }
            if(nome=="Gerente")
            {
                foreach(var chave in new[]{"pacientes-recepcao","agenda","faturamento-gerencial"})
                { Conferir(NavegacaoSuite.Ir(chave), "Gerente: abre " + chave); await Dispatcher.Yield(DispatcherPriority.Background); if(shell.TelaAtual is FrameworkElement v) await FotoAsync(v,chave+".png"); }
                Conferir(NavegacaoSuite.Existe("marcar-horario"),"Gerente: Marcar atendimento preservado");
                var anterior=shell.TelaAtual;
                NavegacaoSuite.Ir("configuracoes");
                Conferir(NavegacaoSuite.Voltar() && ReferenceEquals(anterior,shell.TelaAtual),"Voltar preserva instância e filtros da origem");
                var cadastro=new CadastroPacienteViewModel(provider.GetRequiredService<IServiceScopeFactory>(),1001);
                while(cadastro.Carregando) await Task.Delay(10);
                var opcao=cadastro.Convenios.Single(c=>c.Codigo=="UnimedPadrao");
                cadastro.Convenio=opcao;
                cadastro.Documento="";await cadastro.SalvarCommand.ExecuteAsync(null);
                Conferir(!string.IsNullOrWhiteSpace(cadastro.ErroDocumento),"Cadastro exige CPF antes de gravar");
                cadastro.Documento="52998224725";cadastro.Endereco="";await cadastro.SalvarCommand.ExecuteAsync(null);
                Conferir(!string.IsNullOrWhiteSpace(cadastro.ErroEndereco),"Cadastro exige endereço antes de gravar");
                cadastro.Endereco="Rua de demonstração, 100 — Macaé/RJ";cadastro.Mensagem="";
                var janelaCadastro = new CadastroPacienteWindow(cadastro) { ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-30000,Top=-30000 };
                Conferir(janelaCadastro.WindowState==WindowState.Maximized,"Cadastro configurado para nascer maximizado");
                Conferir(double.IsPositiveInfinity(janelaCadastro.MaxWidth) && double.IsPositiveInfinity(janelaCadastro.MaxHeight),"Cadastro maximizado sem limites fixos do monitor primário");
                janelaCadastro.Show(); await Task.Delay(120);
                Conferir(janelaCadastro.WindowState==WindowState.Maximized,"Cadastro permanece maximizado após abrir");
                Render(janelaCadastro,"cadastro-real.png");
                await VerificarCadastroAsync(provider,options,sentinela,cadastro,janelaCadastro,opcao);
                janelaCadastro.Close();
                var shellWindow=new ShellWindow { DataContext=shell,ShowInTaskbar=false,ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-30000,Top=-30000,Width=1366,Height=768 };
                shellWindow.Show();
                foreach(var chave in new[]{"agenda","marcar-horario","pacientes-recepcao","faturamento-gerencial","configuracoes"}) {
                    NavegacaoSuite.Ir(chave);await Task.Delay(160);Render(shellWindow,"shell-"+chave+".png");
                }
                shellWindow.Close();
                using var scopeConfiguracao=provider.CreateScope();
                var parametros=scopeConfiguracao.ServiceProvider.GetRequiredService<Clinica.Application.Servicos.ParametrosService>();
                var clinica=new Clinica.Desktop.Shell.Componentes.DadosClinicaViewModel(provider.GetRequiredService<IServiceScopeFactory>(),provider.GetRequiredService<ISnackbarService>());
                await clinica.CarregarAsync();
                var prestador=await parametros.ObterPrestadorAsync();prestador.CodigosTuss[TipoCodigo.Acupuntura]="12345678";await parametros.SalvarPrestadorAsync(prestador);
                clinica.NomeFantasia="Clínica demonstrativa";await clinica.SalvarCommand.ExecuteAsync(null);
                Conferir((await parametros.ObterPrestadorAsync()).NomeFantasia=="Clínica demonstrativa" && (await parametros.ObterPrestadorAsync()).CodigoTuss(TipoCodigo.Acupuntura)=="12345678","Salvar dados da clínica preserva TUSS atualizado em outro acesso");
                var tiss=provider.GetRequiredService<Clinica.Desktop.ViewModels.ParametrosViewModel>();await tiss.CarregarAsync();
                prestador=await parametros.ObterPrestadorAsync();prestador.ChavePix="pix-ficticio";await parametros.SalvarPrestadorAsync(prestador);
                tiss.TussAcupuntura="87654321";await tiss.SalvarCommand.ExecuteAsync(null);
                Conferir((await parametros.ObterPrestadorAsync()).CodigoTuss(TipoCodigo.Acupuntura)=="87654321" && (await parametros.ObterPrestadorAsync()).ChavePix=="pix-ficticio","Salvar catálogo TISS preserva Pix atualizado em outro acesso");

            }
            await Dispatcher.Yield(DispatcherPriority.Background);
        }
    }
    static async Task VerificarCadastroAsync(ServiceProvider provider, DbContextOptions<ClinicaDbContext> options,
        SentinelaProntuario sentinela, CadastroPacienteViewModel cadastro, CadastroPacienteWindow janela,
        Clinica.Domain.Regras.OpcaoDeConvenio opcao)
    {
        var escopos=provider.GetRequiredService<IServiceScopeFactory>();
        sentinela.AtrasarPaciente=true;sentinela.Proibir=true;sentinela.ProibirHistoricoCadastro=true;sentinela.Leituras.Clear();
        var carregando=new CadastroPacienteViewModel(escopos,1001);
        Conferir(carregando.Carregando && !carregando.PodePreencher,"Carga lenta bloqueia preenchimento antes de apresentar o cadastro");
        await carregando.SalvarCommand.ExecuteAsync(null);
        Conferir(carregando.Mensagem.Contains("Aguarde"),"Salvar durante carga lenta não grava dados incompletos");
        await EsperarCadastroAsync(carregando);
        Conferir(carregando.PodePreencher && carregando.Nome=="Paciente de demonstração","Fim da carga libera formulário com os dados existentes");
        Conferir(sentinela.Leituras.Count==0,"Cadastro carrega somente a ficha, sem atendimentos, códigos ou prontuário");
        sentinela.Proibir=false;sentinela.ProibirHistoricoCadastro=false;sentinela.AtrasarPaciente=false;

        var corpo=(Grid)janela.Content;
        var rolagem=corpo.Children.OfType<ScrollViewer>().Single();
        foreach(var campo in new[]{"Telefone","Email","Carteirinha","IndicadoPor","Observacoes"}) {
            var editor=Visuais(corpo).OfType<TextBox>().Single(b=>b.GetBindingExpression(TextBox.TextProperty)?.ParentBinding.Path.Path==campo);
            var propriedade=typeof(CadastroPacienteViewModel).GetProperty(campo)!;
            var antes=propriedade.GetValue(cadastro);
            editor.SetCurrentValue(TextBox.TextProperty,"Texto de verificação");
            await Dispatcher.Yield(DispatcherPriority.DataBind);
            Conferir(Equals(propriedade.GetValue(cadastro),"Texto de verificação"),$"{campo}: texto atualizado antes de sair do campo para salvar pelo teclado");
            propriedade.SetValue(cadastro,antes);
        }
        var salvar=Visuais(corpo).OfType<Button>().Single(b=>Equals(b.Content,"Salvar cadastro"));
        var cancelar=Visuais(corpo).OfType<Button>().Single(b=>Equals(b.Content,"Cancelar"));
        janela.WindowState=WindowState.Normal;janela.Left=-30000;janela.Top=-30000;
        foreach(var dimensao in new[]{new Size(600,450),new Size(960,720),new Size(1366,768),new Size(1920,1080)})
        {
            janela.Width=dimensao.Width;janela.Height=dimensao.Height;
            await Dispatcher.Yield(DispatcherPriority.Render);janela.UpdateLayout();
            var area=new Rect(0,0,corpo.ActualWidth,corpo.ActualHeight);
            Conferir(area.Contains(salvar.TransformToAncestor(corpo).TransformBounds(new Rect(salvar.RenderSize)))
                && area.Contains(cancelar.TransformToAncestor(corpo).TransformBounds(new Rect(cancelar.RenderSize))),$"Cadastro {dimensao}: Salvar e Cancelar permanecem dentro da área útil");
            Conferir(rolagem.ScrollableWidth<1,$"Cadastro {dimensao}: conteúdo sem rolagem horizontal");
            rolagem.ScrollToEnd();await Dispatcher.Yield(DispatcherPriority.Render);
            var observacoes=Visuais(rolagem).OfType<TextBox>().Single(b=>b.GetBindingExpression(TextBox.TextProperty)?.ParentBinding.Path.Path=="Observacoes");
            var limite=observacoes.TransformToAncestor(rolagem).TransformBounds(new Rect(observacoes.RenderSize));
            Conferir(limite.Top>=-1 && limite.Bottom<=rolagem.ActualHeight+1,$"Cadastro {dimensao}: último campo acessível por rolagem");
            rolagem.ScrollToHome();
            if(dimensao.Width==1366) Render(janela,"cadastro-restaurado-1366.png");
        }
        cadastro.Salvando=true;await Dispatcher.Yield(DispatcherPriority.DataBind);
        Conferir(!salvar.IsEnabled && !cancelar.IsEnabled && !cadastro.PodePreencher,"Durante a gravação não permite editar, salvar novamente ou cancelar");
        janela.Close();Conferir(janela.IsVisible,"Fechar janela durante a gravação é recusado");
        cadastro.Salvando=false;

        var novo=new CadastroPacienteViewModel(escopos);await EsperarCadastroAsync(novo);
        novo.Nome="Cadastro de teste isolado";novo.Documento="111.444.777-35";novo.Endereco="Rua fictícia, 1, Centro, Macaé/RJ";
        novo.Convenio=novo.Convenios.Single(c=>c.Codigo==opcao.Codigo);novo.Email="cadastro@example.com";novo.Telefone="22999990000";
        novo.DataNascimento=new DateTime(1990,1,1);novo.Carteirinha="DEMO";novo.ValidadeCarteirinha=new DateTime(2030,1,1);
        novo.Origem=OrigemPaciente.Indicacao;novo.IndicadoPor="Indicação fictícia";novo.Observacoes="Observação fictícia";
        var concluidos=0;novo.Concluido+=()=>concluidos++;
        await novo.SalvarCommand.ExecuteAsync(null);
        Conferir(concluidos==1 && novo.PacienteId is >0,"Cadastro novo grava e devolve a identidade do paciente");
        using(var db=new ClinicaDbContext(options)) {
            var p=await db.Pacientes.AsNoTracking().SingleOrDefaultAsync(x=>x.Documento=="11144477735");
            Conferir(p is not null && p.Endereco==novo.Endereco && p.Email==novo.Email && p.Telefone==novo.Telefone
                && p.DataNascimento==new DateOnly(1990,1,1) && p.Carteirinha==novo.Carteirinha
                && p.ValidadeCarteirinha==new DateOnly(2030,1,1) && p.Origem==novo.Origem
                && p.IndicadoPor==novo.IndicadoPor && p.Observacoes==novo.Observacoes,"Gravação preserva identificação, contato, endereço, convênio e origem");
        }
        var editar=new CadastroPacienteViewModel(escopos,novo.PacienteId);await EsperarCadastroAsync(editar);
        Conferir(editar.Endereco==novo.Endereco && editar.IndicadoPor==novo.IndicadoPor && editar.Observacoes==novo.Observacoes,"Reabrir cadastro recupera os campos gravados");
        editar.Observacoes="Observação alterada";await editar.SalvarCommand.ExecuteAsync(null);
        if(editar.MensagemEhErro) Console.WriteLine("CADASTRO_TESTE_EDICAO: "+editar.Mensagem);
        using(var db=new ClinicaDbContext(options)) {
            var registros=await db.Pacientes.AsNoTracking().Where(x=>x.Documento=="11144477735").ToListAsync();
            Conferir(registros.Count==1 && registros[0].Observacoes=="Observação alterada" && registros[0].Carteirinha=="DEMO","Editar mantém a mesma ficha e preserva campos não alterados");
        }
    }
    static async Task EsperarCadastroAsync(CadastroPacienteViewModel vm)
    {
        var prazo=Stopwatch.StartNew();
        while(vm.Carregando && prazo.Elapsed<TimeSpan.FromSeconds(10)) await Task.Delay(10);
        if(vm.Carregando) throw new TimeoutException("Cadastro não concluiu a carga em dez segundos no banco de teste.");
    }
    static IEnumerable<DependencyObject> Visuais(DependencyObject pai)
    {
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(pai);i++) {
            var filho=VisualTreeHelper.GetChild(pai,i);yield return filho;
            foreach(var item in Visuais(filho)) yield return item;
        }
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
    public bool AtrasarPaciente { get; set; }
    public bool ProibirHistoricoCadastro { get; set; }
    public List<string> Leituras { get; } = [];
    void Conferir(DbCommand c) {
        if((Proibir && new[]{"Evolucoes","ProblemasPaciente","Hipoteses"}.Any(t=>c.CommandText.Contains(t,StringComparison.OrdinalIgnoreCase)))
            || (ProibirHistoricoCadastro && new[]{"Atendimentos","Codigos"}.Any(t=>c.CommandText.Contains(t,StringComparison.OrdinalIgnoreCase)))) Leituras.Add(c.CommandText);
    }
    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand c,CommandEventData d,InterceptionResult<DbDataReader> result) { Conferir(c);return result; }
    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand c,CommandEventData d,InterceptionResult<DbDataReader> result,CancellationToken ct=default) { Conferir(c);if(AtrasarPaciente && c.CommandText.Contains("Pacientes")) await Task.Delay(150,ct);return result; }
}
