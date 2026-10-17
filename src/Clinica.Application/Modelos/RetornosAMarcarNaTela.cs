namespace Clinica.Application.Modelos;

/// <summary>A contagem dos chips de "Retornos a marcar" — sempre da fila INTEIRA.</summary>
public sealed record ContagemRetornos(int Total, int Atrasados, int EstaSemana, int MaisAdiante, int JaMarcados);

/// <summary>
/// As regras da tela "Retornos a marcar" (Blazor, out/2026) que o ViewModel WPF decidia:
/// o recorte do chip, as contagens, o resumo e a DATA com que o Marcar abre. Subiram para cá
/// pela regra da casa — o que a tela afirma mora onde o <c>dotnet test</c> alcança.
/// </summary>
public static class RetornosAMarcarNaTela
{
    /// <summary>
    /// O recorte da tabela. "Todos" (nulo) é a FILA DE TRABALHO: o já marcado só aparece no
    /// chip dele — misturá-lo faria a lista mostrar como pendente o que o balcão já resolveu.
    /// </summary>
    public static IReadOnlyList<RetornoAMarcar> Recortar(IReadOnlyList<RetornoAMarcar> todas, FaixaRetorno? filtro)
        => todas.Where(l => filtro is null ? !l.JaMarcado : l.Faixa == filtro).ToList();

    public static ContagemRetornos Contar(IReadOnlyCollection<RetornoAMarcar> todas) => new(
        todas.Count(l => !l.JaMarcado),
        todas.Count(l => l.Faixa == FaixaRetorno.Atrasado),
        todas.Count(l => l.Faixa == FaixaRetorno.EstaSemana),
        todas.Count(l => l.Faixa == FaixaRetorno.MaisAdiante),
        todas.Count(l => l.JaMarcado));

    public static string Resumo(IReadOnlyCollection<RetornoAMarcar> todas)
    {
        var c = Contar(todas);
        return c.Total == 0
            ? "Ninguém com retorno pedido sem horário marcado."
            : $"{c.Total} retorno(s) a marcar"
              + (c.Atrasados > 0 ? $" · {c.Atrasados} com a data sugerida já passada" : string.Empty);
    }

    /// <summary>
    /// A data com que o Marcar abre: a sugerida — ou HOJE, quando a sugerida já passou.
    /// Pré-preencher uma data no passado faria o formulário abrir recusando ("marcar para trás").
    /// </summary>
    public static DateOnly DiaParaMarcar(RetornoAMarcar linha, DateOnly hoje)
        => linha.RetornoEm < hoje ? hoje : linha.RetornoEm;

    /// <summary>
    /// O motivo do retorno é dado de saúde: sai da LINHA para quem não tem
    /// <c>VerProntuario</c>, e não só da tela — escondê-lo no desenho deixaria o texto na
    /// memória de uma tela que não tem o direito de tê-lo.
    /// </summary>
    public static IReadOnlyList<RetornoAMarcar> SemNotaSeNaoPode(IReadOnlyList<RetornoAMarcar> linhas, bool podeVerNota)
        => podeVerNota ? linhas : linhas.Select(r => r with { Nota = null }).ToList();

    /// <summary>O tom da pílula "para quando": já marcado, atrasado ou no prazo.</summary>
    public static string Tom(RetornoAMarcar l) => l.JaMarcado ? "sucesso" : l.Atrasado ? "aviso" : "info";
}
