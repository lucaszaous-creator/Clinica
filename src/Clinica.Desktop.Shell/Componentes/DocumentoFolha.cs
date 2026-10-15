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
        // Pré-visualizar: o PDF do que está na tela, SEM emitir — sem número, com a marca
        // PRÉVIA no papel. Conferir a folha antes de numerá-la é o que evita emitir, ver um
        // erro e ter de cancelar com motivo.
        var previa = Botao("Pré-visualizar"); previa.Margin = new Thickness(10, 0, 0, 0); previa.Command = vm.PreverCommand;
        previa.ToolTip = "Abre o PDF desta folha sem emitir: sem número e sem valor de documento"; actions.Children.Add(previa);
        var emit = Botao("Emitir e imprimir", true); emit.IsDefault = true; emit.Margin = new Thickness(10, 0, 0, 0); emit.Command = vm.EmitirCommand; actions.Children.Add(emit);
        var save = Botao("Salvar como modelo", false, true); save.HorizontalAlignment = HorizontalAlignment.Left; save.Command = vm.PedirNomeModeloCommand; save.SetBinding(IsEnabledProperty, new Binding("PodeEditar")); footer.Children.Add(save);
        // Por que o "Emitir" está apagado quando falta quem assina (o login não está ligado
        // a um profissional): a frase fica AO LADO do botão, com a porta para escolher —
        // botão apagado sem explicação se lê como sistema travado (parcela 41).
        var semAssinante = new DockPanel { Margin = new Thickness(0, 10, 0, 0) }; DockPanel.SetDock(semAssinante, Dock.Bottom);
        semAssinante.SetBinding(VisibilityProperty, new Binding("MotivoSemAssinante") { Converter = new TextoParaVisibilidade() });
        var escolher = Botao("Escolher quem assina\u2026", false, true); escolher.Margin = new Thickness(12, 0, 0, 0); escolher.VerticalAlignment = VerticalAlignment.Center;
        escolher.SetBinding(IsEnabledProperty, new Binding("PodeEditar")); escolher.Click += (_, _) => AbrirOpcoes(0);
        DockPanel.SetDock(escolher, Dock.Right); semAssinante.Children.Add(escolher);
        var motivo = Texto("", 14, "Brush.Aviso"); motivo.SetBinding(TextBlock.TextProperty, new Binding("MotivoSemAssinante")); motivo.VerticalAlignment = VerticalAlignment.Center; semAssinante.Children.Add(motivo);
        Children.Add(semAssinante);
        var message = Texto("", 14, "Brush.Texto.Secundario"); message.SetBinding(TextBlock.TextProperty, new Binding("Mensagem")); message.SetBinding(VisibilityProperty, new Binding("Mensagem") { Converter = new TextoParaVisibilidade() }); message.Margin = new Thickness(0, 8, 0, 0); DockPanel.SetDock(message, Dock.Bottom); Children.Add(message);
        System.Windows.Automation.AutomationProperties.SetLiveSetting(message, System.Windows.Automation.AutomationLiveSetting.Polite);
        var surface = new Border { Background = Cor("Brush.Fundo"), CornerRadius = new CornerRadius(8), Padding = new Thickness(45, 20, 45, 20) }; Children.Add(surface);
        // A FOLHA tem largura de folha (out/2026): esticada a 1.200 px ela era uma tela
        // branca enorme com um campo perdido no meio. Centrada e com teto, ela se lê como o
        // papel que vai sair — e a altura é a do CONTEÚDO, com a assinatura logo depois do
        // texto, em vez de esticada até o pé da janela.
        var paper = new Border { Background = Cor("Brush.Superficie"), BorderBrush = Cor("Brush.Borda"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), Padding = new Thickness(40, 28, 40, 28), MaxWidth = 860, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Top };
        scroll.Content = paper; surface.Child = scroll;
        SizeChanged += (_, _) => { surface.Padding = ActualWidth < 900 ? new Thickness(16, 16, 16, 16) : new Thickness(45, 20, 45, 20); paper.Padding = ActualWidth < 900 ? new Thickness(20) : new Thickness(40, 28, 40, 28); };
        var body = new StackPanel(); body.SetBinding(IsEnabledProperty, new Binding("PodeEditar")); paper.Child = body;
        body.Children.Add(CabecalhoDaFolha());
        var centered = Texto("", 18, "Brush.Texto.Primario", true); centered.SetBinding(TextBlock.TextProperty, new Binding("TituloFolha")); centered.HorizontalAlignment = HorizontalAlignment.Center; centered.Margin = new Thickness(0, 0, 0, 20); body.Children.Add(centered);
        body.Children.Add(Alertas());
        if (vm.MostraAtestado) body.Children.Add(Afastamento());
        else if (vm.MostraLaudo) body.Children.Add(CidDoLaudo());
        else if (vm.MostraComparecimento) body.Children.Add(Comparecimento());
        else if (vm.MostraItens) body.Children.Add(Exames());
        body.Children.Add(Editor(vm.RotuloTextoFolha, true, vm.MostraLaudo ? 320 : 220));
        body.Children.Add(BlocoDeAssinatura());
    }

    /// <summary>
    /// O topo da folha: de QUEM é o papel, a data e o número — as três coisas que o PDF
    /// imprime no cabeçalho. O número diz a verdade: ele só nasce na emissão.
    /// </summary>
    FrameworkElement CabecalhoDaFolha()
    {
        var faixa = new Border { BorderBrush = Cor("Brush.Acento"), BorderThickness = new Thickness(0, 0, 0, 2), Padding = new Thickness(0, 0, 0, 12), Margin = new Thickness(0, 0, 0, 22) };
        var grade = new Grid(); grade.ColumnDefinitions.Add(new ColumnDefinition()); grade.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        faixa.Child = grade;
        var quem = new StackPanel();
        var rotulo = Texto("PACIENTE", 12, "Brush.Texto.Secundario", true); quem.Children.Add(rotulo);
        var nome = Texto("", 18, "Brush.Texto.Primario", true); nome.SetBinding(TextBlock.TextProperty, new Binding("PacienteDaFolha")); nome.TextTrimming = TextTrimming.CharacterEllipsis; nome.TextWrapping = TextWrapping.NoWrap; quem.Children.Add(nome);
        var identificacao = Texto("", 12, "Brush.Texto.Secundario"); identificacao.SetBinding(TextBlock.TextProperty, new Binding("IdentificacaoDaFolha"));
        identificacao.SetBinding(VisibilityProperty, new Binding("IdentificacaoDaFolha") { Converter = new TextoParaVisibilidade() }); quem.Children.Add(identificacao);
        grade.Children.Add(quem);
        var numero = new StackPanel { Margin = new Thickness(24, 0, 0, 0), VerticalAlignment = VerticalAlignment.Bottom }; Grid.SetColumn(numero, 1);
        var n = Texto("", 13, "Brush.Texto.Secundario", true); n.SetBinding(TextBlock.TextProperty, new Binding("NumeroDaFolha")); n.HorizontalAlignment = HorizontalAlignment.Right; n.TextWrapping = TextWrapping.NoWrap; numero.Children.Add(n);
        var data = Texto("", 12, "Brush.Texto.Secundario"); data.SetBinding(TextBlock.TextProperty, new Binding("DataDaFolha") { StringFormat = "Data {0}" }); data.HorizontalAlignment = HorizontalAlignment.Right; numero.Children.Add(data);
        grade.Children.Add(numero);
        return faixa;
    }

    /// <summary>
    /// A assinatura LOGO DEPOIS do texto, como no papel: nome e registro de quem assina, ou —
    /// sem assinante — a frase do porquê com a porta para escolher, no MESMO lugar onde o
    /// nome vai aparecer. Antes ela era docada no pé da folha e ficava solta, longe do texto
    /// que ela assina.
    /// </summary>
    FrameworkElement BlocoDeAssinatura()
    {
        var bloco = new StackPanel { Width = 320, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 28, 0, 4) };
        var linha = new Border { BorderBrush = Cor("Brush.Texto.Secundario"), BorderThickness = new Thickness(0, 1, 0, 0), Margin = new Thickness(0, 30, 0, 6) };
        bloco.Children.Add(linha);
        var comAssinante = new StackPanel(); comAssinante.SetBinding(VisibilityProperty, new Binding("TemAssinante") { Converter = new BooleanToVisibilityConverter() });
        var nome = Texto("", 14, "Brush.Texto.Primario", true); nome.HorizontalAlignment = HorizontalAlignment.Center; nome.TextAlignment = TextAlignment.Center; nome.SetBinding(TextBlock.TextProperty, new Binding("NomeDoAssinante")); comAssinante.Children.Add(nome);
        var registro = Texto("", 12, "Brush.Texto.Secundario"); registro.HorizontalAlignment = HorizontalAlignment.Center; registro.TextAlignment = TextAlignment.Center; registro.SetBinding(TextBlock.TextProperty, new Binding("RegistroDoAssinante")); comAssinante.Children.Add(registro);
        bloco.Children.Add(comAssinante);
        var semAssinante = new StackPanel(); semAssinante.SetBinding(VisibilityProperty, new Binding("TemAssinante") { Converter = new VisivelSeFalso() });
        var falta = Texto("", 13, "Brush.Aviso"); falta.TextAlignment = TextAlignment.Center; falta.SetBinding(TextBlock.TextProperty, new Binding("FaltaAssinante")); semAssinante.Children.Add(falta);
        var escolher = Botao("Escolher quem assina\u2026", false, true); escolher.HorizontalAlignment = HorizontalAlignment.Center; escolher.Click += (_, _) => AbrirOpcoes(0); semAssinante.Children.Add(escolher);
        bloco.Children.Add(semAssinante);
        return bloco;
    }

    /// <summary>Visível quando o booleano é FALSO — o <c>BooleanToVisibilityConverter</c> ignora parâmetro.</summary>
    sealed class VisivelSeFalso : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            => value is true ? Visibility.Collapsed : Visibility.Visible;
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            => Binding.DoNothing;
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
        AutorizacaoDoCid(panel);
        return panel;
    }
    /// <summary>
    /// O laudo leva só o CID acima do texto — dias e data são do afastamento. A autorização
    /// e os três avisos são os MESMOS do atestado: duas regras para o mesmo sigilo
    /// divergiriam na primeira correção.
    /// </summary>
    FrameworkElement CidDoLaudo()
    {
        // O código, o atalho da clínica e — AO LADO — o que o código quer dizer. É a
        // descrição que pega o dígito trocado ("M54.4" no lugar de "M54.5" também existe):
        // abaixo, como um aviso solto, ela não era lida (out/2026).
        var panel = new StackPanel();
        var titulo = Texto("CID-10 · opcional", 13, "Brush.Texto.Secundario"); titulo.Margin = new Thickness(0, 0, 0, 7); panel.Children.Add(titulo);
        var linha = new Grid { Margin = new Thickness(0, 0, 0, 14) };
        foreach (var largura in new[] { new GridLength(150), GridLength.Auto, new GridLength(1, GridUnitType.Star) }) linha.ColumnDefinitions.Add(new ColumnDefinition { Width = largura });
        var campo = Campo("Cid"); System.Windows.Automation.AutomationProperties.SetName(campo, "CID-10"); campo.CharacterCasing = CharacterCasing.Upper; linha.Children.Add(campo);
        var procurar = Botao("Procurar\u2026"); procurar.Command = vm.BuscarCidCommand; procurar.Margin = new Thickness(8, 0, 0, 0); procurar.MinHeight = 40; procurar.Padding = new Thickness(14, 0, 14, 0); procurar.VerticalAlignment = VerticalAlignment.Top;
        procurar.ToolTip = "Procurar o código na lista da clínica"; Grid.SetColumn(procurar, 1); linha.Children.Add(procurar);
        var descricao = Texto("", 14, "Brush.Texto.Primario"); descricao.Margin = new Thickness(16, 0, 0, 0); descricao.VerticalAlignment = VerticalAlignment.Center;
        descricao.SetBinding(TextBlock.TextProperty, new Binding("DescricaoCid") { TargetNullValue = string.Empty });
        var semDescricao = Texto("Fora da lista da clínica: confira o código antes de emitir.", 12, "Brush.Texto.Secundario"); semDescricao.Margin = descricao.Margin; semDescricao.VerticalAlignment = VerticalAlignment.Center;
        var descricoes = new Grid(); descricoes.Children.Add(descricao); descricoes.Children.Add(semDescricao); Grid.SetColumn(descricoes, 2); linha.Children.Add(descricoes);
        descricao.SetBinding(VisibilityProperty, new Binding("DescricaoCid") { Converter = new TextoParaVisibilidade() });
        // "Fora da lista" só com código digitado e sem descrição: com o campo vazio não há
        // o que conferir, e a frase seria um aviso sobre nada.
        void AtualizarForaDaLista() => semDescricao.Visibility = !string.IsNullOrWhiteSpace(vm.Cid) && string.IsNullOrEmpty(vm.DescricaoCid) ? Visibility.Visible : Visibility.Collapsed;
        AtualizarForaDaLista();
        vm.PropertyChanged += (_, e) => { if (e.PropertyName is nameof(DocumentoEdicaoViewModel.DescricaoCid) or nameof(DocumentoEdicaoViewModel.Cid)) AtualizarForaDaLista(); };
        panel.Children.Add(linha);
        AutorizacaoDoCid(panel, descricaoAbaixo: false);
        return panel;
    }
    void AutorizacaoDoCid(Panel panel, bool descricaoAbaixo = true)
    {
        var check = new CheckBox { Content = "Paciente autorizou a inclusão do CID", FontSize = 13, Margin = new Thickness(0, -5, 0, 22) }; check.SetBinding(CheckBox.IsCheckedProperty, new Binding("CidAutorizado")); panel.Children.Add(check);
        if (descricaoAbaixo) panel.Children.Add(Aviso("DescricaoCid", null));
        panel.Children.Add(Aviso("ExplicacaoDoCid", "CidVeioDaSessao"));
        panel.Children.Add(Aviso(null, "AvisaCidOmitido", "O CID será omitido do documento enquanto não houver autorização."));
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
    FrameworkElement Editor(string title, bool sheet, double alturaMinima)
    {
        var p = new DockPanel();
        var label = Texto(title, 13, "Brush.Texto.Secundario"); label.Margin = new Thickness(0, 0, 0, 8); DockPanel.SetDock(label, Dock.Top); p.Children.Add(label);
        var editor = new EditorTextoClinico { MinHeight = 240 }; System.Windows.Automation.AutomationProperties.SetName(editor, title); editor.SetBinding(EditorTextoClinico.TextoProperty, new Binding("Corpo") { Mode = BindingMode.TwoWay }); editor.SetBinding(EditorTextoClinico.FormatoProperty, new Binding("CorpoFormatado") { Mode = BindingMode.TwoWay });
        editor.ModoFolha = sheet;
        // A área de escrever tem MOLDURA, a barra N/I PRESA ao campo e altura mínima
        // generosa (out/2026 — o cliente via o laudo como "tela branca enorme": sem borda
        // não se sabe onde clicar para escrever nem até onde vai o texto do documento).
        editor.BarraAnexada = true; editor.MinHeight = alturaMinima;
        editor.SetBinding(EditorTextoClinico.TextoDeAjudaProperty, new Binding("AjudaDoTexto"));
        // O campo cresce com o texto dentro de uma página que rola: sem isto, a roda do
        // mouse sobre ele não chegaria à página (parcela 90).
        Ajudantes.SetRodaDaPagina(editor, true);
        p.Children.Add(editor);
        return p;
    }
}
