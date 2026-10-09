using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

internal static class ReactReconciliacaoQa
{
    internal static async Task Executar()
    {
        var pasta=Path.GetFullPath("artifacts/react-reconciliacao");Directory.CreateDirectory(pasta);
        var web=new WebView2();var janela=new Window{Content=web,Width=900,Height=720,Left=-30000,Top=-30000,ShowInTaskbar=false};janela.Show();
        try
        {
            await web.EnsureCoreWebView2Async(await CoreWebView2Environment.CreateAsync(null,Path.Combine(pasta,"perfil")));
            web.CoreWebView2.SetVirtualHostNameToFolderMapping("qa.local",Path.Combine(AppContext.BaseDirectory,"WebSuite","wwwroot"),CoreWebView2HostResourceAccessKind.DenyCors);
            var pronto=new TaskCompletionSource();web.CoreWebView2.WebMessageReceived+=(_,e)=>{if(e.WebMessageAsJson.Contains("pronto"))pronto.TrySetResult();};
            web.Source=new Uri("https://qa.local/index.html");await pronto.Task.WaitAsync(TimeSpan.FromSeconds(30));
            object Campo(string chave,string tipo,object valor,object[]? opcoes=null)=>new{chave,tipo,rotulo=chave,valor,opcoes=opcoes??[],visivel=true,habilitado=true,obrigatorio=false};
            object Linha(string id)=>new{id,celulas=new{Nome="Registro "+id,Horario="09:00",Profissional="Profissional fictício",Status="Pendente",Sala="Sala 1",Convenio="Convênio fictício",Observacoes="Informação completa "+id},campos=Array.Empty<object>(),acoes=Array.Empty<object>(),selecionada=false};
            object Estado(string selecao,bool inverter=false,string contexto="react-contexto")=>new
            {
                tipo="estado",titulo="QA React",usuario="Demonstração",ocupado=false,rotas=Array.Empty<object>(),
                pagina=new{chave="react-teste",contexto,titulo="Reconciliação de componentes",campos=new[]{Campo("Selecao","select",selecao,[new{valor="a",rotulo="Opção A"},new{valor="b",rotulo="Opção B"}]),Campo("Editor","texto-rico",new{texto="Texto canônico",formato=""})},indicadores=Array.Empty<object>(),acoes=Array.Empty<object>(),carregando=false,naoVerificado=false,mensagemEhErro=false,truncado=false,
                    secoes=new[]{new{chave="Registros",titulo="Registros",campos=Array.Empty<object>(),indicadores=Array.Empty<object>(),acoes=Array.Empty<object>(),tabelas=new[]{new{chave="Linhas",titulo="Registros",vazio="Vazio",colunas=new[]{new{chave="Nome",rotulo="Nome",tipo="texto"},new{chave="Horario",rotulo="Horário",tipo="texto"},new{chave="Profissional",rotulo="Profissional",tipo="texto"},new{chave="Status",rotulo="Situação",tipo="status"},new{chave="Sala",rotulo="Sala",tipo="texto"},new{chave="Convenio",rotulo="Convênio",tipo="texto"},new{chave="Observacoes",rotulo="Observações",tipo="texto"}},linhas=inverter?new[]{Linha("b"),Linha("a")}:new[]{Linha("a"),Linha("b")}}}}}}
            };
            async Task Enviar(object estado){web.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(estado));await Task.Delay(250);}
            async Task Conferir(string js,string mensagem){if(await web.CoreWebView2.ExecuteScriptAsync(js)!="true")throw new Exception(mensagem);}
            await Enviar(Estado("a"));
            await web.CoreWebView2.ExecuteScriptAsync("window.qaSelecao=document.querySelector('[data-campo=Selecao]');qaSelecao.focus();window.qaDetalhe=document.querySelector('[data-linha-id=a] details');qaDetalhe.open=true;window.qaLinha=document.querySelector('[data-linha-id=a]')");
            await Enviar(Estado("b",true));
            await Conferir("qaSelecao===document.querySelector('[data-campo=Selecao]')&&qaSelecao===document.activeElement&&qaSelecao.value==='b'","React não sincronizou seleção programática no mesmo controle focado.");
            await Conferir("qaDetalhe===document.querySelector('[data-linha-id=a] details')&&qaDetalhe.open&&qaLinha===document.querySelector('[data-linha-id=a]')&&document.querySelector('.registro-cartao').dataset.linhaId==='b'","Reordenar registros recriou a linha ou perdeu os detalhes abertos.");
            Console.WriteLine("OK React: seleção programática focada e reordenação preservam identidade dos controles, linhas e detalhes abertos.");
            await web.CoreWebView2.ExecuteScriptAsync("window.qaEditor=document.querySelector('[data-campo=Editor]');qaEditor.focus();const r=document.createRange();r.selectNodeContents(qaEditor);r.collapse(false);const s=getSelection();s.removeAllRanges();s.addRange(r)");
            await web.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.insertText","{\"text\":\" digitado\"}");
            await Enviar(Estado("b",false));
            await Conferir("qaEditor===document.querySelector('[data-campo=Editor]')&&qaEditor===document.activeElement&&qaEditor.innerText==='Texto canônico digitado'&&getSelection().anchorNode&&qaEditor.contains(getSelection().anchorNode)","Resposta do host recriou o editor ou apagou texto/seleção em edição.");
            Console.WriteLine("OK React: editor mantém texto canônico digitado e seleção durante resposta atrasada do host.");
            await Enviar(Estado("a",false,"outro-contexto"));
            await Conferir("document.querySelector('[data-campo=Editor]').innerText==='Texto canônico'&&document.querySelector('[data-campo=Selecao]').value==='a'","Troca de contexto herdou rascunho de outro registro.");
            Console.WriteLine("OK React: novo contexto não herda rascunhos nem seleção de outro registro.");
            await Enviar(new { tipo="estado",titulo="QA React",usuario="Demonstração",ocupado=false,rotas=Array.Empty<object>(),pagina=new { chave="sugestoes",contexto="contexto-sugestoes",titulo="Selecionar paciente",campos=new[]{Campo("Seletor.Termo","texto","Paciente"),Campo("Seletor.Selecionado","selecao","",[new{valor="a",rotulo="Paciente fictício A"},new{valor="b",rotulo="Paciente fictício B"}])},indicadores=Array.Empty<object>(),acoes=Array.Empty<object>(),secoes=Array.Empty<object>(),carregando=false,naoVerificado=false,mensagemEhErro=false,truncado=false } });
            await Conferir("document.querySelectorAll('[data-sugestao-paciente]').length===2&&document.querySelector('[data-campo=\"Seletor.Selecionado\"]').value===''","Sugestões não exibiram opções do host ou escolheram paciente sem confirmação.");
            await web.CoreWebView2.ExecuteScriptAsync("document.querySelector('[data-campo=\"Seletor.Termo\"]').focus()");
            await web.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent","{\"type\":\"keyDown\",\"key\":\"ArrowDown\",\"windowsVirtualKeyCode\":40}");
            await web.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent","{\"type\":\"keyUp\",\"key\":\"ArrowDown\",\"windowsVirtualKeyCode\":40}");
            await Conferir("document.activeElement.dataset.sugestaoPaciente==='a'","Teclado não alcançou as sugestões a partir da busca.");
            await web.CoreWebView2.ExecuteScriptAsync("document.querySelector('[data-sugestao-paciente=b]').click()");await Task.Delay(250);
            await Conferir("document.querySelector('[data-campo=\"Seletor.Selecionado\"]').value==='b'&&!document.querySelector('[data-sugestao-paciente]')","Escolha explícita não selecionou paciente ou deixou resultados abertos.");
            Console.WriteLine("OK React: sugestões mostram apenas opções do host, sem seleção automática; teclado e escolha explícita preservam o seletor original.");
            var mensagens=new List<string>();web.CoreWebView2.WebMessageReceived+=(_,e)=>mensagens.Add(e.WebMessageAsJson);
            foreach(var foco in new[]{"input","resultado"})
            {
                await Enviar(new { tipo="estado",titulo="QA React",usuario="Demonstração",ocupado=false,rotas=Array.Empty<object>(),dialogo=new{id="dialogo-sugestoes-"+foco,ocupado=false,podeFechar=true,pagina=new { chave="sugestoes-dialogo",titulo="Selecionar paciente",campos=new[]{Campo("Seletor.Termo","texto","Paciente"),Campo("Seletor.Selecionado","selecao","",[new{valor="a",rotulo="Paciente fictício A"}])},indicadores=Array.Empty<object>(),acoes=Array.Empty<object>(),secoes=Array.Empty<object>(),carregando=false,naoVerificado=false,mensagemEhErro=false,truncado=false }} });
                await web.CoreWebView2.ExecuteScriptAsync(foco=="input"?"document.querySelector('[data-campo=\"Seletor.Termo\"]').focus()":"document.querySelector('[data-sugestao-paciente]').focus()");mensagens.Clear();
                await web.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent","{\"type\":\"keyDown\",\"key\":\"Escape\",\"windowsVirtualKeyCode\":27}");
                await web.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent","{\"type\":\"keyUp\",\"key\":\"Escape\",\"windowsVirtualKeyCode\":27}");await Task.Delay(100);
                await Conferir("document.querySelector('.dialogo-web')!==null&&!document.querySelector('[data-sugestao-paciente]')","Escape não fechou somente as sugestões no diálogo.");
                if(mensagens.Any(m=>m.Contains("dlg-fechar")))throw new Exception("Escape nas sugestões tentou fechar o diálogo pai: "+foco);
            }
            Console.WriteLine("OK React: Escape na busca e nos resultados fecha somente sugestões, preservando o diálogo pai.");
        }
        finally{web.Dispose();janela.Close();}
    }
}
