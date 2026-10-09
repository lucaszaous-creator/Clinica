using System.IO;
using System.Text.Json;
using System.Windows;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell.Web;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Clinica.Recepcao.Modulo;
using Clinica.Recepcao.ViewModels;
using Clinica.Recepcao.Web;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Wpf;

static class AcompanhamentoConfirmacoesQa
{
    static void Exigir(bool ok, string mensagem) { if (!ok) throw new Exception(mensagem); }
    static JsonElement J(object? valor) => JsonSerializer.SerializeToElement(valor);
    static async Task Esperar(Func<Task<bool>> pronto, string nome)
    {
        for (var i = 0; i < 150; i++) { if (await pronto()) return; await Task.Delay(80); }
        throw new Exception("Tempo excedido: " + nome);
    }
    public static async Task Executar()
    {
        using var conexao = new SqliteConnection("Data Source=:memory:"); await conexao.OpenAsync();
        var opcoes = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(conexao).Options;
        var servicos = new ServiceCollection();
        servicos.AddClinica("Host=127.0.0.1;Port=1;Database=NAO_USAR;Username=NAO_USAR;Timeout=1");
        servicos.AddScoped(_ => new ClinicaDbContext(opcoes));
        servicos.AddSingleton<SessaoUsuario>(); servicos.AddSingleton<SnackbarService>();
        servicos.AddSingleton<ISnackbarService>(s => s.GetRequiredService<SnackbarService>());
        servicos.AddSingleton<IDialogoService, DialogosNativosProibidos>();
        var modulo = new ModuloRecepcao(); modulo.Registrar(servicos);
        using var sp = servicos.BuildServiceProvider();
        var avisos = sp.GetRequiredService<SnackbarService>();
        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ClinicaDbContext>(); await db.Database.EnsureCreatedAsync();
            var usuario = new UsuarioSistema { Nome = "Gestora sintética", Login = "acompanhamento.qa", Perfil = PerfilAcesso.Gerente };
            var profissional = new Profissional { Nome = "Profissional sintético" }; db.AddRange(usuario, profissional);
            for (var i = 1; i <= 48; i++)
            {
                var paciente = new Paciente { Nome = $"Paciente sintético {i:00}", Convenio = Convenio.UnimedPadrao, Email = $"paciente{i}@example.invalid" };
                db.Pacientes.Add(paciente);
                db.Agendamentos.Add(new Agendamento { Paciente = paciente, Profissional = profissional, DataHora = DateTime.Today.AddDays(1).AddHours(8).AddMinutes(i * 10),
                    ModalidadePrevista = ModalidadeAtendimento.Consulta, Status = i <= 24 ? StatusAgendamento.Agendado : i <= 32 ? StatusAgendamento.Cancelado : i <= 40 ? StatusAgendamento.Faltou : StatusAgendamento.Realizado });
                db.Atendimentos.Add(new Atendimento { Paciente = paciente, Data = DateOnly.FromDateTime(DateTime.Today.AddDays(-90)), RealizadoEm = DateTime.Today.AddDays(-90), Modalidade = ModalidadeAtendimento.BsvApenas });
            }
            await db.SaveChangesAsync(); sp.GetRequiredService<SessaoUsuario>().Entrar(usuario);
            await scope.ServiceProvider.GetRequiredService<ConvenioCatalogoService>().RecarregarCacheAsync();
            await scope.ServiceProvider.GetRequiredService<ModalidadeCatalogoService>().RecarregarCacheAsync();
        }
        var definicoes = RecepcaoWebRegistro.CriarPaginas().ToArray();
        using (var pages = new PaginasWebController(sp, definicoes))
        {
            await pages.NavegarAsync("agenda-confirmacoes");
            await Esperar(() => Task.FromResult(!pages.ObterPagina().Carregando), "carregar confirmações");
            await pages.ExecutarAcaoAsync("Gerar"); await pages.ExecutarAcaoAsync("Gerar");
            var tabela = pages.ObterPagina().Secoes.SelectMany(s => s.Tabelas).Single(t => t.Chave == "Contatos");
            Exigir(tabela.Linhas.Count == 24, "Rodada deveria conter somente os 24 horários agendados dentre 48 pacientes.");
            Exigir(tabela.Linhas.All(l => l.Celulas["Profissional"].Contains("Profissional sintético")), "Confirmações omitiram o profissional vinculado ao horário.");
            var linha = tabela.Linhas[0];
            await pages.ExecutarAcaoAsync("Confirmou", "Contatos", linha.Id);
            Exigir(avisos.Historico.Count == 1 && avisos.Historico[0] is { Tipo: TipoSnackbar.Sucesso, Mensagem: "Confirmação de horário registrada." }, "Confirmar horário não alimentou a central real de avisos.");
            var vm = (ConfirmacoesViewModel)pages.ViewModelAtual!;
            Exigir(vm.Resumo.Contains("1 confirmada(s)") && vm.Contatos.Count(c => c.Confirmado) == 1, "Resumo não acompanhou a resposta confirmada.");
            Exigir(!pages.ObterPagina().Secoes.SelectMany(s => s.Tabelas).Single(t => t.Chave == "Contatos").Linhas[0].Acoes.Single(a => a.Chave == "Confirmou").Habilitada, "Resposta já registrada continua habilitada.");
            await pages.ExecutarAcaoAsync("Carregar");
            Exigir(avisos.Historico.Count == 1, "Recarregar confirmações repetiu o aviso da operação.");
            Exigir(vm.Contatos.Count(c => c.Confirmado) == 1, "Confirmação não persistiu ao recarregar.");
            using (var scope = sp.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();
                Exigir(await db.Contatos.CountAsync() == 24, "Gerar rodada repetida duplicou contatos.");
                Exigir(!await db.Agendamentos.AnyAsync(a => a.ChegadaEm != null), "Confirmar horário foi confundido com registrar chegada.");
            }
            Console.WriteLine("OK 48 pacientes: 24 confirmações elegíveis, cancelados/faltosos/realizados excluídos; geração idempotente, resposta persistida, resumo atualizado e nenhuma chegada criada.");

            await pages.NavegarAsync("retorno-pacientes");
            var acompanhamento = (AcompanhamentoViewModel)pages.ViewModelAtual!;
            Exigir(acompanhamento.Pacientes.Count == 48, "Recall por modalidade perdeu pacientes da amostra.");
            await pages.ExecutarAcaoAsync("Atalho:assumir"); Exigir(acompanhamento.Condicao == "A assumir", "Atalho não aplicou condição.");
            await pages.ExecutarAcaoAsync("LimparFiltros");
            await pages.AtualizarCampoAsync("Busca", J("sintetico 07"));
            Exigir(acompanhamento.Pacientes.Count == 1, "Busca por nome sem acento não isolou paciente.");
            var paciente = pages.ObterPagina().Secoes.SelectMany(s => s.Tabelas).Single(t => t.Chave == "Pacientes").Linhas.Single();
            await pages.ExecutarAcaoAsync("Abrir", "Pacientes", paciente.Id);
            Exigir(pages.ObterPagina().Acoes.Any(a => a.Chave == "VoltarLista" && a.Visivel), "Detalhe não oferece retorno à lista.");
            Exigir(!pages.ObterPagina().Acoes.Single(a => a.Chave == "WhatsApp").Habilitada, "Contato sem telefone/consentimento foi habilitado.");
            await pages.AtualizarCampoAsync("Observacao", J("Rascunho sintético que deve ser preservado."));
            await pages.AtualizarCampoAsync("ProximoContato", J(DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd")));
            await pages.ExecutarAcaoAsync("Salvar");
            Exigir(pages.ObterPagina().MensagemEhErro && acompanhamento.DetalheVisivel && acompanhamento.Observacao.Contains("preservado"), "Falha não foi sinalizada ou descartou rascunho.");
            Exigir(avisos.Historico.Count == 2 && avisos.Historico[0].Tipo == TipoSnackbar.Erro && avisos.Historico[0].Mensagem.Contains("Não foi possível salvar o acompanhamento"), "Falha ao salvar não chegou à central de avisos.");
            await pages.AtualizarCampoAsync("ProximoContato", J(DateTime.Today.AddDays(2).ToString("yyyy-MM-dd")));
            await pages.ExecutarAcaoAsync("Salvar");
            Exigir(!pages.ObterPagina().MensagemEhErro && acompanhamento.ListaVisivel, "Correção não salvou/retornou à lista.");
            Exigir(avisos.Historico.Count == 3 && avisos.Historico[0] is { Tipo: TipoSnackbar.Sucesso, Mensagem: "Acompanhamento salvo com histórico." }, "Gravação do acompanhamento não chegou à central de avisos.");
            paciente = pages.ObterPagina().Secoes.SelectMany(s => s.Tabelas).Single(t => t.Chave == "Pacientes").Linhas.Single();
            await pages.ExecutarAcaoAsync("Abrir", "Pacientes", paciente.Id);
            Exigir(acompanhamento.Historico.Any(h => h.Observacao.Contains("preservado")), "Histórico não restaurou atualização administrativa.");
            await pages.ExecutarAcaoAsync("VoltarLista");
            await pages.ExecutarAcaoAsync("Configurar");
            Exigir(pages.ObterPagina().Acoes.Any(a => a.Chave == "VoltarConfiguracao" && a.Visivel), "Configuração não oferece saída sem salvar.");
            await pages.ExecutarAcaoAsync("VoltarConfiguracao");
            await pages.ExecutarAcaoAsync("LimparFiltros");
            Exigir(avisos.Historico.Count == 3, "Navegação/recarga repetiu notificações operacionais.");
            Console.WriteLine("OK acompanhamento: 48 recalls, filtro/atalho, acesso ao detalhe, retorno e configuração; erro mantém rascunho, correção grava histórico; contato externo bloqueado na amostra.");
        }
        await Visual(sp, modulo, definicoes);
        sp.GetRequiredService<SessaoUsuario>().Entrar(new UsuarioSistema { Id = 999, Nome = "Leitura sintética", Login = "leitura.qa", Perfil = PerfilAcesso.Gerente,
            PermissoesNegadas = Permissao.EditarAgenda | Permissao.GerenciarCampanhas | Permissao.VerFaturamento });
        using (var leitura = new PaginasWebController(sp, RecepcaoWebRegistro.CriarPaginas()))
        {
            await leitura.NavegarAsync("agenda-confirmacoes");
            await Esperar(() => Task.FromResult(!leitura.ObterPagina().Carregando), "confirmações somente leitura");
            var linhas = leitura.ObterPagina().Secoes.SelectMany(s => s.Tabelas).Single(t => t.Chave == "Contatos").Linhas;
            Exigir(linhas.SelectMany(l => l.Acoes).All(a => !a.Habilitada), "Leitura habilitou confirmar/enviar sem EditarAgenda.");
            var negou = false;
            try { await leitura.ExecutarAcaoAsync("Confirmou", "Contatos", linhas[1].Id); } catch (UnauthorizedAccessException) { negou = true; }
            Exigir(negou, "Ponte aceitou confirmar sem permissão.");
            negou = false;
            try { await leitura.NavegarAsync("retorno-pacientes"); } catch (UnauthorizedAccessException) { negou = true; }
            Exigir(negou, "Acompanhamento abriu sem permissão de campanha/faturamento.");
        }
        Console.WriteLine("OK permissões negativas: confirmações legíveis com ações indisponíveis; ponte rejeita confirmação e acompanhamento sem autorização.");
    }

    static async Task Visual(IServiceProvider sp, ModuloRecepcao modulo, PaginasWebController.Pagina[] definicoes)
    {
        var pasta = Path.GetFullPath("artifacts/acompanhamento-confirmacoes"); Directory.CreateDirectory(pasta);
        using var view = new SuiteWebView(sp, definicoes, RecepcaoWebRegistro.CriarDialogos().Concat(RegistroCompartilhadoWeb.DialogosCadastro()), modulo.Itens.ToArray(), "Recepção — 48 pacientes sintéticos", "retorno-pacientes");
        var janela = new Window { Content = view, Width = 1440, Height = 900, Left = -30000, Top = -30000, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual }; janela.Show();
        try
        {
            await view.QuandoPronto.WaitAsync(TimeSpan.FromSeconds(45)); var browser = (WebView2)view.Content;
            async Task EsperarJs(string teste) => await Esperar(async () => await browser.CoreWebView2.ExecuteScriptAsync(teste) == "true", teste);
            async Task Navegar(string rota)
            {
                var anterior = await browser.CoreWebView2.ExecuteScriptAsync("document.querySelector('.conteudo [data-contexto]')?.dataset.contexto");
                await view.NavegarAsync(rota);
                await EsperarJs($"document.querySelector('.conteudo [data-contexto]')?.dataset.contexto !== {anterior} && !!document.querySelector('[data-pagina=\"{rota}\"]:not([aria-busy=\"true\"])')");
            }
            foreach (var largura in new[] { 1440, 1044, 900 })
            {
                janela.Width = largura;
                foreach (var rota in new[] { "retorno-pacientes", "agenda-confirmacoes" })
                {
                    await Navegar(rota);
                    await EsperarJs("document.documentElement.scrollWidth<=innerWidth+1");
                    if (rota == "retorno-pacientes")
                        Exigir(await browser.CoreWebView2.ExecuteScriptAsync("![...document.querySelectorAll('[data-comando]')].some(b=>b.textContent.trim()==='Atalho')") == "true", "Atalho genérico continua visível.");
                    using var imagem = File.Create(Path.Combine(pasta, $"{rota}-{largura}.png")); await view.CapturarPreviewAsync(imagem);
                }
            }
            await Navegar("retorno-pacientes");
            await AcoesVisiveisQa.Clicar(browser, "[data-comando=\"Abrir\"]");
            await EsperarJs("!!document.querySelector('[data-comando=\"VoltarLista\"]:not(:disabled)') && !!document.querySelector('[data-comando=\"Salvar\"]:not(:disabled)')");
            Exigir(await browser.CoreWebView2.ExecuteScriptAsync("getComputedStyle(document.querySelector('[data-comando=\"Salvar\"]')).backgroundColor==='rgb(18, 58, 158)'") == "true", "Salvar acompanhamento perdeu destaque azul.");
            Exigir(await browser.CoreWebView2.ExecuteScriptAsync("!document.querySelector('[data-comando=\"Salvar\"]').closest('[popover]') && !document.querySelector('[data-comando=\"VoltarLista\"]').closest('[popover]')") == "true", "Salvar ou Voltar à lista ficaram ocultos no menu.");
            await browser.CoreWebView2.ExecuteScriptAsync("document.querySelector('.conteudo').scrollTo({top:0,behavior:'instant'})");
            using (var imagem = File.Create(Path.Combine(pasta, "acompanhamento-detalhe-900.png"))) await view.CapturarPreviewAsync(imagem);
            await AcoesVisiveisQa.Clicar(browser, "[data-comando=\"VoltarLista\"]");
            await EsperarJs("!!document.querySelector('[data-comando=\"Atalho:assumir\"]')");
            var snack = sp.GetRequiredService<SnackbarService>();
            var avisosAntes = snack.Historico.Count;
            await Navegar("agenda-confirmacoes");
            await EsperarJs("!!document.querySelector('[data-comando=\"Confirmou\"]:not(:disabled)')");
            await AcoesVisiveisQa.Clicar(browser, "[data-comando=\"Confirmou\"]:not(:disabled)");
            await Esperar(() => Task.FromResult(snack.Historico.Count == avisosAntes + 1), "confirmação DOM registra aviso");
            Exigir(snack.Historico[0] is { Tipo: TipoSnackbar.Sucesso, Mensagem: "Confirmação de horário registrada." }, "Clique real não gerou sucesso de confirmação.");
            using (var scope = sp.CreateScope())
                Exigir(await scope.ServiceProvider.GetRequiredService<ClinicaDbContext>().Contatos.CountAsync(c => c.Status == StatusContato.Respondido) == 2, "Confirmação DOM não persistiu junto do aviso.");
            await Navegar("agenda-recepcao");
            await AcoesVisiveisQa.Clicar(browser, "[data-comando=\"ConfirmarSessoes\"]");
            await EsperarJs("!!document.querySelector('.dialogo-web [data-comando=\"ConfirmouCommand\"]:not(:disabled)')");
            Exigir(await browser.CoreWebView2.ExecuteScriptAsync("document.querySelectorAll('.dialogo-web [data-comando=\"ConfirmouCommand\"]:disabled').length===2")=="true","Diálogo permite confirmar novamente as duas respostas já gravadas");
            await AcoesVisiveisQa.Clicar(browser, ".dialogo-web [data-comando=\"ConfirmouCommand\"]:not(:disabled)");
            await Esperar(() => Task.FromResult(snack.Historico.Count == avisosAntes + 2), "confirmação aberta pela agenda também registra aviso");
            using (var scope = sp.CreateScope())
                Exigir(await scope.ServiceProvider.GetRequiredService<ClinicaDbContext>().Contatos.CountAsync(c => c.Status == StatusContato.Respondido) == 3, "Diálogo da agenda não persistiu confirmação");
            await AcoesVisiveisQa.Clicar(browser, ".dialogo-web [data-fechar-dialogo]");
            await EsperarJs("!document.querySelector('.dialogo-web')");
            await EsperarJs("!!document.querySelector('[data-avisos] small')");
            await AcoesVisiveisQa.Clicar(browser, "button[data-avisos]");
            await EsperarJs("!!document.querySelector('.painel-avisos')?.textContent.includes('Confirmação de horário registrada.') && document.querySelector('.painel-avisos').textContent.includes('Acompanhamento salvo com histórico.')");
            await Esperar(() => Task.FromResult(snack.NaoLidos == 0), "avisos marcados lidos");
            await EsperarJs("!document.querySelector('[data-avisos] small')");
            Exigir(snack.Historico.Count == avisosAntes + 2, "Ler aviso apagou histórico.");
            Exigir(snack.Historico.All(a => !a.Mensagem.Contains("Paciente sintético") && !a.Mensagem.Contains("Rascunho")), "Central expôs identificação ou conteúdo de acompanhamento.");
            using (var imagem = File.Create(Path.Combine(pasta, "avisos-operacionais.png"))) await view.CapturarPreviewAsync(imagem);
            Console.WriteLine("OK WebView2: acompanhamento/confirmações em 1440/1044/900, atalhos identificados, salvar azul, voltar sem gravar; confirmar no DOM persiste e alimenta sino, erro/salvamento reais do recall constam no histórico, ler zera contador sem apagar. Sem avisos de recarga, nomes/conteúdo clínico no sino ou acionamento de WhatsApp/e-mail.");
        }
        finally { janela.Close(); }
    }
}
