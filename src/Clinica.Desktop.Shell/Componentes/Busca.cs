namespace Clinica.Desktop.Shell.Componentes;

/// <summary>Repassa para <see cref="Clinica.Application.Formatacao.Busca"/>, onde a regra mora desde out/2026.</summary>
public static class Busca
{
    public static bool Casa(string? texto, string? termo) => global::Clinica.Application.Formatacao.Busca.Casa(texto, termo);
    public static string Simplificar(string texto) => global::Clinica.Application.Formatacao.Busca.Simplificar(texto);
}
