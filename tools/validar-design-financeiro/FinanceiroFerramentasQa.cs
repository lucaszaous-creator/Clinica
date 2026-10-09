using System.IO;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Desktop.Shell.Treinamento;
using Clinica.Domain.Entities;
using Clinica.Financeiro.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Wpf;

/// <summary>Exercita as ferramentas globais no host original do Financeiro, com progresso isolado.</summary>
static class FinanceiroFerramentasQa
{
    public static async Task Executar(IServiceProvider services, FinanceiroWebWindow janela, WebView2 browser, string raiz, string saida)
    {
        async Task Script(string codigo) => await browser.CoreWebView2.ExecuteScriptAsync(codigo);
        async Task Esperar(string expressao)
        {
            for (var i = 0; i < 200; i++)
            {
                if (await browser.CoreWebView2.ExecuteScriptAsync(expressao) == "true") return;
                await Task.Delay(75);
            }
            throw new InvalidOperationException("Ferramentas Financeiro: " + expressao + "; DOM: " + await browser.CoreWebView2.ExecuteScriptAsync("JSON.stringify({video:document.querySelector('video')?.outerHTML,error:document.querySelector('video')?.error?.message,texto:document.body.innerText.slice(0,800)})"));
        }
        async Task Foto(string nome)
        {
            using var imagem = File.Create(Path.Combine(saida, nome + ".png"));
            await janela.TelaWeb.CapturarPreviewAsync(imagem);
        }
        if (await browser.CoreWebView2.ExecuteScriptAsync("!!document.querySelector('[data-fila-infusao]')") != "false")
            throw new InvalidOperationException("Financeiro publicou atalho de infusão sem rota correspondente.");
        services.GetRequiredService<SnackbarService>().Info("Aviso sintético: valor R$ 123,45 para conferência de privacidade");
        await Esperar("!!document.querySelector('[data-avisos] small')");
        await Script("document.querySelector('[data-testid=alternar-privacidade]').click();document.querySelector('[data-avisos]').click()");
        await Esperar("document.querySelector('.painel-avisos')?.textContent.includes('Aviso sintético') && !document.querySelector('[data-avisos] small') && !document.querySelector('.painel-avisos').textContent.includes('123,45') && !document.querySelector('.toast').textContent.includes('123,45')");
        await Foto("financeiro-avisos-privacidade");
        await Script("document.querySelector('.painel-avisos [data-avisos]').click();document.querySelector('[data-testid=alternar-privacidade]').click()");
        await Script("document.querySelector('[data-treinamento]').click()");
        await Esperar("!!document.querySelector('[data-aula=\"pacotes\"]') && document.querySelectorAll('[data-aula]').length===1");
        await Foto("financeiro-treinamento-catalogo");
        await Script("document.getElementById('busca-aula').value='aula inexistente';document.getElementById('busca-aula').dispatchEvent(new Event('input',{bubbles:true}))");
        await Esperar("!document.querySelector('[data-aula]') && document.body.textContent.includes('Nenhuma aula corresponde')");
        await Script("document.getElementById('busca-aula').value='';document.getElementById('busca-aula').dispatchEvent(new Event('input',{bubbles:true}))");
        // A mídia publicada acompanha o QA: o teste de botões não depende da rede/GitHub.
        // O acervo real continua validando o SHA antes de liberar a reprodução.
        var aula = CatalogoTreinamento.Ler().Single(a => a.Id == "pacotes");
        MidiaTreinamentoQa.Preparar(raiz, aula);
        using var cancelamento = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        await new AcervoTreinamento(SessaoUsuario.Atual.UsuarioId, raiz).ObterVideoAsync(aula, cancelamento.Token);
        await Script("document.querySelector('[data-aula=\"pacotes\"]').click()");
        await Esperar("document.querySelector('video')?.readyState>=1");
        await Script("document.querySelector('video').currentTime=12.5;document.querySelector('video').muted=true;document.querySelector('video').play()");
        await Esperar("document.querySelector('video')?.currentTime>13 && !document.querySelector('video').paused");
        await Foto("financeiro-treinamento-video");
        await Script("document.querySelector('video').pause();document.querySelector('[data-aula-concluir]').click()");
        await Esperar("document.querySelector('[data-aula=\"pacotes\"]')?.textContent.includes('Concluída')");
        var progresso = new AcervoTreinamento(SessaoUsuario.Atual.UsuarioId, raiz).Progresso("pacotes");
        if (progresso.Posicao < 13 || !progresso.Concluida) throw new InvalidOperationException("Playback/conclusão não persistiu no acervo isolado.");
        await Script("document.querySelector('[data-sair-treinamento]').click()");
        await Esperar("!document.querySelector('video') && !!document.querySelector('[data-testid=alternar-privacidade]')");
        await Script("document.querySelector('[data-treinamento]').click()");
        await Esperar("document.querySelector('video')?.readyState>=1 && document.querySelector('video').currentTime>=13");
        await Script("document.querySelector('[data-aula-reiniciar]').click()");
        await Esperar("document.querySelector('video')?.currentTime<1");
        await Task.Delay(300);
        progresso = new AcervoTreinamento(SessaoUsuario.Atual.UsuarioId, raiz).Progresso("pacotes");
        if (progresso.Posicao != 0 || !progresso.Concluida) throw new InvalidOperationException("Reinício perdeu conclusão ou não reiniciou posição.");
        await Script("window.__externoQa='pendente';fetch('https://externo.invalid/arquivo').then(()=>window.__externoQa='permitido').catch(()=>window.__externoQa='bloqueado')");
        await Esperar("window.__externoQa==='bloqueado'");
        await Script("document.querySelector('[data-sair-treinamento]').click()");
        await Esperar("!!document.querySelector('[data-testid=alternar-privacidade]')");
        Console.WriteLine("OK ferramentas Financeiro: avisos/lidos e privacidade, catálogo autorizado/filtro, vídeo local publicado SHA/reprodução, progresso/retomada/conclusão/reinício e recurso externo bloqueado.");
    }
}
