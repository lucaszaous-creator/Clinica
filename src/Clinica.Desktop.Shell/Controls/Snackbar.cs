using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clinica.Desktop.Controls;

/// <summary>Tipos de snackbar (definem cor e ícone).</summary>
public enum TipoSnackbar { Info, Sucesso, Erro }

/// <summary>
/// Um aviso já exibido — é o que o sino da topbar lista. O snackbar some em 4 segundos
/// por desenho; quem estava olhando o paciente, e não a tela, perdia o "salvo" ou o erro
/// para sempre. O sino é a segunda via desses avisos, desta SESSÃO (nada é gravado).
/// </summary>
public sealed record AvisoRegistrado(string Mensagem, TipoSnackbar Tipo, DateTime Hora)
{
    public string HoraTexto => Hora.ToString("HH:mm");
}

/// <summary>Notificações transitórias não-bloqueantes (substituem MessageBox informativos).</summary>
public interface ISnackbarService
{
    void Sucesso(string mensagem);
    void Erro(string mensagem);
    void Info(string mensagem);
}

/// <summary>
/// Estado observável do snackbar. O MainWindow renderiza um único host bindado a
/// esta instância (exposta pelo MainViewModel); auto-dispensa após 4s.
/// Seguro para chamadas fora da thread de UI (marshala via Dispatcher).
/// </summary>
public sealed partial class SnackbarService : ObservableObject, ISnackbarService
{
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _timer;

    [ObservableProperty]
    private string _mensagem = string.Empty;

    [ObservableProperty]
    private TipoSnackbar _tipo = TipoSnackbar.Info;

    [ObservableProperty]
    private bool _estaVisivel;

    /// <summary>
    /// Últimos avisos da sessão, do mais recente para o mais antigo (teto de 20 — o sino
    /// responde "o que acabou de acontecer", não é trilha; a trilha é a auditoria).
    /// Só o sino da topbar do SHELL consome isto: no faturamento o sino é o de
    /// pendências, com número vindo do banco — lá esta lista não tem leitor.
    /// </summary>
    public ObservableCollection<AvisoRegistrado> Historico { get; } = new();

    /// <summary>Avisos desde a última vez que o sino foi aberto — o número do badge.</summary>
    [ObservableProperty]
    private int _naoLidos;

    private const int TetoHistorico = 20;

    /// <summary>Zera o badge. Chamado pela topbar ao abrir o sino (thread de UI).</summary>
    public void MarcarLidos() => NaoLidos = 0;

    public SnackbarService()
    {
        _dispatcher = Dispatcher.CurrentDispatcher;
        _timer = new DispatcherTimer(DispatcherPriority.Normal, _dispatcher)
        {
            Interval = TimeSpan.FromSeconds(4)
        };
        _timer.Tick += (_, _) => { _timer.Stop(); EstaVisivel = false; };
    }

    public void Sucesso(string mensagem) => Mostrar(mensagem, TipoSnackbar.Sucesso);
    public void Erro(string mensagem) => Mostrar(mensagem, TipoSnackbar.Erro);
    public void Info(string mensagem) => Mostrar(mensagem, TipoSnackbar.Info);

    private void Mostrar(string mensagem, TipoSnackbar tipo)
    {
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.Invoke(() => Mostrar(mensagem, tipo));
            return;
        }

        _timer.Stop();
        Mensagem = mensagem;
        Tipo = tipo;
        EstaVisivel = true;
        _timer.Start();

        // Já estamos na thread de UI (marshalado acima) — a coleção pode ser tocada aqui.
        Historico.Insert(0, new AvisoRegistrado(mensagem, tipo, DateTime.Now));
        while (Historico.Count > TetoHistorico)
            Historico.RemoveAt(Historico.Count - 1);
        NaoLidos++;
    }
}
