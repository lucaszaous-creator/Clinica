using System.Text.Json;
using System.Windows;
using Clinica.Clinico.Modulo;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Desktop.Shell.Web;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Wpf;

static partial class Fluxos
{
    public static bool SoDestinosDialogos;

    static async Task ValidarDestinosDialogosAsync(IServiceProvider sp, ModuloClinico modulo,
        PaginasWebController.Pagina[] paginas, DialogosWebController.RegistroDialogo[] dialogos)
    {
        int pacienteId, outroPacienteId;
        string pacienteNome;
        using (var escopo = sp.CreateScope())
        {
            var db = escopo.ServiceProvider.GetRequiredService<ClinicaDbContext>();
            var paciente = await db.Pacientes.FirstAsync(p => p.Nome == "Paciente fictícia de demonstração");
            pacienteId = paciente.Id;
            pacienteNome = paciente.Nome;
            var outro = new Paciente { Nome = "Outro paciente sintético dos destinos" };
            var pedido = new DocumentoClinico
            {
                Numero = "QA-DESTINO-EXAME", CodigoVerificacao = "QADESTINO",
                Tipo = TipoDocumentoClinico.PedidoExame, PacienteId = pacienteId,
                Data = DateOnly.FromDateTime(DateTime.Today), Corpo = "Pedido sintético dos destinos"
            };
            db.AddRange(outro, pedido);
            await db.SaveChangesAsync();
            outroPacienteId = outro.Id;
            db.ResultadosExame.Add(new ResultadoExame
            {
                PacienteId = pacienteId, PedidoDocumentoId = pedido.Id, Data = pedido.Data,
                Nome = "Exame sintético dos destinos", Valor = "Resultado sintético dos destinos"
            });
            await db.SaveChangesAsync();
        }

        var foco = sp.GetRequiredService<PacienteEmFoco>();
        using var view = new SuiteWebView(sp, paginas, dialogos, modulo.Itens.ToArray(),
            "Destinos dos diálogos — demonstração", ModuloClinico.ChaveProntuarios);
        var janela = new Window { Content = view, Width = 1100, Height = 788, Left = -30000,
            Top = -30000, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual };
        janela.Show();
        try
        {
            await view.QuandoPronto.WaitAsync(TimeSpan.FromSeconds(45));
            var browser = (WebView2)view.Content;
            async Task Esperar(string expressao)
            {
                for (var i = 0; i < 100; i++)
                {
                    if (await browser.CoreWebView2.ExecuteScriptAsync(expressao) == "true") return;
                    await Task.Delay(80);
                }
                throw new Exception("Destino não alcançado pelo clique: " + expressao + " — " +
                    await browser.CoreWebView2.ExecuteScriptAsync("JSON.stringify({rota:document.querySelector('main')?.dataset.rota,pagina:document.querySelector('main')?.textContent,acoes:Array.from(document.querySelectorAll('[data-comando]')).map(b=>b.dataset.comando),dialogo:document.querySelector('[data-dialogo]')?.textContent})"));
            }

            foreach (var caso in new[]
            {
                (Origem: ModuloClinico.ChaveProntuarios, Abrir: "Abrir", Acao: "AbrirCompleto", Destino: ModuloClinico.ChaveProntuario, Conteudo: "Evolução sintética para validação da interface."),
                (Origem: ModuloClinico.ChaveExames, Abrir: "VerResultados", Acao: "AbrirNoPaciente", Destino: ModuloClinico.ChaveExamesDoPaciente, Conteudo: "Resultado sintético dos destinos")
            })
            {
                // Começa em outro paciente: não basta trocar a rota, o contexto também precisa mudar.
                foco.Definir(outroPacienteId, "Outro paciente sintético dos destinos");
                await view.NavegarAsync(caso.Origem);
                var seletor = "main [data-comando='" + caso.Abrir + "']";
                await Esperar("!!document.querySelector(" + JsonSerializer.Serialize(seletor) + ")");
                await Task.Delay(500);
                await AcoesVisiveisQa.Clicar(browser, seletor);
                var botaoDestino = "[data-dialogo] [data-comando='" + caso.Acao + "']";
                await Esperar("!!document.querySelector(" + JsonSerializer.Serialize(botaoDestino) + ")");
                Exigir(await browser.CoreWebView2.ExecuteScriptAsync("!document.querySelector(" + JsonSerializer.Serialize(botaoDestino) + ").closest('.acoes-menu-painel')") == "true", "Destino principal escondido em Mais ações: " + caso.Acao);
                await AcoesVisiveisQa.Clicar(browser, botaoDestino);
                await Esperar("!document.querySelector('[data-dialogo]') && document.querySelector('main')?.dataset.rota === " + JsonSerializer.Serialize(caso.Destino));
                await Esperar("document.querySelector('main').textContent.includes(" + JsonSerializer.Serialize(pacienteNome) + ") && document.querySelector('main').textContent.includes(" + JsonSerializer.Serialize(caso.Conteudo) + ")");
                Exigir(foco.PacienteId == pacienteId, "O destino manteve o paciente anterior: " + caso.Acao);
                Console.WriteLine("OK destino real " + caso.Acao + ": diálogo fechado, paciente correto e conteúdo em " + caso.Destino);
            }
        }
        finally { janela.Close(); }
    }
}
