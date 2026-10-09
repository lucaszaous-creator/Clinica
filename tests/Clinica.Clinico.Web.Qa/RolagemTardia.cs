using Microsoft.Web.WebView2.Wpf;

static partial class Fluxos
{
    static async Task ValidarRolagemTardiaAsync(WebView2 browser)
    {
        await browser.CoreWebView2.ExecuteScriptAsync("""
            (()=>{
                const caixa=document.createElement('div');
                caixa.id='qa-rolagem-tardia';
                caixa.style.cssText='position:fixed;inset:20px auto auto 20px;width:320px;height:180px;overflow:auto;z-index:99999;background:white;scroll-behavior:auto';
                caixa.innerHTML='<div style="height:2000px"></div><button type="button">Ação sintética de rolagem</button><div style="height:400px"></div>';
                document.body.append(caixa);
                const botao=caixa.querySelector('button');
                window.__qaRolagem={reiniciou:false,cliques:0,fora:false};
                caixa.addEventListener('scroll',()=>{
                    if(caixa.scrollTop>0&&!window.__qaRolagem.reiniciou){
                        window.__qaRolagem.reiniciou=true;
                        caixa.scrollTo({top:0,behavior:'instant'});
                    }
                });
                botao.addEventListener('click',()=>{
                    const r=botao.getBoundingClientRect();
                    window.__qaRolagem.cliques++;
                    window.__qaRolagem.fora=r.top<0||r.bottom>innerHeight;
                });
            })()
            """);
        try
        {
            await AcoesVisiveisQa.Clicar(browser, "#qa-rolagem-tardia button");
            Exigir(await browser.CoreWebView2.ExecuteScriptAsync("window.__qaRolagem.reiniciou && window.__qaRolagem.cliques===1 && !window.__qaRolagem.fora") == "true",
                "Rolagem reiniciada deve ser recuperada antes de um único clique visível.");
            await browser.CoreWebView2.ExecuteScriptAsync("document.querySelector('#qa-rolagem-tardia button').style.cssText='position:fixed;top:200vh;left:20px'");
            var rejeitou = false;
            try { await AcoesVisiveisQa.Clicar(browser, "#qa-rolagem-tardia button"); }
            catch (Exception ex) when (ex.Message.StartsWith("A ação permaneceu fora da área acessível:")) { rejeitou = true; }
            Exigir(rejeitou && await browser.CoreWebView2.ExecuteScriptAsync("window.__qaRolagem.cliques===1") == "true",
                "Ação permanentemente inacessível precisa falhar sem nenhum clique adicional.");
            Console.WriteLine("OK rolagem tardia: reset entre rolar e clicar recuperado; exatamente um clique visível; ação inacessível rejeitada sem clique.");
        }
        finally
        {
            await browser.CoreWebView2.ExecuteScriptAsync("document.querySelector('#qa-rolagem-tardia')?.remove(); delete window.__qaRolagem;");
        }
    }
}
