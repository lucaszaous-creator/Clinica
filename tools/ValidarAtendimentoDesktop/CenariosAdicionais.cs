using System.Data.Common;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Clinica.Application.Servicos;
using Clinica.Clinico.Modulo;
using Clinica.Clinico.ViewModels;
using Clinica.Clinico.Views;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

static partial class Program
{
    static async Task ValidarExtras(ServiceProvider sp, Paciente patient, Agendamento appointment,
        UsuarioSistema user, DbContextOptions<ClinicaDbContext> options, ConsultaProbe probe)
    {
        var evidencias=new List<object>();
        var scopeFactory=sp.GetRequiredService<IServiceScopeFactory>();
        var focus=sp.GetRequiredService<PacienteEmFoco>();
        int selected=patient.Id;
        var history=new HistoricoConsultaViewModel(scopeFactory,()=>selected);
        probe.Falhar=true;
        await history.RecarregarAsync();
        Check(history.NaoVerificado && !history.Carregando && history.Itens.Count==0,"Falha de banco não foi apresentada como falha.");
        probe.Falhar=false;
        await history.RecarregarAsync();
        Check(!history.NaoVerificado && history.Itens.Count>0,"Atualização não recuperou histórico após falha.");
        probe.BloquearProxima=true;
        var antiga=history.RecarregarAsync();
        await probe.Iniciou.Task.WaitAsync(TimeSpan.FromSeconds(5));
        selected=0;
        await history.RecarregarAsync();
        probe.Liberar.SetResult(); await antiga;
        Check(history.Itens.Count==0 && !history.Carregando,"Resposta antiga reapareceu depois da troca de paciente.");
        evidencias.Add(new {cenario="falha_recuperacao_resposta_atrasada",resultado="passou"});

        var modalHost=new Window{Width=400,Height=300,ShowActivated=false,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-30000,Top=-30000};
        System.Windows.Application.Current.MainWindow=modalHost;modalHost.Show();
        foreach(var profile in new[]{PerfilAcesso.Profissional,PerfilAcesso.Gerente,PerfilAcesso.Enfermagem,PerfilAcesso.Psicologia,PerfilAcesso.Recepcao})
        {
            user.Perfil=profile;sp.GetRequiredService<SessaoUsuario>().Entrar(user);
            var vm=new PacienteWorkspaceViewModel(sp,focus,ModuloClinico.AbaDe(ModuloClinico.ChaveAtendimentoEnfermagem));
            await vm.Atendimento.CarregarAsync();
            var nurse=ModuloClinico.AbaDe(ModuloClinico.ChaveAtendimentoEnfermagem);
            Check(vm.Secoes[nurse].Oculta==(profile!=PerfilAcesso.Enfermagem),"Visibilidade de enfermagem incorreta: "+profile);
            Check(vm.Atendimento.PodeEditarProntuario==(profile is PerfilAcesso.Profissional or PerfilAcesso.Gerente or PerfilAcesso.Psicologia),"Escrita médica incorreta: "+profile);
            await vm.Atendimento.HistoricoConsulta.RecarregarAsync();
            Check(profile!=PerfilAcesso.Recepcao || vm.Atendimento.HistoricoConsulta.Itens.Count==0,"Recepção viu prontuário.");
            foreach(var key in new[]{"receita","atestado","comparecimento","pedido-exame"})
            {
                var folha=CentralDocumentosService.Folha(key)!;
                bool expected=profile!=PerfilAcesso.Recepcao && SessaoUsuario.Atual.Pode(folha.PermissaoEmitir);
                DocumentoEdicaoViewModel? opened=null;
                bool timedOut=false;
                var watch=System.Diagnostics.Stopwatch.StartNew();
                var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(20)};
                timer.Tick+=(_,_)=>{
                    foreach(var window in System.Windows.Application.Current.Windows.OfType<DocumentoWindow>().ToArray())
                    {
                        window.ShowInTaskbar=false; window.ShowActivated=false; window.Left=-30000; window.Top=-30000;
                        opened=window.DataContext as DocumentoEdicaoViewModel;
                        window.Close();
                    }
                    if(watch.Elapsed>TimeSpan.FromSeconds(10)) { timedOut=true;timer.Stop(); }
                };
                timer.Start();
                await vm.Atendimento.EmitirTipoCommand.ExecuteAsync(key);
                timer.Stop();
                Check(!timedOut && (opened!=null)==expected,$"Emissão {profile}/{key}: janela divergente da permissão. {vm.Atendimento.Mensagem}");
                if(opened is not null)
                {
                    var flags=BindingFlags.Instance|BindingFlags.NonPublic;
                    var id=(int)typeof(DocumentoEdicaoViewModel).GetField("_pacienteId",flags)!.GetValue(opened)!;
                    var inherited=typeof(DocumentoEdicaoViewModel).GetField("_heranca",flags)!.GetValue(opened)!;
                    var appointmentId=(int?)inherited.GetType().GetProperty("AgendamentoId")!.GetValue(inherited);
                    Check(id==patient.Id && appointmentId==appointment.Id,"Atalho perdeu vínculo: "+key);
                    Check(opened.TipoSelecionado==folha.TipoClinico,"Atalho abriu documento de outro tipo: "+key);
                }
                else Check(vm.Atendimento.MensagemEhErro,"Emissão negada não informou o motivo.");
                evidencias.Add(new{cenario="emissao",perfil=profile.ToString(),documento=key,permitido=expected,resultado="passou"});
            }
            evidencias.Add(new {cenario="perfil",perfil=profile.ToString(),resultado="passou"});
        }
        modalHost.Close();
        user.Perfil=PerfilAcesso.Gerente;sp.GetRequiredService<SessaoUsuario>().Entrar(user);
        var workspace=new PacienteWorkspaceViewModel(sp,focus,ModuloClinico.AbaDe(ModuloClinico.ChaveAtendimento));
        await workspace.Atendimento.CarregarAsync();
        workspace.Atendimento.TextoEvolucao=string.Join("\n",Enumerable.Repeat("Evolução fictícia extensa para verificar rolagem e preservação do texto.",80));
        for(int i=0;i<12;i++) workspace.Atendimento.CamposPersonalizados.Add(new CampoDaSessao{Id=9000+i,Rotulo="Campo de teste "+i,Tipo=TipoCampoPersonalizado.Texto,Opcoes=[],Resposta="Exemplo fictício"});
        foreach(var config in new[]{(Nome:"expandido",W:1366d,H:820d,Scale:1d),(Nome:"compacto-expandido",W:1100d,H:760d,Scale:1d),(Nome:"escala125",W:1366d,H:820d,Scale:1.25d),(Nome:"escala150",W:1366d,H:820d,Scale:1.5d)})
        {
            var view=new PacienteWorkspaceView{DataContext=workspace};
            var window=new Window{Content=view,Width=config.W,Height=config.H,ShowActivated=false,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-30000,Top=-30000};
            view.LayoutTransform=new ScaleTransform(config.Scale,config.Scale);
            window.Show(); await Task.Delay(120);window.UpdateLayout();
            var expander=Visuals(view).OfType<Expander>().Single(e=>Equals(e.Header,"Campos personalizados"));
            // A coleção muda após a carga para este teste; a visibilidade é ligada explicitamente.
            expander.Visibility=Visibility.Visible; expander.IsExpanded=true;
            await Task.Delay(60);window.UpdateLayout();
            var editor=Visuals(view).OfType<TextBox>().Single(b=>b.Name=="EditorEvolucao");
            Check(editor.ActualHeight>=120,"Editor colapsou com campos expandidos: "+config.Nome+" / "+editor.ActualHeight);
            editor.BringIntoView();await Task.Delay(50);window.UpdateLayout();
            var outer=Visuals(view).OfType<ScrollViewer>().Single(b=>b.Name=="ConteudoScroll");
            var editorRect=editor.TransformToAncestor(outer).TransformBounds(new Rect(editor.RenderSize));
            Check(editorRect.Bottom>0 && editorRect.Top<outer.ViewportHeight,"Rolagem não alcançou editor: "+config.Nome);
            var save=Visuals(view).OfType<BotaoClinico>().Single(b=>b.Texto=="Salvar sessão");
            var saveRect=save.TransformToAncestor(view).TransformBounds(new Rect(save.RenderSize));
            Check(saveRect.Bottom<=view.ActualHeight+1 && saveRect.Right<=view.ActualWidth+1,"Salvar saiu da janela: "+config.Nome);
            var draft=workspace.Atendimento.TextoEvolucao;
            await workspace.Atendimento.HistoricoConsulta.AlternarCommand.ExecuteAsync(null);await Task.Delay(50);window.UpdateLayout();
            await Dispatcher.Yield(DispatcherPriority.ContextIdle);
            var lateral=Visuals(view).OfType<HistoricoConsultaView>().Single();
            var area=(FrameworkElement)Visuals(view).Single(o=>o is FrameworkElement {Name:"AreaEdicao"});
            if(config.Scale>1 || config.W==1100) Check(!area.IsEnabled && lateral.IsVisible,"Edição sob painel não foi desativada.");
            var close=Visuals(lateral).OfType<BotaoClinico>().Single(b=>b.Name=="FecharHistorico");
            Check(FocusManager.GetFocusedElement(window)==close,"Foco não foi para fechar histórico: "+config.Nome+" / "+(FocusManager.GetFocusedElement(window) as FrameworkElement)?.Name+" / visible="+close.IsVisible+" enabled="+close.IsEnabled+" focusable="+close.Focusable+" scope="+FocusManager.GetFocusScope(close).GetType().Name+" keyboard="+(Keyboard.FocusedElement as FrameworkElement)?.Name);
            close.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(close)!,0,Key.Escape){RoutedEvent=Keyboard.PreviewKeyDownEvent});
            await Task.Delay(50);window.UpdateLayout();
            await Dispatcher.Yield(DispatcherPriority.ContextIdle);
            Check(!lateral.IsVisible && FocusManager.GetFocusedElement(window)==editor,"Escape não fechou histórico e restaurou foco da edição.");
            Check(area.IsEnabled && workspace.Atendimento.TextoEvolucao==draft,"Fechar histórico perdeu texto ou manteve edição desativada.");
            var bmp=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);bmp.Render(window);
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bmp));
            using(var stream=File.Create(Path.Combine(output,config.Nome+".png")))encoder.Save(stream);
            evidencias.Add(new{cenario=config.Nome,escalaSimulada=config.Scale,editorAltura=editor.ActualHeight,viewport=outer.ViewportHeight,extensao=outer.ExtentHeight,resultado="passou"});
            window.Close();
        }
        File.WriteAllText(Path.Combine(output,"cenarios-adicionais.json"),JsonSerializer.Serialize(evidencias,new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine("PASS: concorrência, falha/recuperação, cinco perfis, vinte atalhos e layout com campos expandidos e escala simulada.");
    }
    static void Check(bool value,string message) { if(!value) throw new Exception(message); }
}

sealed class ConsultaProbe : DbCommandInterceptor
{
    public bool Falhar, BloquearProxima;
    public TaskCompletionSource Iniciou=new(), Liberar=new();
    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,CommandEventData eventData,InterceptionResult<DbDataReader> result,CancellationToken cancellationToken=default)
    {
        if(Falhar) throw new IOException("Falha simulada do banco de teste.");
        if(BloquearProxima) { BloquearProxima=false;Iniciou.SetResult();await Liberar.Task; }
        return result;
    }
}
