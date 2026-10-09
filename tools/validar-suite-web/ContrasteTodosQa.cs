using System.IO;
using System.Text.Json;
using System.Windows;
using Clinica.Desktop.Shell.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Wpf;

internal static class ContrasteTodosQa
{
    // Luminância relativa sRGB; fundo composto pelos ancestrais, inclusive transparência.
    const string Medir = """
    (()=>{
      const rgb=s=>(s.match(/[\d.]+/g)||[]).map(Number);
      const misturar=(a,b)=>a.slice(0,3).map((v,i)=>v*(a[3]??1)+b[i]*(1-(a[3]??1)));
      const fundo=e=>{const cadeia=[];for(let x=e;x;x=x.parentElement)cadeia.push(x);return cadeia.reverse().reduce((cor,x)=>misturar(rgb(getComputedStyle(x).backgroundColor),cor),[255,255,255]);};
      const lum=c=>c.slice(0,3).map(v=>{v/=255;return v<=.04045?v/12.92:((v+.055)/1.055)**2.4}).reduce((a,v,i)=>a+v*[.2126,.7152,.0722][i],0);
      const contraste=(a,b)=>{const x=lum(a),y=lum(b);return (Math.max(x,y)+.05)/(Math.min(x,y)+.05)};
      const visivel=e=>e.getClientRects().length>0&&getComputedStyle(e).visibility==='visible'&&!e.closest('[hidden]')&&!e.disabled;
      const falhas=[],contagens={campos:0,textos:0,placeholders:0};
      const conferir=(e,tipo,cor,base,minimo)=>{const razao=contraste(misturar(cor,base),base);if(razao<minimo-.01)falhas.push({tipo,campo:e.dataset.campo||e.dataset.comando||e.textContent.trim().slice(0,60),razao:Math.round(razao*100)/100,cor,base})};
      for(const e of document.querySelectorAll('.campo-web input:not([type=checkbox]):not([type=radio]),.campo-web select,.campo-web textarea,.agenda-busca-paciente input')){
        if(!visivel(e))continue;const estilo=getComputedStyle(e),base=fundo(e);contagens.campos++;
        conferir(e,'texto do campo',rgb(estilo.color),base,4.5);
        if(!e.readOnly)conferir(e,'contorno do campo',rgb(estilo.borderTopColor),fundo(e.parentElement),3);
        if(e.placeholder){contagens.placeholders++;conferir(e,'placeholder',rgb(getComputedStyle(e,'::placeholder').color),base,4.5)}
      }
      for(const e of document.querySelectorAll('.campo-web>label,.indicador-web>span,.indicador-web>strong,.indicador-web>small,.botao,.agenda-situacao,.agenda-filtros-situacao button')){
        if(!visivel(e)||!e.textContent.trim())continue;contagens.textos++;conferir(e,'texto/ação/indicador',rgb(getComputedStyle(e).color),fundo(e),4.5);
      }
      return JSON.stringify({pagina:document.querySelector('[data-pagina]')?.dataset.pagina,contagens,falhas,overflow:document.documentElement.scrollWidth>innerWidth+1});
    })()
    """;

    internal static async Task Executar(IServiceProvider sp)
    {
        var paginas=sp.GetServices<IRegistroModuloWeb>().SelectMany(r=>r.Paginas())
            .Concat(RegistroCompartilhadoWeb.Paginas()).Concat(PaginasPacotesCompartilhados.CriarPaginas())
            .DistinctBy(p=>p.Chave).ToArray();
        var dialogos=RegistroCompartilhadoWeb.Dialogos().Concat(sp.GetServices<IRegistroModuloWeb>().SelectMany(r=>r.Dialogos())).DistinctBy(d=>(d.Chave,d.Tipo)).ToArray();
        // Inventário visual dos contratos; a navegação por perfil é exercitada nos QA de cada aplicativo.
        using var view=new SuiteWebView(sp,paginas,dialogos,paginas.Select(p=>new Clinica.Desktop.Shell.Modulos.ItemMenuModulo{Chave=p.Chave,Rotulo=p.Titulo,Glifo="",Icone="arquivo",Requer=p.Permissao}).ToArray(),"Contraste — cinco módulos sintéticos",paginas[0].Chave);
        var janela=new Window{Content=view,Width=1366,Height=768,Left=-30000,Top=-30000,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.Manual};janela.Show();
        try {
        await view.QuandoPronto.WaitAsync(TimeSpan.FromSeconds(45)); var browser=(WebView2)view.Content;
        var resultados=new List<JsonElement>(); var erros=new List<string>();
        foreach(var largura in new[]{1366,900})
        {
            janela.Width=largura;janela.Height=768;
            foreach(var pagina in paginas)
            {
                await view.NavegarAsync(pagina.Chave);
                var condicao=$"document.querySelector('[data-pagina]')?.dataset.pagina==={JsonSerializer.Serialize(pagina.Chave)}&&document.querySelector('[data-pagina]')?.getAttribute('aria-busy')!=='true'";
                var pronta=false;
                for(var n=0;n<160;n++){if(await browser.CoreWebView2.ExecuteScriptAsync(condicao)=="true"){pronta=true;break;}await Task.Delay(50);}
                if(!pronta)throw new Exception("Rota não abriu para contraste: "+pagina.Chave);
                await Task.Delay(180);
                var resultado=JsonDocument.Parse(JsonSerializer.Deserialize<string>(await browser.CoreWebView2.ExecuteScriptAsync(Medir))!).RootElement.Clone();
                resultados.Add(resultado);
                if(resultado.GetProperty("overflow").GetBoolean()||resultado.GetProperty("falhas").GetArrayLength()>0)erros.Add($"{pagina.Chave} {largura}: {resultado}");
                Console.WriteLine($"CONTRASTE {pagina.Chave} {largura}: {resultado.GetProperty("contagens")}; falhas={resultado.GetProperty("falhas").GetArrayLength()}");
            }
        }
        Directory.CreateDirectory("artifacts/contraste-modulos");
        await File.WriteAllTextAsync("artifacts/contraste-modulos/medicoes.json",JsonSerializer.Serialize(resultados,new JsonSerializerOptions{WriteIndented=true}));
        if(erros.Count>0)throw new Exception(string.Join("\n",erros));
        Console.WriteLine($"OK contraste calculado: {paginas.Length} rotas dos cinco módulos em1366/900px, texto mínimo4,5:1 e contorno3:1; sem overflow.");
        } finally { janela.Close(); }
    }
}
