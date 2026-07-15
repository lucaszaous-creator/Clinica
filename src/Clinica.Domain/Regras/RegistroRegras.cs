namespace Clinica.Domain.Regras;

/// <summary>Seleciona a regra de faturamento correta para cada convênio.</summary>
public sealed class RegistroRegras
{
    private readonly Dictionary<Convenio, IRegraConvenio> _regras;

    public RegistroRegras(IEnumerable<IRegraConvenio>? regras = null)
    {
        var lista = regras?.ToList() ?? DefaultRegras();
        _regras = lista.ToDictionary(r => r.Convenio);
    }

    public IRegraConvenio Para(Convenio convenio)
    {
        if (_regras.TryGetValue(convenio, out var regra))
            return regra;
        throw new InvalidOperationException($"Nenhuma regra cadastrada para o convênio {convenio}.");
    }

    public static List<IRegraConvenio> DefaultRegras() => new()
    {
        new RegraUnimedPadrao(),
        new RegraUnimedIntercambio(),
        new RegraAmil(),
        new RegraPetrobras()
    };
}
