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
/// A divisão é por CLASSE (a fixture de uma classe é montada uma vez por fatia), pelo hash
/// estável do nome — classe nova cai numa fatia sozinha, sem lista para manter. A exceção são
/// as classes marcadas com <see cref="FatiarPorCasoAttribute"/>: elas são grandes demais
/// para caber numa fatia só, e cada CASO (cada tela) vai para a sua.
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

    /// <summary>A fatia (1..n) de uma chave. FNV-1a: estável entre processos e máquinas,
    /// ao contrário de string.GetHashCode, que muda a cada execução.</summary>
    public static int FatiaDe(string chave, int total)
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
