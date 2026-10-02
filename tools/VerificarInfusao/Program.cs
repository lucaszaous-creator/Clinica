using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Clinica.Application.Servicos;
using Clinica.Clinico.Janelas;
using Clinica.Clinico.ViewModels;
using Clinica.Clinico.Views;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

internal static class Program
{
    static string saida="";
    static int verificacoes;
    [STAThread] static int Main(string[] args)
    {
        saida=Path.GetFullPath(args[0]);Directory.CreateDirectory(saida);
        using var log=new StreamWriter(Path.Combine(saida,"bindings.log"));
        PresentationTraceSources.DataBindingSource.Listeners.Add(new TextWriterTraceListener(log));
        PresentationTraceSources.DataBindingSource.Switch.Level=SourceLevels.Error;
        var app=new System.Windows.Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};
        app.Resources.MergedDictionaries.Add(new ResourceDictionary {Source=new Uri("/Clinica.Desktop.Shell;component/Styles/Suite.xaml",UriKind.Relative)});
        app.Startup+=async(_,_)=>
        {
            try { await Verificar(); Console.WriteLine($"PASSOU: {verificacoes} verificações de infusão e cadastro."); app.Shutdown(0); }
            catch(Exception ex) { Console.WriteLine(ex); app.Shutdown(1); }
        };
        return app.Run();
    }
    static void Confere(bool valor,string caso) { if(!valor)throw new Exception(caso);verificacoes++;Console.WriteLine("OK: "+caso); }
    static async Task Verificar()
    {
        using var conn=new SqliteConnection("Data Source=:memory:");conn.Open();
        var options=new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(conn).Options;
        var servicos=new ServiceCollection();servicos.AddClinica("Host=127.0.0.1;Port=1;Database=never_used");
        servicos.AddScoped(_=>new ClinicaDbContext(options));servicos.AddSingleton<SessaoUsuario>();
        using var provider=servicos.BuildServiceProvider();
        using var scope=provider.CreateScope();var db=scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();db.Database.EnsureCreated();
        var paciente=new Paciente {Nome="Paciente fictício — demonstração"};
        var profissional=new Profissional {Nome="Médico de demonstração",RegistroConselho="CRM-SP 123456",Cpf="11111111111",Ativo=true};
        db.Pacientes.Add(paciente);db.Profissionais.Add(profissional);await db.SaveChangesAsync();
        var repo=new ClinicaRepositorio(db);
        var usuario=await new AcessoService(repo).CriarAsync(profissional.Nome,"demo.infusao","Teste#Infusao2026",PerfilAcesso.Profissional);
        usuario.ProfissionalId=profissional.Id;usuario.Profissional=profissional;await db.SaveChangesAsync();
        await repo.SalvarConfiguracaoAsync(ContinuidadeSemAssinatura.Configuracao,"true");await repo.SalvarAsync();
        provider.GetRequiredService<SessaoUsuario>().Entrar(usuario);
        var escopos=provider.GetRequiredService<IServiceScopeFactory>();var dialogo=new DialogoTeste();
        var vm=new PrescricaoInternaEdicaoViewModel(escopos,dialogo,paciente.Id,paciente.Nome,profissional.Id);await vm.Inicializacao;
        Confere(!vm.MensagemEhErro,vm.Mensagem??"Inicialização sem erro");
        Confere(vm.Infusoes.Count==1,"Só Infusão 1 ao abrir");
        var grupo=vm.Infusoes[0];
        var lidocaina=BuscaMedicamentos.Buscar(vm.CatalogoMedicamentos,"li").First();grupo.Itens[0].Descricao=lidocaina.Texto;
        Confere(grupo.Itens[0].Dose is null,"Seleção do medicamento não define dose");
        Confere(grupo.Itens[0].DicaDose=="Quantidade em mL","Lidocaína orienta quantidade em mL");
        var janela=new PrescricaoInternaWindow(vm);Abrir(janela,1180,790);await Render(janela,"prescricao-minimalista.png");
        janela.Width=900;janela.Height=560;await Render(janela,"prescricao-900.png");janela.Close();
        grupo.Volume="250 mL";grupo.Itens[0].Dose="7 mL";grupo.Horario="09:00";grupo.Itens[0].Observacoes="Nota do paciente fictício";
        Confere(await vm.SalvarModeloAsync(grupo,"Composição demonstrativa"),vm.Mensagem??"Salvar modelo");
        var modelo=vm.Modelos.Single(m=>m.Nome=="Composição demonstrativa");
        var conteudo=ModeloInfusao.Ler(modelo.ConfiguracaoInfusao);
        Confere(conteudo.Indicacao is null&&conteudo.Observacoes is null&&conteudo.Itens.All(i=>i.Observacoes is null),"Modelo sem notas do paciente");
        vm.CriarInfusaoCommand.Execute(null);Confere(vm.Infusoes.Count==2,"Segunda infusão criada pelo comando");
        vm.AplicarModelo(vm.Infusoes[1],modelo);Confere(vm.Infusoes[1].Horario is null,"Modelo não reaproveita horário");
        vm.Infusoes[1].Diluente="Diluente demonstrativo";vm.Infusoes[1].Volume="100 mL";
        await vm.SalvarRascunhoCommand.ExecuteAsync(null);Confere(!vm.MensagemEhErro,vm.Mensagem??"Rascunho salvo");
        db.ChangeTracker.Clear();var p=await db.PrescricoesInternas.Include(p=>p.Itens).SingleAsync();
        Confere(p.Itens.OrderBy(i=>i.Ordem).Select(i=>i.GrupoInfusao).SequenceEqual(new int?[]{1,2}),"Banco conserva as duas infusões");
        var reaberta=new PrescricaoInternaEdicaoViewModel(escopos,dialogo,paciente.Id,paciente.Nome,profissional.Id,prescricaoId:p.Id);await reaberta.Inicializacao;
        Confere(reaberta.Infusoes.Count==2&&reaberta.Infusoes[0].Itens[0].Dose=="7 mL"&&reaberta.Infusoes[1].Volume=="100 mL","Reabrir conserva quantidade e preparos");
        var legado=GrupoInfusaoEdicao.Carregar([new(){Descricao="A"},new(){Descricao="B"}],false,null,null);
        Confere(legado.Count==2,"Itens antigos independentes não viram uma mistura");
        var catalogo=new MedicamentosView(escopos);await catalogo.Inicializacao;
        var cadastro=new Window {Title="Medicamentos — Consultório e Gerente",Content=catalogo};Abrir(cadastro,1180,790);await Render(cadastro,"cadastro-medicamentos.png");cadastro.Close();
        reaberta.Fechar=()=>{};await reaberta.AssinarCommand.ExecuteAsync(null);
        Confere(!reaberta.MensagemEhErro,reaberta.Mensagem??"Liberação sem assinatura");
        db.ChangeTracker.Clear();p=await db.PrescricoesInternas.Include(p=>p.Itens).SingleAsync();
        Confere(p.Situacao==SituacaoPrescricao.Liberada&&p.AssinadaEm is null,"Liberada sem registrar assinatura inexistente");
        var pdf=await scope.ServiceProvider.GetRequiredService<PrescricaoInternaPdfService>().GerarPrescricaoAsync(p.Id);
        await File.WriteAllBytesAsync(Path.Combine(saida,"infusao-demonstracao.pdf"),pdf);
    }
    static void Abrir(Window w,double largura,double altura) {w.Width=largura;w.Height=altura;w.ShowActivated=false;w.ShowInTaskbar=false;w.WindowStartupLocation=WindowStartupLocation.Manual;w.Left=-30000;w.Top=-30000;w.Show();}
    static async Task Render(Window w,string nome) {await Task.Delay(100);w.UpdateLayout();var bitmap=new RenderTargetBitmap((int)w.ActualWidth,(int)w.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(w);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var stream=File.Create(Path.Combine(saida,nome));encoder.Save(stream);}
    private sealed class DialogoTeste:IDialogoService
    {
        public bool Confirmar(string titulo,string mensagem)=>true;
        public bool ConfirmarPerigo(string titulo,string mensagem)=>true;
        public void Aviso(string titulo,string mensagem){}
        public string? PerguntarTexto(string titulo,string pergunta,string? inicial=null,bool obrigatorio=true)=>null;
    }
}
