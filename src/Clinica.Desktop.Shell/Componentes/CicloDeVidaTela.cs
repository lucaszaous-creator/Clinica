using System.Windows;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>Libera também as abas materializadas fora da árvore visual atual.</summary>
public static class CicloDeVidaTela
{
    /// <summary>
    /// A tela pode voltar do histórico sozinha (sem o item ter <c>PreservarAoNavegar</c>) quando
    /// TUDO o que ela já montou sabe se reler por trás (<see cref="Modulos.IAtualizarAoVoltar"/>).
    /// Na tela com abas, contam as abas já abertas: uma aba sem releitura voltaria com dado velho.
    /// </summary>
    public static bool SabeAtualizarAoVoltar(object tela) => tela switch
    {
        TelaComAbas abas => abas.ConteudosAbertos.Any() && abas.ConteudosAbertos.All(SabeAtualizarAoVoltar),
        FrameworkElement { DataContext: Modulos.IAtualizarAoVoltar } => true,
        _ => false
    };

    /// <summary>Pede a releitura de fundo do que está VISÍVEL na tela que acabou de voltar.</summary>
    public static void AtualizarAoVoltar(object tela)
    {
        try
        {
            switch (tela)
            {
                case TelaComAbas abas when abas.ConteudoVisivel is { } visivel: AtualizarAoVoltar(visivel); break;
                case FrameworkElement { DataContext: Modulos.IAtualizarAoVoltar a }: a.AtualizarAoVoltar(); break;
            }
        }
        catch (Exception ex) { Configuracao.LogSuite.Registrar("Releitura ao voltar à tela", ex); }
    }

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
