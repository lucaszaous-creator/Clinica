using System.Windows;
using Clinica.Clinico.ViewModels;

namespace Clinica.Clinico.Janelas;

/// <summary>
/// A folha aberta na sala de infusão: onde a técnica checa item a item.
///
/// Não fecha sozinha em nada — nem ao checar, nem ao encerrar. Checar é uma sequência
/// (são vários itens na mesma folha) e fechar depois do primeiro obrigaria a reabrir; e
/// depois de encerrar, quem assinou quer VER a folha com as marcas antes de sair.
/// </summary>
public partial class FolhaExecucaoWindow : Window
{
    public FolhaExecucaoWindow(FolhaExecucaoViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
