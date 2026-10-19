using System.Diagnostics;
using Bunit;
using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Clinica.Ui.Blazor.Componentes;
using Clinica.Ui.Blazor.Hospedagem;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Tests.Desempenho;

/// <summary>Um dos cinco executáveis, do ponto de vista da abertura.</summary>
public enum AppDaSuite { Recepcao, Financeiro, Faturamento, Consultorio, Gerente }

/// <summary>O que uma etapa da abertura custou na rede simulada.</summary>
public sealed record MedidaDaAbertura(
    string Etapa, long Idas, int IdasEmSerie, TimeSpan Tempo, TimeSpan? PrimeiroDado,
    int MaximoSimultaneo, long AberturasFisicas, long Sincronos)
{
    public override string ToString() =>
        $"{Etapa,-44} idas={Idas,4}  em série={IdasEmSerie,3}  tempo={Tempo.TotalMilliseconds,7:0} ms" +
        (PrimeiroDado is { } p ? $"  1º dado={p.TotalMilliseconds,7:0} ms" : "") +
        $"  simult.máx={MaximoSimultaneo,2}  físicas novas={AberturasFisicas}  síncronas={Sincronos}";
}

/// <summary>
/// O CAMINHO DE ABERTURA de cada app, executado contra a rede simulada (out/2026 — a direção
/// relatou 15–30 s até as telas iniciais terem dados nos cinco apps). Cada etapa chama o MESMO
/// código que o app chama: o preparo do banco (<see cref="AberturaDoBanco"/>), o login
/// (<see cref="AcessoService"/>), a tela inicial (o componente Razor, montado pela MESMA
/// <c>Raiz</c> que o navegador da janela usa, ou o serviço da tela WPF) e o que roda por trás
/// dela na abertura (os avisos do cabeçalho, a conclusão automática, os lembretes, a cópia de
/// segurança).
/// </summary>
public sealed class AberturaDosApps(BancoDaAbertura banco)
{
    public RedeSimulada Rede { get; } = new();

    /// <summary>O faturamento chamava MigrateAsync em TODA abertura, mesmo sem pendência.</summary>
    public bool MigrarSempreNoFaturamento { get; set; } = true;

    // ==================== montagem ====================

    public IServiceProvider Servicos()
    {
        var s = new ServiceCollection();
        Registrar(s);
        return s.BuildServiceProvider();
    }

    private void Registrar(IServiceCollection s)
    {
        s.AddClinica("Host=medicao;Database=medicao");
        foreach (var d in s.Where(d =>
                     d.ServiceType == typeof(DbContextOptions<ClinicaDbContext>) ||
                     d.ServiceType == typeof(DbContextOptions) ||
                     d.ServiceType == typeof(ClinicaDbContext)).ToList())
            s.Remove(d);
        s.AddDbContext<ClinicaDbContext>(o => o.UseSqlite(banco.Cadeia)
            .AddInterceptors(Rede.ParaComandos, Rede.ParaConexoes));
        s.AddSingleton<SessaoUsuario>();
    }

    public static PerfilAcesso PerfilDo(AppDaSuite app) => app switch
    {
        AppDaSuite.Recepcao => PerfilAcesso.Recepcao,
        AppDaSuite.Financeiro => PerfilAcesso.Financeiro,
        AppDaSuite.Faturamento => PerfilAcesso.Faturista,
        AppDaSuite.Consultorio => PerfilAcesso.Profissional,
        _ => PerfilAcesso.Gerente
    };

    public static Type? TelaInicial(AppDaSuite app) => app switch
    {
        AppDaSuite.Recepcao => typeof(Clinica.Ui.Blazor.Telas.InicioRecepcao),
        AppDaSuite.Financeiro => typeof(Clinica.Ui.Blazor.Telas.Caixa),
        AppDaSuite.Faturamento => typeof(Clinica.Ui.Blazor.Telas.PendenciasFaturamento),
        AppDaSuite.Gerente => typeof(Clinica.Ui.Blazor.Telas.MesaDaDirecao),
        _ => null // o Meu dia é WPF: mede-se o serviço que a ViewModel chama
    };

    // ==================== medição ====================

    /// <summary>Mede um passo: idas, idas em SÉRIE (tempo ÷ ida) e o pico de simultâneas.</summary>
    public async Task<MedidaDaAbertura> MedirAsync(string etapa, int fisicasProntas, Func<Task> passo)
    {
        Rede.Reiniciar(fisicasProntas);
        var inicio = Stopwatch.GetTimestamp();
        await passo();
        await EsperarQuietoAsync(inicio);
        return Medida(etapa, inicio, null);
    }

    private MedidaDaAbertura Medida(string etapa, long inicio, TimeSpan? primeiro)
    {
        var tempo = Stopwatch.GetElapsedTime(inicio, Math.Max(Rede.UltimaAtividade, inicio));
        return new MedidaDaAbertura(etapa, Rede.Comandos, Rede.IdasEmSerie, tempo, primeiro,
            Rede.MaximoSimultaneo, Rede.AberturasFisicas, Rede.Sincronos);
    }

    /// <summary>Espera o banco ficar quieto (nada no ar por 4 idas) — é quando a etapa terminou.</summary>
    private async Task EsperarQuietoAsync(long inicio, Func<bool>? pronta = null)
    {
        var quieto = TimeSpan.FromMilliseconds(Math.Max(120, Rede.Ida.TotalMilliseconds * 4));
        var prazo = DateTime.UtcNow.AddMinutes(3);
        while (DateTime.UtcNow < prazo)
        {
            await Task.Delay(10);
            if (Rede.EmVoo > 0) continue;
            if (Stopwatch.GetElapsedTime(Math.Max(Rede.UltimaAtividade, inicio)) < quieto) continue;
            if (pronta is not null && !pronta()) continue;
            return;
        }
        throw new TimeoutException("A etapa não terminou em 3 minutos.");
    }

    // ==================== as etapas ====================

    /// <summary>Antes do login: o preparo do banco (migrations? catálogos) e "há usuário?".</summary>
    public async Task AntesDoLoginAsync(IServiceProvider sp, AppDaSuite app)
    {
        if (app == AppDaSuite.Faturamento)
        {
            // ESPELHO de Clinica.Desktop/App.xaml.cs (o faturamento não usa o preparo da suíte
            // e é WPF puro; aqui se repete a mesma sequência de chamadas ao banco).
            using var e = sp.CreateScope();
            var db = e.ServiceProvider.GetRequiredService<ClinicaDbContext>();
            var pendentes = await db.Database.GetPendingMigrationsAsync();
            var aplicadas = await db.Database.GetAppliedMigrationsAsync();
            if (pendentes.Any()) throw new InvalidOperationException("Há migration pendente na medição.");
            if (MigrarSempreNoFaturamento) await db.Database.MigrateAsync();
            await e.ServiceProvider.GetRequiredService<ConvenioCatalogoService>().RecarregarCacheAsync();
            await e.ServiceProvider.GetRequiredService<ModalidadeCatalogoService>().RecarregarCacheAsync();
            await e.ServiceProvider.GetRequiredService<EspecialidadeCatalogoService>().RecarregarCacheAsync();
            await e.ServiceProvider.GetRequiredService<ParametrosService>().PrestadorConfiguradoAsync();
            _ = aplicadas;
        }
        else
        {
            await AberturaDoBanco.PrepararAsync(sp, migrar: (_, _) => throw new InvalidOperationException("Há migration pendente na medição."));
        }
        using var escopo = sp.CreateScope();
        await escopo.ServiceProvider.GetRequiredService<AcessoService>().ExisteUsuarioAtivoAsync();
    }

    /// <summary>O clique em Entrar.</summary>
    public async Task<UsuarioSistema> LoginAsync(IServiceProvider sp, AppDaSuite app)
    {
        using var escopo = sp.CreateScope();
        var r = await escopo.ServiceProvider.GetRequiredService<AcessoService>()
            .AutenticarAsync(banco.Usuarios[PerfilDo(app)].Login, BancoDaAbertura.Senha);
        if (r.Usuario is null) throw new InvalidOperationException("Login da medição falhou: " + r.Erro);
        new SessaoUsuario().Entrar(r.Usuario);
        return r.Usuario;
    }

    /// <summary>
    /// O que roda por trás da tela inicial, na abertura: os avisos do cabeçalho do shell, a
    /// conclusão automática e os ganchos do executável (lembretes, cópia, links).
    /// </summary>
    public async Task FundoDaAberturaAsync(IServiceProvider sp, AppDaSuite app)
    {
        var sessao = SessaoUsuario.Atual;
        var tarefas = new List<Task>();
        async Task Em<T>(Func<T, Task> f) where T : notnull
        {
            try
            {
                using var escopo = sp.CreateScope();
                await f(escopo.ServiceProvider.GetRequiredService<T>());
            }
            catch (Exception) { /* a tabela do posto pode não existir no SQLite: a ida já foi contada */ }
        }

        if (app != AppDaSuite.Faturamento)
        {
            // ShellViewModel: assinaturas de infusão (quem checa ou prescreve com vínculo).
            if (sessao.Pode(Permissao.ChecarPrescricao) || sessao.Pode(Permissao.Prescrever) && sessao.ProfissionalId is > 0)
                tarefas.Add(Em<ChecagemPrescricaoService>(s => s.ContarAssinaturasPendentesAsync(sessao.UsuarioId,
                    sessao.ProfissionalId, sessao.Pode(Permissao.ChecarPrescricao), sessao.Pode(Permissao.Prescrever))));
            // ShellWindow.Loaded → pedidos da enfermagem e respostas dos pacientes (Recepção e Gerente).
            if (sessao.Perfil is PerfilAcesso.Recepcao or PerfilAcesso.Gerente)
            {
                tarefas.Add(Em<IPlanejamentoEnfermagemService>(async s =>
                {
                    var r = await s.ResumoAsync(sessao.UsuarioId);
                    if (r.Novos > 0) await s.ListarAsync(sessao.UsuarioId);
                }));
                tarefas.Add(Em<AvisosDeConfirmacaoService>(s => s.ListarAsync(DateTime.Now, DateTime.Today)));
            }
            // SuiteApp: a conclusão automática começa já (uma volta).
            tarefas.Add(Em<ConclusaoAutomaticaService>(async s =>
            {
                await s.CandidatosAsync(ConclusaoAutomaticaService.Agora, 0);
                if (sessao.Perfil == PerfilAcesso.Gerente) await s.ContarPendenciasAsync(ConclusaoAutomaticaService.Agora);
            }));
        }

        switch (app)
        {
            case AppDaSuite.Recepcao:
                tarefas.Add(Em<LembreteEmailService>(s => s.EnviarLembretesDaAberturaAsync()));
                break;
            case AppDaSuite.Gerente:
                // Em fila no OnStartup do Gerente: cópia → links → lembretes.
                tarefas.Add(Task.Run(async () =>
                {
                    await Em<PoliticaBackupService>(s => s.ExecutarSeVencidoAsync());
                    await Em<PublicacaoDocumentoService>(s => s.ExpirarVencidosAsync());
                    await Em<ColetaRemotaTermoService>(s => s.LimparVencidasAsync());
                    await Em<LembreteEmailService>(s => s.EnviarLembretesDaAberturaAsync());
                }));
                break;
            case AppDaSuite.Faturamento:
                // App.xaml.cs do faturamento: a rodada (Faturista não é dispensado) e o aviso.
                tarefas.Add(Task.Run(async () =>
                {
                    await Em<RodadaPendenciasService>(async s =>
                    {
                        var hoje = DateOnly.FromDateTime(DateTime.Today);
                        await s.GarantirInicioAsync(hoje);
                        await s.ObterStatusAsync(hoje);
                    });
                    await Em<PendenciaService>(s => s.CodigosPendentesAsync(DateOnly.FromDateTime(DateTime.Today)));
                }));
                break;
        }

        await Task.WhenAll(tarefas);
    }

    /// <summary>
    /// A tela inicial com dados. Blazor: o componente montado pela Raiz, medido até o banco
    /// ficar quieto e o esqueleto sair. Consultório: o serviço do Meu dia.
    /// </summary>
    public async Task<MedidaDaAbertura> TelaInicialAsync(IServiceProvider sp, AppDaSuite app, int fisicasProntas,
        Func<Task>? junto = null, string etapa = "Tela inicial com dados")
    {
        Rede.Reiniciar(fisicasProntas);
        var inicio = Stopwatch.GetTimestamp();
        TimeSpan? primeiro = null;
        var fundo = junto is null ? Task.CompletedTask : Task.Run(junto);

        if (TelaInicial(app) is { } tipo)
        {
            using var ctx = new TestContext();
            ctx.JSInterop.Mode = JSRuntimeMode.Loose;
            Registrar(ctx.Services);
            ctx.Services.AddTelasBlazor();
            ctx.Services.AddSingleton<IHostDaTela, HostDaMedicao>();
            var tela = ctx.RenderComponent<Raiz>(p => p.Add(r => r.Tela, tipo));
            bool SemEsqueleto()
            {
                var m = tela.Markup;
                return !m.Contains("class=\"esqueleto") && !m.Contains("esqueleto ");
            }
            var vigia = Task.Run(async () =>
            {
                while (primeiro is null && Stopwatch.GetElapsedTime(inicio) < TimeSpan.FromMinutes(3))
                {
                    if (SemEsqueleto()) primeiro = Stopwatch.GetElapsedTime(inicio);
                    await Task.Delay(2);
                }
            });
            await EsperarQuietoAsync(inicio, SemEsqueleto);
            await vigia;
            await fundo;
            await EsperarQuietoAsync(inicio);
            if (tela.Markup.Contains("Algo deu errado nesta tela"))
                throw new InvalidOperationException("A tela inicial quebrou na medição.");
        }
        else
        {
            // ESPELHO de MeuDiaViewModel.CarregarAsync (WPF): o dia, depois a contagem de
            // pendências e o bloqueio do dia, no MESMO escopo e em fila.
            using var escopo = sp.CreateScope();
            var consultorio = escopo.ServiceProvider.GetRequiredService<ConsultorioService>();
            var prof = SessaoUsuario.Atual.ProfissionalId;
            var hoje = DateOnly.FromDateTime(DateTime.Today);
            await consultorio.DoDiaAsync(hoje, prof);
            primeiro = Stopwatch.GetElapsedTime(inicio);
            await consultorio.RegistrosPendentesAsync(hoje, prof);
            await escopo.ServiceProvider.GetRequiredService<BloqueioAgendaService>()
                .NoPeriodoAsync(DateTime.Today, DateTime.Today.AddDays(1));
            await fundo;
            await EsperarQuietoAsync(inicio);
        }
        return Medida(etapa, inicio, primeiro);
    }

    /// <summary>Sem disco nem shell: a tela inicial não pede nada disso ao abrir.</summary>
    private sealed class HostDaMedicao : IHostDaTela
    {
        public Task<string?> ExportarCsvAsync(IReadOnlyList<string> c, IReadOnlyList<IReadOnlyList<string>> l, string n) => Task.FromResult<string?>(null);
        public Task<string?> SalvarEAbrirAsync(byte[] c, string n, string f, string e) => Task.FromResult<string?>(null);
        public Task<ArquivoEscolhido?> EscolherArquivoAsync(string f, string t) => Task.FromResult<ArquivoEscolhido?>(null);
        public Task<bool> CopiarAsync(string texto) => Task.FromResult(true);
        public Task<bool> NavegarAsync(string chave) => Task.FromResult(false);
        public Task<bool> AbrirEnderecoAsync(string url) => Task.FromResult(false);
        public Task<bool> AbrirFichaDoPacienteAsync(int pacienteId) => Task.FromResult(false);
        public Task<string?> PerguntarOndeSalvarAsync(string n, string f, string e) => Task.FromResult<string?>(null);
        public Task<string?> PerguntarPastaAsync(string t) => Task.FromResult<string?>(null);
        public Task<string?> GravarArquivoAsync(string c, byte[] b) => Task.FromResult<string?>(null);
    }
}
