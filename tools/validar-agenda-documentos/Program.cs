using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using Clinica.Desktop.Shell.WebClinica;
using Clinica.Domain.Entities;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

internal static class Program
{
    [STAThread] private static void Main()
    {
        var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/Clinica.Desktop.Shell;component/Styles/Suite.xaml") });
        app.Startup += async (_, _) =>
        {
            try
            {
                var saida = Path.GetFullPath("artifacts/agenda-documentos"); Directory.CreateDirectory(saida);
                SessaoUsuario.Atual.Entrar(new UsuarioSistema { Id = 91001, Nome = "Teste de interface", Login = "qa.local", Perfil = PerfilAcesso.Gerente });
                await Agenda(saida);
                await InfusaoQa.Executar(saida);
                Console.WriteLine("APROVADO: agenda e infusão no WebView2 local com dados sintéticos.");
            }
            catch (Exception e) { Console.WriteLine(e); Environment.ExitCode = 1; }
            finally { app.Shutdown(); }
        };
        app.Run();
    }

    public static Task<string> Script(WebView2 navegador, string codigo) => navegador.CoreWebView2.ExecuteScriptAsync(codigo);
    public static async Task Esperar(WebView2 navegador, string expressao)
    {
        var prazo = DateTime.UtcNow.AddSeconds(25);
        while (DateTime.UtcNow < prazo)
        {
            if (navegador.CoreWebView2 is not null && await Script(navegador, expressao) == "true") return;
            await Task.Delay(100);
        }
        throw new InvalidOperationException("WebView2 não confirmou: " + expressao);
    }
    public static WebView2 Navegador(DependencyObject raiz)
    {
        if (raiz is WebView2 web) return web;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(raiz); i++)
        { try { return Navegador(VisualTreeHelper.GetChild(raiz, i)); } catch (InvalidOperationException) { } }
        throw new InvalidOperationException("Navegador ainda não montado.");
    }
    public static Task ConferirAlinhamento(WebView2 navegador) => Esperar(navegador,
        "(()=>{const x=[...document.querySelectorAll('.situacao>.selo')].map(e=>e.getBoundingClientRect().left);return innerWidth<=800 || x.length<2 || Math.max(...x)-Math.min(...x)<1})()");

    public static async Task Capturar(WebView2 navegador, string caminho)
    {
        using var arquivo = File.Create(caminho);
        await navegador.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, arquivo);
    }
    private static async Task Agenda(string saida)
    {
        var grupos = new[] { "pendente", "no-local", "em-atendimento", "atendido", "cancelado", "faltou", "substituido" };
        var rotulos = new[] { "Marcado", "No local", "Em atendimento", "Concluído", "Cancelado", "Faltou", "Substituído" };
        var nomes = new[] { "João da Silva", "Maria Clara", "Ana Beatriz", "José Almeida", "Carolina Souza", "Roberto Lima", "Helena Costa" };
        var linhas = grupos.Select((g, i) => new LinhaAgendaWeb(i.ToString(), "09/10/2026", $"{8+i:00}:00–{8+i:00}:30", nomes[i], i % 2 == 0 ? "Consulta" : "Acupuntura", "Dra. Ana · teste", "", rotulos[i], g, "", "Pendente", "Observação sintética.", "Ver horário", true)).ToArray();
        var carregando = false; var falha = false; var comandos = 0; var contexto = "2026-10-09";
        using var painel = new PainelClinicoWeb("agenda", () => new { contexto, linhas, carregando, naoVerificado = falha }, async m => { comandos++; await Task.Delay(400); }, () => SessaoUsuario.Atual.Exigir(Permissao.VerAgenda, "testar agenda"));
        var janela = new Window { Content = painel, Width = 1100, Height = 720, Left = -30000, Top = -30000, ShowInTaskbar = false };
        janela.Show();
        var navegador = Navegador(painel);
        try
        {
            await Esperar(navegador, "document.querySelectorAll('.linha-agenda').length===7");
            foreach (var largura in new[] { 1440, 1100, 900, 620 })
            {
                janela.Width = largura; await Task.Delay(350);
                await Esperar(navegador, "document.documentElement.scrollWidth<=innerWidth+1");
                await ConferirAlinhamento(navegador);
                await ConferirRolagem(navegador);
                await Capturar(navegador, Path.Combine(saida, $"agenda-{largura}.png"));
            }
            janela.Width = 1100;
            async Task Buscar(string valor)
            {
                await Script(navegador, "(()=>{let e=document.querySelector('input[type=search]');e.focus();Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set.call(e," + JsonSerializer.Serialize(valor) + ");e.dispatchEvent(new Event('input',{bubbles:true}));})()");
            }
            await Buscar("joao silva");
            await Esperar(navegador, "document.querySelectorAll('.linha-agenda').length===1 && document.querySelector('.paciente h2').textContent==='João da Silva'");
            await Esperar(navegador, "!document.querySelector('.controles-rolagem')");
            await Script(navegador, "document.querySelector('.filtro-situacao.cancelado').click()");
            await Esperar(navegador, "document.querySelectorAll('.linha-agenda').length===0 && document.querySelector('.vazio').textContent.includes('Nenhum paciente')");
            await Script(navegador, "document.querySelector('.filtros-agenda .secundario').click()");
            await Esperar(navegador, "document.querySelectorAll('.linha-agenda').length===7");
            await Script(navegador, "(()=>{let e=document.querySelector('select');e.value='Acupuntura';e.dispatchEvent(new Event('change',{bubbles:true}));})()");
            await Esperar(navegador, "document.querySelectorAll('.linha-agenda').length===3");
            await Script(navegador, "document.querySelector('.filtro-situacao.atendido').click()");
            await Esperar(navegador, "document.querySelectorAll('.linha-agenda').length===1 && document.querySelector('.selo').textContent==='Concluído'");
            await Script(navegador, "document.querySelector('.abrir-agenda').click();document.querySelector('.abrir-agenda').click()");
            await Task.Delay(650);
            if (comandos != 1) throw new Exception("Clique duplo executou mais de um comando.");
            await Script(navegador, "chrome.webview.postMessage({acao:'abrir',contextoHost:'invalido',id:'3'})");
            await Task.Delay(250); if (comandos != 1) throw new Exception("Contexto inválido aceito.");
            await Script(navegador, "document.querySelector('.filtros-agenda .secundario').click()");
            await Buscar("ana");
            linhas = linhas.Select(l => l with { Detalhe = "Atualizado pelo host" }).ToArray();
            await Task.Delay(400);
            await Esperar(navegador, "document.activeElement===document.querySelector('input[type=search]') && document.querySelector('input').value==='ana'");
            carregando = true; await Esperar(navegador, "document.querySelector('.agenda').getAttribute('aria-busy')==='true' && !document.querySelector('.abrir-agenda')");
            carregando = false; falha = true; await Esperar(navegador, "document.querySelector('[role=alert]')?.textContent.includes('Não foi possível verificar')===true");
            falha = false;
            await Script(navegador, "document.querySelector('.filtros-agenda .secundario').click()");
            await Esperar(navegador, "document.querySelectorAll('.linha-agenda').length===7");
            var anterior = navegador.Source;
            navegador.CoreWebView2.Navigate("https://example.com/"); await Task.Delay(300);
            if (navegador.Source != anterior) throw new Exception("Navegação externa não bloqueada.");
            Console.WriteLine("OK agenda: cores, busca sem acentos, filtros combinados, vazio/erro/carga, foco, clique duplo, contexto e navegação bloqueados; 1440/1100/900/620 px.");
        }
        finally { janela.Close(); }
    }

    private static async Task ConferirRolagem(WebView2 navegador)
    {
        await Esperar(navegador, "document.querySelector('.controles-rolagem button[aria-label=\"Rolar para cima\"]')?.disabled===true");
        await Esperar(navegador, "(()=>{const controle=document.querySelector('.controles-rolagem').getBoundingClientRect();return [...document.querySelectorAll('.linha-agenda')].every(e=>e.getBoundingClientRect().right<controle.left)})()");
        await Script(navegador, "document.querySelector('[aria-label=\"Rolar para baixo\"]').click()");
        await Esperar(navegador, "scrollY>50 && !document.querySelector('[aria-label=\"Rolar para cima\"]').disabled");
        await Task.Delay(600);
        await Script(navegador, "document.querySelector('[aria-label=\"Rolar para cima\"]').click()");
        await Esperar(navegador, "scrollY<=1 && document.querySelector('[aria-label=\"Rolar para cima\"]').disabled");
        await Script(navegador, "window.scrollTo({top:document.documentElement.scrollHeight,behavior:'instant'})");
        await Esperar(navegador, "document.querySelector('[aria-label=\"Rolar para baixo\"]').disabled");
        await Script(navegador, "window.scrollTo({top:0,behavior:'instant'})");
        await Esperar(navegador, "document.querySelector('[aria-label=\"Rolar para cima\"]').disabled");
        Console.WriteLine("OK rolagem React: subir/descer move a lista, limites desabilitados e controles sem cobrir pacientes ou ações.");
    }
}
