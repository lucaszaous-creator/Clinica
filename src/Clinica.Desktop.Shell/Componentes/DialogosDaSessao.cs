using Clinica.Desktop.Controls;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>Apresentação assíncrona de diálogos, isolada no fluxo que a solicitou.</summary>
public interface IDialogosDaSessao
{
    Task<bool?> AbrirAsync(string tipo, object viewModel);
    Task<string?> PerguntarTextoAsync(string titulo, string pergunta, string? textoInicial, bool obrigatorio);
    Task<bool> ConfirmarAsync(string titulo, string mensagem, bool perigoso);
    Task AvisoAsync(string titulo, string mensagem);
}

/// <summary>
/// O shell nativo continua como fallback. A sessão web instala seu apresentador somente
/// durante comandos e callbacks de formulário; não há estado global de janela nem loop bloqueante.
/// </summary>
public static class DialogosDaSessao
{
    private static readonly AsyncLocal<IDialogosDaSessao?> Apresentador = new();
    public static IDisposable Usar(IDialogosDaSessao apresentador)
    {
        var anterior = Apresentador.Value;
        Apresentador.Value = apresentador;
        return new Escopo(anterior);
    }

    public static Task<bool?> AbrirAsync(string tipo, object viewModel, Func<bool?> nativo) =>
        Apresentador.Value is { } atual ? atual.AbrirAsync(tipo, viewModel) : Task.FromResult(nativo());

    public static Task<string?> PerguntarTextoAsync(IDialogoService nativo, string titulo, string pergunta,
        string? textoInicial = null, bool obrigatorio = true) =>
        Apresentador.Value is { } atual ? atual.PerguntarTextoAsync(titulo, pergunta, textoInicial, obrigatorio)
            : Task.FromResult(nativo.PerguntarTexto(titulo, pergunta, textoInicial, obrigatorio));

    public static Task<bool> ConfirmarAsync(IDialogoService nativo, string titulo, string mensagem) =>
        Apresentador.Value is { } atual ? atual.ConfirmarAsync(titulo, mensagem, false)
            : Task.FromResult(nativo.Confirmar(titulo, mensagem));

    public static Task<bool> ConfirmarPerigoAsync(IDialogoService nativo, string titulo, string mensagem) =>
        Apresentador.Value is { } atual ? atual.ConfirmarAsync(titulo, mensagem, true)
            : Task.FromResult(nativo.ConfirmarPerigo(titulo, mensagem));

    public static Task AvisoAsync(IDialogoService nativo, string titulo, string mensagem)
    {
        if (Apresentador.Value is { } atual) return atual.AvisoAsync(titulo, mensagem);
        nativo.Aviso(titulo, mensagem);
        return Task.CompletedTask;
    }

    private sealed class Escopo(IDialogosDaSessao? anterior) : IDisposable
    {
        private bool _fechado;
        public void Dispose()
        {
            if (_fechado) return;
            _fechado = true;
            Apresentador.Value = anterior;
        }
    }
}
