using System.Windows;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>Libera também as abas materializadas fora da árvore visual atual.</summary>
public static class CicloDeVidaTela
{
    public static bool PodeReutilizar(object tela)
    {
        if (tela is WebClinica.PainelClinicoWeb painel) return painel.PodeReutilizar;
        return tela is not DependencyObject elemento
            || LogicalTreeHelper.GetChildren(elemento).Cast<object>().All(PodeReutilizar);
    }

    public static void Descartar(object tela)
    {
        if (tela is IDisposable descartavel) { descartavel.Dispose(); return; }
        if (tela is DependencyObject elemento)
            foreach (var filho in LogicalTreeHelper.GetChildren(elemento).Cast<object>().ToArray())
                Descartar(filho);
    }
}
