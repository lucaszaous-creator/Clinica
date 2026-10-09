using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using Clinica.Desktop.Controls;
using Clinica.Domain.Entities;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>Folha de edição com o desenho aprovado do modelo B.</summary>
internal sealed class DocumentoFolha : DockPanel
{
    readonly DocumentoEdicaoViewModel vm;
    readonly Window owner;
    Brush Cor(string key) => (Brush)System.Windows.Application.Current.FindResource(key);
    public DocumentoFolha(DocumentoEdicaoViewModel model, Window janela, ScrollViewer scroll)
    {
        vm = model; owner = janela; DataContext = vm; Margin = new Thickness(36, 24, 36, 24);
        SetResourceReference(TextElement.FontSizeProperty, "Fonte.Corpo");
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 24) };
        var modelo = Botao("Usar modelo"); modelo.SetBinding(IsEnabledProperty, new Binding("PodeEditar"));
        DockPanel.SetDock(modelo, Dock.Right); header.Children.Add(modelo);
        modelo.Click += (_, _) => AbrirOpcoes(2);
        var title = new StackPanel(); var nome = Texto("", 26, "Brush.Texto.Primario", true); nome.SetBinding(TextBlock.TextProperty, new Binding("TituloDocumento")); title.Children.Add(nome);
        var patient = Texto("", 14, "Brush.Acento"); patient.SetBinding(TextBlock.TextProperty, new Binding("ContextoDocumento"));
        var dados = Botao("", false, true); dados.Content = patient; dados.FontWeight = FontWeights.Normal; dados.Padding = new Thickness(0); dados.MinHeight = 0; dados.HorizontalAlignment = HorizontalAlignment.Left; dados.Margin = new Thickness(0, 7, 0, 0);
        dados.ToolTip = "Editar data, profissional, título e observações"; dados.SetBinding(IsEnabledProperty, new Binding("PodeEditar")); dados.Click += (_, _) => AbrirOpcoes(0); title.Children.Add(dados); header.Children.Add(title);
        DockPanel.SetDock(header, Dock.Top); Children.Add(header);
        var footer = new DockPanel { Margin = new Thickness(0, 18, 0, 0) }; DockPanel.SetDock(footer, Dock.Bottom); Children.Add(footer);
        var actions = new StackPanel { Orientation = Orientation.Horizontal }; DockPanel.SetDock(actions, Dock.Right); footer.Children.Add(actions);
        var close = Botao("Fechar"); close.IsCancel = true; actions.Children.Add(close);
        var emit = Botao("Emitir e imprimir", true); emit.IsDefault = true; emit.Margin = new Thickness(10, 0, 0, 0); emit.Command = vm.EmitirCommand; actions.Children.Add(emit);
        var save = Botao("Salvar como modelo", false, true); save.HorizontalAlignment = HorizontalAlignment.Left; save.Command = vm.PedirNomeModeloCommand; save.SetBinding(IsEnabledProperty, new Binding("PodeEditar")); footer.Children.Add(save);
        var message = Texto("", 14, "Brush.Texto.Secundario"); message.SetBinding(TextBlock.TextProperty, new Binding("Mensagem")); message.SetBinding(VisibilityProperty, new Binding("Mensagem") { Converter = new TextoParaVisibilidade() }); message.Margin = new Thickness(0, 8, 0, 0); DockPanel.SetDock(message, Dock.Bottom); Children.Add(message);
        System.Windows.Automation.AutomationProperties.SetLiveSetting(message, System.Windows.Automation.AutomationLiveSetting.Polite);
        var surface = new Border { Background = Cor("Brush.Fundo"), CornerRadius = new CornerRadius(8), Padding = new Thickness(45, 20, 45, 20) }; Children.Add(surface);
        var paper = new Border { Background = Cor("Brush.Superficie"), BorderBrush = Cor("Brush.Borda"), BorderThickness = new Thickness(1), Padding = new Thickness(36, 24, 36, 20) }; scroll.Content = paper; surface.Child = scroll;
        paper.SetBinding(MinHeightProperty, new Binding("ViewportHeight") { Source = scroll });
        SizeChanged += (_, _) => { surface.Padding = ActualWidth < 900 ? new Thickness(16, 16, 16, 16) : new Thickness(45, 20, 45, 20); paper.Padding = ActualWidth < 900 ? new Thickness(20) : new Thickness(36, 24, 36, 20); };
        var body = new DockPanel(); body.SetBinding(IsEnabledProperty, new Binding("PodeEditar")); paper.Child = body;
        var heading = new StackPanel(); DockPanel.SetDock(heading, Dock.Top); body.Children.Add(heading);
        var centered = Texto("", 18, "Brush.Texto.Primario", true); centered.SetBinding(TextBlock.TextProperty, new Binding("TituloFolha")); centered.HorizontalAlignment = HorizontalAlignment.Center; centered.Margin = new Thickness(0, 0, 0, 20); heading.Children.Add(centered);
        heading.Children.Add(Alertas());
        if (vm.MostraAtestado) heading.Children.Add(Afastamento());
        else if (vm.MostraComparecimento) heading.Children.Add(Comparecimento());
        else if (vm.MostraItens) heading.Children.Add(Exames());
        var signer = new StackPanel { Margin = new Thickness(0, 18, 0, 0) }; DockPanel.SetDock(signer, Dock.Bottom); body.Children.Add(signer);
        var line = new Border { BorderBrush = Cor("Brush.Borda"), BorderThickness = new Thickness(0, 1, 0, 0), Width = 270, HorizontalAlignment = HorizontalAlignment.Center, Padding = new Thickness(0, 10, 0, 0) };
        var responsavel = Texto("", 12, "Brush.Texto.Secundario"); responsavel.SetBinding(TextBlock.TextProperty, new Binding("ResponsavelDocumento")); line.Child = responsavel; signer.Children.Add(line);
        body.Children.Add(Editor(vm.RotuloTextoFolha, true));
    }
    void AbrirOpcoes(int aba)
    {
        new DocumentoOpcoesWindow(vm, aba) { Owner = owner }.ShowDialog();
        ((DocumentoWindow)owner).AtualizarFolha();
    }
    FrameworkElement Alertas()
    {
        var panel = new StackPanel();
        foreach (var (items, visible) in new[] { ("AlertasClinicos", "TemAlertasClinicos"), ("ExigenciasLegais", "TemExigenciasLegais"), ("ColisoesAlergia", "ColideComAlergia") })
        {
            var list = new ItemsControl { Margin = new Thickness(0, 0, 0, 12) };
            list.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(items)); list.SetBinding(VisibilityProperty, new Binding(visible) { Converter = new BooleanToVisibilityConverter() });
            var template = new DataTemplate(); var text = new FrameworkElementFactory(typeof(TextBlock)); text.SetBinding(TextBlock.TextProperty, new Binding()); text.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap); text.SetResourceReference(TextBlock.ForegroundProperty, "Brush.Erro"); template.VisualTree = text; list.ItemTemplate = template; panel.Children.Add(list);
        }
        var check = new CheckBox { Content = new TextBlock { Text = "Eu vi o alerta de alergia e assumo esta prescrição", TextWrapping = TextWrapping.Wrap }, Margin = new Thickness(0, 0, 0, 12) };
        check.SetBinding(CheckBox.IsCheckedProperty, new Binding("AlergiaConferida")); check.SetBinding(VisibilityProperty, new Binding("ColideComAlergia") { Converter = new BooleanToVisibilityConverter() }); panel.Children.Add(check);
        return panel;
    }
    TextBlock Texto(string text, double size, string cor, bool bold = false)
    {
        var block = new TextBlock { Text = text, Foreground = Cor(cor), FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal, TextWrapping = TextWrapping.Wrap };
        block.SetResourceReference(TextBlock.FontSizeProperty, size switch { 26 => "Fonte.DocumentoTitulo", 18 => "Fonte.H3", 13 => "Fonte.Tabela", 12 => "Fonte.Legenda", _ => "Fonte.Corpo" }); return block;
    }
    Button Botao(string text, bool primary = false, bool link = false)
    {
        var b = new Button { Content = text, Style = (Style)System.Windows.Application.Current.FindResource(primary ? (object)typeof(Button) : "BotaoDocumento"), Padding = new Thickness(16, 9, 16, 9), MinHeight = 38, VerticalAlignment = VerticalAlignment.Center, FontSize = 14 };
        if (link) { b.BorderThickness = new Thickness(0); b.Background = Brushes.Transparent; b.Foreground = Cor("Brush.Acento"); b.Padding = new Thickness(0, 8, 0, 8); }
        return b;
    }
    TextBox Campo(string path) { var b = new TextBox { Height = 40, VerticalAlignment = VerticalAlignment.Top, Padding = new Thickness(11, 0, 11, 0) }; b.SetBinding(TextBox.TextProperty, new Binding(path) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged }); return b; }
    StackPanel Rotulo(string text, FrameworkElement control) { System.Windows.Automation.AutomationProperties.SetName(control, text); var p = new StackPanel(); var t = Texto(text, 13, "Brush.Texto.Secundario"); t.Margin = new Thickness(0, 0, 0, 7); p.Children.Add(t); p.Children.Add(control); return p; }
    Grid Colunas(params FrameworkElement[] controls)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 20) };
        for (int i = 0; i < controls.Length; i++) { grid.ColumnDefinitions.Add(new ColumnDefinition()); Grid.SetColumn(controls[i], i); controls[i].Margin = new Thickness(0, 0, i == controls.Length - 1 ? 0 : 20, 0); grid.Children.Add(controls[i]); }
        return grid;
    }
    FrameworkElement Afastamento()
    {
        var panel = new StackPanel();
        var date = new DatePicker { Height = 40 }; date.SetBinding(DatePicker.SelectedDateProperty, new Binding("PeriodoInicio"));
        panel.Children.Add(Colunas(Rotulo("Dias de afastamento", Campo("DiasAfastamentoTexto")), Rotulo("Início do afastamento", date), Rotulo("CID · opcional", Campo("Cid"))));
        var check = new CheckBox { Content = "Paciente autorizou a inclusão do CID", FontSize = 13, Margin = new Thickness(0, -5, 0, 22) }; check.SetBinding(CheckBox.IsCheckedProperty, new Binding("CidAutorizado")); panel.Children.Add(check);
        panel.Children.Add(Aviso("DescricaoCid", null));
        panel.Children.Add(Aviso("ExplicacaoDoCid", "CidVeioDaSessao"));
        panel.Children.Add(Aviso(null, "AvisaCidOmitido", "O CID será omitido do documento enquanto não houver autorização."));
        return panel;
    }
    FrameworkElement Comparecimento()
    {
        var date = new DatePicker { Height = 40 }; date.SetBinding(DatePicker.SelectedDateProperty, new Binding("PeriodoInicio"));
        var panel = new StackPanel();
        panel.Children.Add(Colunas(Rotulo("Dia do atendimento", date), Rotulo("Chegada", Campo("HoraChegadaTexto")), Rotulo("Saída", Campo("HoraSaidaTexto"))));
        panel.Children.Add(Aviso(null, "HorasVieramDaSessao", "Horários preenchidos a partir do atendimento. Confira antes de emitir."));
        return panel;
    }
    FrameworkElement Exames()
    {
        var section = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
        var list = new StackPanel(); section.Children.Add(list);
        void Atualizar()
        {
            list.Children.Clear();
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            foreach (var width in new[] { new GridLength(3, GridUnitType.Star), new GridLength(90), new GridLength(3, GridUnitType.Star), new GridLength(36) }) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = width });
            var labels = new[] { "Exame / procedimento", "Quantidade", "Indicação clínica" };
            for (var i = 0; i < labels.Length; i++) { var t = Texto(labels[i], 13, "Brush.Texto.Secundario"); Grid.SetColumn(t, i); grid.Children.Add(t); }
            list.Children.Add(grid);
            foreach (var item in vm.Itens)
            {
                var row = new Grid { DataContext = item, Margin = new Thickness(0, 0, 0, 10) };
                foreach (var width in new[] { new GridLength(3, GridUnitType.Star), new GridLength(90), new GridLength(3, GridUnitType.Star), new GridLength(36) }) row.ColumnDefinitions.Add(new ColumnDefinition { Width = width });
                var paths = new[] { "Descricao", "Quantidade", "Detalhe" };
                for (var i = 0; i < paths.Length; i++)
                {
                    var field = Campo(paths[i]);
                    System.Windows.Automation.AutomationProperties.SetName(field, labels[i]);
                    if (i != 1) { field.Height = double.NaN; field.MinHeight = 40; field.TextWrapping = TextWrapping.Wrap; field.AcceptsReturn = true; field.Padding = new Thickness(11, 8, 11, 8); }
                    field.Margin = new Thickness(0, 0, 12, 0); Grid.SetColumn(field, i); row.Children.Add(field);
                }
                var remove = Botao("×", false, true); remove.Foreground = Cor("Brush.Erro"); remove.ToolTip = "Remover exame"; System.Windows.Automation.AutomationProperties.SetName(remove, "Remover exame"); remove.HorizontalAlignment = HorizontalAlignment.Center; Grid.SetColumn(remove, 3); row.Children.Add(remove);
                remove.Click += (_, _) => { vm.RemoverItemCommand.Execute(item); Atualizar(); }; list.Children.Add(row);
            }
        }
        Atualizar();
        var add = Botao("+ Adicionar exame", false, true); add.HorizontalAlignment = HorizontalAlignment.Left; add.Click += (_, _) => { vm.AdicionarItemCommand.Execute(null); Atualizar(); }; section.Children.Add(add);
        return section;
    }
    FrameworkElement Aviso(string? path, string? visible, string text = "")
    {
        var block = Texto(text, 12, "Brush.Texto.Secundario"); block.Margin = new Thickness(0, 0, 0, 12);
        if (path is not null) block.SetBinding(TextBlock.TextProperty, new Binding(path));
        block.SetBinding(VisibilityProperty, visible is null
            ? new Binding(path!) { Converter = new TextoParaVisibilidade() }
            : new Binding(visible) { Converter = new BooleanToVisibilityConverter() });
        return block;
    }
    FrameworkElement Editor(string title, bool sheet)
    {
        var p = new DockPanel();
        var label = Texto(title, 13, "Brush.Texto.Secundario"); label.Margin = new Thickness(0, 0, 0, 8); DockPanel.SetDock(label, Dock.Top); p.Children.Add(label);
        var editor = new EditorTextoClinico { MinHeight = 150 }; System.Windows.Automation.AutomationProperties.SetName(editor, title); editor.SetBinding(EditorTextoClinico.TextoProperty, new Binding("Corpo") { Mode = BindingMode.TwoWay }); editor.SetBinding(EditorTextoClinico.FormatoProperty, new Binding("CorpoFormatado") { Mode = BindingMode.TwoWay }); p.Children.Add(editor);
        editor.ModoFolha = sheet;
        return p;
    }
}
