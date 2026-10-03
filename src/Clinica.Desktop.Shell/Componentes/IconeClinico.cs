using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>Ícones vetoriais do atendimento aprovado, com grade e traço uniformes.</summary>
public sealed class IconeClinico : Control
{
    public static readonly DependencyProperty NomeProperty = DependencyProperty.Register(nameof(Nome), typeof(string), typeof(IconeClinico), new FrameworkPropertyMetadata("record", FrameworkPropertyMetadataOptions.AffectsRender));
    public string Nome { get => (string)GetValue(NomeProperty); set => SetValue(NomeProperty, value); }
    public IconeClinico() { Width = 20; Height = 20; IsHitTestVisible = false; Focusable = false; }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var name=Nome;
        var data=name switch{
            "record"=>"M 9,4 L 6,4 Q 4,4 4,6 L 4,20 Q 4,22 6,22 L 18,22 Q 20,22 20,20 L 20,6 Q 20,4 18,4 L 15,4 M 9,2 L 15,2 15,6 9,6 Z M 8,11 L 16,11 M 8,15 L 16,15 M 8,18 L 13,18",
            "rx"=>"M 9,4 L 4,9 C 0,13 6,19 10,15 L 15,10 C 19,6 13,0 9,4 Z M 6.5,6.5 L 12.5,12.5 M 16,14 L 22,14 M 19,11 L 19,17",
            "infusion"=>"M 12,2 L 12,5 M 8,5 L 16,5 Q 18,5 18,7 L 18,16 Q 18,18 16,18 L 8,18 Q 6,18 6,16 L 6,7 Q 6,5 8,5 Z M 6,12 L 18,12 M 12,18 L 12,20 Q 12,22 15,22 L 17,22 M 12,8 L 12,10 M 11,9 L 13,9",
            "history"=>"M 4,7 A 9,9 0 1 1 3,15 M 3,3 L 3,8 8,8 M 12,7 L 12,12 16,14",
            "folder"=>"M 3,10 L 3,5 Q 3,3 5,3 L 9,3 12,6 19,6 Q 21,6 21,8 L 21,10 M 3,10 L 22,10 19,20 2,20 Z",
            "exam"=>"M 9,2 L 15,2 M 10,2 L 10,9 4,19 Q 3,22 6,22 L 18,22 Q 21,22 20,19 L 14,9 14,2 M 7,15 L 17,15 M 10,18 L 10.1,18 M 14,19 L 14.1,19",
            "patient"=>"M 16,7 A 4,4 0 1 1 8,7 A 4,4 0 1 1 16,7 M 4,21 L 4,19 C 4,12 20,12 20,19 L 20,21",
            "chart"=>"M 3,3 L 3,21 22,21 M 7,15 L 11,11 15,13 21,5 M 16,5 L 21,5 21,10",
            "document"=>"M 13,2 L 5,2 Q 3,2 3,4 L 3,20 Q 3,22 5,22 L 19,22 Q 21,22 21,20 L 21,10 13,2 Z M 13,2 L 13,10 21,10 M 8,15 L 11,18 16,13",
            "back"=>"M 20,12 L 4,12 M 10,6 L 4,12 10,18",
            "save"=>"M 21,12 A 9,9 0 1 1 12,3 M 8,11 L 12,15 21,5",
            "print"=>"M 7,8 L 7,2 17,2 17,8 M 7,18 L 4,18 Q 2,18 2,16 L 2,10 Q 2,8 4,8 L 20,8 Q 22,8 22,10 L 22,16 Q 22,18 20,18 L 17,18 M 7,14 L 17,14 17,22 7,22 Z M 18,11 L 19,11",
            "close"=>"M 6,6 L 18,18 M 6,18 L 18,6",
            "model"=>"M 8,2 L 20,2 Q 22,2 22,4 L 22,16 M 4,6 L 16,6 Q 18,6 18,8 L 18,20 Q 18,22 16,22 L 4,22 Q 2,22 2,20 L 2,8 Q 2,6 4,6 Z M 6,11 L 14,11 M 6,15 L 14,15 M 6,18 L 11,18",
            "body"=>"M 14.5,4 A 2.5,2.5 0 1 1 9.5,4 A 2.5,2.5 0 1 1 14.5,4 M 4,9 L 8,8 16,8 20,9 M 8,8 L 9,14 7,22 M 16,8 L 15,14 17,22 M 9,14 L 15,14",
            "more"=>"M 4,12 L 4.1,12 M 12,12 L 12.1,12 M 20,12 L 20.1,12",
            "check"=>"M 4,12 L 9,17 20,6",
            _=>"M 12,4 L 12,20 M 4,12 L 20,12"};
        var pen = new Pen(Foreground, name == "more" ? 3 : 1.8) { StartLineCap=PenLineCap.Round, EndLineCap=PenLineCap.Round, LineJoin=PenLineJoin.Round };
        dc.PushTransform(new ScaleTransform(ActualWidth / 24, ActualHeight / 24));
        dc.DrawGeometry(null, pen, Geometry.Parse(data));
        dc.Pop();
    }
}

/// <summary>Botão do design system com ícone vetorial e rótulo acessível.</summary>
public sealed class BotaoClinico : Button
{
    public static readonly DependencyProperty TextoProperty = DependencyProperty.Register(nameof(Texto), typeof(string), typeof(BotaoClinico), new PropertyMetadata(""));
    public static readonly DependencyProperty IconeProperty = DependencyProperty.Register(nameof(Icone), typeof(string), typeof(BotaoClinico), new PropertyMetadata("record"));
    public string Texto { get => (string)GetValue(TextoProperty); set => SetValue(TextoProperty,value); }
    public string Icone { get => (string)GetValue(IconeProperty); set => SetValue(IconeProperty,value); }
    public BotaoClinico()
    {
        SetResourceReference(StyleProperty,"BotaoSecundario");
        MinHeight=38; MinWidth=0; Padding=new Thickness(14,9,14,9); FontSize=13; VerticalAlignment=VerticalAlignment.Center;
        var panel=new Grid(); panel.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto }); panel.ColumnDefinitions.Add(new ColumnDefinition());
        var icon=new IconeClinico { Margin=new Thickness(0,0,8,0), VerticalAlignment=VerticalAlignment.Center };
        icon.SetBinding(IconeClinico.NomeProperty,new Binding(nameof(Icone)){Source=this});
        icon.SetBinding(ForegroundProperty,new Binding(nameof(Foreground)){Source=this});
        var label=new TextBlock { VerticalAlignment=VerticalAlignment.Center, TextWrapping=TextWrapping.Wrap };
        Grid.SetColumn(label,1);
        label.SetBinding(TextBlock.TextProperty,new Binding(nameof(Texto)){Source=this});
        panel.Children.Add(icon);panel.Children.Add(label);Content=panel;
        SetBinding(System.Windows.Automation.AutomationProperties.NameProperty,new Binding(nameof(Texto)){Source=this});
    }
}
