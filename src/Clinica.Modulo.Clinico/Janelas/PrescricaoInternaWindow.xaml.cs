using System.Windows;
using System.Windows.Controls;
using Clinica.Desktop.Controls;
using Clinica.Domain.Entities;
using Clinica.Clinico.ViewModels;

namespace Clinica.Clinico.Janelas;

/// <summary>
/// Escrever e assinar a prescrição de infusão.
///
/// Só fecha sozinha quando a folha é ASSINADA: salvar rascunho deixa a janela aberta, com
/// a mensagem inline, porque quem salvou rascunho quase sempre vai continuar escrevendo.
/// </summary>
public partial class PrescricaoInternaWindow : Window
{
    private readonly PrescricaoInternaEdicaoViewModel _vm;

    public PrescricaoInternaWindow(PrescricaoInternaEdicaoViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;

        vm.Fechar = () => DialogResult = true;
        Closed += (_, _) => vm.Fechar = null;
    }

    /// <summary>A folha foi assinada nesta janela — quem abriu recarrega a lista.</summary>
    public bool Assinou => _vm.Assinou;

    private void Mais_Click(object sender, RoutedEventArgs e)
    {
        if(sender is not Button {DataContext:GrupoInfusaoEdicao grupo} botao)return;
        var menu=new ContextMenu();var remover=new MenuItem {Header="Remover infusão"};
        remover.Click+=(_,_)=>_vm.RemoverInfusaoCommand.Execute(grupo);menu.Items.Add(remover);
        menu.PlacementTarget=botao;menu.IsOpen=true;
    }
    private void Modelos_Click(object sender, RoutedEventArgs e)
    {
        if(sender is not Button {DataContext:GrupoInfusaoEdicao grupo} botao)return;
        var menu=new ContextMenu();var usar=new MenuItem {Header="Usar modelo salvo"};var salvar=new MenuItem {Header="Salvar infusão como modelo"};
        usar.Click+=(_,_)=>AbrirModelos(grupo,false);salvar.Click+=(_,_)=>AbrirModelos(grupo,true);
        menu.Items.Add(usar);menu.Items.Add(salvar);menu.PlacementTarget=botao;menu.IsOpen=true;
    }
    private void AbrirModelos(GrupoInfusaoEdicao grupo,bool salvar)
    {
        var painel=new StackPanel {Margin=new Thickness(24)};
        var janela=new Window {Title=salvar?"Salvar infusão como modelo":"Usar modelo salvo",Width=580,SizeToContent=SizeToContent.Height,MaxHeight=650,Owner=this,WindowStartupLocation=WindowStartupLocation.CenterOwner,Content=painel};
        painel.Children.Add(new TextBlock {Text=janela.Title,FontSize=20,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,14)});
        var mensagem=new TextBlock {TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,10,0,0)};
        if(salvar)
        {
            painel.Children.Add(new TextBlock {Text="Nome do modelo"});var nome=new TextBox {MaxLength=100,Margin=new Thickness(0,6,0,12)};painel.Children.Add(nome);
            painel.Children.Add(new TextBlock {Text="Guarda o preparo e os medicamentos desta infusão, sem dados do paciente ou horário.",TextWrapping=TextWrapping.Wrap});
            var botao=new Button {Content="Salvar modelo",HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,16,0,0)};
            botao.Click+=async (_,_)=>{botao.IsEnabled=false;try{if(await _vm.SalvarModeloAsync(grupo,nome.Text.Trim()))janela.Close();else mensagem.Text=_vm.Mensagem;}finally{botao.IsEnabled=true;}};painel.Children.Add(botao);
        }
        else
        {
            var busca=new BuscaModeloClinico {Modelos=_vm.Modelos};painel.Children.Add(busca);
            var botao=new Button {Content="Usar e revisar",HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,16,0,0)};
            botao.Click+=(_,_)=>{if(busca.Selecionado is not ModeloDocumento modelo){mensagem.Text="Selecione um modelo.";return;}_vm.AplicarModelo(grupo,modelo);janela.Close();};painel.Children.Add(botao);
        }
        painel.Children.Add(mensagem);janela.ShowDialog();
    }
}
