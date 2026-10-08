using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

// Exercita o mesmo renderer e a mesma ponte dos aplicativos com contratos sintéticos.
internal static class AcoesMenuQa
{
    internal static async Task Executar()
    {
        var pasta = Path.GetFullPath("artifacts/acoes-menu-web"); Directory.CreateDirectory(pasta);
        var navegador = new WebView2();
        var janela = new Window { Content = navegador, Width = 900, Height = 720, Left = -30000, Top = -30000, ShowInTaskbar = false };
        janela.Show();
        try
        {
            await navegador.EnsureCoreWebView2Async(await CoreWebView2Environment.CreateAsync(null, Path.Combine(pasta, "perfil")));
            navegador.CoreWebView2.SetVirtualHostNameToFolderMapping("qa.local", Path.Combine(AppContext.BaseDirectory, "WebSuite", "wwwroot"), CoreWebView2HostResourceAccessKind.DenyCors);
            var pronto = new TaskCompletionSource();
            var mensagens = new List<string>();
            navegador.CoreWebView2.WebMessageReceived += (_, e) => { mensagens.Add(e.WebMessageAsJson); if (e.WebMessageAsJson.Contains("pronto")) pronto.TrySetResult(); };
            navegador.Source = new Uri("https://qa.local/index.html");
            await pronto.Task.WaitAsync(TimeSpan.FromSeconds(30));
            object Acao(string chave, string estilo = "secundario", bool habilitada = true) => new { chave, rotulo = chave, estilo, habilitada, visivel = true };
            var acoes = new[] { Acao("Avancar", "primario"), Acao("Editar"), Acao("Ficha"), Acao("Termo"), Acao("Cancelamento", "perigo"), Acao("Desabilitada", habilitada: false) };
            var linha = new { id = "registro-sintetico", celulas = new { Paciente = "Paciente fictício", Horario = "09:00" }, campos = Array.Empty<object>(), acoes, selecionada = false };
            var tabela = new { chave = "Linhas", titulo = "Atendimentos", colunas = new[] { new { chave = "Paciente", rotulo = "Paciente", tipo = "texto" }, new { chave = "Horario", rotulo = "Horário", tipo = "texto" } }, linhas = new[] { linha }, vazio = "Vazio" };
            var secao = new { chave = "Agenda", titulo = "Agenda", campos = Array.Empty<object>(), indicadores = Array.Empty<object>(), acoes = Array.Empty<object>(), tabelas = new[] { tabela } };
            async Task Conferir(string js, string erro)
            {
                if (await navegador.CoreWebView2.ExecuteScriptAsync(js) != "true") throw new Exception(erro);
            }
            async Task Tecla(string key, int codigo)
            {
                await navegador.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent", JsonSerializer.Serialize(new { type = "keyDown", key, windowsVirtualKeyCode = codigo }));
                await navegador.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent", JsonSerializer.Serialize(new { type = "keyUp", key, windowsVirtualKeyCode = codigo }));
            }
            foreach (var largura in new[] { 900, 1366 })
            {
                janela.Width = largura;
                navegador.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { tipo = "estado", titulo = "QA sintético", usuario = "Demonstração", ocupado = false, rotas = new[] { new { chave = "teste", rotulo = "Teste", grupo = "Gestão" } }, pagina = new { chave = "teste", contexto = "contexto-sintetico", titulo = "Agenda", campos = Array.Empty<object>(), indicadores = Array.Empty<object>(), acoes = Array.Empty<object>(), secoes = new[] { secao }, carregando = false, naoVerificado = false, mensagemEhErro = false, truncado = false } }));
                await Task.Delay(250);
                await Conferir("document.querySelectorAll('[data-comando]').length===6 && [...document.querySelectorAll('[data-comando]')].filter(b=>b.getClientRects().length).length===2", "A compactação perdeu comandos ou exibiu mais de duas ações.");
                await Conferir("(() => {const b=document.querySelector('[data-abrir-acoes]');b.click();const p=document.querySelector('.acoes-menu-painel');return p.matches(':popover-open')&&b.getAttribute('aria-expanded')==='true'&&document.activeElement.dataset.comando==='Ficha'&&p.querySelector('[role=separator]')!==null})()", "Menu não abriu, perdeu foco ou separação das ações perigosas.");
                await Conferir("(() => {const p=document.querySelector('.acoes-menu-painel');const r=p.getBoundingClientRect();return r.left>=0&&r.right<=innerWidth&&r.top>=0&&r.bottom<=innerHeight&&document.elementFromPoint(r.left+20,r.top+20)?.closest('.acoes-menu-painel')===p})()", "Menu recortado ou fora da janela.");
                await Tecla("End", 35);
                await Conferir("document.activeElement.dataset.comando==='Cancelamento'", "End não pulou ação desabilitada.");
                await Tecla("ArrowDown", 40);
                await Conferir("document.activeElement.dataset.comando==='Ficha'", "Setas não circularam pelos itens habilitados.");
                await Tecla("Escape", 27);
                await Conferir("!document.querySelector('.acoes-menu-painel').matches(':popover-open')&&document.activeElement.hasAttribute('data-abrir-acoes')&&document.activeElement.getAttribute('aria-expanded')==='false'", "Escape não fechou/devolveu foco.");
                await Tecla("ArrowDown", 40);
                await Conferir("document.querySelector('.acoes-menu-painel').matches(':popover-open')", "Teclado não abriu menu.");
                await navegador.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent", "{\"type\":\"mousePressed\",\"x\":10,\"y\":150,\"button\":\"left\",\"clickCount\":1}");
                await navegador.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent", "{\"type\":\"mouseReleased\",\"x\":10,\"y\":150,\"button\":\"left\",\"clickCount\":1}");
                await Conferir("!document.querySelector('.acoes-menu-painel').matches(':popover-open')", "Clique fora não fechou menu.");
                await navegador.CoreWebView2.ExecuteScriptAsync("document.querySelector('[data-abrir-acoes]').click();document.querySelector('[data-comando=Termo]').click()");
                await Task.Delay(100);
                var mensagem = mensagens.Select(s => JsonDocument.Parse(s)).LastOrDefault(j => j.RootElement.TryGetProperty("chave", out var chave) && chave.GetString() == "Termo");
                if (mensagem is null || mensagem.RootElement.GetProperty("acao").GetString() != "pagina-acao" || mensagem.RootElement.GetProperty("tabela").GetString() != "Linhas" || mensagem.RootElement.GetProperty("linha").GetString() != "registro-sintetico" || mensagem.RootElement.GetProperty("contexto").GetString() != "contexto-sintetico") throw new Exception("Menu alterou o comando ou o contexto da ponte.");
                await Conferir("!document.querySelector('.acoes-menu-painel').matches(':popover-open')", "Comando não fechou menu.");
                await navegador.CoreWebView2.ExecuteScriptAsync("document.querySelector('[data-abrir-acoes]').click()");
                using var arquivo = File.Create(Path.Combine(pasta, $"menu-{largura}.png"));
                await navegador.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, arquivo);
                Console.WriteLine($"OK WebView2 {largura}px: seis ações preservadas, duas diretas; menu íntegro, teclado, clique fora e contexto da ponte.");
            }
            object CampoClinico(string chave, string rotulo, string valor) => new { chave, rotulo, valor, tipo = "leitura", visivel = true, habilitado = true, obrigatorio = false, opcoes = Array.Empty<object>() };
            var pacienteClinico = new { chave = "consultorio-paciente", contexto = "contexto-paciente", titulo = "Ficha do paciente", subtitulo = "Paciente fictício da composição", campos = new[] { CampoClinico("Paciente", "Paciente", "Paciente fictício da composição"), CampoClinico("Cabecalho.Linha", "Identificação", "Convênio sintético · 2 sessões"), CampoClinico("Contexto", "Contexto", "Escolhido na busca; sem vínculo com horário."), CampoClinico("Cronometro", "Tempo de atendimento", "12 minutos") }, indicadores = Array.Empty<object>(), acoes = new[] { Acao("Voltar"), Acao("VerFichaWeb"), Acao("EmitirDocumentos"), Acao("IniciarSessao", "primario") }, secoes = Array.Empty<object>(), carregando = false, naoVerificado = false, mensagemEhErro = false, truncado = false };
            navegador.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { tipo = "estado", titulo = "QA sintético", usuario = "Demonstração", ocupado = false, rotas = Array.Empty<object>(), pagina = pacienteClinico }));
            await Task.Delay(250);
            await Conferir("document.querySelectorAll('.clinico-identidade').length===1&&document.querySelector('.clinico-identidade').innerText.includes('Paciente fictício da composição')&&document.querySelector('.clinico-identidade').innerText.includes('Convênio sintético · 2 sessões')&&document.querySelector('.clinico-identidade').innerText.includes('Escolhido na busca; sem vínculo com horário.')", "Cabeçalho clínico omitiu identificação ou contexto.");
            await Conferir("document.querySelectorAll('output').length===4&&document.querySelectorAll('[data-comando]').length===4&&document.querySelector('.pagina-clinica').innerText.includes('12 minutos')&&document.querySelector('[data-comando=VerFichaWeb]').getAttribute('aria-current')==='page'", "Composição clínica duplicou/omitiu campos ou ações.");
            Console.WriteLine("OK composição clínica: identidade e contexto únicos, campo adicional preservado, quatro ações e aba ativa.");
            var formulario = new { chave = "formulario-teste", titulo = "Formulário sintético", campos = Array.Empty<object>(), indicadores = Array.Empty<object>(), acoes = new[] { Acao("Salvar", "primario"), Acao("Cancelar"), Acao("OutraAcao"), Acao("Excluir", "perigo") }, secoes = Array.Empty<object>(), carregando = false, naoVerificado = false, mensagemEhErro = false, truncado = false };
            navegador.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { tipo = "estado", titulo = "QA sintético", usuario = "Demonstração", ocupado = false, rotas = Array.Empty<object>(), dialogo = new { id = "dialogo-sintetico", pagina = formulario, ocupado = false, podeFechar = true } }));
            await Task.Delay(250);
            await Conferir("(() => {const rodape=document.querySelector('.dialogo-rodape');return rodape.querySelector('[data-comando=Salvar]').getClientRects().length>0&&rodape.querySelector('[data-comando=Cancelar]').getClientRects().length>0})()", "Salvar ou Cancelar deixou de ficar visível no formulário.");
            mensagens.Clear();
            await navegador.CoreWebView2.ExecuteScriptAsync("document.querySelector('.dialogo-rodape [data-abrir-acoes]').click()");
            await Tecla("Escape", 27);
            await Task.Delay(100);
            if (mensagens.Any(m => m.Contains("dlg-fechar"))) throw new Exception("Escape do menu tentou fechar o formulário.");
            await Conferir("document.querySelector('.dialogo-web')!==null&&!document.querySelector('.dialogo-rodape .acoes-menu-painel').matches(':popover-open')", "Escape perdeu o formulário.");
            Console.WriteLine("OK WebView2: formulário conserva Salvar/Cancelar; Escape fecha somente o menu.");
        }
        finally { janela.Close(); }
    }
}
