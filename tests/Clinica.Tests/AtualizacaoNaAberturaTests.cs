using Clinica.Atualizacao;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// A atualização ANTES DO LOGIN (nov/2026): só a versão obrigatória segura a abertura, com tempo limite;
/// sem internet abre direto; o limite de tentativas do PR #324 vale antes de instalar.
/// </summary>
public class AtualizacaoNaAberturaTests
{
    private sealed class Passos : IPassosDaAtualizacao
    {
        public Func<CancellationToken, Task<NovidadeNaAbertura?>> Procurar = _ => Task.FromResult<NovidadeNaAbertura?>(null);
        public Func<IProgress<int>, Task> Baixar = p => { p.Report(100); return Task.CompletedTask; };
        public string? Motivo;
        public int Reabriu, AoFechar, Tentativas;
        public readonly TaskCompletionSource AgendouAoFechar = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<NovidadeNaAbertura?> ProcurarAsync(CancellationToken ct) => Procurar(ct);
        public Task BaixarAsync(IProgress<int> progresso, CancellationToken ct) => Baixar(progresso);
        public void InstalarEReabrir() => Reabriu++;
        public void InstalarAoFechar() { AoFechar++; AgendouAoFechar.TrySetResult(); }
        public string? MotivoParaNaoInstalar(string versao) => Motivo;
        public void RegistrarTentativa(string versao) => Tentativas++;
    }

    private sealed class Tela : ITelaDeAtualizacao
    {
        public string? Mostrou; public bool Fechou; public readonly List<int> Progressos = [];
        public void Mostrar(string versao) => Mostrou = versao;
        public void Progresso(int porcento) { lock (Progressos) Progressos.Add(porcento); }
        public void Fechar() => Fechou = true;
    }

    private static readonly List<string> Log = [];
    private static void Registrar(string frase, Exception? _) { lock (Log) Log.Add(frase); }

    [Fact]
    public void Obrigatoria_e_a_marca_nas_notas_da_versao()
    {
        Assert.True(AtualizacaoNaAbertura.EhObrigatoria("Gerente 1.2.80\n<!-- clinica:obrigatoria -->\nAtualização obrigatória: a versão tem migration."));
        Assert.False(AtualizacaoNaAbertura.EhObrigatoria("Gerente 1.2.81"));
        Assert.False(AtualizacaoNaAbertura.EhObrigatoria(null));
    }

    [Fact]
    public async Task Sem_novidade_segue_direto_sem_tela()
    {
        var tela = new Tela();
        Assert.Equal(DesfechoDaAbertura.Seguir, await AtualizacaoNaAbertura.ExecutarAsync(new Passos(), tela, Registrar));
        Assert.Null(tela.Mostrou);
    }

    [Fact]
    public async Task Sem_internet_abre_direto()
    {
        var passos = new Passos { Procurar = _ => throw new HttpRequestException("Sem conexão") };
        var tela = new Tela();
        Assert.Equal(DesfechoDaAbertura.Seguir, await AtualizacaoNaAbertura.ExecutarAsync(passos, tela, Registrar));
        Assert.Null(tela.Mostrou);
        Assert.Equal(0, passos.Reabriu);
    }

    [Fact]
    public async Task Procura_que_nao_responde_no_tempo_abre_direto()
    {
        var nunca = new TaskCompletionSource<NovidadeNaAbertura?>();
        var passos = new Passos { Procurar = _ => nunca.Task };
        var inicio = DateTime.UtcNow;
        Assert.Equal(DesfechoDaAbertura.Seguir,
            await AtualizacaoNaAbertura.ExecutarAsync(passos, new Tela(), Registrar, limiteDaProcura: TimeSpan.FromMilliseconds(50)));
        Assert.True(DateTime.UtcNow - inicio < TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Versao_comum_nao_segura_o_login_e_instala_ao_fechar()
    {
        var download = new TaskCompletionSource();
        var passos = new Passos
        {
            Procurar = _ => Task.FromResult<NovidadeNaAbertura?>(new("1.2.80", Obrigatoria: false)),
            Baixar = _ => download.Task
        };
        var tela = new Tela();
        Assert.Equal(DesfechoDaAbertura.Seguir, await AtualizacaoNaAbertura.ExecutarAsync(passos, tela, Registrar));
        Assert.Null(tela.Mostrou); // nenhuma tela
        Assert.Equal(0, passos.AoFechar);
        download.SetResult();
        await passos.AgendouAoFechar.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(0, passos.Reabriu);
    }

    [Fact]
    public async Task Obrigatoria_mostra_a_tela_com_progresso_instala_e_reabre()
    {
        var passos = new Passos
        {
            Procurar = _ => Task.FromResult<NovidadeNaAbertura?>(new("1.2.80", Obrigatoria: true)),
            Baixar = p => { p.Report(40); p.Report(100); return Task.Delay(20); }
        };
        var tela = new Tela();
        Assert.Equal(DesfechoDaAbertura.Reiniciando, await AtualizacaoNaAbertura.ExecutarAsync(passos, tela, Registrar));
        Assert.Equal("1.2.80", tela.Mostrou);
        Assert.Equal(1, passos.Tentativas); // anotada ANTES de entregar ao Velopack (#324)
        Assert.Equal(1, passos.Reabriu);
    }

    [Fact]
    public async Task Obrigatoria_que_passa_do_tempo_libera_o_login_e_instala_ao_fechar()
    {
        var download = new TaskCompletionSource();
        var passos = new Passos
        {
            Procurar = _ => Task.FromResult<NovidadeNaAbertura?>(new("1.2.80", Obrigatoria: true)),
            Baixar = _ => download.Task
        };
        var tela = new Tela();
        Assert.Equal(DesfechoDaAbertura.Seguir,
            await AtualizacaoNaAbertura.ExecutarAsync(passos, tela, Registrar, limiteDaObrigatoria: TimeSpan.FromMilliseconds(50)));
        Assert.True(tela.Fechou);
        Assert.Equal(0, passos.Reabriu);
        download.SetResult();
        await passos.AgendouAoFechar.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Obrigatoria_com_falha_no_download_abre_na_versao_atual()
    {
        var passos = new Passos
        {
            Procurar = _ => Task.FromResult<NovidadeNaAbertura?>(new("1.2.80", Obrigatoria: true)),
            Baixar = _ => Task.FromException(new IOException("disco cheio"))
        };
        var tela = new Tela();
        Assert.Equal(DesfechoDaAbertura.Seguir, await AtualizacaoNaAbertura.ExecutarAsync(passos, tela, Registrar));
        Assert.True(tela.Fechou);
        Assert.Equal(0, passos.Reabriu);
    }

    [Fact]
    public async Task Tentativas_esgotadas_nao_mostram_a_tela_de_novo()
    {
        var passos = new Passos
        {
            Procurar = _ => Task.FromResult<NovidadeNaAbertura?>(new("1.2.80", Obrigatoria: true)),
            Motivo = "já tentada 2 vezes"
        };
        var tela = new Tela();
        Assert.Equal(DesfechoDaAbertura.Seguir, await AtualizacaoNaAbertura.ExecutarAsync(passos, tela, Registrar));
        Assert.Null(tela.Mostrou);
        Assert.Equal(0, passos.Reabriu);
    }

    [Fact]
    public void Limites_padrao_sao_os_combinados()
    {
        Assert.Equal(TimeSpan.FromMinutes(2), AtualizacaoNaAbertura.LimiteDaObrigatoria);
        Assert.True(AtualizacaoNaAbertura.LimiteDaProcura <= TimeSpan.FromSeconds(10));
    }
}
