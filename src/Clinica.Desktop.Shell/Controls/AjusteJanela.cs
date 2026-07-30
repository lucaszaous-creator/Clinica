using System.Windows;

namespace Clinica.Desktop.Controls;

/// <summary>
/// Mantém TODA janela da suíte dentro da área útil do monitor.
///
/// O app de faturamento já resolvia isto desde cedo (`MainWindow.AjustarParaTela`), e o
/// comentário de lá conta o sintoma: em tela menor que o padrão, "a última coluna das
/// tabelas ficava passando da tela". A suíte nasceu sem esse ajuste — a `ShellWindow`
/// abre com 1280×760 e o monitor do balcão é 1366×**768**: descontada a barra de tarefas,
/// a janela já nascia mais alta que a tela, e o rodapé (onde ficam os botões dos
/// formulários) caía atrás da barra.
///
/// Pior nos diálogos: quase todos usam `SizeToContent="Height"`, então **crescem com o
/// conteúdo** sem limite nenhum. Com a escala do Windows em 150% ou 175% — comum em
/// notebook —, um formulário de 600px vira 1050px físicos e some pela borda de baixo.
///
/// Por que um handler de CLASSE e não uma chamada em cada janela: estilo implícito de
/// `Window` não pega janela derivada (o WPF procura o recurso pela chave do tipo CONCRETO,
/// e todas as nossas são subclasses), e pedir uma linha no construtor de cada janela é
/// pedir que a próxima esqueça. Registrado uma vez na abertura, vale para as vinte que
/// existem hoje e para as que vierem.
///
/// ⚠️ Não usar `MaxWidth`/`MaxHeight` para isto: com eles definidos, MAXIMIZAR a janela a
/// mantém menor que o quadro do Windows e sobram faixas pretas em volta. É a mesma nota
/// que está no faturamento — o erro já foi cometido uma vez.
/// </summary>
public static class AjusteJanela
{
    private static bool _instalado;

    /// <summary>
    /// Liga o ajuste para todas as janelas do processo. Chamado uma vez, na abertura.
    /// </summary>
    public static void Instalar()
    {
        if (_instalado) return;
        _instalado = true;

        EventManager.RegisterClassHandler(
            typeof(Window),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler((remetente, _) =>
            {
                if (remetente is Window janela) Ajustar(janela);
            }));
    }

    /// <summary>
    /// Encolhe a janela até a área útil e a recentraliza. Só age quando ela passa —
    /// janela que cabe fica exatamente onde o autor pediu.
    /// </summary>
    public static void Ajustar(Window janela)
    {
        // Maximizada ou minimizada, quem manda no tamanho é o Windows.
        if (janela.WindowState != WindowState.Normal) return;

        var util = SystemParameters.WorkArea;

        // A moldura (barra de título e bordas) não entra em ActualWidth/ActualHeight, mas
        // ocupa tela. Sem descontá-la, uma janela "do tamanho da área útil" ainda vaza.
        var larguraMax = util.Width;
        var alturaMax = util.Height;

        var largura = janela.ActualWidth > 0 ? janela.ActualWidth : janela.Width;
        var altura = janela.ActualHeight > 0 ? janela.ActualHeight : janela.Height;

        if (double.IsNaN(largura) || double.IsNaN(altura)) return;

        var precisaEncolher = largura > larguraMax || altura > alturaMax;
        if (!precisaEncolher) return;

        // `SizeToContent` reexpandiria a janela logo depois de encolhermos: quem cresce
        // com o conteúdo tem de parar de crescer para o limite valer.
        janela.SizeToContent = SizeToContent.Manual;

        if (largura > larguraMax) janela.Width = larguraMax;
        if (altura > alturaMax) janela.Height = alturaMax;

        // Recentraliza DENTRO da área útil: encolher sem mover deixaria a janela colada
        // na borda de cima, com metade do formulário fora do lado direito.
        janela.Left = util.Left + Math.Max((larguraMax - janela.Width) / 2, 0);
        janela.Top = util.Top + Math.Max((alturaMax - janela.Height) / 2, 0);
    }
}
