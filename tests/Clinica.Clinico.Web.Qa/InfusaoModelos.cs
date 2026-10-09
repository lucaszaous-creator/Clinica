using System.IO;
using System.Text.Json;
using System.Windows;
using Clinica.Application.Servicos;
using Clinica.Clinico.Modulo;
using Clinica.Clinico.Web;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Desktop.Shell.Web;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Wpf;

static partial class Fluxos
{
 public static bool SoInfusaoModelos;
 static async Task ValidarInfusaoModelosAsync(IServiceProvider sp, ModuloClinico modulo,
  PaginasWebController.Pagina[] defs, DialogosWebController.RegistroDialogo[] registros)
 {
  var pasta = Path.GetFullPath("artifacts/infusao-modelos-pr245"); Directory.CreateDirectory(pasta);
  int pacienteId, sessaoId;
  using (var scope = sp.CreateScope())
  {
   var db = scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();
   var paciente = await db.Pacientes.SingleAsync(); pacienteId = paciente.Id;
   paciente.Endereco = "Rua Sintética, 100, Centro, Cidade de Teste";
   var profissional = await db.Profissionais.SingleAsync(); profissional.RegistroConselho = "CRM-SP 123456";
   var tecnica = new Profissional {Nome="Enfermeira sintética", RegistroConselho="COREN-SP 999999", Ativo=true};
   var usuario = await db.Usuarios.SingleAsync(u => u.Id == enfermeiro.Id); usuario.Profissional = tecnica;
   var sessao = new Agendamento {PacienteId=pacienteId, ProfissionalId=profissional.Id, DataHora=DateTime.Today.AddHours(8), ModalidadePrevista=ModalidadeAtendimento.BsvApenas};
   db.AddRange(sessao, new Agendamento {PacienteId=pacienteId, ProfissionalId=profissional.Id, DataHora=DateTime.Today.AddHours(9), ModalidadePrevista=ModalidadeAtendimento.BsvApenas});
   db.Add(new ModeloEvolucao {Nome="Roteiro sintético de evolução", TextoEvolucao="Texto aplicado do modelo de evolução", Conduta="Conduta do modelo sintético", ProfissionalId=null});
   var itens = new[] {
    new ItemPrescricaoInterna {Ordem=1,GrupoInfusao=1,Descricao="Medicamento sintético A",Dose="2 mL",Diluente="SF 0,9%",Volume="250 mL",Via=ViaAdministracao.Endovenosa,TempoInfusao="40 min"},
    new ItemPrescricaoInterna {Ordem=2,GrupoInfusao=1,Descricao="Medicamento sintético B",Dose="1 mL",Diluente="SF 0,9%",Volume="250 mL",Via=ViaAdministracao.Endovenosa,TempoInfusao="40 min"},
    new ItemPrescricaoInterna {Ordem=3,GrupoInfusao=2,Descricao="Medicamento sintético C",Dose="3 mL",Diluente="SG 5%",Volume="100 mL",Via=ViaAdministracao.Endovenosa,TempoInfusao="20 min"}};
   db.Add(new ModeloDocumento {Nome="Duas infusões sintéticas",Tipo=TipoDocumentoClinico.Receita,ParaInfusao=true,Corpo="A + B; C",ConfiguracaoInfusao=new ModeloInfusao(null,null,itens.Select(ModeloInfusao.De).ToArray()).Guardar()});
   db.Add(new ModeloDocumento {Nome="Receita sintética",Tipo=TipoDocumentoClinico.Receita,Corpo="Conteúdo aplicado do modelo de receita"});
   db.Add(new MedicamentoCadastro {Codigo="QA-A",Nome="Medicamento sintético A",AtualizadoEm=DateTime.Now});
   db.Add(new MedicamentoCadastro {Codigo="QA-B",Nome="Medicamento sintético B",AtualizadoEm=DateTime.Now});
   await db.SaveChangesAsync(); sessaoId=sessao.Id;
   enfermeiro.ProfissionalId=tecnica.Id; enfermeiro.Profissional=tecnica;
   var repo = scope.ServiceProvider.GetRequiredService<Clinica.Application.Abstracoes.IClinicaRepositorio>();
   await repo.SalvarConfiguracaoAsync(ContinuidadeSemAssinatura.Configuracao,"true"); await repo.SalvarAsync();
  }
  sp.GetRequiredService<PacienteEmFoco>().Definir(pacienteId,"Paciente fictícia de demonstração",sessaoId,null,DateOnly.FromDateTime(DateTime.Today));
  async Task ComTela(string rota, Func<SuiteWebView,WebView2,Window,Task> testar)
  {
   using var view = new SuiteWebView(sp,defs,registros,modulo.Itens.ToArray(),"Validação sintética",rota);
   var janela = new Window {Content=view,Width=1440,Height=900,Left=-30000,Top=-30000,ShowInTaskbar=false,ShowActivated=false}; janela.Show();
   try {await view.QuandoPronto.WaitAsync(TimeSpan.FromSeconds(60));await testar(view,(WebView2)view.Content,janela);}
   finally {janela.Close();}
  }
  static string Q(string s)=>JsonSerializer.Serialize(s);
  async Task Esperar(WebView2 b,string expressao)
  {
   for(var i=0;i<160;i++){if(await b.CoreWebView2.ExecuteScriptAsync(expressao)=="true")return;await Task.Delay(75);}
   await File.WriteAllTextAsync(Path.Combine(pasta,"falha-dom.txt"),await b.CoreWebView2.ExecuteScriptAsync("document.body.innerText"));
   throw new Exception("Estado não atingido: "+expressao);
  }
  Task Clicar(WebView2 b,string comando)=>AcoesVisiveisQa.Clicar(b,"[data-comando="+Q(comando)+"]");
  async Task Campo(WebView2 b,string chave,string valor,int n=0)
  {
   var seletor="[data-campo="+Q(chave)+"]";
   await Esperar(b,$"!!document.querySelectorAll({Q(seletor)})[{n}] && !document.querySelectorAll({Q(seletor)})[{n}].disabled");
   await b.CoreWebView2.ExecuteScriptAsync($"(()=>{{const e=document.querySelectorAll({Q(seletor)})[{n}];e.focus();if(e.isContentEditable)e.textContent={Q(valor)};else e.value={Q(valor)};e.dispatchEvent(new Event('input',{{bubbles:true}}));e.dispatchEvent(new Event('change',{{bubbles:true}}));e.blur();}})()");
   await Task.Delay(250);
  }
  async Task EscolherModelo(WebView2 b,string termo,string nome,bool teclado)
  {
   await Esperar(b,"!!document.querySelector('[data-busca-modelo]:not(:disabled)')");
   const string conteudo="JSON.stringify({evolucao:document.querySelector('[data-campo=\"Atendimento.TextoEvolucao\"]')?.textContent,corpo:document.querySelector('[data-campo=Corpo]')?.textContent,itens:Array.from(document.querySelectorAll('[data-item-infusao] [data-campo=Descricao]')).map(e=>e.value)})";
   var antes=await b.CoreWebView2.ExecuteScriptAsync(conteudo);
   await b.CoreWebView2.ExecuteScriptAsync("(()=>{const e=document.querySelector('[data-busca-modelo]');e.focus();e.value='zzzzsemresultado';e.dispatchEvent(new Event('input',{bubbles:true}));})()");
   await Esperar(b,"document.querySelector('.modelo-sugestoes')?.textContent.includes('Nenhum modelo encontrado')===true");
   await b.CoreWebView2.ExecuteScriptAsync($"(()=>{{const e=document.querySelector('[data-busca-modelo]');e.focus();e.value={Q(termo)};e.dispatchEvent(new Event('input',{{bubbles:true}}));}})()");
   await Esperar(b,$"Array.from(document.querySelectorAll('.modelo-sugestoes [role=option]')).some(e=>e.textContent.includes({Q(nome)}))");
   Exigir(antes==await b.CoreWebView2.ExecuteScriptAsync(conteudo),"Digitar uma busca aplicou modelo sem escolher a sugestão.");
   await Task.Delay(250);
   using(var captura=File.Create(Path.Combine(pasta,"autocomplete-"+termo+".png")))await b.CoreWebView2.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png,captura);
   if(teclado) await b.CoreWebView2.ExecuteScriptAsync("document.querySelector('[data-busca-modelo]').dispatchEvent(new KeyboardEvent('keydown',{key:'Enter',bubbles:true}))");
   else await AcoesVisiveisQa.ClicarExpressao(b,$"Array.from(document.querySelectorAll('.modelo-sugestoes [role=option]')).find(e=>e.textContent.includes({Q(nome)}))");
  }
  async Task Fechar(WebView2 b){await AcoesVisiveisQa.Clicar(b,"[data-fechar-dialogo]");await Esperar(b,"!document.querySelector('[data-dialogo]')");}
  async Task Captura(SuiteWebView view,string nome){await Task.Delay(250);using var f=File.Create(Path.Combine(pasta,nome+".png"));await view.CapturarPreviewAsync(f);}
  await ComTela(ModuloClinico.ChaveAtendimento,async(view,b,janela)=>
  {
   await Esperar(b,"!!document.querySelector('[data-campo=\"Atendimento.TextoEvolucao\"]')");
   await Campo(b,"Atendimento.TextoEvolucao","Texto anterior para conferir substituição visível");
   await Clicar(b,"Atendimento.AbrirModelos");
   await EscolherModelo(b,"roteiro","Roteiro sintético",true);
   await Esperar(b,"!document.querySelector('[data-dialogo]') && document.querySelector('[data-campo=\"Atendimento.TextoEvolucao\"]')?.textContent==='Texto aplicado do modelo de evolução'");
   await Clicar(b,"Atendimento.Salvar");
   await Task.Delay(400);
   using(var scope=sp.CreateScope())Exigir(await scope.ServiceProvider.GetRequiredService<ClinicaDbContext>().Evolucoes.AnyAsync(e=>e.AgendamentoId==sessaoId&&e.TextoEvolucao=="Texto aplicado do modelo de evolução"),"Modelo de evolução não persistiu na sessão.");
   Console.WriteLine("OK modelo de evolução: aplicar substitui conteúdo no DOM e Salvar persiste na sessão correta.");
   await Clicar(b,"Atendimento.PrescreverInfusao");
   await Esperar(b,"!!document.querySelector('.prescricao-infusao') && !document.querySelector('[data-campo=Volume]').disabled");
   Exigir(await b.CoreWebView2.ExecuteScriptAsync("!!document.querySelector('[data-comando=CopiarUltimaPrescricao]')?.getClientRects().length && !!document.querySelector('.dialogo-rodape [data-comando=SalvarRascunho]')?.getClientRects().length")=="true","Cópia ou rascunho escondidos.");
   await EscolherModelo(b,"duas","Duas infusões",false);
   await Esperar(b,"document.querySelectorAll('[data-infusao]').length===2 && document.querySelectorAll('[data-item-infusao]').length===3");
   Exigir(await b.CoreWebView2.ExecuteScriptAsync("Array.from(document.querySelectorAll('[data-infusao]')).map(g=>g.querySelectorAll('[data-item-infusao]').length).join(',')==='2,1' && document.querySelector('[data-campo=Volume]').value==='250 mL'")=="true","Modelo não reuniu medicamentos e preparo por infusão.");
   await b.CoreWebView2.ExecuteScriptAsync("document.querySelector('.dialogo-corpo').scrollTop=0");
   await Captura(view,"prescricao-modelo-1440");
   janela.Width=1044;janela.Height=788;await Task.Delay(250);
   Exigir(await b.CoreWebView2.ExecuteScriptAsync("document.documentElement.scrollWidth<=innerWidth+1 && document.querySelector('.dialogo-corpo').scrollWidth<=document.querySelector('.dialogo-corpo').clientWidth+1")=="true","Infusão transborda no notebook.");
   await Captura(view,"prescricao-modelo-1044");
   await Campo(b,"Descricao","Medicamento sintético");
   await Esperar(b,"!!document.querySelector('.infusao-sugestoes')");
   await b.CoreWebView2.ExecuteScriptAsync("document.querySelector('[data-campo=Descricao]').dispatchEvent(new KeyboardEvent('keydown',{key:'Enter',bubbles:true}))");
   await Esperar(b,"document.querySelector('[data-campo=Descricao]').value==='Medicamento sintético A'");
   await Clicar(b,"SalvarRascunho");await Task.Delay(350);
   using(var scope=sp.CreateScope()){
    var db=scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();
    var p=await db.PrescricoesInternas.Include(p=>p.Itens).SingleAsync();
    Exigir(p.AgendamentoId==sessaoId&&p.Itens.Count==3&&p.Situacao==SituacaoPrescricao.Rascunho,"Rascunho perdeu vínculo ou itens.");
    Exigir(!(await scope.ServiceProvider.GetRequiredService<ChecagemPrescricaoService>().DoDiaAsync(DateOnly.FromDateTime(DateTime.Today))).Any(),"Rascunho foi para enfermagem.");
   }
   await Fechar(b);
   await view.NavegarAsync(ModuloClinico.ChavePrescricaoInfusao);
   await Esperar(b,"!!document.querySelector('[data-comando=Editar]:not(:disabled)')");
   await Clicar(b,"Editar");
   await Esperar(b,"document.querySelectorAll('[data-infusao]').length===2 && document.querySelectorAll('[data-item-infusao]').length===3");
   Exigir(await b.CoreWebView2.ExecuteScriptAsync("document.querySelector('[data-campo=Volume]').value==='250 mL' && document.querySelector('[data-campo=Dose]').value==='2 mL'")=="true","Reabrir rascunho perdeu preparo ou dose do modelo.");
   await Clicar(b,"Assinar");await Esperar(b,"!document.querySelector('[data-dialogo]')");
   await view.NavegarAsync(ModuloClinico.ChaveAtendimento);
   await Esperar(b,"!!document.querySelector('[data-comando=\"Atendimento.PrescreverInfusao\"]')");
   await Clicar(b,"Atendimento.PrescreverInfusao");await Esperar(b,"!!document.querySelector('[data-comando=CopiarUltimaPrescricao]:not(:disabled)')");
   await Clicar(b,"CopiarUltimaPrescricao");await Esperar(b,"document.querySelectorAll('[data-item-infusao]').length===3");
   await Fechar(b);
   using(var scope=sp.CreateScope())Exigir(await scope.ServiceProvider.GetRequiredService<ClinicaDbContext>().PrescricoesInternas.CountAsync()==1,"Copiar sem salvar gravou prescrição.");
   Console.WriteLine("OK prescrição: modelo de dois grupos, sugestão por teclado, rascunho fora da fila, liberação e cópia sem gravar.");
   await Clicar(b,"emitir-receita");await Esperar(b,"!!document.querySelector('.documento-composicao')");
   await EscolherModelo(b,"receita","Receita sintética",true);
   await Esperar(b,"document.querySelector('[data-campo=Corpo]')?.textContent.includes('Conteúdo aplicado do modelo de receita')===true");
   await Esperar(b,"(()=>{const e=document.querySelector('[data-campo=Corpo]').getBoundingClientRect(),c=document.querySelector('.dialogo-corpo').getBoundingClientRect();return e.top>=c.top && e.bottom<=c.bottom})()");
   Exigir(await b.CoreWebView2.ExecuteScriptAsync("!!document.querySelector('.dialogo-rodape [data-comando=Emitir]')?.getClientRects().length && document.querySelectorAll('.documento-composicao .tabela-web').length===0")=="true","Emissão oculta ou quadros vazios presentes.");
   await Captura(view,"documento-modelo");
   await Clicar(b,"Emitir");
   for(var n=0;n<120&&ultimoPdfEntregue is null;n++)await Task.Delay(100);
   if(ultimoPdfEntregue is null) await File.WriteAllTextAsync(Path.Combine(pasta,"falha-documento.txt"),await b.CoreWebView2.ExecuteScriptAsync("document.body.innerText"));
   Exigir(ultimoPdfEntregue is not null&&File.Exists(ultimoPdfEntregue),"Modelo de receita não gerou PDF.");
   Console.WriteLine("OK documento: modelo aplicado visível, ação Emitir direta e PDF gerado.");
   await Esperar(b,"!document.querySelector('[data-dialogo]')");
   foreach(var entrada in new[]{"VerFichaWeb","VerAtendimentoWeb"})
   {
    await Clicar(b,entrada);await Task.Delay(250);await Clicar(b,"VerExamesWeb");
    await Esperar(b,"document.querySelectorAll('.secoes-clinicas [data-comando=\"Anexos.EscolherSecao\"]').length===4");
    foreach(var (nome,tabela) in new[]{("Aguardando","PedidosAguardando"),("Com resultado","Resultados"),("Arquivos da ficha","ArquivosDaFicha"),("Anexos de sessão","Anexos")})
    {
     await AcoesVisiveisQa.ClicarExpressao(b,$"Array.from(document.querySelectorAll('.secoes-clinicas button')).find(e=>e.textContent.startsWith({Q(nome)}))");
     await Esperar(b,$"!!document.querySelector('[data-tabela-container=\"Anexos.{tabela}\"]') && Array.from(document.querySelectorAll('.secoes-clinicas button')).find(e=>e.textContent.startsWith({Q(nome)}))?.getAttribute('aria-pressed')==='true'");
     Exigir(await b.CoreWebView2.ExecuteScriptAsync("document.querySelectorAll('.tabela-web[data-tabela-container^=\"Anexos.\"]').length===1")=="true","Seção escolhida mistura conteúdos de outras abas.");
    }
   }
   await Captura(view,"exames-secao-selecionada");
   Console.WriteLine("OK ficha e atendimento: quatro seções de exames/anexos acionadas; somente o conteúdo escolhido aparece.");
   await Clicar(b,"VerFichaWeb");
   await Esperar(b,"document.querySelectorAll('.secoes-clinicas [data-comando=\"Anamnese.AbrirSecao\"]').length===6");
   for(var n=0;n<6;n++)
   {
    var botao=$"document.querySelectorAll('.secoes-clinicas [data-comando=\"Anamnese.AbrirSecao\"]')[{n}]";
    await AcoesVisiveisQa.ClicarExpressao(b,botao);
    await Esperar(b,$"{botao}.getAttribute('aria-pressed')==='true' && document.querySelector('output[id*=\"Anamnese.RotuloDaSecao\"]')?.textContent.includes({botao}.textContent)===true");
   }
   Console.WriteLine("OK seis seções de anamnese: cada clique seleciona a seção e muda o título do conteúdo.");
   foreach(var (rota,comando,leitura) in new[]{("VerMedidasWeb","Medidas.Acompanhar","Medidas.LeituraSerie"),("VerAvaliacoesWeb","Avaliacoes.Escolher","Avaliacoes.LeituraCurva")})
   {
    await Clicar(b,rota);
    var seletor=".secoes-clinicas [data-comando="+Q(comando)+"]";
    await Esperar(b,$"document.querySelectorAll({Q(seletor)}).length>1");
    var quantidade=JsonSerializer.Deserialize<int>(await b.CoreWebView2.ExecuteScriptAsync($"document.querySelectorAll({Q(seletor)}).length"));
    for(var n=0;n<quantidade;n++)
    {
     var botao=$"document.querySelectorAll({Q(seletor)})[{n}]";
     await AcoesVisiveisQa.ClicarExpressao(b,botao);
     await Esperar(b,$"{botao}.textContent.trim().length>0 && {botao}.getAttribute('aria-pressed')==='true' && document.querySelector('output[id*=\"{leitura}\"]')?.textContent.includes({botao}.textContent.trim())===true");
    }
    Console.WriteLine($"OK {quantidade} opções de {rota}: nome visível e conteúdo correspondente à seleção.");
   }
  });
  sp.GetRequiredService<SessaoUsuario>().Entrar(enfermeiro);
  await ComTela(ModuloClinico.ChaveSalaInfusao,async(view,b,janela)=>
  {
   await Esperar(b,"document.body.innerText.includes('Medicamento sintético A')");
   await Clicar(b,"Abrir");await Esperar(b,"document.querySelector('[data-dialogo]')?.textContent.includes('Execução da infusão')===true");
   Exigir(await b.CoreWebView2.ExecuteScriptAsync("document.querySelector('[data-dialogo]').textContent.includes('250 mL') && document.querySelector('[data-dialogo]').textContent.includes('Medicamento sintético C')")=="true","Enfermagem não recebeu o preparo e os medicamentos.");
   await Captura(view,"enfermagem-execucao");
   await Clicar(b,"AnotarCommand");await Esperar(b,"!!document.querySelector('[data-campo=Texto]') && !!document.querySelector('[data-comando=Registrar]:not(:disabled)')");
   await Campo(b,"Texto","Evolução de enfermagem vinculada à infusão e à sessão correta");
   await Clicar(b,"Registrar");
   await Esperar(b,"document.querySelector('[data-dialogo]')?.textContent.includes('Evolução de enfermagem salva')===true");
   using(var scope=sp.CreateScope()){
    var e=await scope.ServiceProvider.GetRequiredService<ClinicaDbContext>().EvolucoesEnfermagem.SingleAsync();
    Exigir(e.AgendamentoId==sessaoId&&e.PrescricaoInternaId!=null,"Enfermagem perdeu sessão ou prescrição.");
   }
   Console.WriteLine("OK enfermagem: folha liberada recebida e evolução gravada na sessão da prescrição, mesmo com duas sessões no dia.");
  });
  sp.GetRequiredService<SessaoUsuario>().Entrar(gestor);
  await ComTela(ModuloClinico.ChaveProntuario,async(view,b,janela)=>
  {
   await Esperar(b,"!!document.querySelector('[data-comando=\"Prontuario.LinhaDoTempo.Ver\"]:not(:disabled)')");
   await Clicar(b,"Prontuario.LinhaDoTempo.Ver");
   await Esperar(b,"document.querySelector('[data-pagina=LeituraEvolucaoEnfermagem]')?.textContent.includes('Evolução de enfermagem vinculada')===true");
   Exigir(await b.CoreWebView2.ExecuteScriptAsync("document.querySelector('[data-pagina=LeituraEvolucaoEnfermagem]').textContent.includes('Prescrição: PRE')")=="true","Leitura de enfermagem omitiu a prescrição vinculada.");
   await Captura(view,"ver-evolucao-enfermagem");await Fechar(b);
   await Clicar(b,"Prontuario.AbrirSessao");
   await Esperar(b,"document.querySelector('[data-dialogo]')?.textContent.includes('Evolução de enfermagem vinculada')===true");
   await Captura(view,"sessao-com-enfermagem");
   Console.WriteLine("OK Ver abre o registro completo e a sessão médica mostra a enfermagem vinculada.");
   await Fechar(b);
   await Clicar(b,"VerAtendimentoWeb");
   await Esperar(b,"document.querySelector('[data-campo=\"Atendimento.TextoEvolucao\"]')?.textContent==='Texto aplicado do modelo de evolução'");
   Console.WriteLine("OK evolução com modelo reaberta em outra tela: conteúdo persistido preservado.");
  });
 }
}
