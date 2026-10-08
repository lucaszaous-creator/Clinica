using System.Text.Json;
using System.Globalization;
using System.Data.Common;
using Clinica.Desktop.Controls;
using Clinica.Financeiro.Modulo;
using Clinica.Financeiro.ViewModels;
using Clinica.Domain.Entities;
using Clinica.Financeiro.Web;
using Clinica.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Registro e autorização das quinze páginas, com VMs e serviços reais em memória.</summary>
public static class PaginasWebQa
{
    public static async Task Executar(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();
        Exigir(db.Database.IsSqlite() && new SqliteConnectionStringBuilder(db.Database.GetConnectionString()).DataSource == ":memory:",
            "O QA das páginas só pode rodar no SQLite em memória.");
        var erros = FinanceiroPaginasController.ValidarRegistro();
        Exigir(erros.Count == 0, "Contrato estático inválido: " + string.Join("; ", erros));
        var sessao = SessaoUsuario.Atual;
        var original = await db.Set<UsuarioSistema>().AsNoTracking().Include(u => u.Profissional).SingleAsync(u => u.Id == sessao.UsuarioId);
        var totalAntes = await db.Set<LancamentoFinanceiro>().CountAsync();
        string[] rotas = ["caixa", "contas", "inadimplencia", "plano-contas", "fluxo-caixa", "resultado", "fechamento-caixa", "recebiveis", "conciliacao", "extrato-banco", "producao", "pacotes", "estoque", "repasses", "taxas"];
        using var paginas = new FinanceiroPaginasController(services);
        var contextos = new HashSet<string>(StringComparer.Ordinal);
        var tabelaIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var rota in rotas)
        {
            await paginas.NavegarAsync(rota);
            await Aguardar(paginas);
            // Aguarda explicitamente a mesma carga da ação Atualizar. O extrato só carrega após importação.
            if (rota != "extrato-banco") await paginas.ExecutarAcaoAsync("Carregar");
            await Aguardar(paginas);
            var pagina = paginas.ObterPagina();
            Exigir(pagina.Chave == rota && !string.IsNullOrWhiteSpace(pagina.Titulo), $"Página {rota} incorreta.");
            Exigir(!pagina.NaoVerificado && !pagina.MensagemEhErro, $"Falha ao carregar {rota}: {pagina.Mensagem}");
            Exigir(contextos.Add(pagina.Contexto), "Navegação reutilizou contexto anterior.");
            Exigir(pagina.Secoes.Count > 0, $"{rota} não expôs suas seções.");
            foreach (var tabela in pagina.Secoes.SelectMany(s => s.Tabelas))
            {
                Exigir(tabela.Colunas.Count > 0, $"Tabela {rota}/{tabela.Chave} sem colunas.");
                foreach (var linha in tabela.Linhas)
                {
                    Exigir(Guid.TryParse(linha.Id, out _), "Identificador público não é opaco.");
                    Exigir(tabelaIds.Add(rota + "/" + tabela.Chave + "/" + linha.Id), "Linhas duplicadas no descriptor.");
                    Exigir(tabela.Colunas.All(c => linha.Celulas.ContainsKey(c.Chave)), "Linha perdeu coluna declarada.");
                }
            }
            foreach (var grafico in pagina.Secoes.SelectMany(s => s.Graficos ?? []))
            {
                Exigir(grafico.Tipo is "linha" or "barra", "Gráfico sem tipo conhecido.");
                Exigir(grafico.Pontos.All(p => p.Valor is null || double.IsFinite(p.Valor.Value)), "Gráfico contém valor não finito.");
                Exigir(grafico.Pontos.All(p => !string.IsNullOrWhiteSpace(p.ValorFormatado)), "Gráfico perdeu rótulo do valor.");
            }
            if (rota == "fluxo-caixa")
            {
                var serie = pagina.Secoes.Single(s => s.Chave == "meses");
                var meses = serie.Tabelas.Single().Linhas;
                Exigir(serie.Graficos?.Count == 2 && meses.Count == 12, "Fluxo perdeu as duas séries e janela de 12 meses.");
                foreach (var grafico in serie.Graficos!)
                foreach (var ponto in grafico.Pontos)
                {
                    var mes = meses.Single(l => l.Celulas["Mes"] == ponto.Rotulo);
                    var valor = decimal.Parse(mes.Celulas[grafico.Chave == "entradas" ? "Entradas" : "Resultado"], NumberStyles.Currency, CultureInfo.GetCultureInfo("pt-BR"));
                    Exigir(ponto.Valor is not null && Math.Abs(ponto.Valor.Value - (double)valor) < 0.001,
                        "Gráfico não corresponde aos valores realizados da tabela real.");
                }
                Exigir(serie.Graficos.SelectMany(g => g.Pontos).Any(p => p.Valor != 0), "Dados sintéticos não exercitaram as curvas reais.");
            }
            await Rejeitar(() => paginas.AtualizarCampoAsync("UsuarioId", JsonSerializer.SerializeToElement(123)), "Campo fora da allowlist aceito.");
            await Rejeitar(() => paginas.ExecutarAcaoAsync("GetType"), "Comando fora da allowlist aceito.");
            Console.WriteLine($"PAGINAS WEB OK {rota}: {pagina.Secoes.Count} seções, {pagina.Secoes.Sum(s => s.Tabelas.Sum(t => t.Linhas.Count))} linhas.");
        }
        await Rejeitar(() => paginas.NavegarAsync("../usuarios"), "Rota fora do Financeiro aceita.");
        await paginas.NavegarAsync("caixa"); await Aguardar(paginas);
        await paginas.ExecutarAcaoAsync("Carregar");
        var primeiro = paginas.ObterPagina();
        Exigir(contextos.Add(primeiro.Contexto), "Voltar à mesma rota preservou token antigo.");
        var linhaAntiga = primeiro.Secoes.SelectMany(s => s.Tabelas).Single(t => t.Chave == "Linhas").Linhas.First();
        await Rejeitar(() => paginas.ExecutarAcaoAsync("Cancelar", "Linhas", Guid.NewGuid().ToString("N")), "Linha arbitrária aceita.");
        await Rejeitar(() => paginas.AtualizarCampoAsync("Valor", JsonSerializer.SerializeToElement("1"), "Linhas", linhaAntiga.Id), "Edição de valor não exposta foi aceita.");
        await paginas.AtualizarCampoAsync("FiltroTexto", JsonSerializer.SerializeToElement("FILTRO_QA_SEM_CORRESPONDENCIA_9f4a"));
        await Aguardar(paginas);
        Exigir(paginas.ObterPagina().Secoes.SelectMany(s => s.Tabelas).Single(t => t.Chave == "Linhas").Linhas.Count == 0, "Filtro real não foi aplicado.");
        await Rejeitar(() => paginas.ExecutarAcaoAsync("Cancelar", "Linhas", linhaAntiga.Id), "Linha removida pelo filtro ainda pôde receber ação.");
        await paginas.ExecutarAcaoAsync("LimparFiltro");
        try
        {
            var leitura = new UsuarioSistema { Id = original.Id, Nome = "QA somente leitura", Login = "qa.leitura", Perfil = PerfilAcesso.Gerente,
                PermissoesNegadas = PerfisAcesso.Todas & ~(Permissao.VerFinanceiro | Permissao.VenderPacote) };
            sessao.Entrar(leitura);
            await paginas.NavegarAsync("caixa"); await Aguardar(paginas);
            Exigir(!paginas.ObterPagina().Acoes.Single(a => a.Chave == "NovoLancamento").Habilitada, "Escrita habilitada em sessão somente leitura.");
            await Rejeitar(() => paginas.ExecutarAcaoAsync("NovoLancamento"), "Sessão de leitura executou escrita.");
            await paginas.NavegarAsync("fechamento-caixa"); await Aguardar(paginas);
            var campos = paginas.ObterPagina().Secoes.SelectMany(s => s.Campos);
            Exigir(!campos.Single(c => c.Chave == "ValorContado").Habilitado, "Campo financeiro editável sem permissão.");
            await Rejeitar(() => paginas.AtualizarCampoAsync("ValorContado", JsonSerializer.SerializeToElement("100")), "Campo financeiro mudou sem permissão.");
            sessao.Entrar(new UsuarioSistema());
            await Rejeitar(() => paginas.NavegarAsync("caixa"), "Sessão ausente abriu página financeira.");
            await Rejeitar(() => Task.FromResult(paginas.ObterPagina()), "Dados permaneceram legíveis após encerrar sessão.");
        }
        finally { sessao.Entrar(original); }
        Exigir(await db.Set<LancamentoFinanceiro>().CountAsync() == totalAntes, "Verificações de rejeição alteraram lançamentos.");
        await VerificarCargaPendente(services, db);
        Console.WriteLine("PAGINAS WEB OK: 15 rotas, campos/comandos registrados, contexto, linhas atuais, leitura sem escrita e perda de sessão.");
    }

    private static async Task VerificarCargaPendente(IServiceProvider origem, ClinicaDbContext db)
    {
        // Pausa consultas reais antes de o SQLite executar: o teste enxerga a janela em que
        // a interface antes apresentava vazio e aceitava operar sobre a coleção anterior.
        var barreira = new BarreiraDeLeitura();
        var opcoes = new DbContextOptionsBuilder<ClinicaDbContext>()
            .UseSqlite(db.Database.GetDbConnection()).AddInterceptors(barreira).Options;
        var registros = new ServiceCollection();
        registros.AddClinica("Host=127.0.0.1;Port=1;Database=NAO_USAR;Username=NAO_USAR;Timeout=1");
        registros.AddScoped(_ => new ClinicaDbContext(opcoes));
        registros.AddSingleton(origem.GetRequiredService<SessaoUsuario>());
        registros.AddSingleton(origem.GetRequiredService<ISnackbarService>());
        registros.AddSingleton(origem.GetRequiredService<IDialogoService>());
        new ModuloFinanceiro().Registrar(registros);
        using var lentos = registros.BuildServiceProvider();
        using var pagina = new FinanceiroPaginasController(lentos);
        foreach (var rota in new[] { "contas", "fluxo-caixa", "fechamento-caixa", "recebiveis", "taxas" })
        {
            barreira.Bloquear();
            try
            {
                await pagina.NavegarAsync(rota);
                var pendente = pagina.ObterPagina();
                Exigir(pendente.Carregando && barreira.Pendentes > 0, $"{rota} não sinalizou sua carga inicial pendente.");
                Exigir(pendente.Acoes.Concat(pendente.Secoes.SelectMany(s => s.Acoes)).All(a => !a.Habilitada), $"{rota} permite ação durante a consulta.");
                Exigir(pendente.Campos.Concat(pendente.Secoes.SelectMany(s => s.Campos)).All(c => !c.Habilitado), $"{rota} permite editar enquanto a consulta sobrescreveria campos.");
                Exigir(pendente.Secoes.SelectMany(s => s.Tabelas).All(t => t.Vazio == "Consultando os registros…"), $"{rota} anunciou ausência de registros antes de consultar.");
                await Rejeitar(() => pagina.ExecutarAcaoAsync("Carregar"), $"{rota} aceitou comando concorrente à carga inicial.");
            }
            finally { barreira.LiberarTodas(); }
            await Aguardar(pagina);
            Exigir(!pagina.ObterPagina().Carregando, $"{rota} permaneceu carregando após a consulta.");
            await pagina.ExecutarAcaoAsync("Carregar");
        }

        // Duas cargas da mesma VM: a resposta superada termina primeiro; a flag só pode
        // cair quando a consulta mais nova terminar, incluindo os caminhos de return.
        var contas = lentos.GetRequiredService<ContasViewModel>();
        var limite = DateTime.UtcNow.AddSeconds(15);
        while (contas.Carregando)
        {
            Exigir(DateTime.UtcNow < limite, "Carga preparatória das contas não terminou.");
            await Task.Delay(20);
        }
        barreira.Bloquear();
        try
        {
            var primeira = contas.CarregarAsync();
            var segunda = contas.CarregarAsync();
            Exigir(barreira.Pendentes == 2 && contas.Carregando, "O teste não reteve as duas cargas concorrentes.");
            barreira.LiberarPrimeira();
            await primeira;
            Exigir(contas.Carregando && !segunda.IsCompleted, "Resposta superada retirou o indicador da carga ainda pendente.");
            barreira.LiberarTodas();
            await segunda;
            Exigir(!contas.Carregando, "Contador de cargas não retornou a zero.");
        }
        finally { barreira.LiberarTodas(); }
        Console.WriteLine("PAGINAS WEB OK: cinco cargas iniciais retidas, comandos bloqueados e duas consultas concorrentes sem liberação prematura.");
    }

    private sealed class BarreiraDeLeitura : DbCommandInterceptor
    {
        private readonly List<TaskCompletionSource<bool>> _esperas = [];
        private bool _ativa;
        public int Pendentes { get { lock (_esperas) return _esperas.Count(e => !e.Task.IsCompleted); } }
        public void Bloquear() { lock (_esperas) { _esperas.Clear(); _ativa = true; } }
        public void LiberarPrimeira()
        {
            TaskCompletionSource<bool> primeira;
            lock (_esperas) primeira = _esperas.First(e => !e.Task.IsCompleted);
            primeira.TrySetResult(true);
        }
        public void LiberarTodas()
        {
            TaskCompletionSource<bool>[] esperas;
            lock (_esperas) { _ativa = false; esperas = _esperas.ToArray(); }
            foreach (var espera in esperas) espera.TrySetResult(true);
        }
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Task? pendente = null;
            lock (_esperas)
            {
                if (_ativa)
                {
                    var espera = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    _esperas.Add(espera); pendente = espera.Task;
                }
            }
            if (pendente is not null) await pendente.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            return result;
        }
    }

    private static async Task Aguardar(FinanceiroPaginasController paginas)
    {
        await Task.Delay(60);
        var limite = DateTime.UtcNow.AddSeconds(15);
        while (paginas.ObterPagina().Carregando)
        {
            if (DateTime.UtcNow > limite) throw new InvalidOperationException("Carga da página excedeu 15 segundos.");
            await Task.Delay(25);
        }
    }
    private static async Task Rejeitar(Func<Task> acao, string falha)
    {
        try { await acao(); }
        catch (InvalidOperationException) { return; }
        catch (UnauthorizedAccessException) { return; }
        throw new InvalidOperationException(falha);
    }
    private static void Exigir(bool condicao, string erro) { if (!condicao) throw new InvalidOperationException(erro); }
}
