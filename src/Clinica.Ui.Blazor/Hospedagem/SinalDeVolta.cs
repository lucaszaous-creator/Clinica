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

/// <summary>
/// Os atalhos de teclado que chegam à tela à vista (<see cref="TelaBase.AoAtalhoAsync"/>). Os três primeiros valem para
/// toda tela (o que o faturamento WPF roteava: Ctrl+S, Ctrl+P, F5). Os três últimos (24/11/2026, F1b do "CRM com cara de
/// WhatsApp") só chegam à tela que os DECLARA no elemento raiz — <c>data-atalhos="buscar nova fechar"</c> (js/janela.js):
/// <list type="bullet">
/// <item><see cref="Buscar"/>: Ctrl+F — a busca DA TELA (sem a declaração, Ctrl+F é a busca de telas da moldura);</item>
/// <item><see cref="Nova"/>: Ctrl+Alt+N — o "novo" da tela (a nova conversa, no WhatsApp);</item>
/// <item><see cref="Fechar"/>: Esc — fechar o que a tela tem aberto, do mais de cima para baixo. O Esc não é engolido:
/// campo e moldura continuam a ouvi-lo, então a tela que o declara não o trata também no campo.</item>
/// </list>
/// </summary>
public enum AtalhoDeTela { Salvar, Imprimir, Atualizar, Buscar, Nova, Fechar }
