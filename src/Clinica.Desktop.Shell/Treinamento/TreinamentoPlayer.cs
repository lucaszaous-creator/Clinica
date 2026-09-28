using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Input;

namespace Clinica.Desktop.Shell.Treinamento;

public sealed class TreinamentoPlayer : UserControl
{
    private readonly AulaTreinamento _aula;
    private readonly AcervoTreinamento _acervo;
    private readonly Action _atualizado;
    private readonly MediaElement _video=new(){LoadedBehavior=MediaState.Manual,UnloadedBehavior=MediaState.Manual,Stretch=Stretch.Uniform,Volume=.8};
    private readonly TextBlock _mensagem=new(){Foreground=Brushes.White,FontSize=18,TextWrapping=TextWrapping.Wrap,Margin=new(28),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
    private readonly Slider _posicao=new(){Minimum=0,Margin=new(12,0,12,0),MinWidth=100};
    private readonly TextBlock _tempo=new(){VerticalAlignment=VerticalAlignment.Center,MinWidth=110,Foreground=Brushes.White};
    private readonly Button _play=new(){Content="Pausar",MinWidth=90,Margin=new(0,0,10,0)};
    private readonly DispatcherTimer _timer=new(){Interval=TimeSpan.FromMilliseconds(400)};
    private readonly Grid _root=new(){Background=new SolidColorBrush(Color.FromRgb(20,40,68))};
    private CancellationTokenSource? _cancel;
    private bool _aberto,_tocando,_sliderAtualizando,_parado;
    private int _ultimoSalvo;
    private double? _buscarQuandoAbrir;
    private Window? _cheia;

    public TreinamentoPlayer(AulaTreinamento aula,AcervoTreinamento acervo,Action atualizado)
    {
        _aula=aula;_acervo=acervo;_atualizado=atualizado;Height=530;
        _root.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});
        _root.RowDefinitions.Add(new(){Height=GridLength.Auto});
        _root.RowDefinitions.Add(new(){Height=GridLength.Auto});
        var area=new Grid{MinHeight=250};area.Children.Add(_video);area.Children.Add(_mensagem);_root.Children.Add(area);
        var barra=new DockPanel{Margin=new(12)};Grid.SetRow(barra,1);_root.Children.Add(barra);
        var cheia=new Button{Content="Tela cheia",Margin=new(8,0,0,0)};DockPanel.SetDock(cheia,Dock.Right);barra.Children.Add(cheia);cheia.Click+=(_,_)=>TelaCheia();
        var volume=new Slider{Minimum=0,Maximum=1,Value=.8,Width=75,Margin=new(10,0,10,0),VerticalAlignment=VerticalAlignment.Center,ToolTip="Volume"};System.Windows.Automation.AutomationProperties.SetName(volume,"Volume da aula");DockPanel.SetDock(volume,Dock.Right);barra.Children.Add(volume);volume.ValueChanged+=(_,_)=>_video.Volume=volume.Value;
        DockPanel.SetDock(_tempo,Dock.Right);barra.Children.Add(_tempo);
        DockPanel.SetDock(_play,Dock.Left);barra.Children.Add(_play);_play.Click+=(_,_)=>{if(!_aberto){_ = CarregarAsync();return;}if(_tocando){_video.Pause();_tocando=false;_play.Content="Reproduzir";}else{_video.Play();_tocando=true;_play.Content="Pausar";}};
        barra.Children.Add(_posicao);System.Windows.Automation.AutomationProperties.SetName(_posicao,"Posição da aula");
        _posicao.ValueChanged+=(_,_)=>{if(_aberto&&!_sliderAtualizando)_video.Position=TimeSpan.FromSeconds(_posicao.Value);};
        var dica=new TextBlock{Text="Voz Bella · Legendas incorporadas · Telas reais com dados fictícios",Foreground=Brushes.White,Margin=new(14,0,14,10),TextWrapping=TextWrapping.Wrap};Grid.SetRow(dica,2);_root.Children.Add(dica);
        Content=_root;
        _video.MediaOpened+=(_,_)=>
        {
            if(_parado)return;
            _aberto=true;_mensagem.Visibility=Visibility.Collapsed;_posicao.Maximum=_video.NaturalDuration.HasTimeSpan?_video.NaturalDuration.TimeSpan.TotalSeconds:_aula.Duracao;
            var p=_acervo.Progresso(_aula.Id);_video.Position=TimeSpan.FromSeconds(Math.Clamp(_buscarQuandoAbrir??(p.Concluida?0:p.Posicao),0,Math.Max(0,_posicao.Maximum-1)));
            _buscarQuandoAbrir=null;_video.Play();_tocando=true;_play.Content="Pausar";_timer.Start();
        };
        _video.MediaFailed+=(_,_)=>Erro("Não foi possível reproduzir o vídeo. Clique em Tentar novamente. Se persistir, confira os componentes de mídia do Windows.");
        _video.MediaEnded+=(_,_)=>{_tocando=false;_play.Content="Reproduzir";MarcarConcluida();_video.Position=TimeSpan.Zero;};
        _timer.Tick+=(_,_)=>
        {
            if(!_aberto)return;
            _sliderAtualizando=true;_posicao.Value=_video.Position.TotalSeconds;_sliderAtualizando=false;
            _tempo.Text=$"{_video.Position:mm\\:ss} / {TimeSpan.FromSeconds(_posicao.Maximum):mm\\:ss}";
            if((int)_video.Position.TotalSeconds/5!=_ultimoSalvo){_ultimoSalvo=(int)_video.Position.TotalSeconds/5;Salvar();}
        };
        Loaded+=async(_,_)=>{if(!_aberto)await CarregarAsync();};
        Unloaded+=(_,_)=>Parar();
    }
    private async Task CarregarAsync()
    {
        _cancel?.Cancel();_cancel?.Dispose();var cancel=_cancel=new();_parado=false;_aberto=false;
        _video.Stop();_video.Source=null;_timer.Stop();_mensagem.Text="Carregando vídeo…";_mensagem.Visibility=Visibility.Visible;_play.IsEnabled=false;
        try
        {
            var path=await _acervo.ObterVideoAsync(_aula,cancel.Token);
            if(cancel.IsCancellationRequested||_parado)return;
            _video.Source=new Uri(path);_video.Play();
        }
        catch(OperationCanceledException) when(cancel.IsCancellationRequested){}
        catch(OperationCanceledException){Erro("O carregamento demorou mais que o esperado. Confira a conexão e clique em Tentar novamente.");}
        catch(Exception e) when(e is IOException or System.Net.Http.HttpRequestException or InvalidOperationException or UnauthorizedAccessException)
        {if(!cancel.IsCancellationRequested)Erro("Não foi possível carregar esta aula. Confira a conexão e clique em Tentar novamente. O progresso foi mantido.");}
        finally{if(ReferenceEquals(_cancel,cancel))_play.IsEnabled=true;}
    }
    private void Erro(string mensagem){_video.Stop();_timer.Stop();_aberto=false;_tocando=false;_mensagem.Text=mensagem;_mensagem.Visibility=Visibility.Visible;_play.Content="Tentar novamente";_play.IsEnabled=true;}
    public void Buscar(double segundos){if(_aberto){_video.Position=TimeSpan.FromSeconds(Math.Clamp(segundos,0,Math.Max(0,_posicao.Maximum-1)));_video.Play();_tocando=true;_play.Content="Pausar";}else _buscarQuandoAbrir=segundos;}
    public void MarcarConcluida(){Salvar(true);_atualizado();}
    private void Salvar(bool? concluida=null)
    {
        if(!_aberto&&concluida is null)return;
        try{var p=_acervo.Progresso(_aula.Id);_acervo.Salvar(_aula.Id,_aberto?_video.Position.TotalSeconds:p.Posicao,concluida??p.Concluida);}
        catch(Exception e) when(e is IOException or UnauthorizedAccessException){_tempo.ToolTip="Não foi possível salvar o progresso neste computador.";}
    }
    public void Parar(){if(_parado)return;_parado=true;Salvar();_cancel?.Cancel();_timer.Stop();_video.Stop();_video.Source=null;_aberto=false;_tocando=false;if(_cheia is not null)_cheia.Close();}
    private void TelaCheia()
    {
        if(_cheia is not null){_cheia.Close();return;}
        Content=null;
        var janela=_cheia=new Window{Title=_aula.Titulo,Owner=Window.GetWindow(this),WindowState=WindowState.Maximized,WindowStyle=WindowStyle.None,Background=Brushes.Black,Content=_root};
        janela.KeyDown+=(_,e)=>{if(e.Key==Key.Escape)janela.Close();};
        janela.Closed+=(_,_)=>{janela.Content=null;Content=_root;_cheia=null;};janela.Show();
    }
}
