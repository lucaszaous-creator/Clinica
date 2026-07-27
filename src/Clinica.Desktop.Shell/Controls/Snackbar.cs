using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clinica.Desktop.Controls;

/// <summary>Tipos de snackbar (definem cor e ícone).</summary>
public enum TipoSnackbar { Info, Sucesso, Erro }

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
    }
}
