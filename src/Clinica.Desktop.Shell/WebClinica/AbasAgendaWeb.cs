using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Clinica.Domain.Entities;

namespace Clinica.Desktop.Shell.WebClinica;

public static class AbasAgendaWeb
{
    /// <summary>A lista pesquisável complementa a grade, que conserva bloqueios, vagas e ações existentes.</summary>
    public static PainelClinicoWeb Montar(Grid destino, string rotuloOriginal, Func<object> estado, Func<JsonElement, Task> executar)
    {
        var original = new Grid();
        foreach (var filho in destino.Children.Cast<UIElement>().ToArray())
        { destino.Children.Remove(filho); original.Children.Add(filho); }
        var painel = new PainelClinicoWeb("agenda", estado, executar,
            () => SessaoUsuario.Atual.Exigir(Permissao.VerAgenda, "consultar a agenda"));
        var abas = new TabControl();
        abas.Items.Add(new TabItem { Header = "Lista com busca", Content = painel });
        abas.Items.Add(new TabItem { Header = rotuloOriginal, Content = original });
        destino.Children.Add(abas);
        return painel;
    }
}
