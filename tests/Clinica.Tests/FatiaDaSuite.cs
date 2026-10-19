using System.Reflection;
using System.Text;
using Xunit.Abstractions;
using Xunit.Sdk;

[assembly: Xunit.TestFramework("Clinica.Tests.FatiaDaSuite", "Clinica.Tests")]

namespace Clinica.Tests;

/// <summary>
/// Divide a suíte em N fatias para o CI rodar em jobs PARALELOS (out/2026: ela levava 27 min
/// num job só, e o PR esperava 32). A suíte não pode rodar em paralelo DENTRO do processo —
/// há estado estático compartilhado (ver AssemblyInfo.cs) —, mas processos diferentes não
/// dividem nada; então cada job roda a sua fatia, em série, como sempre.
///
/// Com <c>CLINICA_FATIA=i/n</c> (i de 1 a n) a descoberta descarta o que não é da fatia i.
/// Sem a variável, NADA muda: a rodada local continua sendo a suíte inteira.
///
/// A divisão é por CLASSE (a fixture de uma classe é montada uma vez por fatia). A exceção são
/// as classes marcadas com <see cref="FatiarPorCasoAttribute"/>: elas são grandes demais
/// para caber numa fatia só, e cada CASO (cada tela) vai para a sua.
///
/// ⚠️ PELO TEMPO MEDIDO, desde out/2026. Pelo hash do nome as fatias saíam desiguais (7,2
/// contra 3,9 min), e o PR espera a mais lenta. Agora <c>tempos-dos-testes.json</c> (gravado
/// por <c>tools/medir-tempos-dos-testes.py</c>) diz quanto cada chave demora, e as conhecidas
/// são distribuídas da mais longa para a mais curta, cada uma na fatia mais leve até ali — o
/// "split by timings" dos CIs grandes. Chave fora do arquivo (teste novo) cai pelo hash, como
/// antes: tempo desatualizado só piora o equilíbrio, nunca deixa teste de fora.
/// </summary>
public sealed class FatiaDaSuite(IMessageSink sink) : XunitTestFramework(sink)
{
    public const string Variavel = "CLINICA_FATIA";

    protected override ITestFrameworkDiscoverer CreateDiscoverer(IAssemblyInfo assemblyInfo)
        => new Descobridor(assemblyInfo, SourceInformationProvider, DiagnosticMessageSink);

    protected override ITestFrameworkExecutor CreateExecutor(AssemblyName assemblyName)
        => new Executor(assemblyName, SourceInformationProvider, DiagnosticMessageSink);

    /// <summary>Lê "i/n"; nulo quando a variável não existe ou não faz sentido (roda tudo).</summary>
    public static (int Indice, int Total)? Ler(string? valor)
    {
        var partes = (valor ?? "").Split('/');
        return partes.Length == 2 && int.TryParse(partes[0], out var i) && int.TryParse(partes[1], out var n) && n >= 1 && i >= 1 && i <= n
            ? (i, n) : null;
    }

    /// <summary>
    /// A fatia (1..n) de uma chave: pela tabela de tempos quando a chave foi medida, pelo hash
    /// quando não.
    /// </summary>
    public static int FatiaDe(string chave, int total)
        => Distribuir(Tempos.Value, total).TryGetValue(chave, out var fatia) ? fatia : FatiaPeloHash(chave, total);

    private static readonly Lazy<IReadOnlyDictionary<string, double>> Tempos = new(LerTempos);

    /// <summary>A tabela embutida (vazia quando o recurso não foi embutido no build).</summary>
    public static IReadOnlyDictionary<string, double> TemposMedidos => Tempos.Value;
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, IReadOnlyDictionary<string, int>> _distribuicoes = new();

    private static IReadOnlyDictionary<string, double> LerTempos()
    {
        using var fluxo = typeof(FatiaDaSuite).Assembly.GetManifestResourceStream("tempos-dos-testes.json");
        if (fluxo is null) return new Dictionary<string, double>();
        return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, double>>(fluxo) ?? new();
    }

    /// <summary>
    /// Maior primeiro, cada uma na fatia mais leve até ali (LPT). Determinística: empate de
    /// tempo desempata pelo nome, empate de carga pela fatia de menor número — os quatro jobs
    /// do CI calculam a MESMA tabela, ou um teste ficaria em duas fatias e outro em nenhuma.
    /// </summary>
    public static IReadOnlyDictionary<string, int> Distribuir(IReadOnlyDictionary<string, double> tempos, int total)
    {
        if (ReferenceEquals(tempos, Tempos.Value) && _distribuicoes.TryGetValue(total, out var pronta)) return pronta;
        var carga = new double[total];
        var mapa = new Dictionary<string, int>();
        foreach (var (chave, segundos) in tempos.OrderByDescending(t => t.Value).ThenBy(t => t.Key, StringComparer.Ordinal))
        {
            var leve = 0;
            for (var i = 1; i < total; i++) if (carga[i] < carga[leve]) leve = i;
            carga[leve] += segundos;
            mapa[chave] = leve + 1;
        }
        if (ReferenceEquals(tempos, Tempos.Value)) _distribuicoes[total] = mapa;
        return mapa;
    }

    /// <summary>FNV-1a: estável entre processos e máquinas, ao contrário de
    /// string.GetHashCode, que muda a cada execução.</summary>
    public static int FatiaPeloHash(string chave, int total)
    {
        var h = 2166136261u;
        foreach (var b in Encoding.UTF8.GetBytes(chave)) { h ^= b; h *= 16777619u; }
        return (int)(h % (uint)total) + 1;
    }

    public static string ChaveDe(ITestCase caso)
    {
        var classe = caso.TestMethod.TestClass.Class;
        var porCaso = classe.GetCustomAttributes(typeof(FatiarPorCasoAttribute).AssemblyQualifiedName).Any();
        return porCaso ? caso.DisplayName : classe.Name;
    }

    sealed class Descobridor(IAssemblyInfo a, ISourceInformationProvider s, IMessageSink d) : XunitTestFrameworkDiscoverer(a, s, d)
    {
        readonly (int Indice, int Total)? _fatia = Ler(Environment.GetEnvironmentVariable(Variavel));
        protected override bool FindTestsForType(ITestClass testClass, bool includeSourceInformation, IMessageBus messageBus, ITestFrameworkDiscoveryOptions discoveryOptions)
            => base.FindTestsForType(testClass, includeSourceInformation, _fatia is { } f ? new Filtro(messageBus, f) : messageBus, discoveryOptions);
    }

    sealed class Executor(AssemblyName a, ISourceInformationProvider s, IMessageSink d) : XunitTestFrameworkExecutor(a, s, d)
    {
        protected override ITestFrameworkDiscoverer CreateDiscoverer() => new Descobridor(AssemblyInfo, SourceInformationProvider, DiagnosticMessageSink);
    }

    sealed class Filtro(IMessageBus interno, (int Indice, int Total) fatia) : IMessageBus
    {
        public bool QueueMessage(IMessageSinkMessage message)
            => message is ITestCaseDiscoveryMessage m && FatiaDe(ChaveDe(m.TestCase), fatia.Total) != fatia.Indice || interno.QueueMessage(message);
        public void Dispose() { }
    }
}

/// <summary>A classe é dividida entre as fatias caso a caso, e não inteira (ver <see cref="FatiaDaSuite"/>).</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class FatiarPorCasoAttribute : Attribute;
