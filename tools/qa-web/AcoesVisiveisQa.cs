using System.Text.Json;
using Microsoft.Web.WebView2.Wpf;

// O teste alcança a ação pelo mesmo menu visível que a pessoa utiliza.
internal static class AcoesVisiveisQa
{
    internal static Task Clicar(WebView2 browser, string seletor, bool opcional = false) =>
        ClicarExpressao(browser, "document.querySelector(" + JsonSerializer.Serialize(seletor) + ")", opcional);

    internal static async Task ClicarExpressao(WebView2 browser, string expressao, bool opcional = false)
    {
        string Script(string etapa) => $$"""
            (()=>{try{
                const b={{expressao}};
                if(!b){if({{JsonSerializer.Serialize(opcional)}})return true;throw Error('Ação de teste ausente');}
                if(b.disabled)throw Error('Ação de teste desabilitada');
                const menu=b.closest('.acoes-menu-painel');
                const abrir=menu?document.querySelector('[data-abrir-acoes="'+CSS.escape(menu.id)+'"]'):null;
                const etapa={{JsonSerializer.Serialize(etapa)}};
                if(etapa==='rolar'){
                    const alvo=menu&&!menu.matches(':popover-open')?abrir:b;
                    if(!alvo)throw Error('Acionador do menu ausente');
                    alvo.scrollIntoView({block:'center',inline:'nearest',behavior:'instant'});
                    return true;
                }
                if(etapa==='abrir'){
                    if(menu&&!menu.matches(':popover-open'))abrir.click();
                    return true;
                }
                if(!b.getClientRects().length)throw Error('Ação não está visível após abrir seu menu');
                if(menu){
                    const m=menu.getBoundingClientRect(),r=b.getBoundingClientRect();
                    if(r.bottom>m.bottom)menu.scrollTop+=r.bottom-m.bottom+4;
                    else if(r.top<m.top)menu.scrollTop-=m.top-r.top+4;
                }
                const r=b.getBoundingClientRect();
                if(r.width<=0||r.height<=0||r.left<0||r.right>innerWidth+1||r.top<0||r.bottom>innerHeight+1)
                    return {repetir:true,erro:'Ação fora da área acessível: '+JSON.stringify({retangulo:r.toJSON(),largura:innerWidth,altura:innerHeight})};
                b.click();return true;
            }catch(e){return {erro:String(e)};} })()
            """;
        // Uma resposta de navegação pode reposicionar a página depois da primeira rolagem.
        // Reencontra e alcança a ação novamente, sem repetir cliques ou aceitar ação inacessível.
        string? ultimoDeslocamento = null;
        for (var tentativa = 0; tentativa < 20; tentativa++)
        {
            foreach (var etapa in new[] { "rolar", "abrir", "clicar" })
            {
                var resultado = await browser.CoreWebView2.ExecuteScriptAsync(Script(etapa));
                if (resultado != "true")
                {
                    using var estado = JsonDocument.Parse(resultado);
                    if (etapa == "clicar" && estado.RootElement.TryGetProperty("repetir", out var repetir) && repetir.GetBoolean())
                    {
                        ultimoDeslocamento = resultado;
                        break;
                    }
                    throw new Exception("Não foi possível " + etapa + " a ação visível: " + expressao + " — " + resultado);
                }
                if (etapa == "clicar") return;
                await Task.Delay(80);
            }
        }
        throw new Exception("A ação permaneceu fora da área acessível: " + expressao + " — " + ultimoDeslocamento);
    }
}
