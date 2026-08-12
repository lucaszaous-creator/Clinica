using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Clinica.Desktop.Configuracao;

namespace Clinica.Desktop.Controls;

/// <summary>
/// Clicar num dado da tela COPIA aquele dado (parcela 57).
///
/// Por que existe
/// --------------
/// A clínica não vive só neste sistema: a guia é efetivada no portal da operadora, e para
/// isso a secretária retipa o nome do paciente, o telefone, a carteirinha e o número da
/// guia lá do outro lado. Digitar de novo o que já está na tela é onde nasce o erro que o
/// projeto inteiro combate — o "O" no lugar do zero (ver <c>RegraNumeroGuia</c>), o nome
/// com uma letra trocada, o telefone com um dígito a menos. Copiar não erra.
///
/// Como usar
/// ---------
/// <code>
/// &lt;TextBlock Text="{Binding PacienteNome}" Style="{StaticResource CelulaCopiavel}" /&gt;
/// &lt;TextBlock Text="Ver" ctrl:Copiavel.Texto="{Binding Carteirinha}" /&gt;
/// </code>
/// <see cref="TextoProperty"/> vence; sem ele, copia-se o próprio <c>Text</c> do
/// <see cref="TextBlock"/>. É essa segunda forma que permite UM estilo servir a qualquer
/// coluna, sem repetir o binding do valor em cada uma.
/// </summary>
public static class Copiavel
{
    /// <summary>Quantas vezes tentar antes de desistir da área de transferência.</summary>
    private const int Tentativas = 3;

    /// <summary>O elemento copia o PRÓPRIO texto ao ser clicado.</summary>
    public static readonly DependencyProperty AtivoProperty = DependencyProperty.RegisterAttached(
        "Ativo", typeof(bool), typeof(Copiavel), new PropertyMetadata(false, AoLigar));

    public static void SetAtivo(DependencyObject alvo, bool valor) => alvo.SetValue(AtivoProperty, valor);

    public static bool GetAtivo(DependencyObject alvo) => (bool)alvo.GetValue(AtivoProperty);

    /// <summary>
    /// Texto a copiar, explícito. Para quando o que se copia não é o que está escrito —
    /// o telefone só com dígitos, por exemplo, que é como o portal da operadora o quer.
    /// </summary>
    public static readonly DependencyProperty TextoProperty = DependencyProperty.RegisterAttached(
        "Texto", typeof(string), typeof(Copiavel), new PropertyMetadata(null, AoLigar));

    public static void SetTexto(DependencyObject alvo, string? valor) => alvo.SetValue(TextoProperty, valor);

    public static string? GetTexto(DependencyObject alvo) => (string?)alvo.GetValue(TextoProperty);

    private static void AoLigar(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement elemento) return;

        var ligado = GetAtivo(d) || GetTexto(d) is not null;

        elemento.MouseLeftButtonUp -= AoClicar;
        if (!ligado) return;

        elemento.MouseLeftButtonUp += AoClicar;

        // ⚠️ TextBlock SEM fundo só é clicável em cima das LETRAS — o WPF não faz teste de
        // acerto onde não há pintura. Sem isto, clicar no espaço entre o fim do nome e a
        // borda da célula não copiaria nada, e a pessoa concluiria que às vezes funciona e
        // às vezes não. Transparente pinta nada e ainda assim recebe o clique.
        if (elemento is TextBlock { Background: null } bloco)
            bloco.Background = Brushes.Transparent;
    }

    private static void AoClicar(object remetente, MouseButtonEventArgs e)
    {
        if (remetente is not FrameworkElement elemento) return;

        var texto = GetTexto(elemento) ?? (elemento as TextBlock)?.Text;
        if (string.IsNullOrWhiteSpace(texto)) return;   // nada a copiar: não se finge que copiou

        if (TentarCopiar(texto, out var falha))
        {
            Avisar(elemento, "Copiado!", erro: false);
            return;
        }

        // Degradação com rastro (regra do projeto). E ela NUNCA aparece como sucesso: dizer
        // "copiado" e a pessoa colar o conteúdo anterior no portal da operadora é pior do
        // que não ter o recurso — ela cola sem conferir, porque o sistema afirmou que deu
        // certo.
        LogErros.Registrar("Área de transferência — não foi possível copiar", falha);
        Avisar(elemento, "Não deu para copiar — outro programa está usando a área de "
                         + "transferência. Tente de novo.", erro: true);
    }

    /// <summary>
    /// Copia com algumas tentativas.
    ///
    /// A área de transferência do Windows é um recurso ÚNICO da máquina e fica bloqueada
    /// enquanto outro programa a segura — o navegador, o Excel e o próprio portal da
    /// operadora fazem isso o tempo todo, por alguns milissegundos. Uma tentativa só
    /// falharia sozinha de vez em quando, sem motivo aparente para quem está usando.
    /// </summary>
    // NotNullWhen: devolver false garante a exceção preenchida (toda iteração que falha a
    // grava, e há pelo menos uma) — é o que deixa o chamador registrá-la sem aviso CS8604.
    private static bool TentarCopiar(string texto, [NotNullWhen(false)] out Exception? falha)
    {
        falha = null;
        for (var i = 0; i < Tentativas; i++)
        {
            try
            {
                Clipboard.SetDataObject(texto, copy: true);
                return true;
            }
            catch (Exception ex)
            {
                falha = ex;
                // Curto de propósito: é a UI que está parada aqui, e o objetivo é
                // atravessar o instante em que o outro programa solta o recurso.
                System.Threading.Thread.Sleep(60);
            }
        }

        return false;
    }

    /// <summary>
    /// Confirma NO LUGAR DO CLIQUE, e não numa barra no rodapé.
    ///
    /// Quem copia a terceira célula de uma linha está olhando para ela; um aviso que
    /// aparece a quinhentos pixels dali não é lido, e aí a pessoa clica de novo sem saber
    /// se funcionou.
    /// </summary>
    private static void Avisar(FrameworkElement alvo, string mensagem, bool erro)
    {
        // A dica de ajuda do próprio elemento é suspensa enquanto o aviso está no ar:
        // duas tooltips abertas em cima uma da outra escondem justamente a que importa.
        var tinhaDica = ToolTipService.GetIsEnabled(alvo);
        ToolTipService.SetIsEnabled(alvo, false);

        var aviso = new ToolTip
        {
            Content = mensagem,
            PlacementTarget = alvo,
            Placement = PlacementMode.Top,
            StaysOpen = true,
            IsOpen = true
        };

        var relogio = new DispatcherTimer
        {
            // O erro fica mais tempo: ele pede uma ação (tentar de novo), e some antes
            // de ser lido é o mesmo que não avisar.
            Interval = TimeSpan.FromMilliseconds(erro ? 3200 : 900)
        };

        relogio.Tick += (_, _) =>
        {
            relogio.Stop();
            aviso.IsOpen = false;
            ToolTipService.SetIsEnabled(alvo, tinhaDica);
        };

        relogio.Start();
    }
}
