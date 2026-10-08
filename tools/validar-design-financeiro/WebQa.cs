using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using Clinica.Financeiro.Web;
using Microsoft.Web.WebView2.Wpf;

/// <summary>Teste de integração do conteúdo empacotado com WebView2 e banco sintético do harness.</summary>
static class WebQa
{
    public static async Task Executar(IServiceProvider servicos, string saida)
    {
        string? destino = null;
        var janela = new FinanceiroWebWindow(servicos, chave => destino = chave)
        {
            Width = 1440, Height = 900, Left = -30000, Top = -30000,
            WindowStartupLocation = WindowStartupLocation.Manual, ShowInTaskbar = false
        };
        System.Windows.Application.Current.MainWindow = janela;
        try
        {
            janela.Show();
            await janela.TelaWeb.QuandoPronto.WaitAsync(TimeSpan.FromSeconds(45));
            var navegador = Desc<WebView2>(janela).Single();
            async Task<string> Ler(string codigo) => await navegador.CoreWebView2.ExecuteScriptAsync(codigo);
            async Task Esperar(string expressao)
            {
                var prazo = DateTime.UtcNow.AddSeconds(15);
                while (DateTime.UtcNow < prazo)
                {
                    if (await Ler(expressao) == "true") return;
                    await Task.Delay(100);
                }
                throw new InvalidOperationException("WebView2 não confirmou: " + expressao);
            }
            const string seletorLinhas = "document.querySelectorAll('[data-testid=tabela-lancamentos] tr')";
            await Esperar($"{seletorLinhas}.length > 3");
            await Esperar("![...document.querySelectorAll('td.valor')].some(c => /[+−-]\\s*[+−-]/.test(c.textContent))");
            if (!navegador.Source.AbsoluteUri.StartsWith("https://financeiro.clinica.local/"))
                throw new InvalidOperationException("A interface não usa os arquivos locais esperados.");
            if (await Ler("document.querySelector('img').complete && document.querySelector('img').naturalWidth > 0") != "true")
                throw new InvalidOperationException("Logo local não carregada.");
            foreach (var (largura, altura) in new[] { (1440, 900), (1100, 720), (900, 600) })
            {
                janela.Width = largura; janela.Height = altura;
                await Task.Delay(300);
                await Esperar("document.documentElement.scrollWidth <= window.innerWidth + 1");
                using var destinoImagem = File.Create(Path.Combine(saida, $"financeiro-web-{largura}.png"));
                await janela.TelaWeb.CapturarPreviewAsync(destinoImagem);
                Console.WriteLine($"OK web {largura}x{altura}: assets locais, logo, layout e dados C#.");
            }
            janela.Width = 1440; janela.Height = 900;
            await Task.Delay(200);
            await Ler("document.querySelector('[data-testid=alternar-privacidade]').click()");
            await Esperar("!(/R\\$\\s*[0-9]/.test(document.querySelector('.conteudo').innerText))");
            await Ler("document.querySelector('[data-testid=alternar-privacidade]').click()");
            await Ler("document.querySelector('[data-testid=tabela-lancamentos]').scrollIntoView({block:'start'})");
            await Esperar("document.querySelector('.conteudo').scrollTop > 0");
            using (var imagemMovimentos = File.Create(Path.Combine(saida, "financeiro-web-movimentacoes.png")))
                await janela.TelaWeb.CapturarPreviewAsync(imagemMovimentos);
            var antes = int.Parse(await Ler(seletorLinhas + ".length"));
            await Ler("chrome.webview.postMessage({acao:'filtrar',valor:'Materiais'})");
            await Esperar($"{seletorLinhas}.length > 0 && {seletorLinhas}.length < {antes}");
            await Ler("chrome.webview.postMessage({acao:'filtrar',valor:''})");
            await Esperar($"{seletorLinhas}.length === {antes}");
            await Ler("chrome.webview.postMessage({acao:'mes',valor:'2040-01'})");
            await Esperar("document.querySelector('[data-testid=valor-resultado]')?.textContent.includes('0,00') === true");
            await Ler("chrome.webview.postMessage({acao:'mes',valor:" + JsonSerializer.Serialize(DateTime.Today.ToString("yyyy-MM")) + "})");
            await Esperar($"{seletorLinhas}.length === {antes}");
            await Ler("chrome.webview.postMessage({acao:'navegar',valor:'contas'})");
            await Task.Delay(200);
            if (destino != "contas") throw new InvalidOperationException("Rota permitida não chegou ao shell.");
            destino = null;
            await Ler("chrome.webview.postMessage({acao:'navegar',valor:'rota-inexistente'})");
            await Task.Delay(200);
            if (destino is not null) throw new InvalidOperationException("Rota fora da lista foi aceita.");
            navegador.CoreWebView2.Navigate("https://example.com/");
            await Task.Delay(250);
            if (!navegador.CoreWebView2.Source.StartsWith("https://financeiro.clinica.local/"))
                throw new InvalidOperationException("A navegação saiu da origem local permitida.");
            await Esperar($"{seletorLinhas}.length === {antes}");
            Console.WriteLine("OK bridge web: filtros reais, limpar, mês vazio, retorno do mês e navegação restrita.");
        }
        finally { janela.Close(); }
    }
    static IEnumerable<T> Desc<T>(DependencyObject pai) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(pai); i++)
        {
            var filho = VisualTreeHelper.GetChild(pai, i);
            if (filho is T encontrado) yield return encontrado;
            foreach (var item in Desc<T>(filho)) yield return item;
        }
    }
}
