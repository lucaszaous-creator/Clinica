using Clinica.Clinico.Modulo;
using Clinica.Desktop.Shell.Web;
using Microsoft.Web.WebView2.Wpf;

static partial class Fluxos
{
    static async Task ValidarCamposComplementaresAsync(PaginasWebController pages, DialogosWebController dialogs, Func<Task<DialogoWebDto>> esperar)
    {
        var abrir = pages.ExecutarAcaoAsync("Atendimento.AbrirDetalhe");
        var dialogo = await esperar();
        var linhas = dialogo.Pagina.Secoes.SelectMany(s => s.Tabelas).Single(t => t.Chave == "CamposPersonalizados").Linhas;
        Exigir(linhas.Count == 3, "Diálogo não oferece os campos personalizados retirados da evolução.");
        foreach (var linha in linhas)
        {
            var campo = linha.Campos.Single(c => c.Visivel);
            var valor = campo.Chave == "RespostaTextoWeb" ? "Resposta sintética preservada"
                : campo.Opcoes.Single(o => o.Rotulo == (campo.Chave == "RespostaListaWeb" ? "Opção B" : "Sim")).Valor;
            await dialogs.AtualizarCampoAsync(dialogo.Id, campo.Chave, J(valor), "CamposPersonalizados", linha.Id);
        }
        dialogs.Fechar(dialogo.Id);
        await abrir;
        Console.WriteLine("OK campos complementares: três tipos editados pelo diálogo da ponte; gravação usa o mesmo rascunho da sessão.");
    }

    static async Task ValidarEvolucaoSimplificadaAsync(SuiteWebView view, WebView2 browser)
    {
        async Task Esperar(string expressao)
        {
            for (var i = 0; i < 100; i++)
            {
                if (await browser.CoreWebView2.ExecuteScriptAsync(expressao) == "true") return;
                await Task.Delay(80);
            }
            throw new Exception("Evolução simplificada não atingiu estado: " + expressao);
        }
        // NavegarAsync aguarda o host; a ponte entrega o novo contexto ao DOM depois.
        var contextoAnterior = await browser.CoreWebView2.ExecuteScriptAsync("document.querySelector('[data-comando=\"Atendimento.AbrirDetalhe\"]')?.dataset.contexto ?? null");
        await view.NavegarAsync(ModuloClinico.ChaveAtendimento);
        await Esperar("!!document.querySelector('[data-comando=\"Atendimento.AbrirDetalhe\"]') && document.querySelector('[data-comando=\"Atendimento.AbrirDetalhe\"]').dataset.contexto !== " + contextoAnterior);
        Exigir(await browser.CoreWebView2.ExecuteScriptAsync("!document.querySelector('[data-tabela-container=\"Atendimento.AlertasClinicos\"],[data-tabela-container=\"Atendimento.Alertas\"],[data-tabela-container=\"Atendimento.CamposPersonalizados\"]')") == "true",
            "Evolução ainda exibe os blocos dispensados pelo proprietário.");
        await AcoesVisiveisQa.Clicar(browser, "[data-comando=\"Atendimento.AbrirDetalhe\"]");
        await Esperar("!!document.querySelector('[data-dialogo] [data-campo=\"RespostaTextoWeb\"]')");
        Exigir(await browser.CoreWebView2.ExecuteScriptAsync("document.querySelector('[data-dialogo] [data-campo=\"RespostaTextoWeb\"]').value === 'Resposta sintética preservada' && document.querySelector('[data-dialogo] [data-campo=\"RespostaListaWeb\"]').selectedOptions[0].textContent === 'Opção B' && document.querySelector('[data-dialogo] [data-campo=\"RespostaSimNaoWeb\"]').selectedOptions[0].textContent === 'Sim'") == "true",
            "Reabertura da sessão não restaurou os campos complementares persistidos.");
        await AcoesVisiveisQa.Clicar(browser, "[data-dialogo] [data-fechar-dialogo]");
        await Esperar("!document.querySelector('[data-dialogo]')");
        Console.WriteLine("OK WebView2 evolução simplificada: três tabelas ausentes; campos complementares acessíveis no diálogo e restaurados do SQLite.");
    }
}
