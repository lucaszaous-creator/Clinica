using System.IO;
using System.Text.Json;
using System.Windows;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Desktop.Shell.Web;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Wpf;

// Regressão da PR 245: os campos são preenchidos pelo DOM real. Nenhuma propriedade
// do ViewModel nem mensagem de bridge é injetada diretamente durante as operações.
internal static class BuscasDigitadasQa
{
    private static string Js(string? texto) => JsonSerializer.Serialize(texto);
    private static void Exigir(bool ok, string erro) { if (!ok) throw new Exception(erro); }

    internal static async Task Executar()
    {
        using var con = new SqliteConnection("Data Source=:memory:"); await con.OpenAsync();
        var op = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(con).Options;
        var sc = new ServiceCollection();
        sc.AddClinica("Host=127.0.0.1;Port=1;Database=NAO_USAR;Username=NAO_USAR;Timeout=1");
        sc.AddScoped(_ => new ClinicaDbContext(op)); sc.AddSingleton<SessaoUsuario>();
        sc.AddSingleton<PacienteEmFoco>(); sc.AddSingleton<SnackbarService>();
        sc.AddSingleton<ISnackbarService>(sp => sp.GetRequiredService<SnackbarService>());
        sc.AddSingleton<IDialogoService, DialogosNativosProibidos>();
        IModuloApp[] modulos = [new Clinica.Faturamento.Modulo.ModuloFaturamento(), new Clinica.Financeiro.Modulo.ModuloFinanceiro(), new Clinica.Gerente.Modulo.ModuloGerente(), new Clinica.Recepcao.Modulo.ModuloRecepcao()];
        foreach (var modulo in modulos) modulo.Registrar(sc);
        using var sp = sc.BuildServiceProvider(); int pacienteId;
        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ClinicaDbContext>(); await db.Database.EnsureCreatedAsync();
            var usuario = new UsuarioSistema { Nome = "Gerente fictício", Login = "gerente.busca.qa", Perfil = PerfilAcesso.Gerente };
            var paciente = new Paciente { Nome = "Zuleica Sintética Busca", Documento = "52998224725", Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino };
            var outro = new Paciente { Nome = "Abel Outro Sintético", Documento = "11144477735", Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Masculino };
            db.AddRange(usuario, paciente, outro); await db.SaveChangesAsync(); pacienteId = paciente.Id;
            sp.GetRequiredService<SessaoUsuario>().Entrar(usuario);
            var atendimentos = scope.ServiceProvider.GetRequiredService<AtendimentoService>();
            await atendimentos.LancarAsync(paciente.Id, DateOnly.FromDateTime(DateTime.Today.AddDays(-2)), ModalidadeAtendimento.AcupunturaComEletro);
            await atendimentos.LancarAsync(outro.Id, DateOnly.FromDateTime(DateTime.Today.AddDays(-2)), ModalidadeAtendimento.AcupunturaComEletro);
        }
        var paginas = Clinica.Faturamento.Web.FaturamentoWebRegistro.CriarPaginas()
            .Concat(Clinica.Financeiro.Web.FinanceiroSuiteRegistro.CriarPaginas())
            .Concat(Clinica.Gerente.Web.GerenteWebRegistro.CriarPaginas()).ToArray();
        foreach (var pagina in paginas)
            foreach (var tabela in pagina.Secoes.SelectMany(s => s.Tabelas))
                Exigir(tabela.Colunas.Length > 0, "Tabela sem identidade: " + pagina.Chave + "/" + tabela.Chave);
        var dialogos = Clinica.Faturamento.Web.FaturamentoWebRegistro.CriarDialogos()
            .Concat(Clinica.Financeiro.Web.FinanceiroSuiteDialogos.CriarDialogos())
            .Concat(Clinica.Gerente.Web.GerenteWebRegistro.CriarDialogos())
            .Concat(RegistroCompartilhadoWeb.Dialogos()).GroupBy(d => (d.Chave, d.Tipo)).Select(g => g.First()).ToArray();
        var menu = modulos.SelectMany(m => m.Itens).Where(m => paginas.Any(p => p.Chave == m.Chave)).GroupBy(m => m.Chave).Select(g => g.First()).ToArray();
        using var view = new SuiteWebView(sp, paginas, dialogos, menu, "Teste sintético de buscas", "faturamento-guias");
        var window = new Window { Content = view, Width = 1300, Height = 800, Left = -30000, Top = -30000, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual };
        window.Show(); var pasta = Path.GetFullPath("artifacts/buscas-digitadas-web"); Directory.CreateDirectory(pasta);
        try
        {
            await view.QuandoPronto.WaitAsync(TimeSpan.FromSeconds(45)); var browser = (WebView2)view.Content;
            async Task Esperar(string expressao, string descricao)
            {
                for (var i = 0; i < 150; i++) { if (await browser.CoreWebView2.ExecuteScriptAsync(expressao) == "true") return; await Task.Delay(60); }
                var estado = await browser.CoreWebView2.ExecuteScriptAsync("document.querySelector('.dialogo-web')?.innerText??document.querySelector('.conteudo')?.innerText");
                throw new Exception(descricao + ": " + estado);
            }
            async Task Digitar(string campo, string valor, bool dialogo = false)
            {
                var seletor = (dialogo ? ".dialogo-web " : ".conteudo ") + "[data-campo=\"" + campo + "\"]";
                await Esperar($"!!document.querySelector({Js(seletor)})&&!document.querySelector({Js(seletor)}).disabled", "Campo não disponível " + campo);
                // Dispara input como a digitação da pessoa; aguarda o debounce real, sem change/blur.
                var script = $"(()=>{{const e=document.querySelector({Js(seletor)});e.focus();e.value={Js(valor)};e.dispatchEvent(new Event('input',{{bubbles:true}}));return true}})()";
                Exigir(await browser.CoreWebView2.ExecuteScriptAsync(script) == "true", "Não digitou " + campo); await Task.Delay(700);
            }
            async Task Escolher(string campo, string texto, bool dialogo = true)
            {
                var seletor = (dialogo ? ".dialogo-web " : ".conteudo ") + "select[data-campo=\"" + campo + "\"]";
                await Esperar($"[...document.querySelector({Js(seletor)})?.options??[]].some(o=>o.textContent.includes({Js(texto)}))", "Opção não apareceu " + campo);
                await browser.CoreWebView2.ExecuteScriptAsync($"(()=>{{const e=document.querySelector({Js(seletor)});e.value=[...e.options].find(o=>o.textContent.includes({Js(texto)})).value;e.dispatchEvent(new Event('change',{{bubbles:true}}));}})()"); await Task.Delay(700);
            }
            async Task Clicar(string comando, bool dialogo = false, string? tabela = null)
            {
                var seletor = (dialogo ? ".dialogo-web " : ".conteudo ") + "button[data-comando=\"" + comando + "\"]" + (tabela is null ? "" : "[data-tabela=\"" + tabela + "\"]");
                await Esperar($"!!document.querySelector({Js(seletor)})&&!document.querySelector({Js(seletor)}).disabled", "Ação não habilitou " + comando);
                await browser.CoreWebView2.ExecuteScriptAsync($"document.querySelector({Js(seletor)}).click()"); await Task.Delay(700);
            }
            async Task Foto(string nome) { using var arquivo = File.Create(Path.Combine(pasta, nome + ".png")); await view.CapturarPreviewAsync(arquivo); }
            async Task Fechar() { await browser.CoreWebView2.ExecuteScriptAsync("document.querySelector('[data-fechar-dialogo]').click()"); await Esperar("!document.querySelector('.dialogo-web')", "Cancelar não fechou"); }

            await Digitar("TermoPaciente", "zuleica"); await Clicar("Buscar");
            await Esperar("document.querySelector('[data-tabela-container=\"Resultados\"]')?.innerText.includes('Zuleica Sintética Busca')&&!document.querySelector('[data-tabela-container=\"Resultados\"]')?.innerText.includes('Abel Outro')", "Busca de guias pelo nome");
            await Digitar("TermoPaciente", "52998224725"); await Clicar("Buscar");
            await Esperar("document.querySelector('[data-tabela-container=\"Resultados\"]')?.innerText.includes('Zuleica Sintética Busca')", "Busca de guias pelo CPF"); await Foto("01-guias-cpf");
            await Digitar("TermoPaciente", "Paciente inexistente"); await Clicar("Buscar");
            await Esperar("document.querySelector('[data-tabela-container=\"Resultados\"] tbody')?.innerText.includes('Nenhum')", "Busca vazia de guias");
            await view.NavegarAsync("faturamento-pendencias"); await Task.Delay(700); await Clicar("Anotar", tabela: "Codigos");
            await Digitar("Texto", "Anotação digitada e gravada pela interface", true); await Clicar("confirmar", true);
            await Esperar("!document.querySelector('.dialogo-web')", "Anotação não fechou");
            using (var scope = sp.CreateScope()) Exigir(await scope.ServiceProvider.GetRequiredService<ClinicaDbContext>().Codigos.AnyAsync(c => c.ObservacaoPendencia == "Anotação digitada e gravada pela interface"), "Anotação não persistiu no banco");
            await Clicar("DarBaixa", tabela: "Codigos"); await Fechar(); Console.WriteLine("OK Faturamento DOM: nome/CPF/vazio, anotar persistido e baixa cancelada.");

            await view.NavegarAsync("caixa"); await Task.Delay(700); await Clicar("NovoLancamento");
            await Escolher("Tipo", "Entrada"); await Digitar("Seletor.Termo", "Zuleica", true);
            await Escolher("Seletor.Selecionado", "Zuleica Sintética Busca");
            await Digitar("Seletor.Termo", "52998224725", true); await Escolher("Seletor.Selecionado", "Zuleica Sintética Busca");
            await Digitar("Descricao", "Receita sintética buscada na interface", true); await Digitar("Valor", "125,50", true); await Foto("02-financeiro-paciente");
            await Clicar("salvar", true); await Esperar("!document.querySelector('.dialogo-web')", "Salvar lançamento não fechou");
            using (var scope = sp.CreateScope()) Exigir(await scope.ServiceProvider.GetRequiredService<ClinicaDbContext>().Set<LancamentoFinanceiro>().AnyAsync(l => l.Descricao == "Receita sintética buscada na interface" && l.PacienteId == pacienteId && l.Valor == 125.50m), "Lançamento perdeu paciente/valor digitado");
            await Digitar("FiltroTexto", "Receita sintética"); await Esperar("document.querySelector('[data-tabela-container=\"Linhas\"]')?.innerText.includes('Receita sintética buscada')", "Filtro de caixa não encontrou lançamento");
            await Digitar("FiltroTexto", "Inexistente"); await Esperar("!document.querySelector('[data-tabela-container=\"Linhas\"]')?.innerText.includes('Receita sintética buscada')", "Filtro de caixa não retirou lançamento");
            await Clicar("LimparFiltro"); await Clicar("NovoLancamento"); await Digitar("Descricao", "Lançamento cancelado", true); await Fechar();
            using (var scope = sp.CreateScope()) Exigir(!await scope.ServiceProvider.GetRequiredService<ClinicaDbContext>().Set<LancamentoFinanceiro>().AnyAsync(l => l.Descricao == "Lançamento cancelado"), "Cancelamento gravou lançamento");
            Console.WriteLine("OK Financeiro DOM: busca nome/CPF, seleção paciente, entrada persistida, filtro/vazio/limpar e cancelar.");

            await view.NavegarAsync("acessos"); await Task.Delay(700); await Clicar("Novo"); await Clicar("salvar", true);
            await Esperar("!!document.querySelector('.dialogo-web')&&document.querySelector('.dialogo-web').innerText.includes('nome')", "Usuário vazio não validou");
            await Digitar("Nome", "Usuário fictício criado no DOM", true); await Digitar("Login", "usuario.dom.qa", true); await Digitar("Senha", "SenhaSintetica!2468", true);
            await Clicar("salvar", true); await Esperar("!document.querySelector('.dialogo-web')", "Salvar usuário não fechou");
            using (var scope = sp.CreateScope()) Exigir(await scope.ServiceProvider.GetRequiredService<ClinicaDbContext>().Set<UsuarioSistema>().AnyAsync(u => u.Login == "usuario.dom.qa" && u.Nome == "Usuário fictício criado no DOM"), "Usuário digitado não persistiu");
            await Foto("03-gerente-usuario");
            await browser.CoreWebView2.ExecuteScriptAsync("[...document.querySelectorAll('[data-tabela-container=\"Usuarios\"] tbody tr')].find(r=>r.innerText.includes('usuario.dom.qa')).querySelector('[data-comando=\"Editar\"]').click()");
            await Digitar("Nome", "Nome cancelado no DOM", true); await Fechar();
            using (var scope = sp.CreateScope()) Exigir(!await scope.ServiceProvider.GetRequiredService<ClinicaDbContext>().Set<UsuarioSistema>().AnyAsync(u => u.Nome == "Nome cancelado no DOM"), "Cancelar edição gravou nome");
            await browser.CoreWebView2.ExecuteScriptAsync("[...document.querySelectorAll('[data-tabela-container=\"Usuarios\"] tbody tr')].find(r=>r.innerText.includes('usuario.dom.qa')).querySelector('[data-comando=\"Editar\"]').click()");
            await Digitar("Nome", "Nome editado no DOM", true); await Clicar("salvar", true); await Esperar("!document.querySelector('.dialogo-web')", "Editar usuário não fechou");
            using (var scope = sp.CreateScope()) Exigir(await scope.ServiceProvider.GetRequiredService<ClinicaDbContext>().Set<UsuarioSistema>().AnyAsync(u => u.Login == "usuario.dom.qa" && u.Nome == "Nome editado no DOM"), "Editar usuário não persistiu");
            Console.WriteLine("OK Gerente DOM: validar, criar, abrir, cancelar e editar usuário persistido. Sem banco externo.");
            await view.NavegarAsync("guarda-prontuario"); await Task.Delay(700);
            await Digitar("Paciente.Termo", "zuleica"); await Escolher("Paciente.Selecionado", "Zuleica Sintética Busca", false);
            await Esperar("[...document.querySelectorAll('.conteudo output')].some(o=>o.textContent==='Zuleica Sintética Busca')", "Seleção não carregou guarda do paciente");
            await Foto("04-gerente-guarda-paciente");
            await view.NavegarAsync("auditoria"); await Task.Delay(700);
            await Digitar("Paciente.Termo", "52998224725"); await Escolher("Paciente.Selecionado", "Zuleica Sintética Busca", false);
            await Esperar("document.querySelector('[data-tabela-container=\"Eventos\"]')?.innerText.includes('Zuleica Sintética Busca')&&!document.querySelector('[data-tabela-container=\"Eventos\"]')?.innerText.includes('Abel Outro')", "Seleção não filtrou auditoria do paciente");
            await Foto("05-gerente-auditoria-paciente");
            Console.WriteLine("OK Gerente DOM: busca/seleção em Guarda e Auditoria dispara contexto do paciente escolhido.");
            var seletor = new EscolherPacienteWebViewModel("Paciente do teste", sp.GetRequiredService<IServiceScopeFactory>());
            bool? confirmado = null;
            var escolha = view.ExecutarComDialogosAsync(async () => confirmado = await view.Dialogos.AbrirAsync("EscolherPaciente", seletor));
            await Digitar("Seletor.Termo", "52998224725", true);
            await Esperar("(()=>{const o=[...document.querySelector('.dialogo-web select[data-campo=\"Seletor.Selecionado\"]')?.options??[]].filter(o=>o.value);return o.length===1&&o[0].textContent.includes('Zuleica Sintética Busca')})()", "Seletor compartilhado continuou mostrando pacientes fora do CPF");
            await Escolher("Seletor.Selecionado", "Zuleica Sintética Busca");
            await Clicar("Confirmar", true); await escolha.WaitAsync(TimeSpan.FromSeconds(5));
            Exigir(confirmado == true && seletor.Seletor.Selecionado?.Id == pacienteId, "Seletor compartilhado não devolveu o paciente digitado");
            Console.WriteLine("OK seletor compartilhado DOM: CPF digitado, resultado, seleção e confirmação preservam o paciente.");
            await view.NavegarAsync("metas"); await Task.Delay(700); await Clicar("NovaMeta");
            await Digitar("Valor", "12.5", true); await Digitar("Observacoes", "Meta decimal digitada no DOM", true); await Clicar("salvar", true);
            await Esperar("!document.querySelector('.dialogo-web')", "Meta decimal não salvou");
            using (var scope = sp.CreateScope())
            {
                var meta = (await scope.ServiceProvider.GetRequiredService<MetaService>().DoAnoAsync(DateTime.Today.Year)).Single(m => m.Observacoes == "Meta decimal digitada no DOM");
                Exigir(meta.Valor == 12.5m, "Meta decimal perdeu casas ao salvar: digitado 12.5, persistido " + meta.Valor.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            Console.WriteLine("OK Meta DOM: 12.5 persistiu exatamente 12,5 sem multiplicar por dez.");
            await Clicar("NovaMeta"); await Digitar("Valor", "12,5", true); await Digitar("Observacoes", "Meta com vírgula digitada no DOM", true); await Clicar("salvar", true);
            await Esperar("!document.querySelector('.dialogo-web')", "Meta com vírgula não salvou");
            using (var scope = sp.CreateScope()) Exigir((await scope.ServiceProvider.GetRequiredService<MetaService>().DoAnoAsync(DateTime.Today.Year)).Any(m => m.Observacoes == "Meta com vírgula digitada no DOM" && m.Valor == 12.5m), "Meta com vírgula perdeu casas ao salvar");
            Console.WriteLine("OK Meta DOM: 12,5 e 12.5 produzem o mesmo valor exato.");
        }
        finally { window.Close(); }
    }
}
