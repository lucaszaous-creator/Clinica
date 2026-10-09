using System.IO;
using System.Windows;
using Clinica.Clinico.Modulo;
using Clinica.Clinico.Web;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Desktop.Shell.Web;
using Clinica.Infrastructure;
using Clinica.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Wpf;

static partial class Fluxos
{
    static async Task ValidarAcoesExpostasAsync(SuiteWebView view, WebView2 browser, Window janela, string saida)
    {
        await view.NavegarAsync(ModuloClinico.ChaveAtendimento);
        foreach (var largura in new[] { 1440, 1044, 900 })
        {
            janela.Width = largura; janela.Height = 900;
            await Task.Delay(300);
            var ok = await browser.CoreWebView2.ExecuteScriptAsync("""
                (() => {
                    const comandos=['emitir-receita','emitir-atestado','emitir-comparecimento','emitir-exame','Atendimento.PrescreverInfusao','AtualizarFicha','ConsultarFicha','ConsultarExames','Atendimento.Salvar','Atendimento.AbrirModelos','Atendimento.AbrirMapa'];
                    return comandos.every(chave=>{
                        const b=document.querySelector(`[data-comando="${chave}"]`);
                        return b && b.getClientRects().length && !b.closest('[popover]') && b.getBoundingClientRect().width>=32;
                    }) && !document.querySelector('.clinico-cabecalho [data-abrir-acoes],.secao-acoes-expostas [data-abrir-acoes]')
                    && [...document.querySelectorAll('.secao-acoes-expostas .cabecalho-secao [data-comando]')].every(b=>b.getBoundingClientRect().bottom<=document.querySelector('[data-campo="Atendimento.EvaAntes"]').getBoundingClientRect().top)
                    && [...document.querySelectorAll('.clinico-fluxo .primario:not(:disabled),.secao-acoes-expostas .primario:not(:disabled)')].every(b=>getComputedStyle(b).backgroundColor==='rgb(18, 58, 158)')
                    && [...document.querySelectorAll('.clinico-faixa-acoes .botao:not(:disabled)')].every(b=>getComputedStyle(b).color==='rgb(18, 58, 158)' && getComputedStyle(b).backgroundColor!=='rgb(18, 58, 158)')
                    && document.documentElement.scrollWidth<=innerWidth+1;
                })()
                """);
            Exigir(ok == "true", "Ações clínicas ocultas, sem azul ou fora da composição: " + largura);
            await browser.CoreWebView2.ExecuteScriptAsync("document.querySelector('.conteudo').scrollTo(0,0)");
            using (var f = File.Create(Path.Combine(saida, $"atendimento-acoes-{largura}.png"))) await view.CapturarPreviewAsync(f);
            await browser.CoreWebView2.ExecuteScriptAsync("document.querySelector('.secao-acoes-expostas').scrollIntoView({block:'start'})");
            using (var f = File.Create(Path.Combine(saida, $"atendimento-sessao-{largura}.png"))) await view.CapturarPreviewAsync(f);
        }
        Console.WriteLine("OK atendimento: documentos, ficha e salvar sem menus; ações acima da EVA, hierarquia entre confirmação e ações auxiliares, sem vazamento em 1440/1044/900.");
    }

    static async Task ValidarConclusaoVisivelAsync(IServiceProvider sp, SuiteWebView view, WebView2 browser, Window janela, string saida)
    {
        using (var escopo = sp.CreateScope())
        {
            var db = escopo.ServiceProvider.GetRequiredService<ClinicaDbContext>();
            var evolucao = await db.Evolucoes.Include(e => e.Paciente).SingleAsync();
            sp.GetRequiredService<PacienteEmFoco>().Definir(evolucao.PacienteId, evolucao.Paciente.Nome, evolucao.AgendamentoId, null, evolucao.Data);
        }
        janela.Width = 1440; janela.Height = 900;
        await view.NavegarAsync(ModuloClinico.ChaveAtendimento);
        async Task Esperar(string expressao, WebView2? navegador = null)
        {
            for (var i = 0; i < 120; i++)
            {
                if (await (navegador ?? browser).CoreWebView2.ExecuteScriptAsync(expressao) == "true") return;
                await Task.Delay(80);
            }
            throw new Exception("Atendimento vinculado não atingiu o estado: " + expressao);
        }
        await Esperar("!!document.querySelector('[data-comando=\"FinalizarSessao\"]:not(:disabled)')");
        Exigir(await browser.CoreWebView2.ExecuteScriptAsync("getComputedStyle(document.querySelector('[data-comando=\"FinalizarSessao\"]')).backgroundColor==='rgb(18, 58, 158)'") == "true", "Concluir habilitado precisa de destaque azul.");
        await browser.CoreWebView2.ExecuteScriptAsync("document.querySelector('.conteudo').scrollTo({top:0,behavior:'instant'})");
        using (var f = File.Create(Path.Combine(saida, "atendimento-vinculado.png"))) await view.CapturarPreviewAsync(f);
        await AcoesVisiveisQa.Clicar(browser, "[data-comando=\"FinalizarSessao\"]");
        await Esperar("!!document.querySelector('[data-dialogo]') && document.querySelector('[data-dialogo]').textContent.includes('Concluir a sessão registrada?')");
        await AcoesVisiveisQa.Clicar(browser, "[data-dialogo] [data-fechar-dialogo]");
        await Esperar("!document.querySelector('[data-dialogo]')");
        using (var escopo = sp.CreateScope())
            Exigir(!await escopo.ServiceProvider.GetRequiredService<ClinicaDbContext>().Agendamentos.AnyAsync(a => a.FimAtendimentoEm != null), "Cancelar a confirmação concluiu o atendimento.");
        Console.WriteLine("OK conclusão: horário vinculado mostra botão azul direto, abre a confirmação real e cancelar preserva a sessão.");

        await Esperar("!!document.querySelector('[data-comando=\"IniciarSessao\"]:not(:disabled)') && !document.querySelector('[data-comando=\"ReabrirSessao\"]')");
        await AcoesVisiveisQa.Clicar(browser, "[data-comando=\"IniciarSessao\"]");
        await Esperar("!document.querySelector('[data-comando=\"IniciarSessao\"]') && !document.querySelector('[data-comando=\"ReabrirSessao\"]') && !!document.querySelector('[data-comando=\"FinalizarSessao\"]:not(:disabled)')");
        using (var escopo = sp.CreateScope())
        {
            var db = escopo.ServiceProvider.GetRequiredService<ClinicaDbContext>();
            var evolucao = await db.Evolucoes.SingleAsync();
            var horario = await db.Agendamentos.SingleAsync(a => a.Id == evolucao.AgendamentoId);
            Exigir(horario.InicioAtendimentoEm is not null && horario.FimAtendimentoEm is null, "Iniciar não persistiu o início ou concluiu antecipadamente.");
        }

        await ValidarEvolucaoSimplificadaAsync(view, browser);

        UsuarioSistema medico;
        using (var escopo = sp.CreateScope())
        {
            var db = escopo.ServiceProvider.GetRequiredService<ClinicaDbContext>();
            medico = new UsuarioSistema { Nome = "Médico sintético", Login = "medico.acoes.qa", Perfil = PerfilAcesso.Profissional, ProfissionalId = gestor.ProfissionalId };
            db.Usuarios.Add(medico);
            await db.SaveChangesAsync();
        }
        try
        {
            sp.GetRequiredService<SessaoUsuario>().Entrar(medico);
            // A ponte pertence à sessão autenticada; o novo perfil precisa da própria view.
            using var viewMedico = new SuiteWebView(sp, ClinicoWebRegistro.CriarPaginas(),
                ClinicoWebRegistro.CriarDialogos().Concat(RegistroCompartilhadoWeb.Dialogos()).DistinctBy(d => (d.Chave, d.Tipo)),
                new ModuloClinico().Itens.ToArray(), "Clínico — médico sintético", ModuloClinico.ChaveAtendimento);
            var janelaMedico = new Window { Content = viewMedico, Width = 1440, Height = 900, Left = -30000, Top = -30000, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual };
            janelaMedico.Show();
            try
            {
                await viewMedico.QuandoPronto.WaitAsync(TimeSpan.FromSeconds(45));
                var browserMedico = (WebView2)viewMedico.Content;
                await Esperar("!!document.querySelector('.prontuario-contexto-sessao')?.textContent.includes('Em atendimento')", browserMedico);
                await Esperar("!document.querySelector('[data-comando=\"IniciarSessao\"]') && !document.querySelector('[data-comando=\"ReabrirSessao\"]') && !document.querySelector('[data-comando=\"FinalizarSessao\"]') && !!document.querySelector('[data-comando=\"Atendimento.Salvar\"]:not(:disabled)')", browserMedico);
                using var f = File.Create(Path.Combine(saida, "atendimento-profissional-iniciado.png"));
                await viewMedico.CapturarPreviewAsync(f);
            }
            finally { janelaMedico.Close(); }
            Console.WriteLine("OK ações por estado e perfil: iniciar some após início persistido; médico mantém Salvar sessão explícito sem Concluir duplicado; gerente mantém conclusão com confirmação.");
        }
        finally
        {
            sp.GetRequiredService<SessaoUsuario>().Entrar(gestor);
        }
    }
}
