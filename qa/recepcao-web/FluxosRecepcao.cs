using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Clinica.Recepcao.Web;
using Clinica.Recepcao.Modulo;
using Clinica.Recepcao.ViewModels;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell.Web;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Desktop.Shell.Componentes.Cadastro;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Application.Servicos;
using Clinica.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Wpf;
static class FluxosRecepcao
{
    static void Check(bool ok,string text) { if(!ok) throw new Exception(text); }
    static JsonElement J(object? valor) => JsonSerializer.SerializeToElement(valor);
    static async Task Esperar(Func<bool> ok,string text) { for(var i=0;i<150;i++) { if(ok()) return; await Task.Delay(40); } throw new Exception("Tempo excedido: "+text); }
    public static async Task Executar(bool web)
    {
        using var con=new SqliteConnection("Data Source=:memory:"); await con.OpenAsync();
        var options=new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(con).Options;
        var services=new ServiceCollection(); services.AddClinica("Host=127.0.0.1;Port=1;Database=NAO_USAR;Username=NAO_USAR;Timeout=1");
        services.AddScoped(_=>new ClinicaDbContext(options)); services.AddSingleton<SessaoUsuario>(); services.AddSingleton<SnackbarService>();services.AddSingleton<ISnackbarService>(s=>s.GetRequiredService<SnackbarService>());services.AddSingleton<IDialogoService,DialogosNativosProibidos>();
        services.AddSingleton<IFabricaFichaPaciente,FabricaNativaProibida>();services.AddSingleton<PacienteEmFoco>();
        var modulo=new ModuloRecepcao();modulo.Registrar(services);using var sp=services.BuildServiceProvider();
        int profissionalId;
        using(var scope=sp.CreateScope()) {
            var db=scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();await db.Database.EnsureCreatedAsync();
            var usuario=new UsuarioSistema{Nome="Recepção QA",Login="recepcao.qa",Perfil=PerfilAcesso.Gerente};db.Add(usuario);
            var profissional=new Profissional{Nome="Profissional demonstração"};db.Add(profissional);await db.SaveChangesAsync();profissionalId=profissional.Id;sp.GetRequiredService<SessaoUsuario>().Entrar(usuario);
            await scope.ServiceProvider.GetRequiredService<ConvenioCatalogoService>().RecarregarCacheAsync();
            await scope.ServiceProvider.GetRequiredService<ModalidadeCatalogoService>().RecarregarCacheAsync();
            await scope.ServiceProvider.GetRequiredService<EspecialidadeCatalogoService>().RecarregarCacheAsync();
        }
        var defs=RecepcaoWebRegistro.CriarPaginas().ToArray();
        FerramentasSuiteQA.Executar(sp);
        using var pages=new PaginasWebController(sp,defs);
        using var dialogs=new DialogosWebController(RecepcaoWebRegistro.CriarDialogos().Concat(RegistroCompartilhadoWeb.DialogosCadastro()));
        using var contexto=DialogosDaSessao.Usar(dialogs);
        foreach(var def in defs) {
            await pages.NavegarAsync(def.Chave); await Esperar(()=>!pages.ObterPagina().Carregando,def.Chave);
            var dto=pages.ObterPagina();Check(!dto.MensagemEhErro,def.Chave+": "+dto.Mensagem);Console.WriteLine("OK página real "+def.Chave);
        }
        async Task<DialogoWebDto> Dialogo(string chave) {await Esperar(()=>dialogs.EstadoAtual?.Pagina.Chave==chave,chave);return dialogs.EstadoAtual!;}
        await pages.NavegarAsync("pacientes-recepcao");
        var criar=pages.ExecutarAcaoAsync("NovoPaciente"); var cadastro=await Dialogo("CadastroPaciente");
        await dialogs.ExecutarAcaoAsync(cadastro.Id,"SalvarCommand");Check(dialogs.EstadoAtual?.Pagina.MensagemEhErro==true,"Cadastro sem nome não recusado");
        await dialogs.AtualizarCampoAsync(cadastro.Id,"Nome",J("Paciente sintética QA"));await dialogs.AtualizarCampoAsync(cadastro.Id,"Documento",J("52998224725"));
        var op=dialogs.EstadoAtual!.Pagina.Campos.Single(c=>c.Chave=="Convenio").Opcoes.First(o=>o.Rotulo.Contains("Particular"));await dialogs.AtualizarCampoAsync(cadastro.Id,"Convenio",J(op.Valor));
        var abrirFoto=dialogs.ExecutarAcaoAsync(cadastro.Id,"CapturarFotoCommand"); var foto=await Dialogo("CapturaFoto");
        try {await dialogs.AtualizarCampoAsync(foto.Id,"FotoDataUrl",J("data:text/html;base64,PGgxPk5BTzwvaDE+"));throw new Exception("Tipo de foto inválido aceito");}catch(Exception ex) when(ex is InvalidOperationException || ex.InnerException is InvalidOperationException){}
        await dialogs.AtualizarCampoAsync(foto.Id,"FotoDataUrl",J(FotoSintetica()));await dialogs.ExecutarAcaoAsync(foto.Id,"ConfirmarCommand");await abrirFoto;
        using(var scope=sp.CreateScope()) Check(!await scope.ServiceProvider.GetRequiredService<ClinicaDbContext>().Pacientes.AnyAsync(),"Foto gravou cadastro antes de Salvar");
        await dialogs.ExecutarAcaoAsync(cadastro.Id,"SalvarCommand");await criar;
        int pacienteId;
        using(var scope=sp.CreateScope()){var paciente=await scope.ServiceProvider.GetRequiredService<ClinicaDbContext>().Pacientes.SingleAsync();Check(paciente.Nome=="Paciente sintética QA"&&paciente.FotoMiniatura is {Length:>0},"Cadastro/foto não persistiram");pacienteId=paciente.Id;}
        var cancelar=pages.ExecutarAcaoAsync("NovoPaciente");cadastro=await Dialogo("CadastroPaciente");await dialogs.AtualizarCampoAsync(cadastro.Id,"Nome",J("Não deve persistir"));dialogs.Fechar(cadastro.Id);await cancelar;
        using(var scope=sp.CreateScope())Check(await scope.ServiceProvider.GetRequiredService<ClinicaDbContext>().Pacientes.CountAsync()==1,"Cancelar cadastro persistiu");
        var escopos=sp.GetRequiredService<IServiceScopeFactory>();
        var agenda=new AgendamentoEdicaoViewModel(escopos) { ProfissionalPreferidoId=profissionalId };
        var marcar=dialogs.AbrirAsync("RecepcaoAgendamento",agenda);var ag=await Dialogo("RecepcaoAgendamento");await Esperar(()=>agenda.Profissionais.Count>0,"profissionais");
        agenda.Seletor.Termo = "Paciente sintética QA"; await agenda.Seletor.BuscarAsync(imediato:true);
        var selecao=dialogs.EstadoAtual!.Pagina.Campos.Single(c=>c.Chave=="Seletor.Selecionado").Opcoes.Single();
        await dialogs.AtualizarCampoAsync(ag.Id,"Seletor.Selecionado",J(selecao.Valor));await dialogs.AtualizarCampoAsync(ag.Id,"Data",J(DateTime.Today.AddDays(1).ToString("yyyy-MM-dd")));await dialogs.AtualizarCampoAsync(ag.Id,"Hora",J("10:00"));await dialogs.AtualizarCampoAsync(ag.Id,"Duracao",J("30"));
        var prof=dialogs.EstadoAtual!.Pagina.Campos.Single(c=>c.Chave=="Profissional").Opcoes.First();await dialogs.AtualizarCampoAsync(ag.Id,"Profissional",J(prof.Valor));
        await dialogs.ExecutarAcaoAsync(ag.Id,"SalvarCommand");Check(await marcar==true,"Agendamento não confirmou: "+agenda.Mensagem);
        using(var scope=sp.CreateScope())Check(await scope.ServiceProvider.GetRequiredService<ClinicaDbContext>().Agendamentos.AnyAsync(a=>a.PacienteId==pacienteId),"Horário não persistiu");
        await pages.NavegarAsync("agenda-recepcao");await Esperar(()=>!pages.ObterPagina().Carregando,"agenda disponível");
        await pages.AtualizarCampoAsync("Dia",J(DateTime.Today.AddDays(1).ToString("yyyy-MM-dd")));await Esperar(()=>!pages.ObterPagina().Carregando,"agenda do amanhã");
        LinhaWebDto Linha(string tabela) => pages.ObterPagina().Secoes.SelectMany(x=>x.Tabelas).Single(t=>t.Chave==tabela).Linhas.First();
        var abrirHorario=pages.ExecutarAcaoAsync("AbrirHorario","horarios",Linha("horarios").Id);var detalhe=await Dialogo("RecepcaoDetalheHorario");await dialogs.ExecutarAcaoAsync(detalhe.Id,"RemarcarCommand");ag=await Dialogo("RecepcaoAgendamento");
        await dialogs.AtualizarCampoAsync(ag.Id,"Hora",J("11:00"));await dialogs.ExecutarAcaoAsync(ag.Id,"SalvarCommand");await abrirHorario;
        using(var scope=sp.CreateScope())Check((await scope.ServiceProvider.GetRequiredService<ClinicaDbContext>().Agendamentos.SingleAsync()).DataHora.Hour==11,"Remarcação não persistiu");
        var cancelarHorario=pages.ExecutarAcaoAsync("AbrirHorario","horarios",Linha("horarios").Id);detalhe=await Dialogo("RecepcaoDetalheHorario");await dialogs.ExecutarAcaoAsync(detalhe.Id,"CancelarCommand");var pergunta=await Dialogo("Confirmacao");await dialogs.ExecutarAcaoAsync(pergunta.Id,"confirmar");await cancelarHorario;
        using(var scope=sp.CreateScope())Check((await scope.ServiceProvider.GetRequiredService<ClinicaDbContext>().Agendamentos.SingleAsync()).Status==StatusAgendamento.Cancelado,"Cancelamento não preservou horário");
        using(var scope=sp.CreateScope())await scope.ServiceProvider.GetRequiredService<ContasService>().LancarContaAsync(TipoLancamento.Entrada,"Sessão sintética QA",120m,DateOnly.FromDateTime(DateTime.Today),pacienteId:pacienteId);
        await pages.NavegarAsync("pagamentos-recepcao");await pages.AtualizarCampoAsync("Seletor.Termo",J("Paciente sintética QA"));
        await Esperar(()=>pages.ObterPagina().Campos.Single(c=>c.Chave=="Seletor.Selecionado").Opcoes.Count>0,"busca pagamento");var pacienteOpcao=pages.ObterPagina().Campos.Single(c=>c.Chave=="Seletor.Selecionado").Opcoes.First();await pages.AtualizarCampoAsync("Seletor.Selecionado",J(pacienteOpcao.Valor));await Esperar(()=>!pages.ObterPagina().Carregando,"cobranças");
        var cancelarRecebimento=pages.ExecutarAcaoAsync("Receber","Linhas",Linha("Linhas").Id);var recibo=await Dialogo("Recebimento");dialogs.Fechar(recibo.Id);await cancelarRecebimento;
        using(var scope=sp.CreateScope())Check((await scope.ServiceProvider.GetRequiredService<ClinicaDbContext>().Lancamentos.SingleAsync()).Status==StatusLancamento.Previsto,"Cancelar recebimento baixou cobrança");
        var receber=pages.ExecutarAcaoAsync("Receber","Linhas",Linha("Linhas").Id);recibo=await Dialogo("Recebimento");await dialogs.ExecutarAcaoAsync(recibo.Id,"ConfirmarCommand");Check(dialogs.EstadoAtual?.Pagina.MensagemEhErro==true,"Pagamento sem forma passou");
        var dinheiro=dialogs.EstadoAtual!.Pagina.Campos.Single(c=>c.Chave=="Forma").Opcoes.First(o=>o.Rotulo=="Dinheiro");await dialogs.AtualizarCampoAsync(recibo.Id,"Forma",J(dinheiro.Valor));await dialogs.AtualizarCampoAsync(recibo.Id,"ValorInformado",J("40,00"));await dialogs.ExecutarAcaoAsync(recibo.Id,"ConfirmarCommand");await receber;
        using(var scope=sp.CreateScope()) {var cobrancas=await scope.ServiceProvider.GetRequiredService<ClinicaDbContext>().Lancamentos.ToListAsync();Check(cobrancas.Count==2 && cobrancas.Single(c=>c.Status==StatusLancamento.Realizado).Valor==40m && cobrancas.Single(c=>c.Status==StatusLancamento.Previsto).Valor==80m,"Recebimento parcial não conservou saldo");}
        try{await dialogs.ExecutarAcaoAsync(recibo.Id,"ConfirmarCommand");throw new Exception("Modal antigo duplicou pagamento");}catch(InvalidOperationException){}
        Console.WriteLine("OK persistência real: cadastro obrigatório, convênio, foto validada/pendente, salvar paciente+foto, cancelar cadastro, marcar/remarcar/cancelar horário, cancelar pagamento, validar forma, receber parcial 40/120 e rejeitar repetição de modal.");
        if(web) {
            using(var scope=sp.CreateScope()) {
                var db=scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();var pacienteB=new Paciente{Nome="Paciente B — demonstração",Sexo=Sexo.Feminino,Convenio=Convenio.Personalizado,ConvenioCodigo="Particular"};var pacienteC=new Paciente{Nome="Paciente C — demonstração",Sexo=Sexo.Feminino,Convenio=Convenio.Personalizado,ConvenioCodigo="Particular"};db.AddRange(pacienteB,pacienteC);await db.SaveChangesAsync();
                var agendaVisual=scope.ServiceProvider.GetRequiredService<AgendaService>();await agendaVisual.AgendarAsync(pacienteId,DateTime.Today.AddDays(1).AddHours(9),ModalidadeAtendimento.AcupunturaComEletro,"Dados demonstrativos",profissionalId:profissionalId);await agendaVisual.AgendarAsync(pacienteB.Id,DateTime.Today.AddDays(1).AddHours(10),ModalidadeAtendimento.AcupunturaComEletro,"Dados demonstrativos",profissionalId:profissionalId);await agendaVisual.AgendarAsync(pacienteC.Id,DateTime.Today.AddDays(1).AddHours(14),ModalidadeAtendimento.AcupunturaComEletro,"Dados demonstrativos",profissionalId:profissionalId);
            }
            await Visual(sp,modulo,defs);
        }
        sp.GetRequiredService<SessaoUsuario>().Entrar(new UsuarioSistema{Id=999,Nome="Profissional QA restrito",Login="restrito.qa",Perfil=PerfilAcesso.Profissional});
        using(var restrito=new PaginasWebController(sp,RecepcaoWebRegistro.CriarPaginas()))
        {
            try{await restrito.NavegarAsync("equipe");throw new Exception("Perfil sem GerenciarEquipe abriu equipe");}catch(Exception ex) when(ex is InvalidOperationException or UnauthorizedAccessException){}
            try{await pages.ExecutarAcaoAsync("Carregar");throw new Exception("Página antiga executou depois da troca de sessão");}catch(Exception ex) when(ex is InvalidOperationException or UnauthorizedAccessException){}
        }
        Console.WriteLine("OK acesso negado: perfil restrito não abre equipe; contexto anterior rejeita troca de usuário.");
    }
    static string FotoSintetica()
    {
        var bitmap=BitmapSource.Create(4,4,96,96,PixelFormats.Bgr24,null,Enumerable.Repeat((byte)120,48).ToArray(),12);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var stream=new MemoryStream();encoder.Save(stream);return "data:image/png;base64,"+Convert.ToBase64String(stream.ToArray());
    }
    static async Task Visual(IServiceProvider sp,ModuloRecepcao modulo,PaginasWebController.Pagina[] defs)
    {
        var saida=Path.GetFullPath("artifacts/recepcao-web");Directory.CreateDirectory(saida);
        var menu=modulo.Itens.Where(i=>defs.Any(d=>d.Chave==i.Chave)).Append(new ItemMenuModulo{Chave="consultorio-pacientes",Rotulo="Pacientes em tratamento",Glifo="",Oculto=true,Requer=Permissao.VerProntuario}).ToArray();
        using var view=new SuiteWebView(sp,defs,RecepcaoWebRegistro.CriarDialogos().Concat(RegistroCompartilhadoWeb.DialogosCadastro()),menu,"Recepção — demonstração","painel-recepcao");
        var window=new Window{Content=view,Width=1366,Height=768,Left=-30000,Top=-30000,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.Manual};window.Show();
        try {await view.QuandoPronto.WaitAsync(TimeSpan.FromSeconds(45));var browser=(WebView2)view.Content;
            foreach(var size in new[]{(1366,768),(1100,720),(900,600)}) {window.Width=size.Item1;window.Height=size.Item2;await Task.Delay(250);
                foreach(var page in defs) {await view.NavegarAsync(page.Chave);await Task.Delay(400);var ok=await browser.CoreWebView2.ExecuteScriptAsync("document.documentElement.scrollWidth<=innerWidth+1 && !!document.querySelector('.navegacao-topo') && !document.querySelector('.sidebar')");Check(ok=="true","Layout inválido "+page.Chave);using(var file=File.Create(Path.Combine(saida,page.Chave+"-"+size.Item1+".png")))await view.CapturarPreviewAsync(file);Console.WriteLine("OK WebView2 "+page.Chave+" "+size.Item1);
                    if(page.Chave=="agenda-recepcao" && size.Item1 is 1366 or 900) {
                        var semSobreposicao=await browser.CoreWebView2.ExecuteScriptAsync("(()=>{const a=[...document.querySelectorAll('.agenda-sessao')];if(a.length<3)return false;a[0].scrollIntoView({block:'center'});return a.every((c,i)=>i===0||c.getBoundingClientRect().top>=a[i-1].getBoundingClientRect().bottom-1)})()");
                        Check(semSobreposicao=="true","Cartões sintéticos de 30min se sobrepõem");await Task.Delay(150);
                        using var agendaFoto=File.Create(Path.Combine(saida,"agenda-horarios-"+size.Item1+".png"));await view.CapturarPreviewAsync(agendaFoto);Console.WriteLine("OK agenda 30min sem sobreposição "+size.Item1);
                    }
                }
            }
        } finally {window.Close();}
    }
}
sealed class DialogosNativosProibidos:IDialogoService
{
    public bool Confirmar(string t,string m)=>throw new Exception("Diálogo nativo: "+t);
    public bool ConfirmarPerigo(string t,string m)=>throw new Exception("Diálogo nativo: "+t);
    public void Aviso(string t,string m)=>throw new Exception("Diálogo nativo: "+t);
    public string? PerguntarTexto(string t,string p,string? textoInicial=null,bool obrigatorio=true)=>throw new Exception("Diálogo nativo: "+t);
}
sealed class FabricaNativaProibida:IFabricaFichaPaciente {public FrameworkElement Criar(PacienteEmFoco p,Action? voltar=null,int secao=2)=>throw new Exception("Ficha WPF proibida no teste web");}
