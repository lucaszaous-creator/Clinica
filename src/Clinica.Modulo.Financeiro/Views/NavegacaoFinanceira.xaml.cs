using System.Windows;
using System.Windows.Controls;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Modulos;

namespace Clinica.Financeiro.Views;

/// <summary>Navegação compacta exclusiva do executável Financeiro, com rótulos opcionais.</summary>
public partial class NavegacaoFinanceira : UserControl
{
    public static readonly DependencyProperty MostrarNomesProperty = DependencyProperty.Register(
        nameof(MostrarNomes), typeof(bool), typeof(NavegacaoFinanceira), new PropertyMetadata(false));
    public bool MostrarNomes { get => (bool)GetValue(MostrarNomesProperty); set => SetValue(MostrarNomesProperty,value); }
    private bool _inicializada;
    public NavegacaoFinanceira()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (_inicializada || DataContext is not ShellViewModel vm) return;
            _inicializada = true;
            var grupo = vm.Grupos.FirstOrDefault(g=>g.Grupo==GrupoSidebar.Financeiro);
            if(grupo is null) return;
            vm.GrupoSelecionado=grupo;
            var inicio=grupo.Itens.FirstOrDefault();
            if(inicio is not null && vm.NavegarCommand.CanExecute(inicio))vm.NavegarCommand.Execute(inicio);
        };
    }
    private void EscolherGrupo(object sender, RoutedEventArgs e)
    {
        if(sender is Button {DataContext:GrupoMenuModulo grupo} && DataContext is ShellViewModel vm)
            vm.GrupoSelecionado=grupo;
    }
    private void AlternarRotulos(object sender, RoutedEventArgs e)
    {
        MostrarNomes=!MostrarNomes;
        ColunaSecoes.Width=new GridLength(MostrarNomes?208:64);
        var descricao=MostrarNomes?"Recolher nomes das seções":"Mostrar nomes das seções";
        AlternarNomes.ToolTip=descricao;
        System.Windows.Automation.AutomationProperties.SetName(AlternarNomes,descricao);
    }
}
