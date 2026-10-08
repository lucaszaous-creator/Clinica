using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

// Cenário visual sintético: não substitui os testes de serviços ou gravação dos módulos.
internal static class RolagemQa
{
    internal static async Task Executar()
    {
        var pasta = Path.GetFullPath("artifacts/rolagem-web"); Directory.CreateDirectory(pasta);
        var navegador = new WebView2();
        var janela = new Window { Content = navegador, Width = 900, Height = 720, Left = -30000, Top = -30000, ShowInTaskbar = false };
        janela.Show();
        try
        {
            var ambiente = await CoreWebView2Environment.CreateAsync(null, Path.Combine(pasta, "perfil"));
            await navegador.EnsureCoreWebView2Async(ambiente);
            navegador.CoreWebView2.SetVirtualHostNameToFolderMapping("qa.local", Path.Combine(AppContext.BaseDirectory, "WebSuite", "wwwroot"), CoreWebView2HostResourceAccessKind.DenyCors);
            var pronto = new TaskCompletionSource();
            navegador.CoreWebView2.WebMessageReceived += (_, e) => { if (e.WebMessageAsJson.Contains("pronto")) pronto.TrySetResult(); };
            navegador.Source = new Uri("https://qa.local/index.html");
            await pronto.Task.WaitAsync(TimeSpan.FromSeconds(30));

            object Tabela(string chave) => new { chave, titulo = chave, colunas = Enumerable.Range(1, 6).Select(i => new { chave = "Col" + i, rotulo = "Coluna " + i, tipo = "texto" }).ToArray(), linhas = new[] { new { id = chave, celulas = Enumerable.Range(1, 6).ToDictionary(i => "Col" + i, i => "Texto extenso de demonstração " + i), campos = Array.Empty<object>(), acoes = Array.Empty<object>(), selecionada = false } }, vazio = "Sem registros" };
            object Secao(string chave, object[] tabelas) => new { chave, titulo = chave, campos = Array.Empty<object>(), indicadores = Array.Empty<object>(), acoes = Array.Empty<object>(), tabelas };
            async Task Estado(object[] secoes)
            {
                navegador.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { tipo = "estado", titulo = "QA sintético", usuario = "Demonstração", ocupado = false, rotas = new[] { new { chave = "teste", rotulo = "Teste", grupo = "Gestão" } }, pagina = new { chave = "teste", contexto = "mesmo-contexto", titulo = "Separação visual e rolagem", campos = Array.Empty<object>(), indicadores = Array.Empty<object>(), acoes = Array.Empty<object>(), secoes, carregando = false, naoVerificado = false, mensagemEhErro = false, truncado = false } }));
                await Task.Delay(250);
            }
            async Task Conferir(string js, string erro)
            {
                if (await navegador.CoreWebView2.ExecuteScriptAsync(js) != "true") throw new Exception(erro);
            }
            await Estado([Secao("Primeira", [Tabela("A")]), Secao("Segunda", [Tabela("B")])]);
            await Conferir("(() => {const a=document.querySelector('[data-tabela-container=A] .tabela-scroll');a.style.width='300px';a.querySelector('table').style.minWidth='900px';a.scrollLeft=200;return a.scrollLeft===200})()", "Cenário técnico precisa de região com rolagem.");
            await Estado([Secao("Segunda", [Tabela("B")])]);
            await Conferir("document.querySelector('[data-tabela-container=B] .tabela-scroll').scrollLeft===0", "Tabela B herdou deslocamento da tabela removida A.");
            await Conferir("getComputedStyle(document.querySelector('.conteudo')).backgroundColor!==getComputedStyle(document.querySelector('.secao-web')).backgroundColor", "Fundo e seção precisam ter contraste.");
            Console.WriteLine("OK WebView2: tabela removida não transfere rolagem; superfícies distintas.");

            object Registro(string chave, string[] nomes, object[] campos) => new {
                chave, titulo = chave, colunas = nomes.Select(n => new { chave=n, rotulo=n.EndsWith("Paciente.Nome")?"Paciente":n, tipo="texto" }).ToArray(),
                linhas = new[] { new { id="sintetico", celulas=nomes.ToDictionary(n=>n,n=>"Dado completo de demonstração: "+n), campos,
                    acoes=new[] { new { chave="Abrir",rotulo="Abrir",habilitada=true,estilo="primario",visivel=true },new { chave="Editar",rotulo="Editar",habilitada=true,estilo="secundario",visivel=true },new { chave="Historico",rotulo="Histórico",habilitada=true,estilo="secundario",visivel=true } }, selecionada=false } }, vazio="Sem registros" };
            var nomesGuia=new[] {"Atendimento.Paciente.Nome","Atendimento.Paciente.ConvenioNome","Tipo","Ordem","NumeroGuiaReal","DataBaixa","Glosa","Observacao"};
            var nomesHistorico=new[] {"Data","Eva","Profissional","Queixa","Conduta","Orientacoes"};
            var historico=Registro("HistoricoSintetico",nomesHistorico,[new {chave="Evolucao",rotulo="Evolução completa",tipo="texto-rico-leitura",valor=new {texto="Conteúdo integral de evolução fictícia para conferir leitura e preservação de detalhes.",formato=""},opcoes=Array.Empty<object>(),visivel=true,habilitado=false,obrigatorio=false}]);
            var guias=Registro("GuiasSinteticas",nomesGuia,[]);
            foreach(var largura in new[]{900,1366,1920})
            {
                janela.Width=largura;await Estado([Secao("Guias",[guias]),Secao("Histórico",[historico])]);
                await Conferir("document.querySelectorAll('.lista-compacta').length===2", "Guias e histórico devem usar linhas responsivas.");
                await Conferir("[...document.querySelectorAll('[data-tabela-container]')].every(t=>t.scrollWidth<=t.clientWidth+1)&&document.documentElement.scrollWidth<=innerWidth+1", "Registros vazaram horizontalmente.");
                await Conferir("document.querySelectorAll('[data-coluna]').length===14&&document.querySelectorAll('[data-comando]').length===6&&document.querySelectorAll('[data-campo=Evolucao]').length===1", "Perda de coluna, ação ou evolução na apresentação compacta.");
                await Conferir("(() => {document.querySelectorAll('.registro-detalhes').forEach(d=>d.open=true);return [...document.querySelectorAll('[data-tabela-container]')].every(t=>t.scrollWidth<=t.clientWidth+1)&&document.querySelector('[data-campo=Evolucao]').innerText.includes('Conteúdo integral')})()", "Detalhes abertos perderam conteúdo ou criaram rolagem horizontal.");
                using var captura=File.Create(Path.Combine(pasta,$"listas-detalhes-{largura}.png"));
                await navegador.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,captura);
                Console.WriteLine($"OK WebView2: histórico e guias em {largura}px, 14 colunas, 6 ações e texto rico íntegros, sem arrastar lateralmente.");
            }

            var colunas = new[] { "InicioISO", "FimISO", "PacienteNome", "Modalidade", "Profissional", "Sala", "RegistroPendente" }.Select(chave => new { chave, rotulo = chave, tipo = "texto" }).ToArray();
            var sessoes = Enumerable.Range(0, 197).Select(i => new { id = i.ToString(), celulas = new Dictionary<string, string> { ["InicioISO"] = $"2026-10-{5 + i % 7:00}T09:00:00", ["FimISO"] = $"2026-10-{5 + i % 7:00}T09:30:00", ["PacienteNome"] = "Paciente fictício da demonstração " + i, ["Modalidade"] = "Acupuntura", ["Profissional"] = "Profissional de demonstração", ["Sala"] = "Sala 1", ["RegistroPendente"] = "Conclusão pendente" }, campos = Array.Empty<object>(), acoes = new[] { new { chave = "Abrir", rotulo = "Abrir paciente", habilitada = true, estilo = "primario", visivel = true } }, selecionada = false }).ToArray();
            var semana = Secao("Semana", [new { chave = "horarios", titulo = "Sessões", colunas, linhas = sessoes, vazio = "Sem sessões" }, new { chave = "faixas", titulo = "Disponibilidade", colunas = Array.Empty<object>(), linhas = Array.Empty<object>(), vazio = "Sem disponibilidade" }]);
            foreach (var largura in new[] { 900, 1366 })
            {
                janela.Width = largura; await Estado([semana]);
                await Conferir("document.querySelectorAll('.agenda-sessao').length===197&&[...document.querySelectorAll('.agenda-sessao')].every(e=>e.getBoundingClientRect().width>=220)&&document.documentElement.scrollWidth<=innerWidth+1", "Semana densa perdeu sessões, comprimiu cartões ou vazou horizontalmente.");
                await Conferir("(() => {const dia=document.querySelector('.agenda-sessoes-dia');dia.scrollTop=130;const d=dia.querySelector('details');d.open=true;return dia.scrollTop===130&&d.open})()", "Cenário precisa de rolagem diária e detalhes.");
                await Estado([semana]);
                await Conferir("document.querySelector('.agenda-sessoes-dia').scrollTop===130&&document.querySelector('.agenda-sessoes-dia details').open", "Atualização perdeu rolagem diária ou detalhes abertos.");
                await navegador.CoreWebView2.ExecuteScriptAsync("document.querySelector('.agenda-sessoes-dia').scrollTop=0");
                using var arquivo = File.Create(Path.Combine(pasta, $"semana-197-{largura}.png"));
                await navegador.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, arquivo);
                Console.WriteLine($"OK WebView2: 197 sessões em {largura}px; rolagem diária e detalhes preservados.");
            }
        }
        finally { janela.Close(); }
    }
}
