using System.Text.Json;

namespace Clinica.Desktop.Shell.Treinamento;

public sealed record CapituloTreinamento(double Inicio, string Titulo, string Texto);
public sealed record AulaTreinamento
{
    public string Id { get; init; } = "";
    public string Titulo { get; init; } = "";
    public string Categoria { get; init; } = "";
    public string Descricao { get; init; } = "";
    public string[] Telas { get; init; } = [];
    public string[] Funcoes { get; init; } = [];
    public CapituloTreinamento[] Capitulos { get; init; } = [];
    public double Duracao { get; init; }
    public string Sha256 { get; init; } = "";
    public string DuracaoTexto => TimeSpan.FromSeconds(Duracao).ToString(@"mm\:ss");
}

public static class CatalogoTreinamento
{
    public static IReadOnlyList<AulaTreinamento> Ler()
    {
        using var stream=typeof(CatalogoTreinamento).Assembly.GetManifestResourceStream("Clinica.Desktop.Shell.Treinamento.catalogo.json");
        if(stream is null) return [];
        return JsonSerializer.Deserialize<AulaTreinamento[]>(stream,new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??[];
    }

    public static IReadOnlyList<AulaTreinamento> Filtrar(IEnumerable<AulaTreinamento> aulas, IEnumerable<string> telasPermitidas)
    {
        var telas=telasPermitidas.ToHashSet(StringComparer.Ordinal);
        return aulas.Where(a=>a.Telas.Length>0 && a.Telas.All(telas.Contains))
            .DistinctBy(a=>a.Id).OrderBy(a=>a.Categoria).ThenBy(a=>a.Titulo).ToArray();
    }
}

public sealed record ProgressoAula(double Posicao=0,bool Concluida=false);
