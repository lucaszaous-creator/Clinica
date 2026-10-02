using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Clinica.Domain;

namespace Clinica.Desktop.Controls;

public sealed class BuscaMedicamento : UserControl
{
    public static readonly DependencyProperty CatalogoProperty=DependencyProperty.Register(nameof(Catalogo),typeof(IEnumerable),typeof(BuscaMedicamento),new PropertyMetadata(null));
    public static readonly DependencyProperty TextoProperty=DependencyProperty.Register(nameof(Texto),typeof(string),typeof(BuscaMedicamento),new FrameworkPropertyMetadata("",FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,TextoAlterado));
    public IEnumerable? Catalogo {get=>(IEnumerable?)GetValue(CatalogoProperty);set=>SetValue(CatalogoProperty,value);}
    public string Texto {get=>(string)GetValue(TextoProperty);set=>SetValue(TextoProperty,value);}
    private readonly TextBox busca=new() { MinHeight=38,MaxLength=20000,TextWrapping=TextWrapping.Wrap,AcceptsReturn=true };
    private readonly ListBox lista=new() { DisplayMemberPath="Rotulo",MaxHeight=160,Visibility=Visibility.Collapsed };
    private readonly TextBlock aviso=new() { Visibility=Visibility.Collapsed,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,4,0,0) };
    private bool atualizando;
    public BuscaMedicamento()
    {
        System.Windows.Automation.AutomationProperties.SetName(busca,"Medicamento ou prescrição livre");
        Ajudantes.SetPlaceholder(busca,"Buscar medicamento…");
        busca.ToolTip="Digite 2 letras. Selecione uma sugestão ou escreva livremente. Dose e preparo são informados pelo prescritor.";
        Content=new StackPanel { Children={busca,lista,aviso} };
        busca.TextChanged+=(_,_)=>{if(atualizando)return;SetCurrentValue(TextoProperty,busca.Text);Filtrar();};
        busca.GotKeyboardFocus+=(_,_)=>Filtrar();
        busca.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Down&&lista.Items.Count>0){lista.SelectedIndex=0;lista.Focus();(lista.ItemContainerGenerator.ContainerFromIndex(0) as ListBoxItem)?.Focus();e.Handled=true;}else if(e.Key==Key.Escape){Fechar();e.Handled=true;}};
        lista.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Enter){Escolher();e.Handled=true;}else if(e.Key==Key.Escape){busca.Focus();Fechar();e.Handled=true;}};
        lista.PreviewMouseLeftButtonUp+=(_,_)=>Escolher();
        LostKeyboardFocus+=(_,_)=>Dispatcher.BeginInvoke(new Action(()=>{if(!IsKeyboardFocusWithin)Fechar();}));
    }
    private void Fechar(){lista.Visibility=Visibility.Collapsed;aviso.Visibility=Visibility.Collapsed;}
    private void Filtrar()
    {
        if(!busca.IsKeyboardFocusWithin || busca.Text.Trim().Length<2){Fechar();return;}
        var encontrados=BuscaMedicamentos.Buscar(Catalogo?.Cast<MedicamentoSugerido>()??[],busca.Text);
        lista.ItemsSource=encontrados;lista.Visibility=encontrados.Count>0?Visibility.Visible:Visibility.Collapsed;
        aviso.Text="Sem correspondência. Você pode escrever livremente ou cadastrar em Prescrições › Medicamentos.";
        aviso.Visibility=encontrados.Count==0?Visibility.Visible:Visibility.Collapsed;
    }
    private void Escolher(){if(lista.SelectedItem is not MedicamentoSugerido m)return;SetCurrentValue(TextoProperty,m.Texto);busca.Focus();busca.CaretIndex=busca.Text.Length;Fechar();}
    private static void TextoAlterado(DependencyObject d,DependencyPropertyChangedEventArgs e){var c=(BuscaMedicamento)d;if(c.busca.Text==(string?)e.NewValue)return;c.atualizando=true;c.busca.Text=(string?)e.NewValue??"";c.atualizando=false;}
}
