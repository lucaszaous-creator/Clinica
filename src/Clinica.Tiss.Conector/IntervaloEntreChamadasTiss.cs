using System.Collections.Concurrent;

namespace Clinica.Tiss.Conector;

/// <summary>
/// O INTERVALO MÍNIMO entre duas idas à MESMA operadora (docs/tiss-conector.md, "Limite de chamadas").
/// A operadora é reconhecida pelo servidor do web service (o host do endereço): convênios diferentes
/// atendidos pelo mesmo servidor contam juntos, e operadoras diferentes não esperam uma pela outra.
///
/// A conta vale do FIM de uma ida ao começo da próxima: uma operadora lenta ganha mais folga, nunca
/// menos. Vale para toda transação — elegibilidade, envio, situação do protocolo e cancelamento — e
/// não muda nada na regra do envio (a espera acontece ANTES do carimbo; nada é repetido por ela).
///
/// Mora em memória e é uma por processo: o conector é um só por desenho, e ao reiniciar a primeira
/// ida sai sem esperar (um reinício leva mais que o intervalo).
/// </summary>
public sealed class IntervaloEntreChamadasTiss(TimeSpan intervalo, Func<DateTime> agora, Func<TimeSpan, CancellationToken, Task> esperar)
{
    private readonly ConcurrentDictionary<string, DateTime> _ultimaIda = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>O de produção: relógio do sistema e <see cref="Task.Delay(TimeSpan, CancellationToken)"/>.</summary>
    public IntervaloEntreChamadasTiss(TimeSpan intervalo) : this(intervalo, () => DateTime.UtcNow, Task.Delay) { }

    public TimeSpan Intervalo => intervalo;

    /// <summary>Espera o que falta para a próxima ida a <paramref name="operadora"/> e devolve quanto esperou.</summary>
    public async Task<TimeSpan> AguardarVezAsync(string operadora, CancellationToken ct = default)
    {
        if (intervalo <= TimeSpan.Zero || !_ultimaIda.TryGetValue(Chave(operadora), out var ultima)) return TimeSpan.Zero;
        var falta = ultima + intervalo - agora();
        if (falta <= TimeSpan.Zero) return TimeSpan.Zero;
        await esperar(falta, ct);
        return falta;
    }

    /// <summary>A ida a <paramref name="operadora"/> terminou (com resposta, sem resposta ou com erro).</summary>
    public void RegistrarIda(string operadora) => _ultimaIda[Chave(operadora)] = agora();

    private static string Chave(string operadora) => (operadora ?? string.Empty).Trim();
}
