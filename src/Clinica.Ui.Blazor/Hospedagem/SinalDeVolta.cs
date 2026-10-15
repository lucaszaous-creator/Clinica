namespace Clinica.Ui.Blazor.Hospedagem;

/// <summary>
/// "A pessoa voltou a esta aba" — o equivalente Blazor do <c>IAtualizarAoVoltar</c> do WPF
/// (out/2026). O hospedeiro avisa (o shell guarda a aba montada e, ao voltar, pede a
/// releitura); a TelaBase repassa à tela pelo <see cref="TelaBase.AoVoltarAsync"/>. Sem
/// isto, a aba voltaria com o que mostrava quando saiu — o lançamento feito na outra
/// máquina não aparecia até alguém clicar em algo.
/// </summary>
public sealed class SinalDeVolta
{
    public event Action? Voltou;

    public void Avisar() => Voltou?.Invoke();
}
