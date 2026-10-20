using System.Windows;
using Clinica.Desktop.Shell.Componentes;

namespace Clinica.Desktop.Controls;

/// <summary>
/// Diálogos bloqueantes (confirmação/aviso) abstraídos para os ViewModels não
/// dependerem de MessageBox diretamente (testabilidade e ponto único de estilo).
/// Snackbar (ISnackbarService) continua sendo o canal para mensagens informativas.
/// </summary>
public interface IDialogoService
{
    /// <summary>Pergunta Sim/Não neutra. Retorna true se o usuário confirmar.</summary>
    bool Confirmar(string titulo, string mensagem);

    /// <summary>Pergunta Sim/Não para ação destrutiva/perigosa (ícone de alerta).</summary>
    bool ConfirmarPerigo(string titulo, string mensagem);

    /// <summary>Aviso simples com OK.</summary>
    void Aviso(string titulo, string mensagem);

    /// <summary>
    /// Pede um texto ao usuário. Retorna <c>null</c> quando ele DESISTE (Cancelar/Esc) — e
    /// nesse caso o chamador não deve seguir com a ação.
    ///
    /// ⚠️ Com <paramref name="obrigatorio"/> em <c>false</c> a janela aceita resposta em
    /// branco e devolve <see cref="string.Empty"/>: é isso que separa "desisti" de "siga,
    /// sem texto". Pergunta que anuncia o campo como opcional PRECISA passar <c>false</c> —
    /// senão a janela recusa o vazio e sobra ao usuário o Cancelar, que quer dizer o
    /// contrário do que o chamador vai entender.
    /// </summary>
    string? PerguntarTexto(string titulo, string pergunta, string? textoInicial = null,
                           bool obrigatorio = true);
}

/// <summary>
/// As perguntas e avisos do shell — em HTML, na moldura do app, sempre que dá (out/2026).
///
/// Desde que a moldura é Blazor, a pergunta feita com a janela principal à frente sai na
/// camada de diálogos do navegador (<see cref="Clinica.Ui.Blazor.Hospedagem.Dialogos"/>), com a
/// mesma cara das telas — e a <c>PromptWindow</c> deixou de ser a regra. Duas situações ainda
/// caem na caixa nativa, e as duas são de propósito:
///
/// 1. A pergunta vem de DENTRO de uma janela WPF que ainda abre por cima do app (área
///    congelada, documentos, cadastro): a janela modal desabilita a principal, e uma pergunta
///    desenhada no navegador dela ficaria atrás, sem ninguém poder clicar.
/// 2. A pergunta é feita no MEIO de um evento ou de uma renderização do próprio Blazor: o
///    renderizador só desenha o diálogo quando o evento termina, e esperar a resposta ali
///    dentro travaria a tela para sempre. É raro (as pontes das telas Blazor abrem janelas
///    WPF, e a pergunta nasce dentro delas), e o desfecho seguro é a caixa nativa.
///
/// O contrato continua SÍNCRONO — os ViewModels WPF esperam a resposta na linha seguinte —, e
/// por isso a espera pelo diálogo HTML é um laço de mensagens aninhado (o mesmo que o
/// <c>ShowDialog</c> faz por baixo).
/// </summary>
public sealed class DialogoService : IDialogoService
{
    private Clinica.Ui.Blazor.Hospedagem.Dialogos? _html;
    private Window? _janela;

    /// <summary>A moldura entrega os diálogos do navegador da janela principal.</summary>
    public void LigarMoldura(Clinica.Ui.Blazor.Hospedagem.Dialogos dialogos, Window janela)
    {
        _html = dialogos;
        _janela = janela;
    }

    /// <summary>A moldura saiu (janela fechada): volta tudo para a caixa nativa.</summary>
    public void Desligar()
    {
        _html = null;
        _janela = null;
    }

    public bool Confirmar(string titulo, string mensagem) =>
        NoHtml(d => d.ConfirmarAsync(titulo, mensagem), out var sim)
            ? sim
            : MessageBox.Show(mensagem, titulo, MessageBoxButton.YesNo, MessageBoxImage.Question)
              == MessageBoxResult.Yes;

    public bool ConfirmarPerigo(string titulo, string mensagem) =>
        NoHtml(d => d.ConfirmarAsync(titulo, mensagem, perigoso: true), out var sim)
            ? sim
            : MessageBox.Show(mensagem, titulo, MessageBoxButton.YesNo, MessageBoxImage.Warning)
              == MessageBoxResult.Yes;

    public void Aviso(string titulo, string mensagem)
    {
        if (NoHtml(async d => { await d.AvisoAsync(titulo, mensagem); return true; }, out _)) return;
        MessageBox.Show(mensagem, titulo, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public string? PerguntarTexto(string titulo, string pergunta, string? textoInicial = null,
                                 bool obrigatorio = true)
    {
        // Cancelar continua sendo NULL nas duas formas (checagem 39): a camada HTML devolve
        // null ao desistir, e a PromptWindow também.
        if (NoHtml(d => d.PerguntarTextoAsync(titulo, pergunta, textoInicial, obrigatorio), out var resposta))
            return resposta;

        var janela = new PromptWindow(titulo, pergunta, textoInicial, obrigatorio)
        {
            // Qualificado: dentro de Clinica.*, "Application" é o namespace Clinica.Application.
            Owner = JanelaDona.Atual()
        };
        return janela.ShowDialog() == true ? janela.Resposta : null;
    }

    /// <summary>
    /// Faz a pergunta no navegador e espera a resposta — quando dá (ver o cabeçalho). Falso
    /// quando a pergunta tem de ir para a caixa nativa.
    /// </summary>
    private bool NoHtml<T>(Func<Clinica.Ui.Blazor.Hospedagem.Dialogos, Task<T>> perguntar, out T resposta)
    {
        resposta = default!;
        var despachante = System.Windows.Application.Current?.Dispatcher;
        if (_html is not { } dialogos || _janela is not { IsLoaded: true } janela || despachante is null) return false;
        if (!despachante.CheckAccess())
        {
            var (usou, valor) = despachante.Invoke(() => (NoHtml(perguntar, out var v), v));
            resposta = valor;
            return usou;
        }
        if (!ReferenceEquals(JanelaDona.Atual(), janela) || DentroDoBlazor()) return false;

        var tarefa = perguntar(dialogos);
        if (!tarefa.IsCompleted)
        {
            var quadro = new System.Windows.Threading.DispatcherFrame();
            tarefa.ContinueWith(_ => quadro.Continue = false, TaskScheduler.Default);
            System.Windows.Threading.Dispatcher.PushFrame(quadro);
        }
        resposta = tarefa.GetAwaiter().GetResult();
        return true;
    }

    /// <summary>
    /// A chamada nasceu dentro de um evento ou de uma renderização do Blazor? Ali o diálogo só
    /// seria desenhado depois de o evento terminar — e o evento estaria esperando o diálogo.
    /// </summary>
    private static bool DentroDoBlazor()
        => new System.Diagnostics.StackTrace(false).GetFrames().Any(f =>
            f.GetMethod()?.DeclaringType is { } tipo
            && typeof(Microsoft.AspNetCore.Components.RenderTree.Renderer).IsAssignableFrom(tipo));
}
