using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Clinica.Domain.Entities;

namespace Clinica.Desktop.Shell.WebClinica;

public static class FiltrosAgendaWeb
{
    /// <summary>Busca na própria agenda: conserva a tabela, a grade e todas as ações.</summary>
    public static PainelClinicoWeb Montar(Grid destino, Func<object> estado, Func<JsonElement, Task> executar)
    {
        var original = new Grid();
        foreach (var filho in destino.Children.Cast<UIElement>().ToArray())
        { destino.Children.Remove(filho); original.Children.Add(filho); }
        var painel = new PainelClinicoWeb("filtros-agenda", estado, executar,
            () => SessaoUsuario.Atual.Exigir(Permissao.VerAgenda, "consultar a agenda")) { Height = 180 };
        destino.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        destino.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(original, 1);
        destino.Children.Add(painel);
        destino.Children.Add(original);
        return painel;
    }
}
