using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Automation;
using Clinica.Domain.Entities;

namespace Clinica.Desktop.Controls;

/// <summary>Busca em todos os nomes, com sugestões e navegação por teclado.</summary>
public sealed class BuscaModeloClinico : UserControl
{
    public static readonly DependencyProperty ModelosProperty=DependencyProperty.Register(nameof(Modelos),typeof(IEnumerable),typeof(BuscaModeloClinico),new PropertyMetadata(null,AlterouModelos));
    public static readonly DependencyProperty SelecionadoProperty=DependencyProperty.Register(nameof(Selecionado),typeof(ModeloDocumento),typeof(BuscaModeloClinico),new FrameworkPropertyMetadata(null,FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,Selecionou));
    public IEnumerable? Modelos {get=>(IEnumerable?)GetValue(ModelosProperty);set=>SetValue(ModelosProperty,value);}
    public ModeloDocumento? Selecionado {get=>(ModeloDocumento?)GetValue(SelecionadoProperty);set=>SetValue(SelecionadoProperty,value);}
    private readonly TextBox busca=new(){MinHeight=38};
    private readonly ListBox lista=new(){DisplayMemberPath="Nome",MaxHeight=160,Visibility=Visibility.Collapsed};
    private readonly TextBlock resumo=new(){TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,0,4)};
    private bool atualizando;
    public BuscaModeloClinico()
    {
        AutomationProperties.SetName(busca,"Buscar modelo pelo nome");busca.ToolTip="Digite parte do nome. Use as setas e Enter para escolher.";
        Ajudantes.SetPlaceholder(busca,"Digite parte do nome do modelo…");
        AutomationProperties.SetName(lista,"Sugestões de modelos");AutomationProperties.SetLiveSetting(resumo,AutomationLiveSetting.Polite);
        Content=new StackPanel {Children={busca,lista,resumo}};
        busca.TextChanged+=(_,_)=>{if(!atualizando)Filtrar();};
        busca.GotKeyboardFocus+=(_,_)=>Filtrar();
        busca.PreviewKeyDown+=(_,e)=> {
            if(e.Key==Key.Down&&lista.Items.Count>0){lista.SelectedIndex=0;lista.Focus();((ListBoxItem)lista.ItemContainerGenerator.ContainerFromIndex(0))?.Focus();e.Handled=true;}
            if(e.Key==Key.Escape){lista.Visibility=Visibility.Collapsed;e.Handled=true;}
        };
        lista.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Enter){Escolher();e.Handled=true;}else if(e.Key==Key.Escape){lista.Visibility=Visibility.Collapsed;busca.Focus();e.Handled=true;}};
        lista.PreviewMouseLeftButtonUp+=(_,_)=>Escolher();
        LostKeyboardFocus+=(_,_)=>Dispatcher.BeginInvoke(new Action(()=>{if(!IsKeyboardFocusWithin)lista.Visibility=Visibility.Collapsed;}));
    }
    private static void AlterouModelos(DependencyObject d,DependencyPropertyChangedEventArgs e)
    {
        var b=(BuscaModeloClinico)d;
        if(e.OldValue is INotifyCollectionChanged a)a.CollectionChanged-=b.Mudou;
        if(e.NewValue is INotifyCollectionChanged n)n.CollectionChanged+=b.Mudou;
        b.Filtrar();
    }
    private void Mudou(object? s,NotifyCollectionChangedEventArgs e)=>Filtrar();
    private void Filtrar()
    {
        var todos=(Modelos?.Cast<ModeloDocumento>()??[]).Where(m=>CultureInfo.GetCultureInfo("pt-BR").CompareInfo.IndexOf(m.Nome,busca.Text.Trim(),CompareOptions.IgnoreCase|CompareOptions.IgnoreNonSpace)>=0).ToList();
        lista.ItemsSource=todos.Take(20);lista.Visibility=busca.IsKeyboardFocusWithin?Visibility.Visible:Visibility.Collapsed;
        resumo.Text=todos.Count==0?"Nenhum modelo encontrado. Escreva o documento e salve um novo modelo.":$"{todos.Count} modelo(s) encontrado(s)."+(todos.Count>20?" Continue digitando para refinar.":"");
    }
    private void Escolher(){if(lista.SelectedItem is ModeloDocumento m){SetCurrentValue(SelecionadoProperty,m);lista.Visibility=Visibility.Collapsed;}}
    private static void Selecionou(DependencyObject d,DependencyPropertyChangedEventArgs e)
    {
        var b=(BuscaModeloClinico)d;b.atualizando=true;
        b.busca.Text=(e.NewValue as ModeloDocumento)?.Nome??"";b.atualizando=false;
        b.resumo.Text=e.NewValue is ModeloDocumento m? $"Selecionado: {m.Nome}. Revise a prévia antes de usar.":"Digite para buscar um modelo.";
    }
}
