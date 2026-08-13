using System.Windows;
using Clinica.Gerente.ViewModels;

namespace Clinica.Gerente.Views;

/// <summary>
/// Onde a clínica escreve o termo que o paciente assina. Ver
/// <see cref="ModelosTermoViewModel"/> para as decisões.
/// </summary>
public partial class ModelosTermoWindow : Window
{
    public ModelosTermoWindow(ModelosTermoViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
