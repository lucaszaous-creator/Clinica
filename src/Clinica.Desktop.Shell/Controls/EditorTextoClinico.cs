using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Automation;
using Clinica.Domain;

namespace Clinica.Desktop.Controls;

/// <summary>Editor de texto clínico. Persiste somente texto, negrito e itálico.</summary>
public sealed class EditorTextoClinico : UserControl
{
    public static readonly DependencyProperty TextoProperty = DependencyProperty.Register(nameof(Texto),typeof(string),typeof(EditorTextoClinico),new FrameworkPropertyMetadata(null,FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,Alterado));
    public static readonly DependencyProperty FormatoProperty = DependencyProperty.Register(nameof(Formato),typeof(string),typeof(EditorTextoClinico),new FrameworkPropertyMetadata(null,FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,Alterado));
    public string? Texto {get=>(string?)GetValue(TextoProperty);set=>SetValue(TextoProperty,value);}
    public string? Formato {get=>(string?)GetValue(FormatoProperty);set=>SetValue(FormatoProperty,value);}
    public static readonly DependencyProperty ModoFolhaProperty = DependencyProperty.Register(
        nameof(ModoFolha), typeof(bool), typeof(EditorTextoClinico),
        new PropertyMetadata(false, (d, _) => ((EditorTextoClinico)d).AplicarModoFolha()));
    public bool ModoFolha { get => (bool)GetValue(ModoFolhaProperty); set => SetValue(ModoFolhaProperty, value); }
    public static readonly DependencyProperty SomenteLeituraProperty = DependencyProperty.Register(
        nameof(SomenteLeitura), typeof(bool), typeof(EditorTextoClinico),
        new PropertyMetadata(false, (d, _) => ((EditorTextoClinico)d).AplicarModoFolha()));
    public bool SomenteLeitura { get => (bool)GetValue(SomenteLeituraProperty); set => SetValue(SomenteLeituraProperty, value); }
    private readonly List<Button> botoes = [];
    private readonly StackPanel barra = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(4) };
    private Border borda = null!;
    private readonly RichTextBox campo = new() {AcceptsReturn=true,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,MinHeight=85,Padding=new Thickness(10),BorderThickness=new Thickness(0)};
    private bool escrevendo, agendado;
    public EditorTextoClinico()
    {
        var painel=new DockPanel();
        foreach(var (nome,comando) in new[]{("Negrito",EditingCommands.ToggleBold),("Itálico",EditingCommands.ToggleItalic)}) {
            var b=new Button {Content=nome,MinWidth=78,MinHeight=32,Margin=new Thickness(2),Focusable=false,Command=comando,CommandTarget=campo,ToolTip=nome=="Negrito"?"Negrito (Ctrl+B)":"Itálico (Ctrl+I)"};
            AutomationProperties.SetName(b,nome);barra.Children.Add(b);botoes.Add(b);
        }
        DockPanel.SetDock(barra,Dock.Top);painel.Children.Add(barra);painel.Children.Add(campo);
        borda=new Border {BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(8),Child=painel};
        borda.SetResourceReference(Border.BorderBrushProperty,"Brush.Borda");
        Content=borda;
        campo.TextChanged+=(_,_)=>Guardar();
        campo.InputBindings.Add(new KeyBinding(EditingCommands.ToggleBold, Key.B, ModifierKeys.Control));
        campo.InputBindings.Add(new KeyBinding(EditingCommands.ToggleItalic, Key.I, ModifierKeys.Control));
        // O contrato guarda texto, negrito e itálico. Listas criadas por atalhos viram
        // blocos List no WPF e não podem escapar do serializador de parágrafos.
        foreach (var comando in new[]
                 {
                     EditingCommands.ToggleBullets, EditingCommands.ToggleNumbering,
                     EditingCommands.ToggleUnderline, EditingCommands.ToggleSubscript,
                     EditingCommands.ToggleSuperscript, EditingCommands.IncreaseFontSize,
                     EditingCommands.DecreaseFontSize, EditingCommands.AlignCenter,
                     EditingCommands.AlignRight, EditingCommands.AlignJustify,
                     EditingCommands.IncreaseIndentation, EditingCommands.DecreaseIndentation
                 })
            campo.CommandBindings.Add(new CommandBinding(comando,
                (_, e) => e.Handled = true,
                (_, e) => { e.CanExecute = false; e.Handled = true; }));
        DataObject.AddPastingHandler(campo,(_,e)=> {
            if(e.DataObject.GetDataPresent(DataFormats.UnicodeText)) {
                var texto=e.DataObject.GetData(DataFormats.UnicodeText) as string??"";
                e.DataObject=new DataObject(DataFormats.UnicodeText,texto);
                e.FormatToApply=DataFormats.UnicodeText;
            } else e.CancelCommand();
        });
        Loaded+=(_,_)=> {AutomationProperties.SetName(campo,AutomationProperties.GetName(this));AplicarModoFolha();Renderizar();};
    }

    /// <summary>Devolve o cursor ao texto, inclusive depois de fechar o histórico.</summary>
    public void FocarTexto()
    {
        campo.Focus();
        FocusManager.SetFocusedElement(FocusManager.GetFocusScope(campo), campo);
    }
    private void AplicarModoFolha()
    {
        if (borda is null) return;
        borda.BorderThickness = new Thickness(SomenteLeitura || ModoFolha ? 0 : 1);
        campo.IsReadOnly = SomenteLeitura;
        campo.IsReadOnlyCaretVisible = false;
        campo.Padding = new Thickness(SomenteLeitura ? 0 : ModoFolha ? 16 : 10);
        campo.MinHeight = SomenteLeitura ? 0 : 85;
        campo.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        campo.Background = System.Windows.Media.Brushes.Transparent;
        barra.Visibility = SomenteLeitura ? Visibility.Collapsed : Visibility.Visible;
        for (var i = 0; i < botoes.Count; i++)
        {
            var b = botoes[i];
            b.Content = ModoFolha ? (i == 0 ? "N" : "I") : (i == 0 ? "Negrito" : "Itálico");
            b.MinWidth = ModoFolha ? 30 : 78; b.MinHeight = ModoFolha ? 28 : 32;
            if (ModoFolha)
            {
                b.SetResourceReference(StyleProperty, "BotaoSecundario");
                b.Width = 30; b.Height = 28; b.Padding = new Thickness(2);
                b.BorderThickness = new Thickness(0); b.Background = System.Windows.Media.Brushes.Transparent;
                b.SetResourceReference(ForegroundProperty, "Brush.Texto.Secundario");
                b.FontWeight = i == 0 ? FontWeights.Bold : FontWeights.Normal;
                b.FontStyle = i == 0 ? FontStyles.Normal : FontStyles.Italic;
            }
            else
            {
                foreach (var property in new[] { StyleProperty, WidthProperty, HeightProperty, PaddingProperty, BorderThicknessProperty, BackgroundProperty, ForegroundProperty, FontWeightProperty, FontStyleProperty })
                    b.ClearValue(property);
            }
        }
        campo.Document.LineHeight = ModoFolha ? 24 : double.NaN;
    }
    private static void Alterado(DependencyObject d,DependencyPropertyChangedEventArgs e)
    {
        var editor=(EditorTextoClinico)d;
        if(editor.escrevendo||editor.agendado)return;
        editor.agendado=true;
        editor.Dispatcher.BeginInvoke(new Action(()=>{editor.agendado=false;editor.Renderizar();}));
    }
    private void Renderizar()
    {
        if(escrevendo)return;
        escrevendo=true;
        try {
            var p=new Paragraph {Margin=new Thickness(0)};
            foreach(var t in TextoFormatado.Ler(Texto,Formato))p.Inlines.Add(new Run(t.Texto){FontWeight=t.Negrito?FontWeights.Bold:FontWeights.Normal,FontStyle=t.Italico?FontStyles.Italic:FontStyles.Normal});
            campo.Document=new FlowDocument(p){PagePadding=new Thickness(0),FontFamily=FontFamily,FontSize=FontSize,LineHeight=ModoFolha?24:double.NaN};
        } finally {escrevendo=false;}
    }
    private static IEnumerable<TrechoTexto> Trechos(InlineCollection inlines)
    {
        foreach(var inline in inlines) {
            if(inline is Run r)yield return new(r.Text,r.FontWeight>=FontWeights.Bold,r.FontStyle==FontStyles.Italic);
            else if(inline is LineBreak)yield return new("\n");
            else if(inline is Span s)foreach(var t in Trechos(s.Inlines))yield return t;
        }
    }
    private void Guardar()
    {
        if(escrevendo || SomenteLeitura)return;
        var trechos=new List<TrechoTexto>();
        var primeiro = true;
        foreach(var p in campo.Document.Blocks.OfType<Paragraph>()) {
            if(!primeiro)trechos.Add(new("\n"));
            primeiro = false;
            trechos.AddRange(Trechos(p.Inlines));
            if(p.Inlines.Count==0)trechos.Add(new(""));
        }
        escrevendo=true;
        try {SetCurrentValue(TextoProperty,string.Concat(trechos.Select(t=>t.Texto)));SetCurrentValue(FormatoProperty,TextoFormatado.Guardar(trechos)??"");}
        finally {escrevendo=false;}
    }
}
