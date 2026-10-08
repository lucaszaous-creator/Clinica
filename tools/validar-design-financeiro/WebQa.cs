using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using Clinica.Financeiro.Web;
using Microsoft.Web.WebView2.Wpf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Clinica.Infrastructure;
using Clinica.Financeiro.Modulo;
using Clinica.Desktop.Shell.Web;
using Clinica.Domain.Entities;

/// <summary>Teste de integração do conteúdo empacotado com WebView2 e banco sintético do harness.</summary>
static class WebQa
{
    public static async Task Executar(IServiceProvider servicos, string saida, bool apenasNavegacao = false)
    {
        var falhas = FinanceiroPaginasController.ValidarRegistro();
        if (falhas.Count > 0) throw new InvalidOperationException(string.Join("\n", falhas));
        var treinamento = Path.Combine(saida, "treinamento-" + Guid.NewGuid().ToString("N"));
        var janela = new FinanceiroWebWindow(servicos, treinamento)
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
                    if (System.Windows.Application.Current.Windows.Cast<Window>().Any(w => w != janela && w.IsVisible))
                        throw new InvalidOperationException("Uma operação financeira abriu uma janela nativa.");
                    if (await Ler(expressao) == "true") return;
                    await Task.Delay(100);
                }
                throw new InvalidOperationException("WebView2 não confirmou: " + expressao);
            }
            const string seletorLinhas = "document.querySelectorAll('[data-testid=tabela-lancamentos] tr')";
            var rotas = new ModuloFinanceiro().Itens.Where(i => i.Abas.Count == 0 && i.Chave != ModuloFinanceiro.ChaveAjuda).Select(i => i.Chave).Distinct().ToArray();
            await Esperar($"{seletorLinhas}.length > 3");
            await Esperar("!!document.querySelector('.navegacao-topo') && !document.querySelector('.trilho-financeiro') && document.querySelector('.conteudo').getBoundingClientRect().left === 0");
            await Ler("document.querySelector('[data-grupo=contas]').dispatchEvent(new PointerEvent('pointerover',{bubbles:true,pointerType:'mouse'}))");
            await Esperar("document.querySelector('#nav-contas').getAttribute('aria-expanded') === 'true' && !document.querySelector('#submenu-contas').hidden");
            await Ler("document.querySelector('#nav-contas').focus();document.querySelector('#nav-contas').dispatchEvent(new KeyboardEvent('keydown',{key:'ArrowDown',bubbles:true}))");
            await Esperar("document.activeElement.closest('#submenu-contas') !== null");
            await Ler("document.activeElement.dispatchEvent(new KeyboardEvent('keydown',{key:'Escape',bubbles:true}))");
            await Esperar("document.querySelector('#submenu-contas').hidden && document.activeElement.id === 'nav-contas'");
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
                await Esperar("JSON.stringify([...document.querySelectorAll('.navegacao-topo [data-action=navegar]')].map(b=>b.dataset.value).sort()) === " + JsonSerializer.Serialize(JsonSerializer.Serialize(rotas.OrderBy(r => r).ToArray())));
                await Esperar("(()=>{const itens=[...document.querySelectorAll('.navegacao-topo>*,.busca-global,.ferramenta-topo,.usuario-area')].map(e=>e.getBoundingClientRect());return itens.every(r=>r.left>=0 && r.right<=innerWidth+1) && !itens.some((a,i)=>itens.slice(i+1).some(b=>Math.min(a.right,b.right)>Math.max(a.left,b.left)+1 && Math.min(a.bottom,b.bottom)>Math.max(a.top,b.top)+1));})()");
                await Esperar("[...document.querySelectorAll('.busca-global,.ferramenta-topo')].every(b=>{const r=b.getBoundingClientRect();return r.width>0&&r.height>0&&b.contains(document.elementFromPoint(r.x+r.width/2,r.y+r.height/2))})");
                foreach (var grupo in new[] { "caixa", "contas", "recebimentos", "gestao", "analises" })
                {
                    // Clique sem hover (toque), navegação por setas e encerramento pelo teclado.
                    await Ler("document.querySelector('#nav-" + grupo + "').click()");
                    await Esperar("document.querySelector('#nav-" + grupo + "').getAttribute('aria-expanded') === 'true'");
                    await Ler("document.querySelector('#nav-" + grupo + "').focus();document.activeElement.dispatchEvent(new KeyboardEvent('keydown',{key:'ArrowDown',bubbles:true}))");
                    await Esperar("document.activeElement === document.querySelector('#submenu-" + grupo + " button')");
                    await Ler("document.activeElement.dispatchEvent(new KeyboardEvent('keydown',{key:'End',bubbles:true}))");
                    await Esperar("document.activeElement === [...document.querySelectorAll('#submenu-" + grupo + " button')].at(-1)");
                    await Ler("document.activeElement.dispatchEvent(new KeyboardEvent('keydown',{key:'ArrowDown',bubbles:true}))");
                    await Esperar("document.activeElement === document.querySelector('#submenu-" + grupo + " button')");
                    await Esperar("[...document.querySelectorAll('#submenu-" + grupo + " button')].every(e=>{const r=e.getBoundingClientRect();return r.width>0 && r.left>=0 && r.right<=innerWidth+1 && r.top>=0 && r.bottom<=innerHeight;})");
                    using (var menuImagem = File.Create(Path.Combine(saida, $"financeiro-topo-{grupo}-{largura}.png")))
                        await janela.TelaWeb.CapturarPreviewAsync(menuImagem);
                    await Ler("document.activeElement.dispatchEvent(new KeyboardEvent('keydown',{key:'Escape',bubbles:true}))");
                    await Esperar("document.querySelector('#submenu-" + grupo + "').hidden && document.activeElement.id === 'nav-" + grupo + "'");
                    // Hover abre, sair com o ponteiro fecha quando o foco já saiu do grupo.
                    await Ler("document.querySelector('.conteudo').focus();document.querySelector('[data-grupo=" + grupo + "]').dispatchEvent(new PointerEvent('pointerover',{bubbles:true,pointerType:'mouse'}))");
                    await Esperar("!document.querySelector('#submenu-" + grupo + "').hidden");
                    await Ler("document.querySelector('[data-grupo=" + grupo + "]').dispatchEvent(new PointerEvent('pointerout',{bubbles:true,pointerType:'mouse',relatedTarget:document.body}))");
                    await Esperar("document.querySelector('#submenu-" + grupo + "').hidden");
                }
                using var destinoImagem = File.Create(Path.Combine(saida, $"financeiro-web-{largura}.png"));
                await janela.TelaWeb.CapturarPreviewAsync(destinoImagem);
                Console.WriteLine($"OK web {largura}x{altura}: assets locais, logo, layout e dados C#.");
            }
            if (apenasNavegacao) { Console.WriteLine($"OK navegação superior: {rotas.Length} rotas, 5 menus, hover, clique, teclado e capturas em três tamanhos."); return; }
            janela.Width = 1440; janela.Height = 900;
            await Task.Delay(200);
            await FinanceiroFerramentasQa.Executar(servicos, janela, navegador, treinamento, saida);
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
            async Task Navegar(string chave)
            {
                // Exercita a porta do usuário: hover abre o grupo e o clique envia a rota.
                var seletor = JsonSerializer.Serialize(".navegacao-topo [data-action=navegar][data-value='" + chave + "']");
                await Ler("(()=>{const item=document.querySelector(" + seletor + ");if(!item)throw new Error('Rota ausente no topo');item.closest('.grupo-topo').dispatchEvent(new PointerEvent('pointerover',{bubbles:true,pointerType:'mouse'}));})()");
                await Esperar("(()=>{const item=document.querySelector(" + seletor + ");const r=item.getBoundingClientRect();return r.width>0 && r.left>=0 && r.right<=innerWidth+1 && r.top>=0 && r.bottom<=innerHeight;})()");
                await Ler("document.querySelector(" + seletor + ").click()");
                await Esperar("document.querySelector('.conteudo').dataset.rota === " + JsonSerializer.Serialize(chave));
                await Esperar("document.querySelector('.conteudo').getAttribute('aria-busy') === 'false'");
                await Task.Delay(150);
            }
            async Task Clicar(string seletor)
            {
                await Esperar("document.querySelector(" + JsonSerializer.Serialize(seletor) + ")?.disabled === false");
                await AcoesVisiveisQa.Clicar(navegador, seletor);
            }
            async Task Campo(string chave, string valor)
            {
                var seletor = ".dialogo-web [data-campo='" + chave + "']";
                await Esperar("!!document.querySelector(" + JsonSerializer.Serialize(seletor) + ")");
                await Ler("(()=>{let el=document.querySelector(" + JsonSerializer.Serialize(seletor) + ");el.value=" + JsonSerializer.Serialize(valor) + ";el.dispatchEvent(new Event('input',{bubbles:true}));el.dispatchEvent(new Event('change',{bubbles:true}));})()");
                await Task.Delay(150);
            }
            async Task Fechar()
            {
                await Clicar(".dialogo-cabecalho [data-fechar-dialogo]");
                await Esperar("!document.querySelector('.dialogo-web')");
            }
            async Task Capturar(string nome)
            {
                using var arquivo = File.Create(Path.Combine(saida, nome + ".png"));
                await janela.TelaWeb.CapturarPreviewAsync(arquivo);
            }
            await Esperar("JSON.stringify([...document.querySelectorAll('.navegacao-topo [data-action=navegar]')].map(b=>b.dataset.value).sort()) === " + JsonSerializer.Serialize(JsonSerializer.Serialize(rotas.OrderBy(r => r).ToArray())));
            foreach (var (largura, altura) in new[] { (1440, 900), (1100, 720), (900, 600) })
            {
                janela.Width = largura; janela.Height = altura;
                foreach (var rota in rotas)
                {
                    await Navegar(rota);
                    await Esperar("document.documentElement.scrollWidth <= innerWidth + 1");
                    await Esperar("!!document.querySelector('.conteudo h1')");
                    await Capturar($"financeiro-completo-{rota}-{largura}");
                    Console.WriteLine($"OK página web {rota} {largura}x{altura}: mesma janela, título, navegação e layout.");
                }
            }
            janela.Width = 1100; janela.Height = 720;
            await Navegar("caixa");
            await Clicar("[data-action='novo']");
            await Esperar("!!document.querySelector('.dialogo-web')");
            await Clicar(".dialogo-rodape [data-comando='salvar']");
            await Esperar("!!document.querySelector('.dialogo-corpo [role=alert]')");
            await Campo("Descricao", "QA WEB despesa integral");
            await Campo("Valor", "123,45");
            await Ler("chrome.webview.postMessage({acao:'dlg-campo',id:document.querySelector('.dialogo-web').dataset.dialogo,chave:'Data',valor:'data-invalida'})");
            await Clicar(".dialogo-rodape [data-comando='salvar']");
            await Esperar("document.querySelector('.dialogo-corpo')?.innerText.includes('Corrija os campos inválidos') === true");
            using (var scope = servicos.CreateScope())
                if (await scope.ServiceProvider.GetRequiredService<ClinicaDbContext>().Lancamentos.AnyAsync(l => l.Descricao == "QA WEB despesa integral"))
                    throw new InvalidOperationException("Campo inválido foi ignorado ao gravar com valor anterior.");
            await Campo("Data", DateTime.Today.ToString("yyyy-MM-dd"));
            await Esperar("[...document.querySelectorAll('.dialogo-rodape button')].filter(b => b.textContent.trim() === 'Cancelar').length === 1");
            await Capturar("financeiro-formulario-lancamento");
            await Clicar(".dialogo-rodape [data-comando='salvar']");
            await Esperar("!document.querySelector('.dialogo-web')");
            using (var scope = servicos.CreateScope())
            {
                var registro = await scope.ServiceProvider.GetRequiredService<ClinicaDbContext>().Lancamentos.AsNoTracking().SingleAsync(l => l.Descricao == "QA WEB despesa integral");
                if (registro.Valor != 123.45m) throw new InvalidOperationException("O lançamento web não preservou o valor digitado.");
            }
            // CPF digitado sem blur: reproduz a busca que o usuário faz no formulário,
            // com o seletor C# consultando um banco sintético e publicando opções reais.
            int pacienteBuscaId;
            using (var scope = servicos.CreateScope())
            {
                var dbBusca = scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();
                var pacienteBusca = new Paciente { Nome = "Zuleica Sintética Busca Financeiro", Documento = "52998224725", Convenio = Clinica.Domain.Convenio.UnimedIntercambio, Sexo = Clinica.Domain.Sexo.Feminino };
                dbBusca.Pacientes.Add(pacienteBusca); await dbBusca.SaveChangesAsync(); pacienteBuscaId = pacienteBusca.Id;
            }
            await Clicar("[data-action='novo']");
            await Esperar("!!document.querySelector('.dialogo-web')");
            await Ler("(()=>{const e=document.querySelector('.dialogo-web select[data-campo=Tipo]');e.value=[...e.options].find(o=>o.textContent==='Entrada').value;e.dispatchEvent(new Event('change',{bubbles:true}));})()");
            await Esperar("!!document.querySelector('.dialogo-web input[data-campo=\"Seletor.Termo\"]')");
            await Ler("(()=>{const e=document.querySelector('.dialogo-web input[data-campo=\"Seletor.Termo\"]');e.focus();e.value='52998224725';e.dispatchEvent(new Event('input',{bubbles:true}));})()");
            await Esperar("[...document.querySelector('.dialogo-web select[data-campo=\"Seletor.Selecionado\"]').options].some(o=>o.textContent.includes('Zuleica Sintética Busca Financeiro'))");
            await Ler("(()=>{const e=document.querySelector('.dialogo-web select[data-campo=\"Seletor.Selecionado\"]');e.value=[...e.options].find(o=>o.textContent.includes('Zuleica Sintética Busca Financeiro')).value;e.dispatchEvent(new Event('change',{bubbles:true}));})()");
            await Campo("Descricao", "QA WEB receita paciente digitado"); await Campo("Valor", "12.5");
            await Capturar("financeiro-busca-paciente-cpf");
            await Clicar(".dialogo-rodape [data-comando='salvar']"); await Esperar("!document.querySelector('.dialogo-web')");
            using (var scope = servicos.CreateScope())
            {
                var registro = await scope.ServiceProvider.GetRequiredService<ClinicaDbContext>().Lancamentos.AsNoTracking().SingleAsync(l => l.Descricao == "QA WEB receita paciente digitado");
                if (registro.PacienteId != pacienteBuscaId || registro.Valor != 12.5m)
                    throw new InvalidOperationException("Financeiro standalone perdeu paciente buscado pelo CPF ou valor decimal 12.5.");
            }
            Console.WriteLine("OK Financeiro standalone DOM: CPF sem blur, resultado, seleção e gravação paciente + decimal 12.5.");
            await Clicar("[data-action='novo']");
            await Esperar("!!document.querySelector('.dialogo-web')");
            await Campo("Descricao", "QA WEB não salvar");
            await Campo("Valor", "999");
            await Fechar();
            using (var scope = servicos.CreateScope())
                if (await scope.ServiceProvider.GetRequiredService<ClinicaDbContext>().Lancamentos.AnyAsync(l => l.Descricao == "QA WEB não salvar"))
                    throw new InvalidOperationException("Cancelar gravou o lançamento.");
            await Navegar("contas");
            await Clicar("[data-comando='NovaConta']");
            await Esperar("!!document.querySelector('.dialogo-web')");
            await Campo("Descricao", "QA WEB conta prevista");
            await Campo("Valor", "240,00");
            await Capturar("financeiro-formulario-conta");
            await Clicar(".dialogo-rodape [data-comando='salvar']");
            await Esperar("!document.querySelector('.dialogo-web')");
            using (var scope = servicos.CreateScope())
            {
                var registro = await scope.ServiceProvider.GetRequiredService<ClinicaDbContext>().Lancamentos.AsNoTracking().SingleAsync(l => l.Descricao == "QA WEB conta prevista");
                if (registro.Valor != 240m || registro.Status != Clinica.Domain.Entities.StatusLancamento.Previsto)
                    throw new InvalidOperationException("Conta web não preservou valor/status previsto.");
            }
            await Navegar("plano-contas");
            await Clicar("[data-comando='NovaCategoria']");
            await Esperar("!!document.querySelector('.dialogo-web')");
            await Campo("Codigo", "QA_WEB"); await Campo("Nome", "Categoria web de teste");
            await Clicar(".dialogo-rodape [data-comando='salvar']");
            await Esperar("!document.querySelector('.dialogo-web')");
            using (var scope = servicos.CreateScope())
                if (!await scope.ServiceProvider.GetRequiredService<ClinicaDbContext>().CategoriasFinanceiras.AnyAsync(c => c.Codigo == "QA_WEB"))
                    throw new InvalidOperationException("Categoria criada via web não persistiu.");
            await Navegar("estoque");
            await Clicar("[data-comando='NovoItem']");
            await Esperar("!!document.querySelector('.dialogo-web')");
            await Campo("Nome", "Item web de teste");
            await Capturar("financeiro-formulario-estoque");
            await Clicar(".dialogo-rodape [data-comando='salvar']");
            await Esperar("!document.querySelector('.dialogo-web')");
            using (var scope = servicos.CreateScope())
                if (!await scope.ServiceProvider.GetRequiredService<ClinicaDbContext>().ItensEstoque.AnyAsync(i => i.Nome == "Item web de teste"))
                    throw new InvalidOperationException("Item criado via web não persistiu.");
            await Navegar("caixa");
            await Ler("chrome.webview.postMessage({acao:'navegar',valor:'rota-inexistente'})");
            await Task.Delay(200);
            await Esperar("document.querySelector('.conteudo').dataset.rota === 'caixa'");
            navegador.CoreWebView2.Navigate("https://example.com/");
            await Task.Delay(250);
            if (!navegador.CoreWebView2.Source.StartsWith("https://financeiro.clinica.local/"))
                throw new InvalidOperationException("A navegação saiu da origem local permitida.");
            await Esperar($"{seletorLinhas}.length >= {antes}");
            var usuarioAnterior = await servicos.GetRequiredService<ClinicaDbContext>().Usuarios.AsNoTracking().SingleAsync(u => u.Id == SessaoUsuario.Atual.UsuarioId);
            try
            {
                SessaoUsuario.Atual.Entrar(new UsuarioSistema { Id = usuarioAnterior.Id + 100000, Nome = "Outra sessão sintética", Login = "qa-troca", Perfil = PerfilAcesso.Recepcao });
                await Ler("chrome.webview.postMessage({acao:'treinamento'})");
                EntradaWebWindow? aviso = null;
                for (var i = 0; i < 100; i++)
                {
                    aviso = System.Windows.Application.Current.Windows.OfType<EntradaWebWindow>().FirstOrDefault();
                    if (aviso?.Content is WebView2 entrada && entrada.CoreWebView2 is not null &&
                        await entrada.CoreWebView2.ExecuteScriptAsync("document.body.innerText.includes('Seu acesso ao Financeiro não está disponível')") == "true") break;
                    await Task.Delay(100);
                }
                if (janela.IsVisible || janela.TelaWeb.Content is not null || aviso?.Content is not WebView2 paginaAviso ||
                    await paginaAviso.CoreWebView2.ExecuteScriptAsync("document.body.innerText.includes('Seu acesso ao Financeiro não está disponível') && !document.body.innerText.includes('Consulta particular')") != "true")
                    throw new InvalidOperationException("Perda da sessão não descartou o DOM financeiro e abriu aviso web limpo.");
                aviso.Close();
                Console.WriteLine("OK acesso revogado: DOM financeiro descartado e aviso local HTML sem dados anteriores.");
            }
            finally { SessaoUsuario.Atual.Entrar(usuarioAnterior); }
            Console.WriteLine("OK web completo: 15 páginas em três dimensões, formulários com gravação e cancelamento, validação, filtros reais e navegação restrita sem telas WPF.");
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
