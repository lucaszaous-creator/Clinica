using System.IO;
using Clinica.Clinico.Modulo;
using Clinica.Desktop.Shell.Web;
using Clinica.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Wpf;

static partial class Fluxos
{
    static async Task ValidarFichaVisualAsync(IServiceProvider sp, SuiteWebView view, WebView2 browser, Func<string?> pdfEntregue)
    {
        async Task Esperar(string expressao)
        {
            for (var i = 0; i < 120; i++)
            {
                if (await browser.CoreWebView2.ExecuteScriptAsync(expressao) == "true") return;
                await Task.Delay(80);
            }
            Console.WriteLine("FICHA DOM " + await browser.CoreWebView2.ExecuteScriptAsync("JSON.stringify({titulo:document.title,texto:document.body.innerText.slice(-16000),comandos:Array.from(document.querySelectorAll('[data-comando]')).map(b=>({chave:b.dataset.comando,disabled:b.disabled}))})"));
            throw new Exception("Ficha não atingiu estado visual: " + expressao);
        }
        Task Clicar(string comando) => AcoesVisiveisQa.Clicar(browser, "[data-comando=\"" + comando + "\"]");
        async Task Fechar()
        {
            await AcoesVisiveisQa.Clicar(browser, "[data-dialogo] [data-fechar-dialogo]");
            await Esperar("!document.querySelector('[data-dialogo]')");
        }
        await view.NavegarAsync(ModuloClinico.ChavePaciente);
        await Esperar("document.querySelectorAll('[data-comando=\"Capa.AbrirSessao\"]').length === 2");
        Exigir(await browser.CoreWebView2.ExecuteScriptAsync("document.querySelectorAll('[data-comando=\"Capa.AbrirSessao\"]:disabled').length === 1 && document.body.textContent.includes('Sem evolução vinculada')") == "true", "Sessão sem evolução deve ficar identificada e indisponível para leitura.");
        await AcoesVisiveisQa.Clicar(browser, "[data-comando=\"Capa.AbrirSessao\"]:not(:disabled)");
        await Esperar("!!document.querySelector('[data-dialogo]') && document.querySelector('[data-dialogo]').textContent.includes('Evolução sintética')");
        await Fechar();
        Console.WriteLine("OK WebView2 ficha: Ver sessão direto abre a evolução vinculada; agendamento sem evolução fica indisponível.");
        await Clicar("VerHistoricoWeb");
        await Esperar("!!document.querySelector('[data-comando=\"Prontuario.AbrirSessao\"]')");
        await Clicar("Prontuario.AbrirSessao");
        await Esperar("!!document.querySelector('[data-dialogo]') && document.querySelector('[data-dialogo]').textContent.includes('Evolução sintética')");
        Exigir(await browser.CoreWebView2.ExecuteScriptAsync("document.querySelector('[data-dialogo]').textContent.includes('Paciente fictícia de demonstração')") == "true", "Sessão abriu com paciente incorreto.");

        await Clicar("ImprimirCommand");
        for (var i = 0; i < 150 && pdfEntregue() is null; i++) await Task.Delay(100);
        var arquivo = pdfEntregue();
        Exigir(arquivo is not null && File.Exists(arquivo), "Imprimir sessão não entregou PDF.");
        using (var pdf = PdfSharp.Pdf.IO.PdfReader.Open(arquivo!, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import))
            Exigir(pdf.PageCount > 0, "PDF da sessão sem páginas.");
        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();
            var documento = await db.DocumentosClinicos.Include(d => d.Itens).OrderByDescending(d => d.Id).FirstAsync();
            var evolucao = await db.Evolucoes.SingleAsync();
            Exigir(documento.PacienteId == evolucao.PacienteId && documento.PeriodoInicio == evolucao.Data && documento.PeriodoFim == evolucao.Data,
                "PDF não foi emitido para o paciente e dia da sessão aberta.");
            Exigir(documento.Itens.Any(i => i.Detalhe?.Contains("Evolução sintética") == true), "Documento emitido omitiu a evolução da sessão.");
        }
        await Esperar("document.querySelector('[data-dialogo]').textContent.includes('emitida')");
        await Clicar("VerCorrecoesCommand");
        await Esperar("document.querySelector('[data-dialogo]')?.textContent.includes('Correções da sessão de') === true && document.querySelector('[data-dialogo]').textContent.includes('Versão atual')");
        await AcoesVisiveisQa.Clicar(browser, "[data-dialogo] [data-fechar-dialogo]");
        await Esperar("!!document.querySelector('[data-comando=\"VerAnexosCommand\"]')");
        await Clicar("VerAnexosCommand");
        await Esperar("document.querySelector('[data-dialogo]')?.textContent.includes('Anexos da sessão') === true");
        await Fechar();
        await Clicar("VerExamesWeb");
        await Esperar("!!document.querySelector('[data-comando=\"Anexos.RegistrarResultado\"]') && document.body.textContent.includes('Exame sintético')");
        await Clicar("VerFichaWeb");
        await Esperar("!!document.querySelector('[data-comando=\"Capa.NovoProblema\"]')");
        await Clicar("Atendimento.PrescreverInfusao");
        await Esperar("document.querySelector('[data-dialogo]')?.textContent.includes('Prescrição de infusão') === true");
        Exigir(await browser.CoreWebView2.ExecuteScriptAsync("document.querySelector('[data-dialogo]').textContent.includes('Paciente fictícia de demonstração')") == "true", "Infusão pela ficha perdeu o paciente.");
        await Fechar();
        using (var scope = sp.CreateScope())
            Exigir(!await scope.ServiceProvider.GetRequiredService<ClinicaDbContext>().PrescricoesInternas.AnyAsync(), "Cancelar infusão pela ficha criou prescrição.");
        Console.WriteLine("OK WebView2 ficha: histórico → abrir/ver sessão → imprimir PDF do paciente/dia → correções → anexos → exames → ficha → infusão cancelada sem gravação.");
    }
}
