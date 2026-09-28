using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Clinica.Recepcao.ViewModels;

// Demonstration host: uses the actual production window, modules, resources and
// services. The only database is an in-memory SQLite connection. SuiteApp startup,
// saved production settings, automatic emails and external signing are never run.
static class Program
{
    static ServiceProvider? services;
    static SqliteConnection? connection;
    static string output = "";
    static bool captureAll;
    static bool trainingQa;
    [STAThread]
    static void Main(string[] args)
    {
        output=Path.GetFullPath(args[0]); Directory.CreateDirectory(output);
        trainingQa=args.Length>1 && args[1]=="qa-treinamento";
        captureAll=args.Length>1 && (args[1]=="todas"||trainingQa);
        if(args.Length>1 && args[1]=="inventario")
        {
            IModuloApp[] catalogModules=[new Clinica.Recepcao.Modulo.ModuloRecepcao(),new Clinica.Clinico.Modulo.ModuloClinico(),new Clinica.Financeiro.Modulo.ModuloFinanceiro(),new Clinica.Faturamento.Modulo.ModuloFaturamento(),new Clinica.Gerente.Modulo.ModuloGerente()];
            var data=catalogModules.SelectMany(m=>m.Itens.Select(OrganizacaoNavegacao.Aplicar).Select(i=>new{modulo=m.Nome,id=i.Chave,titulo=i.Rotulo,oculto=i.Oculto,permissao=i.Requer.ToString(),alguma=i.RequerAlgum.ToString(),perfil=i.PerfilExclusivo?.ToString(),abas=i.Abas}));
            File.WriteAllText(Path.Combine(output,"inventario.json"),System.Text.Json.JsonSerializer.Serialize(data,new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));return;
        }
        var app=new System.Windows.Application {ShutdownMode=ShutdownMode.OnMainWindowClose};
        app.Resources.MergedDictionaries.Add(new ResourceDictionary {Source=new Uri("pack://application:,,,/Clinica.Desktop.Shell;component/Styles/Suite.xaml")});
        app.DispatcherUnhandledException+=(_,e)=>{File.WriteAllText(Path.Combine(output,"erro.txt"),e.Exception.ToString());e.Handled=true;};
        app.Startup+=async(_,_)=>
        {
            try { await Open(app); }
            catch(Exception e){File.WriteAllText(Path.Combine(output,"erro.txt"),e.ToString());app.Shutdown(1);}
        };
        app.Exit+=(_,_)=>{services?.Dispose();connection?.Dispose();};
        app.Run();
    }
    static async Task Open(System.Windows.Application app)
    {
        connection=new SqliteConnection("Data Source=:memory:");await connection.OpenAsync();
        var options=new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(connection).Options;
        IModuloApp[] modules=captureAll
            ? [new Clinica.Recepcao.Modulo.ModuloRecepcao(),new Clinica.Clinico.Modulo.ModuloClinico(),new Clinica.Financeiro.Modulo.ModuloFinanceiro(),new Clinica.Faturamento.Modulo.ModuloFaturamento(),new Clinica.Gerente.Modulo.ModuloGerente()]
            : [new Clinica.Recepcao.Modulo.ModuloRecepcao(),new ModuloContextual(new Clinica.Clinico.Modulo.ModuloClinico())];
        var collection=new ServiceCollection();
        collection.AddClinica("Host=127.0.0.1;Port=1;Database=NAO_USAR;Username=NAO_USAR;Timeout=1");
        collection.AddScoped(_=>new ClinicaDbContext(options));
        collection.AddSingleton<SessaoUsuario>();collection.AddSingleton<SnackbarService>();
        collection.AddSingleton<ISnackbarService>(s=>s.GetRequiredService<SnackbarService>());
        collection.AddSingleton<IDialogoService,DialogosDemo>();
        foreach(var module in modules)module.Registrar(collection);
        services=collection.BuildServiceProvider();
        var db=services.GetRequiredService<ClinicaDbContext>();await db.Database.EnsureCreatedAsync();
        var professional=new Profissional {Nome="Dr. Gustavo · demonstração",RegistroConselho="CRM DEMO",Ativo=true};db.Add(professional);await db.SaveChangesAsync();
        var user=new UsuarioSistema {Nome="Ana · demonstração",Login="treinamento.local",Perfil=PerfilAcesso.Gerente,ProfissionalId=professional.Id};db.Add(user);await db.SaveChangesAsync();
        services.GetRequiredService<SessaoUsuario>().Entrar(user);
        string[] names=["Marina Exemplo","Carlos Exemplo","Beatriz Exemplo","Pedro Exemplo"];
        for(int i=0;i<names.Length;i++)
        {
            var patient=new Paciente {Nome=names[i],Convenio=Convenio.UnimedPadrao,Sexo=i==0||i==2?Sexo.Feminino:Sexo.Masculino,Observacoes="Pessoa fictícia para treinamento.",DataNascimento=new DateOnly(1980+i,5,14)};
            db.Add(patient);await db.SaveChangesAsync();
            db.Atendimentos.Add(new(){PacienteId=patient.Id,Modalidade=ModalidadeAtendimento.BsvApenas,Data=DateOnly.FromDateTime(DateTime.Today.AddDays(-70-i*10)),RealizadoEm=DateTime.Today.AddDays(-70-i*10)});
            db.Acompanhamentos.Add(new(){PacienteId=patient.Id,Tipo=TipoAcompanhamento.Recall,Modalidade=ModalidadeAtendimento.BsvApenas,
                ReferenciaEm=DateTime.Today.AddDays(-70-i*10),CriadoEm=DateTime.Today,CriadoPor=user.Login,ResponsavelId=i==0?user.Id:null,
                ProximoContato=DateOnly.FromDateTime(DateTime.Today.AddDays(i==0?-1:0))});
        }
        await db.SaveChangesAsync();
        if(captureAll)
        {
            var consulta=new Agendamento {PacienteId=1,ProfissionalId=professional.Id,DataHora=DateTime.Today.AddHours(13),ModalidadePrevista=ModalidadeAtendimento.Consulta,Status=StatusAgendamento.Agendado,DuracaoMinutos=30,ChegadaEm=DateTime.Today.AddHours(12.9)};
            db.Agendamentos.Add(consulta);
            db.Agendamentos.Add(new(){PacienteId=2,ProfissionalId=professional.Id,DataHora=DateTime.Today.AddHours(14),ModalidadePrevista=ModalidadeAtendimento.BsvApenas,Status=StatusAgendamento.Agendado,DuracaoMinutos=30});
            await db.SaveChangesAsync();
            await services.GetRequiredService<Clinica.Application.Abstracoes.IAcompanhamentoPacienteService>().ConfigurarAsync(user.Id,professional.Id,0);
            services.GetRequiredService<PacienteEmFoco>().Definir(1,"Marina Exemplo",consulta.Id);
            var allWindow=new ShellWindow {Width=1600,Height=950,WindowStartupLocation=WindowStartupLocation.CenterScreen};
            app.MainWindow=allWindow;allWindow.Show();
            if(trainingQa)await TrainingQa.Run(allWindow,modules,services,output);
            else await AllScreens.CaptureAsync(allWindow,modules,services,output);
            app.Shutdown();return;
        }
        var vm=new ShellViewModel("Recepção — Clínica SemDor · DEMONSTRAÇÃO",modules,services);
        var window=new ShellWindow {DataContext=vm,Width=1600,Height=950,WindowStartupLocation=WindowStartupLocation.CenterScreen};
        app.MainWindow=window;window.Show();
        File.WriteAllText(Path.Combine(output,"pronto.txt"),"Janela real da suite. SQLite em memoria. Nenhuma conexao com producao.");
        await Capture(window,"00-painel");
        vm.Navegar(vm.Itens.Single(i=>i.Rotulo=="Acompanhamento de pacientes"));
        var recall=(AcompanhamentoViewModel)((FrameworkElement)vm.TelaAtual!).DataContext;
        while(recall.Carregando)await Task.Delay(100);
        if(recall.Pacientes.Count!=4)throw new InvalidOperationException("A demonstração deve ter exatamente quatro pacientes fictícios: "+recall.Mensagem);
        await Capture(window,"01-lista");
        recall.DiasRecall="60";await recall.GerarCommand.ExecuteAsync(null);
        if(recall.Pacientes.Count!=4)throw new InvalidOperationException("Busca do recall falhou: "+recall.Mensagem);
        await Capture(window,"02-busca");
        recall.AbrirFiltrosCommand.Execute(null);
        await Capture(window,"03-filtros");
        await recall.VoltarCommand.ExecuteAsync(null);
        recall.Busca="Marina";
        await Capture(window,"04-filtrada");
        await recall.AbrirCommand.ExecuteAsync(recall.Pacientes.Single());
        await Capture(window,"05-contato");
        recall.EtapaEdicao=nameof(EtapaAcompanhamento.RetornarNaData);
        recall.ProximoContato=DateTime.Today.AddDays(2);
        recall.Observacao="Preparar o retorno para acompanhar a autorização do plano. Registro de demonstração, sem contato externo.";
        await Capture(window,"06-preenchido");
        await recall.SalvarCommand.ExecuteAsync(null);
        if(recall.Mensagem!="Acompanhamento salvo com histórico.")throw new InvalidOperationException("Falha ao salvar: "+recall.Mensagem);
        await Capture(window,"07-salvo");
        await recall.AbrirCommand.ExecuteAsync(recall.Pacientes.Single());
        if(recall.Historico.Count==0)throw new InvalidOperationException("Histórico ausente");
        await Capture(window,"08-historico");
        File.WriteAllText(Path.Combine(output,"verificado.txt"),"4 pacientes fictícios; busca por 60 dias; filtro por nome; responsável da sessão; atualização administrativa persistida; histórico conferido. Nenhum contato externo realizado.");
        app.Shutdown();
    }
    static async Task Capture(Window window,string name)
    {
        await window.Dispatcher.InvokeAsync(()=>window.UpdateLayout(),System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        await Task.Delay(700);
        var root=(FrameworkElement)window.Content;
        var bitmap=new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth),(int)Math.Ceiling(root.ActualHeight),96,96,PixelFormats.Pbgra32);
        bitmap.Render(root);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file=File.Create(Path.Combine(output,name+".png"));encoder.Save(file);
    }
}
sealed class DialogosDemo:IDialogoService
{
    public string? PerguntarTexto(string titulo,string pergunta,string? textoInicial=null,bool obrigatorio=true)=>null;
    public bool Confirmar(string titulo,string mensagem)=>false;
    public bool ConfirmarPerigo(string titulo,string mensagem)=>false;
    public void Aviso(string titulo,string mensagem)=>MessageBox.Show(mensagem,titulo);
}
