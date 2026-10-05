using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Media.Imaging;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Clinico.Modulo;
using Clinica.Clinico.ViewModels;
using Clinica.Clinico.Views;
using Clinica.Domain.Entities;
using Clinica.Domain;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

static partial class Program
{
    static string output="";
    static bool somenteCopia;
    static bool somenteFormatacao;
    [STAThread] static int Main(string[] args)
    {
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("pt-BR");
        System.Globalization.CultureInfo.CurrentUICulture = System.Globalization.CultureInfo.GetCultureInfo("pt-BR");
        output=Path.GetFullPath(args[0]);Directory.CreateDirectory(output);
        somenteCopia=args.Contains("--copia");
        somenteFormatacao=args.Contains("--formatacao");
        using var log=new StreamWriter(Path.Combine(output,"bindings.log"));
        log.AutoFlush=true;
        PresentationTraceSources.DataBindingSource.Listeners.Add(new TextWriterTraceListener(log));
        PresentationTraceSources.DataBindingSource.Switch.Level=SourceLevels.Error;
        var app=new System.Windows.Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        app.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/Clinica.Desktop.Shell;component/Styles/Suite.xaml",UriKind.Relative)});
        app.Startup+=async(_,_)=>{try{await Run();app.Shutdown();}catch(Exception ex){Console.WriteLine(ex);app.Shutdown(1);}};
        return app.Run();
    }
    static async Task Run()
    {
        using var conn=new SqliteConnection("Data Source=:memory:");conn.Open();
        var probe=new ConsultaProbe();
        var options=new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(conn).AddInterceptors(probe).Options;
        var services=new ServiceCollection();services.AddClinica("Host=127.0.0.1;Port=1;Database=never_used");
        services.AddScoped(_=>new ClinicaDbContext(options));services.AddSingleton<SessaoUsuario>();
        services.AddSingleton<SnackbarService>();services.AddSingleton<ISnackbarService>(s=>s.GetRequiredService<SnackbarService>());
        services.AddSingleton<IDialogoService,DialogoTeste>();var module=new ModuloClinico();module.Registrar(services);
        using var sp=services.BuildServiceProvider();using var scope=sp.CreateScope();
        var db=scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();db.Database.EnsureCreated();
        var patient=new Paciente{Nome="Marina de demonstração",Endereco="Endereço fictício",DataNascimento=new DateOnly(1982,4,12)};
        var professional=new Profissional{Nome="Profissional de demonstração",RegistroConselho="CRM-RJ 000000",Ativo=true};
        db.AddRange(patient,professional);await db.SaveChangesAsync();
        var user=await new AcessoService(new ClinicaRepositorio(db)).CriarAsync(professional.Nome,"preview.atendimento","Preview#2026",PerfilAcesso.Gerente);
        user.ProfissionalId=professional.Id;await db.SaveChangesAsync();sp.GetRequiredService<SessaoUsuario>().Entrar(user);
        var date=new DateTime(2026,10,2);
        db.Add(new Evolucao { PacienteId=patient.Id, ProfissionalId=professional.Id, Data=new DateOnly(2026,9,25), TextoEvolucao="Paciente relata melhora desde a sessão anterior. Mantido acompanhamento clínico.", EvaAntes=7, EvaDepois=4 });
        db.Add(new EvolucaoEnfermagem { PacienteId=patient.Id, Data=new DateOnly(2026,9,25), Hora=new TimeOnly(13,40), Texto="Acolhimento registrado. Sem intercorrências relatadas.", AutorNome="Enfermagem de demonstração", AutorConselho="COREN 000000", RegistradoEm=DateTime.UtcNow });
        await db.SaveChangesAsync();
        var appointment=new Agendamento{PacienteId=patient.Id,ProfissionalId=professional.Id,DataHora=date.AddHours(14),Status=StatusAgendamento.Agendado,ModalidadePrevista=ModalidadeAtendimento.AcupunturaComEletro,InicioAtendimentoEm=DateTime.Now.AddMinutes(-18)};
        db.Add(appointment);await db.SaveChangesAsync();
        var focus=sp.GetRequiredService<PacienteEmFoco>();focus.Definir(patient.Id,patient.Nome,appointment.Id,null,DateOnly.FromDateTime(date));
        if(somenteFormatacao){await ValidarFormatacao(sp,db,user,professional);return;}
        user.Perfil=PerfilAcesso.Profissional;sp.GetRequiredService<SessaoUsuario>().Entrar(user);
        await ValidarCopia(sp,db,patient,appointment);
        if(somenteCopia) return;
        user.Perfil=PerfilAcesso.Gerente;sp.GetRequiredService<SessaoUsuario>().Entrar(user);
        foreach(var nurse in new[]{false})
        {
            var vm=new PacienteWorkspaceViewModel(sp,focus,ModuloClinico.AbaDe(ModuloClinico.ChaveAtendimentoEnfermagem));
            if(!vm.Secoes[ModuloClinico.AbaDe(ModuloClinico.ChaveAtendimentoEnfermagem)].Oculta || vm.AbaAtual!=ModuloClinico.AbaDe(ModuloClinico.ChaveAtendimento))
                throw new Exception("A aba de registro da enfermagem permanece no prontuário médico.");
            await vm.Atendimento.CarregarAsync();await vm.Enfermagem.CarregarAsync();
            vm.Atendimento.Data=date;vm.Atendimento.EvaAntes=6;vm.Atendimento.EvaDepois=3;
            vm.Atendimento.TextoEvolucao="Queixa e evolução\nPaciente em acompanhamento, refere melhora desde a última sessão.\nMantém desconforto ao final do dia.\n\nAvaliação\nReavaliadas as queixas e a resposta à sessão anterior.\n\nConduta e orientações\nRegistradas as orientações e o plano de acompanhamento.\nRetorno conforme evolução clínica.";
            vm.Enfermagem.Passagem.DataDoAtendimento=date;vm.Enfermagem.Passagem.Hora="14:05";
            vm.Enfermagem.Passagem.Sistolica="120";vm.Enfermagem.Passagem.Diastolica="80";vm.Enfermagem.Passagem.Cardiaca="76";vm.Enfermagem.Passagem.Respiratoria="16";vm.Enfermagem.Passagem.Saturacao="98";
            vm.Enfermagem.Passagem.Texto="Paciente acolhido para o atendimento.\n\nRegistrar aqui observações, cuidados realizados e resposta do paciente.\n\nAs informações deste exemplo são fictícias.";
            foreach(var variant in new[]{"consulta","historico","compacto","compacto-historico"})
            {
                var shell=new ShellViewModel("Consultório",[module],sp);
                var window=new ShellWindow{DataContext=shell,Width=1366,Height=820,ShowActivated=false,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-30000,Top=-30000};
                foreach(var item in shell.Itens)item.EstaAtivo=item.Rotulo=="Prontuário";
                var view=new PacienteWorkspaceView { DataContext=vm };
                shell.TelaAtual=view;
                vm.Atendimento.HistoricoConsulta.Aberto=variant is "historico" or "compacto-historico";
                await vm.Atendimento.HistoricoConsulta.RecarregarAsync();
                if(vm.Atendimento.HistoricoConsulta.Itens.Count!=2) throw new Exception("O histórico não carregou os dois registros de origens distintas.");
                vm.Atendimento.HistoricoConsulta.FiltrarCommand.Execute("Portal");
                if(vm.Atendimento.HistoricoConsulta.Itens.Count!=1 || vm.Atendimento.HistoricoConsulta.Itens[0].Natureza!=Clinica.Domain.Prontuario.NaturezaRegistroClinico.EvolucaoEnfermagem) throw new Exception("Filtro Portal incorreto.");
                vm.Atendimento.HistoricoConsulta.FiltrarCommand.Execute("Sessões");
                if(vm.Atendimento.HistoricoConsulta.Itens.Count!=1 || vm.Atendimento.HistoricoConsulta.Itens[0].Natureza!=Clinica.Domain.Prontuario.NaturezaRegistroClinico.SessaoMedica) throw new Exception("Filtro Sessões incorreto.");
                vm.Atendimento.HistoricoConsulta.FiltrarCommand.Execute("Todos");
                if(variant.StartsWith("compacto")){window.Width=1100;window.Height=760;}


                window.Show();await Task.Delay(250);window.UpdateLayout();
                var nursingTab=Visuals(view).OfType<TabControl>().Single(t=>t.Items.OfType<TabItem>().Any(i=>Equals(i.Header,"Atendimento de enfermagem"))).Items.OfType<TabItem>().Single(t=>Equals(t.Header,"Atendimento de enfermagem"));
                if(nursingTab.Visibility!=Visibility.Collapsed || nursingTab.IsEnabled) throw new Exception("A aba de enfermagem ainda é alcançável pelo teclado do médico.");
                var text=vm.Atendimento.TextoEvolucao;
                vm.Atendimento.HistoricoConsulta.FecharCommand.Execute(null);
                vm.Atendimento.HistoricoConsulta.Aberto=variant is "historico" or "compacto-historico";
                if(vm.Atendimento.TextoEvolucao!=text) throw new Exception("Histórico alterou o rascunho.");
                var buttons=Visuals(view).OfType<BotaoClinico>().ToList();
                buttons.Single(b=>b.Texto=="Documentos emitidos").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.ContextIdle);window.UpdateLayout();
                var tab=Visuals(view).OfType<TabControl>().Single(t=>t.Items.OfType<TabItem>().Any(i=>Equals(i.Header,"Atendimento de enfermagem")));
                if(!Equals(((TabItem)tab.SelectedItem).Header,"Prescrições e documentos")) throw new Exception("Documentos emitidos abriu seção incorreta.");
                vm.AbaAtual=ModuloClinico.AbaDe(ModuloClinico.ChaveAtendimento);
                await Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.ContextIdle);window.UpdateLayout();
                if(vm.Atendimento.TextoEvolucao!=text) throw new Exception("Troca real para documentos perdeu rascunho.");
                buttons=Visuals(view).OfType<BotaoClinico>().ToList();
                foreach(var label in new[]{"Receita","Pedido de exame","Prescrição de infusão","Salvar sessão","Consultar histórico"})
                    if(!buttons.Any(b=>b.Texto==label && b.Command!=null)) throw new Exception("Comando não ligado: "+label);
                await Task.Delay(80); window.UpdateLayout();
                var historyView=Visuals(view).OfType<HistoricoConsultaView>().Single();
                if(historyView.IsVisible!=vm.Atendimento.HistoricoConsulta.Aberto) throw new Exception("A visibilidade do histórico não acompanha o estado do comando.");
                if(!historyView.IsVisible && CampoDoEditor(EditorDaEvolucao(view)).ActualHeight<100) throw new Exception("Editor sem altura útil: "+CampoDoEditor(EditorDaEvolucao(view)).ActualHeight);
                PresentationTraceSources.DataBindingSource.Flush();
                var content=(FrameworkElement)window.Content;var bmp=new RenderTargetBitmap((int)content.ActualWidth,(int)content.ActualHeight,96,96,PixelFormats.Pbgra32);bmp.Render(window);
                var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bmp));
                using(var stream=File.Create(Path.Combine(output,$"{variant}-{(nurse?"enfermagem":"medico")}.png")))encoder.Save(stream);
                window.Close();Console.WriteLine($"Capturado {variant} {(nurse?"enfermagem":"médico")}");
            }
            var draft=vm.Atendimento.TextoEvolucao;
            vm.AbaAtual=ModuloClinico.SecoesDoPaciente.ToList().IndexOf("Prescrições e documentos");
            vm.AbaAtual=ModuloClinico.AbaDe(ModuloClinico.ChaveAtendimento);
            if(vm.Atendimento.TextoEvolucao!=draft) throw new Exception("Navegação perdeu o rascunho.");
            if(!await vm.Atendimento.TentarSalvarAsync()) throw new Exception("Falha na gravação: "+vm.Atendimento.Mensagem);
            var saved=await db.Evolucoes.AsNoTracking().SingleAsync(e=>e.Id==vm.Atendimento.EvolucaoId);
            if(saved.PacienteId!=patient.Id || saved.AgendamentoId!=appointment.Id || saved.TextoEvolucao!=draft || saved.EvaAntes!=6 || saved.EvaDepois!=3) throw new Exception("Gravação não preservou texto, medidas ou vínculo da sessão.");
            var history=vm.Atendimento.HistoricoConsulta;
            user.Perfil=PerfilAcesso.Recepcao;
            sp.GetRequiredService<SessaoUsuario>().Entrar(user);
            history.Carregando=true;
            await history.RecarregarAsync();
            if(history.Itens.Count!=0 || history.Carregando || history.Mensagem is null) throw new Exception("Histórico não bloqueou acesso sem permissão.");
            user.Perfil=PerfilAcesso.Gerente; sp.GetRequiredService<SessaoUsuario>().Entrar(user);
            int selectedId=patient.Id;
            var isolated=new HistoricoConsultaViewModel(sp.GetRequiredService<IServiceScopeFactory>(),()=>selectedId);
            await isolated.RecarregarAsync();
            if(isolated.Itens.Count==0) throw new Exception("Paciente inicial sem registros de teste.");
            selectedId=0; await isolated.RecarregarAsync();
            if(isolated.Itens.Count!=0 || isolated.Carregando) throw new Exception("Histórico reteve dados após retirar paciente.");
            Console.WriteLine("PASS: filtros, permissões, visibilidade, dimensões, rascunho, navegação e gravação vinculada.");
        }
        await ValidarExtras(sp,patient,appointment,user,options,probe);
        await ValidarFormatacao(sp,db,user,professional);
    }
    static IEnumerable<DependencyObject> Visuals(DependencyObject root)
    {
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)
        {var child=VisualTreeHelper.GetChild(root,i);yield return child;foreach(var nested in Visuals(child))yield return nested;}
    }

}
sealed class DialogoTeste:IDialogoService
{
    public string? PerguntarTexto(string titulo,string pergunta,string? textoInicial=null,bool obrigatorio=true)=>null;
    public bool Confirmar(string titulo,string mensagem)=>false;
    public bool ConfirmarPerigo(string titulo,string mensagem)=>false;
    public void Aviso(string titulo,string mensagem){}
}
