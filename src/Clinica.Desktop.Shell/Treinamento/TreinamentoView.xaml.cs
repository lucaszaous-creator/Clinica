using System.Windows;
using System.Windows.Controls;

namespace Clinica.Desktop.Shell.Treinamento;

public partial class TreinamentoView : UserControl
{
    private readonly IReadOnlyList<AulaTreinamento> _aulas;
    private readonly AcervoTreinamento _acervo;
    private AulaTreinamento? _atual;
    private TreinamentoPlayer? _player;
    private bool _pronto;
    private sealed record Cartao(AulaTreinamento Aula,string Estado)
    {
        public string Titulo=>Aula.Titulo;
        public string Categoria=>Aula.Categoria;
        public string Descricao=>Aula.Descricao;
        public string DuracaoTexto=>Aula.DuracaoTexto;
    }
    public TreinamentoView(IReadOnlyList<AulaTreinamento> aulas,int usuarioId,string? dadosLocais=null)
    {
        _aulas=aulas;_acervo=new(usuarioId,dadosLocais);InitializeComponent();
        Categorias.ItemsSource=new[]{"Todas as categorias"}.Concat(aulas.Select(a=>a.Categoria).Distinct().Order());
        Categorias.SelectedIndex=0;_pronto=true;Atualizar();
        Unloaded+=(_,_)=>_player?.Parar();
    }
    private void Filtrar(object sender,RoutedEventArgs e){if(_pronto)Atualizar();}
    private void Atualizar()
    {
        var busca=Busca.Text.Trim();var categoria=Categorias.SelectedItem as string;
        var aulas=_aulas.Where(a=>(string.IsNullOrEmpty(busca) || System.Globalization.CultureInfo.CurrentCulture.CompareInfo.IndexOf(string.Join(" ",a.Funcoes.Prepend(a.Titulo).Append(a.Descricao)),busca,System.Globalization.CompareOptions.IgnoreCase|System.Globalization.CompareOptions.IgnoreNonSpace)>=0)
            && (categoria=="Todas as categorias" || a.Categoria==categoria)
            && (Situacao.SelectedIndex==0 || _acervo.Progresso(a.Id).Concluida==(Situacao.SelectedIndex==2)))
            .Select(a=>new Cartao(a,_acervo.Progresso(a.Id) is {Concluida:true}?"✓ Concluída":_acervo.Progresso(a.Id).Posicao>0?"Continuar aula":"Assistir aula")).ToArray();
        Aulas.ItemsSource=aulas;Vazio.Visibility=aulas.Length==0?Visibility.Visible:Visibility.Collapsed;
        Resumo.Text=$"{aulas.Length} aulas neste filtro · {_aulas.Count(a=>_acervo.Progresso(a.Id).Concluida)} de {_aulas.Count} concluídas";
    }
    private void Abrir(object sender,RoutedEventArgs e)
    {
        if(sender is not Button{Tag:Cartao card})return;
        _player?.Parar();_atual=card.Aula;TituloAula.Text=_atual.Titulo;DescricaoAula.Text=_atual.Descricao;
        Capitulos.ItemsSource=_atual.Capitulos;Detalhe.Visibility=Visibility.Visible;
        _player=new(_atual,_acervo,Atualizar);PlayerHost.Content=_player;Detalhe.BringIntoView();
    }
    private void Fechar(object sender,RoutedEventArgs e){_player?.Parar();PlayerHost.Content=null;_player=null;_atual=null;Detalhe.Visibility=Visibility.Collapsed;Atualizar();}
    private void Concluir(object sender,RoutedEventArgs e){_player?.MarcarConcluida();Atualizar();}
    private void Reiniciar(object sender,RoutedEventArgs e)=>_player?.Buscar(0);
    private void IrCapitulo(object sender,RoutedEventArgs e){if(sender is Button{Tag:double segundos})_player?.Buscar(segundos);}
}
