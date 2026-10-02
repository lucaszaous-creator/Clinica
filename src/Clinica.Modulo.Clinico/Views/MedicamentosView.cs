using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Clinica.Application.Servicos;
using Clinica.Desktop.Shell;
using Clinica.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualBasic.FileIO;
using Microsoft.Win32;

namespace Clinica.Clinico.Views;

public sealed class MedicamentosView : UserControl
{
    private readonly IServiceScopeFactory escopos;
    private IReadOnlyList<MedicamentoCadastro> todos=[];
    private readonly TextBox busca=new() {MinWidth=280};
    private readonly DataGrid tabela=new() {AutoGenerateColumns=false,IsReadOnly=true,SelectionMode=DataGridSelectionMode.Single,MinHeight=180};
    private readonly TextBlock mensagem=new() {TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,8)};
    private readonly TextBox nome=new() {MaxLength=200}, principio=new() {MaxLength=200}, apresentacao=new() {MaxLength=200}, fabricante=new() {MaxLength=160};
    private readonly CheckBox ativo=new() {Content="Ativo nas sugestões",IsChecked=true,Margin=new Thickness(0,10,0,10)};
    private MedicamentoCadastro? selecionado;
    private bool ocupado;
    public Task Inicializacao {get;}
    public MedicamentosView(IServiceScopeFactory escopos)
    {
        this.escopos=escopos;var raiz=new DockPanel {Margin=new Thickness(24)};Content=raiz;
        var topo=new StackPanel();DockPanel.SetDock(topo,Dock.Top);raiz.Children.Add(topo);
        topo.Children.Add(new TextBlock {Text="Medicamentos",FontSize=26,FontWeight=FontWeights.SemiBold});
        topo.Children.Add(new TextBlock {Text="Cadastro compartilhado entre Consultório e Gerente. Nomes DCB da Anvisa e produtos cadastrados pela clínica.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,4)});
        topo.Children.Add(new TextBlock {Text="A base de nomes não define dose, via ou compatibilidade. Cadastre a apresentação do produto para distinguir concentrações.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,12)});
        var acoes=new WrapPanel();topo.Children.Add(acoes);acoes.Children.Add(busca);busca.ToolTip="Buscar nome, princípio ativo ou apresentação";
        Clinica.Desktop.Controls.Ajudantes.SetPlaceholder(busca,"Buscar medicamento…");
        System.Windows.Automation.AutomationProperties.SetName(busca,"Buscar medicamento");
        Botao(acoes,"Novo medicamento",(_,_)=>Limpar());Botao(acoes,"Atualizar",async(_,_)=>await CarregarAsync());
        Botao(acoes,"Importar CSV",async(_,_)=>await ImportarAsync());Botao(acoes,"Modelo de planilha",(_,_)=>ExportarModelo());topo.Children.Add(mensagem);
        var edicao=new StackPanel {Width=300,Margin=new Thickness(20,0,0,0)};DockPanel.SetDock(edicao,Dock.Right);raiz.Children.Add(edicao);
        Campo(edicao,"Nome do medicamento",nome);Campo(edicao,"Princípio ativo",principio);Campo(edicao,"Apresentação / concentração",apresentacao);Campo(edicao,"Fabricante",fabricante);edicao.Children.Add(ativo);
        Botao(edicao,"Salvar cadastro",async(_,_)=>await SalvarAsync());
        foreach(var (titulo,prop) in new[]{("Nome","Nome"),("Princípio ativo","PrincipioAtivo"),("Apresentação","Apresentacao"),("Fonte","Fonte")})
        {
            var estilo=new Style(typeof(TextBlock));estilo.Setters.Add(new Setter(TextBlock.TextWrappingProperty,TextWrapping.Wrap));estilo.Setters.Add(new Setter(FrameworkElement.MarginProperty,new Thickness(4,6,4,6)));
            tabela.Columns.Add(new DataGridTextColumn {Header=titulo,Binding=new Binding(prop),ElementStyle=estilo,Width=new DataGridLength(1,DataGridLengthUnitType.Star)});
        }
        tabela.Columns.Add(new DataGridCheckBoxColumn {Header="Ativo",Binding=new Binding("Ativo")});
        raiz.Children.Add(tabela);busca.TextChanged+=(_,_)=>Filtrar();
        tabela.SelectionChanged+=(_,_)=>{if(tabela.SelectedItem is not MedicamentoCadastro m)return;selecionado=m;nome.Text=m.Nome;principio.Text=m.PrincipioAtivo;apresentacao.Text=m.Apresentacao;fabricante.Text=m.Fabricante;ativo.IsChecked=m.Ativo;};
        Inicializacao=CarregarAsync();
    }
    private static void Campo(Panel p,string rotulo,TextBox campo){p.Children.Add(new TextBlock {Text=rotulo,Margin=new Thickness(0,8,0,4)});p.Children.Add(campo);System.Windows.Automation.AutomationProperties.SetName(campo,rotulo);}
    private static void Botao(Panel p,string texto,RoutedEventHandler acao){var b=new Button {Content=texto,Margin=new Thickness(6,0,0,6)};b.Click+=acao;p.Children.Add(b);}
    private void Limpar(){selecionado=null;tabela.SelectedItem=null;nome.Clear();principio.Clear();apresentacao.Clear();fabricante.Clear();ativo.IsChecked=true;nome.Focus();}
    private async Task CarregarAsync()
    {
        if(ocupado)return;
        try{ocupado=true;using var scope=escopos.CreateScope();todos=await scope.ServiceProvider.GetRequiredService<MedicamentoCatalogoService>().ListarAsync();Filtrar();}
        catch(Exception ex){mensagem.Text=ex.Message;}finally{ocupado=false;}
    }
    private void Filtrar()
    {
        var compare=CultureInfo.GetCultureInfo("pt-BR").CompareInfo;
        var lista=todos.Where(m=>compare.IndexOf(m.Nome+" "+m.PrincipioAtivo+" "+m.Apresentacao+" "+m.Fabricante,busca.Text.Trim(),CompareOptions.IgnoreCase|CompareOptions.IgnoreNonSpace)>=0).ToArray();
        tabela.ItemsSource=lista.OrderByDescending(m=>m.Codigo.StartsWith("clinica:")).ThenBy(m=>m.Nome).Take(250);mensagem.Text=$"{lista.Length:N0} cadastro(s). Exibindo até 250; use a busca para refinar.";
    }
    private async Task SalvarAsync()
    {
        if(ocupado)return;
        try
        {
            ocupado=true;
            var dados=new MedicamentoCadastro {Codigo=selecionado?.Codigo??"",Nome=nome.Text.Trim(),PrincipioAtivo=principio.Text.Trim(),Apresentacao=apresentacao.Text.Trim(),Fabricante=fabricante.Text.Trim(),Ativo=ativo.IsChecked==true};
            using var scope=escopos.CreateScope();
            await scope.ServiceProvider.GetRequiredService<MedicamentoCatalogoService>().SalvarAsync(dados,SessaoUsuario.Atual.UsuarioId);
            ocupado=false;await CarregarAsync();
            selecionado=todos.Single(m=>m.Nome==dados.Nome&&(m.Apresentacao??"")==dados.Apresentacao&&(m.Fabricante??"")==dados.Fabricante);
            mensagem.Text="Cadastro salvo. A busca da prescrição usará os dados atualizados ao abrir a tela.";
        }
        catch(Exception ex){mensagem.Text=ex.Message;}finally{ocupado=false;}
    }
    private void ExportarModelo()
    {
        var d=new SaveFileDialog {Filter="Planilha CSV|*.csv",FileName="medicamentos.csv"};if(d.ShowDialog()!=true)return;
        try{File.WriteAllText(d.FileName,"Nome;PrincipioAtivo;Apresentacao;Fabricante\r\n",new UTF8Encoding(true));mensagem.Text="Preencha a planilha, salve como CSV UTF-8 e use Importar CSV.";}catch(Exception ex){mensagem.Text=ex.Message;}
    }
    private async Task ImportarAsync()
    {
        if(ocupado)return;var d=new OpenFileDialog {Filter="Planilha CSV|*.csv"};if(d.ShowDialog()!=true)return;
        try
        {
            if(new FileInfo(d.FileName).Length>5_000_000)throw new InvalidOperationException("A planilha deve ter até 5 MB.");
            using var parser=new TextFieldParser(d.FileName,Encoding.UTF8,true) {TextFieldType=FieldType.Delimited,HasFieldsEnclosedInQuotes=true};parser.SetDelimiters(";");
            var cabecalho=parser.ReadFields();if(cabecalho is null||!cabecalho.SequenceEqual(new[]{"Nome","PrincipioAtivo","Apresentacao","Fabricante"}))throw new InvalidOperationException("Use o Modelo de planilha, com as quatro colunas e separador ponto e vírgula.");
            var lista=new List<MedicamentoCadastro>();
            while(!parser.EndOfData){var c=parser.ReadFields();if(c is null)continue;if(c.Length!=4)throw new InvalidOperationException("Confira as quatro colunas da planilha.");lista.Add(new(){Nome=c[0],PrincipioAtivo=c[1],Apresentacao=c[2],Fabricante=c[3]});if(lista.Count>5000)throw new InvalidOperationException("Importe até 5.000 medicamentos por vez.");}
            if(MessageBox.Show(Window.GetWindow(this),$"Importar {lista.Count} medicamentos para o cadastro compartilhado?","Importar medicamentos",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
            ocupado=true;using var scope=escopos.CreateScope();await scope.ServiceProvider.GetRequiredService<MedicamentoCatalogoService>().SalvarLoteAsync(lista,SessaoUsuario.Atual.UsuarioId);ocupado=false;await CarregarAsync();mensagem.Text=$"{lista.Count} medicamentos importados.";
        }catch(Exception ex){mensagem.Text=ex.Message;}finally{ocupado=false;}
    }
}
