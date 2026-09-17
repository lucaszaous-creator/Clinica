using System.Windows;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>Consulta contextual: o editor de origem permanece vivo com seu rascunho.</summary>
public partial class ConsultaContextualWindow : Window
{
    public ConsultaContextualWindow(string titulo, FrameworkElement conteudo, string voltar)
    {
        InitializeComponent();
        Title = titulo;
        Area.Content = conteudo;
        Voltar.Content = voltar;
        Owner = JanelaDona.Atual();
    }
}
