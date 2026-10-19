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

    /// <summary>
    /// "A tela saiu de vista" (out/2026, P0 da tela do paciente). As telas ficam MONTADAS
    /// quando a pessoa troca de aba, e o que corre por conta própria — o cronômetro da
    /// sessão, que bate a cada segundo — continuaria correndo escondido. Quem avisa é o
    /// palco, ao trocar a tela à vista; a TelaBase repassa pelo <see cref="TelaBase.AoSairAsync"/>.
    /// </summary>
    public event Action? Saiu;

    public void AvisarSaida() => Saiu?.Invoke();

    /// <summary>
    /// Atalho de teclado da tela À VISTA (Ctrl+S, Ctrl+P, F5) — o equivalente do
    /// <c>IAtalhosDeTela</c> do faturamento WPF. Quem escuta é a <see cref="TelaBase"/>; quem
    /// avisa é o palco, que sabe qual tela está à vista.
    /// </summary>
    public event Func<AtalhoDeTela, Task>? Atalho;

    public Task AvisarAtalhoAsync(AtalhoDeTela atalho) => Atalho?.Invoke(atalho) ?? Task.CompletedTask;
}

/// <summary>Os três atalhos globais que o faturamento WPF roteava para a tela ativa.</summary>
public enum AtalhoDeTela { Salvar, Imprimir, Atualizar }
