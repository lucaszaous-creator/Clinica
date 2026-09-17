using System.Windows;
using Clinica.Recepcao.ViewModels;

namespace Clinica.Recepcao.Janelas;

public partial class HorariosProfissionalWindow : Window
{
    public HorariosProfissionalWindow(HorariosProfissionalViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
