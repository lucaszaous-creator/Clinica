namespace Clinica.Ui.Blazor.Hospedagem;

/// <summary>
/// O "snackbar" das telas Blazor: confirmação PASSAGEIRA de ação que não tem lugar natural
/// na tela. A regra do projeto continua valendo — erro que exige correção fica INLINE,
/// perto do campo; aqui vai o "Lançamento registrado." e a falha de uma ação de linha.
///
/// O erro fica mais tempo (8 s contra 4 s): sumir antes de ser lido é o defeito do snackbar
/// do faturamento que a parcela 53 já pagou.
/// </summary>
public sealed class Notificacoes
{
    private readonly List<Notificacao> _ativas = [];
    private int _proximo;

    public IReadOnlyList<Notificacao> Ativas => _ativas;
    public event Action? Mudou;

    public void Sucesso(string texto) => Mostrar(texto, TomNotificacao.Sucesso, 4000);
    public void Info(string texto) => Mostrar(texto, TomNotificacao.Info, 4000);
    public void Erro(string texto) => Mostrar(texto, TomNotificacao.Erro, 8000);

    private void Mostrar(string texto, TomNotificacao tom, int ms)
    {
        var n = new Notificacao(++_proximo, texto, tom);
        _ativas.Add(n);
        if (_ativas.Count > 3) _ativas.RemoveAt(0);
        Mudou?.Invoke();
        _ = FecharDepois(n, ms);
    }

    private async Task FecharDepois(Notificacao n, int ms)
    {
        await Task.Delay(ms);
        Fechar(n);
    }

    public void Fechar(Notificacao n)
    {
        if (_ativas.Remove(n)) Mudou?.Invoke();
    }
}

public enum TomNotificacao { Sucesso, Info, Erro }

public sealed record Notificacao(int Id, string Texto, TomNotificacao Tom);
