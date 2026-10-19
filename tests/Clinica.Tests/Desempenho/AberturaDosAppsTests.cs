using System.Text;
using Clinica.Application;
using Clinica.Domain.Entities;
using Xunit;
using Xunit.Abstractions;

namespace Clinica.Tests.Desempenho;

/// <summary>
/// A ABERTURA DOS CINCO APPS CONTRA A REDE DA CLÍNICA (out/2026). A direção relatou 15–30 s
/// entre o login e os dados da tela inicial, nos cinco apps. A causa foi MEDIDA aqui, e não
/// deduzida: o caminho de abertura de cada app roda contra um banco com latência por ida
/// (<see cref="RedeSimulada"/>), contando as idas, as que acontecem EM SÉRIE e as conexões
/// físicas que o pool teria de abrir.
///
/// Dois usos:
/// - <b>a medição</b> (<see cref="Medir_a_abertura_dos_cinco_apps"/>), opt-in: com a variável
///   <c>CLINICA_MEDIR_ABERTURA</c> apontando um arquivo, grava a tabela de cada app a 150 e
///   300 ms por ida;
/// - <b>a trava</b> (<see cref="A_tela_inicial_nao_volta_a_fazer_idas_em_serie"/>): o número de
///   idas em série da tela inicial de cada app fica FIXADO — a próxima leitura posta em fila
///   numa delas reprova aqui, meses antes de a clínica sentir.
/// </summary>
[Collection(ColecaoDiagnostico.Nome)]
public sealed class AberturaDosAppsTests(BancoDaAbertura banco, ITestOutputHelper saida)
    : IClassFixture<BancoDaAbertura>, IDisposable
{
    private readonly Action<string, Exception>? _sinkOriginal = Diagnostico.Sink;

    public void Dispose()
    {
        Diagnostico.Sink = _sinkOriginal;
        new SessaoUsuario().Entrar(new UsuarioSistema());
    }

    public static IEnumerable<object[]> Apps() => Enum.GetValues<AppDaSuite>().Select(a => new object[] { a });

    /// <summary>
    /// Idas EM SÉRIE da tela inicial de cada app, medidas em out/2026 depois da correção (a 150 ms
    /// por ida): Recepção 6, Financeiro 1, Faturamento 5, Consultório 3, Gerente 13. O teto tem
    /// folga de 3 para o ruído da máquina; uma leitura nova posta EM FILA numa tela inicial passa
    /// dele e reprova aqui.
    /// </summary>
    [Theory]
    [InlineData(AppDaSuite.Recepcao, 9)]
    [InlineData(AppDaSuite.Financeiro, 4)]
    [InlineData(AppDaSuite.Faturamento, 8)]
    [InlineData(AppDaSuite.Consultorio, 6)]
    [InlineData(AppDaSuite.Gerente, 16)]
    public async Task A_tela_inicial_nao_volta_a_fazer_idas_em_serie(AppDaSuite app, int teto)
    {
        var ptBr = new System.Globalization.CultureInfo("pt-BR");
        System.Globalization.CultureInfo.CurrentCulture = ptBr;
        System.Globalization.CultureInfo.CurrentUICulture = ptBr;

        var aquecer = new AberturaDosApps(banco);
        aquecer.Rede.Ida = TimeSpan.FromMilliseconds(1);
        var sp0 = aquecer.Servicos();
        await aquecer.AntesDoLoginAsync(sp0, app);
        await aquecer.LoginAsync(sp0, app);
        await aquecer.TelaInicialAsync(sp0, app, 64);

        var medicao = new AberturaDosApps(banco);
        medicao.Rede.Ida = TimeSpan.FromMilliseconds(60);
        var sp = medicao.Servicos();
        await medicao.AntesDoLoginAsync(sp, app);
        await medicao.LoginAsync(sp, app);
        var tela = await medicao.TelaInicialAsync(sp, app, 64, etapa: "tela inicial");
        saida.WriteLine(tela.ToString());

        Assert.True(tela.IdasEmSerie <= teto,
            $"{app}: a tela inicial fez {tela.IdasEmSerie} idas em série ao banco (teto {teto}). Leitura independente vai em paralelo (ILeituraParalela).");
    }

    [Fact]
    public async Task Medir_a_abertura_dos_cinco_apps()
    {
        var arquivo = Environment.GetEnvironmentVariable("CLINICA_MEDIR_ABERTURA");
        if (string.IsNullOrWhiteSpace(arquivo)) return; // opt-in: a medição leva minutos

        var ptBr = new System.Globalization.CultureInfo("pt-BR");
        System.Globalization.CultureInfo.CurrentCulture = ptBr;
        System.Globalization.CultureInfo.CurrentUICulture = ptBr;
        var abertura = double.TryParse(Environment.GetEnvironmentVariable("CLINICA_MEDIR_ABERTURA_FISICA_MS"), out var a) ? a : 3000;
        var idas = (Environment.GetEnvironmentVariable("CLINICA_MEDIR_ABERTURA_IDAS_MS") ?? "150;300")
            .Split(';').Select(double.Parse).ToArray();
        var linhas = Environment.GetEnvironmentVariable("CLINICA_MEDIR_ABERTURA_LINHAS") == "1";

        var texto = new StringBuilder();
        // Aquecimento: JIT, modelo do EF e Razor compilam na primeira passada. O que se mede
        // depois é a REDE, não a primeira execução do .NET nesta máquina.
        foreach (var app in Enum.GetValues<AppDaSuite>())
        {
            var aquecer = new AberturaDosApps(banco);
            aquecer.Rede.Ida = TimeSpan.FromMilliseconds(1);
            var sp0 = aquecer.Servicos();
            await aquecer.AntesDoLoginAsync(sp0, app);
            await aquecer.LoginAsync(sp0, app);
            await aquecer.TelaInicialAsync(sp0, app, 64, () => aquecer.FundoDaAberturaAsync(sp0, app));
            new SessaoUsuario().Entrar(new UsuarioSistema());
        }
        foreach (var ms in idas)
        {
            texto.AppendLine($"===== ida ao banco = {ms} ms; conexão física nova = {abertura} ms =====");
            foreach (var app in Enum.GetValues<AppDaSuite>())
            {
                var medicao = new AberturaDosApps(banco);
                medicao.Rede.Ida = TimeSpan.FromMilliseconds(ms);
                medicao.Rede.GuardarLinhas = linhas;
                var sp = medicao.Servicos();

                var antes = await medicao.MedirAsync("1. antes do login (banco, catálogos)", 1, () => medicao.AntesDoLoginAsync(sp, app));
                var login = await medicao.MedirAsync("2. login (Entrar)", 2, () => medicao.LoginAsync(sp, app));
                var so = await medicao.TelaInicialAsync(sp, app, 64, etapa: "3. tela inicial sozinha (pool farto)");
                var linhasDaTela = medicao.Rede.Linhas;
                medicao.Rede.AberturaFisica = TimeSpan.FromMilliseconds(abertura);
                var completa = await medicao.TelaInicialAsync(sp, app, Clinica.Infrastructure.ConexaoRemota.AquecidasNaAbertura, () => medicao.FundoDaAberturaAsync(sp, app),
                    $"4. tela + fundo (pool aquecido: {Clinica.Infrastructure.ConexaoRemota.AquecidasNaAbertura})");
                medicao.Rede.AberturaFisica = TimeSpan.Zero;
                var fundo = await medicao.MedirAsync("5. só o fundo da abertura", 64, () => medicao.FundoDaAberturaAsync(sp, app));

                texto.AppendLine($"--- {app} ---");
                foreach (var m in new[] { antes, login, so, completa, fundo }) texto.AppendLine("  " + m);
                if (linhas)
                    foreach (var l in linhasDaTela)
                        texto.AppendLine($"      {l.Inicio.TotalMilliseconds,7:0} → {l.Fim.TotalMilliseconds,7:0}  {l.Sql}");
                new SessaoUsuario().Entrar(new UsuarioSistema());
            }
        }
        File.WriteAllText(arquivo, texto.ToString());
        saida.WriteLine(texto.ToString());
    }
}
