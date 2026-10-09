using System.Text.Json;
using Clinica.Application.Servicos;
using Clinica.Desktop.Shell.Web;
using Clinica.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Wpf;

internal static class GerenteOperacoesUiQa
{
    internal static async Task Executar(IServiceProvider sp, SuiteWebView view, WebView2 browser)
    {
        async Task Esperar(string condicao)
        {
            for (var i = 0; i < 160; i++)
            {
                if (await browser.CoreWebView2.ExecuteScriptAsync(condicao) == "true") return;
                await Task.Delay(75);
            }
            throw new Exception("Gerente, resultado do clique ausente: " + condicao + "; " + await browser.CoreWebView2.ExecuteScriptAsync("document.body.innerText.slice(-1800)"));
        }
        async Task Clicar(string seletor)
        {
            await Esperar($"!!document.querySelector({JsonSerializer.Serialize(seletor)}) && !document.querySelector({JsonSerializer.Serialize(seletor)}).disabled");
            await AcoesVisiveisQa.Clicar(browser, seletor);
        }
        async Task Campo(string chave, string valor, string prefixo = ".dialogo-web ")
        {
            var seletor = prefixo + "[data-campo='" + chave + "']";
            await Esperar($"!!document.querySelector({JsonSerializer.Serialize(seletor)}) && !document.querySelector({JsonSerializer.Serialize(seletor)}).disabled");
            await browser.CoreWebView2.ExecuteScriptAsync($"(()=>{{const e=document.querySelector({JsonSerializer.Serialize(seletor)});e.value={JsonSerializer.Serialize(valor)};e.dispatchEvent(new Event('input',{{bubbles:true}}));e.dispatchEvent(new Event('change',{{bubbles:true}}));}})()");
            await Task.Delay(150);
        }
        async Task Salvar()
        {
            await Clicar(".dialogo-web [data-comando='salvar']");
            await Esperar("!document.querySelector('.dialogo-web')");
        }
        await view.NavegarAsync("metas");
        await Clicar("[data-comando='NovaMeta']");
        await Campo("Valor", "1250,50");
        await Campo("Observacoes", "Meta criada pelo botão WebView2");
        await Salvar();
        async Task ConferirMeta(decimal valorEsperado = 1250.50m)
        {
            using var scope = sp.CreateScope();
            var metas = await scope.ServiceProvider.GetRequiredService<MetaService>().DoAnoAsync(DateTime.Today.Year);
            if (!metas.Any(m => m.Valor == valorEsperado && m.Observacoes == "Meta criada pelo botão WebView2"))
                throw new Exception("Salvar/cancelar meta não preservou o valor e as observações esperados.");
        }
        await ConferirMeta();
        await Clicar("[data-comando='Editar']");
        await Esperar("document.querySelector('.dialogo-web [data-campo=Observacoes]')?.value==='Meta criada pelo botão WebView2'");
        await Campo("Valor", "1300,50");
        await Salvar();
        await ConferirMeta(1300.50m);
        await Clicar("[data-comando='Editar']");
        await Campo("Observacoes", "Esta edição será cancelada");
        await Clicar(".dialogo-web [data-fechar-dialogo]");
        await Esperar("!document.querySelector('.dialogo-web')");
        await ConferirMeta(1300.50m);
        await Clicar("[data-comando='Excluir']");
        await Clicar(".dialogo-web [data-fechar-dialogo]");
        await Esperar("!document.querySelector('.dialogo-web')");
        await ConferirMeta(1300.50m);
        Console.WriteLine("OK Gerente botões: criar/salvar meta, editar/reabrir conteúdo, cancelar edição e recusar exclusão preservam dados.");

        await view.NavegarAsync("acessos");
        await Clicar("[data-comando='Novo']");
        await Campo("Nome", "Usuário sintético por clique");
        await Campo("Login", "usuario.clique.qa");
        await Campo("Senha", "SenhaSintetica!2468");
        await Salvar();
        using (var scope = sp.CreateScope())
            if (!await scope.ServiceProvider.GetRequiredService<ClinicaDbContext>().Usuarios.AnyAsync(u => u.Login == "usuario.clique.qa" && u.Nome == "Usuário sintético por clique"))
                throw new Exception("Salvar usuário pela interface não persistiu.");
        Console.WriteLine("OK Gerente botões: Novo usuário e Salvar usuário gravam o cadastro sintético.");

        await view.NavegarAsync("precos-convenio");
        await Clicar("[data-comando='NovoPreco']");
        await Esperar("document.querySelector('.dialogo-web [data-campo=Convenio]')?.options.length>1");
        await browser.CoreWebView2.ExecuteScriptAsync("(()=>{const e=document.querySelector('.dialogo-web [data-campo=Convenio]');e.value=[...e.options].find(o=>o.value).value;e.dispatchEvent(new Event('change',{bubbles:true}));})()");
        await Campo("Valor", "145,50");
        await Salvar();
        using (var scope = sp.CreateScope())
            if (!(await scope.ServiceProvider.GetRequiredService<PrecoConvenioService>().CatalogoAsync()).Any(p => p.Valor == 145.50m))
                throw new Exception("Salvar preço pela interface não persistiu.");
        Console.WriteLine("OK Gerente botões: Novo preço e Salvar preço preservam convênio selecionado e valor decimal.");

        foreach (var rota in new[] { "documentos", "documentos-emitidos" })
        {
            await view.NavegarAsync(rota);
            await Campo("Codigo", "", "");
            await Clicar("[data-comando='Conferir']");
            await Esperar("document.body.innerText.includes('Digite o código impresso')");
            await Campo("Codigo", "CODIGO-SINTETICO-INEXISTENTE", "");
            await Clicar("[data-comando='Conferir']");
            await Esperar("document.body.innerText.includes('Nenhum documento com o código') && document.body.innerText.includes('CODIGO-SINTETICO-INEXISTENTE')");
            Console.WriteLine("OK Conferir em " + rota + ": código vazio explicado; inexistente informa ausência sem emitir documento.");
        }
    }
}
