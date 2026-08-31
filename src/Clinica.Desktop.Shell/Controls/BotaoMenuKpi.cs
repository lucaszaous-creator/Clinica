using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;

namespace Clinica.Desktop.Controls;

/// <summary>
/// O menu "⋯" do cartão de KPI (pedido da direção, ago/2026 — o `KpiCard` do handoff
/// traz o botão na linha do rótulo, à direita).
///
/// O menu mora em CÓDIGO, não em XAML (a regra da parcela 58): um ContextMenu declarado
/// vive num Popup, fora da árvore visual, e os comandos precisariam de
/// `PlacementTarget.Tag` para chegar ao ViewModel — binding que erra o caminho falha em
/// runtime, calado. Em código, o compilar-sombra pega.
///
/// A ação é COPIAR — o mesmo motivo do `Copiavel` do faturamento (parcela 57): o número
/// que a direção leva para a reunião e o valor que se cola na planilha se retipam, e
/// retipar é onde nasce o erro. Menu sem ação é botão que não faz nada (parcela 41), e
/// foi por isso que o "⋯" ficou de fora até a ação existir.
///
/// O botão descobre o rótulo e o valor SOZINHO, comparando o `Style` de cada TextBlock
/// do cartão com os recursos `CardKpi.Rotulo`/`CardKpi.Valor` — zero fiação por cartão,
/// o que é o único jeito de 85 cartões não divergirem. Estilo é recurso COMPARTILHADO:
/// a comparação é por referência, e vale em qualquer tela dos dois design systems.
/// </summary>
public sealed class BotaoMenuKpi : Button
{
    private const int Tentativas = 4;

    public BotaoMenuKpi()
    {
        // O menu abre no CLIQUE (não só no botão direito): é um botão, e o kit o desenha
        // como botão. ToolTip para quem paira sem clicar.
        ToolTip = "Opções do cartão";
    }

    protected override void OnClick()
    {
        base.OnClick();

        var cartao = AncestralCartao();
        var rotulo = TextoDoEstilo(cartao, "CardKpi.Rotulo");
        var valor = TextoDoEstilo(cartao, "CardKpi.Valor");

        var menu = new ContextMenu
        {
            PlacementTarget = this,
            Placement = PlacementMode.Bottom
        };

        if (string.IsNullOrWhiteSpace(valor) || valor == "—")
        {
            // "—" é "não medido": copiar um travessão não serve a ninguém, e o item
            // desabilitado DIZ por quê em vez de sumir (a metade visível, parcela 41).
            menu.Items.Add(new MenuItem { Header = "Nada para copiar — valor não medido", IsEnabled = false });
        }
        else
        {
            var soValor = new MenuItem { Header = "Copiar valor" };
            soValor.Click += (_, _) => Copiar(valor);
            menu.Items.Add(soValor);

            if (!string.IsNullOrWhiteSpace(rotulo))
            {
                var par = new MenuItem { Header = "Copiar rótulo e valor" };
                par.Click += (_, _) => Copiar($"{rotulo}: {valor}");
                menu.Items.Add(par);
            }
        }

        menu.IsOpen = true;
    }

    /// <summary>O Border do cartão: o ancestral cujo Style é o recurso CardKpi.</summary>
    private Border? AncestralCartao()
    {
        var estiloCartao = TryFindResource("CardKpi") as Style;
        DependencyObject? atual = this;
        Border? primeiroBorder = null;

        while (atual is not null)
        {
            if (atual is Border b)
            {
                primeiroBorder ??= b;
                if (estiloCartao is not null && ReferenceEquals(b.Style, estiloCartao)) return b;
            }
            atual = VisualTreeHelper.GetParent(atual);
        }

        // Sem o estilo resolvido (tema fora do design system), o Border mais próximo é o
        // melhor palpite disponível — pior seria o menu abrir sem item nenhum.
        return primeiroBorder;
    }

    /// <summary>O texto do primeiro TextBlock do cartão com o estilo pedido.</summary>
    private string? TextoDoEstilo(DependencyObject? raiz, string chaveDoEstilo)
    {
        if (raiz is null) return null;
        if (TryFindResource(chaveDoEstilo) is not Style estilo) return null;

        var fila = new Queue<DependencyObject>();
        fila.Enqueue(raiz);
        while (fila.Count > 0)
        {
            var atual = fila.Dequeue();
            if (atual is TextBlock tb && ReferenceEquals(tb.Style, estilo)
                && !string.IsNullOrWhiteSpace(tb.Text))
                return tb.Text.Trim();

            var filhos = VisualTreeHelper.GetChildrenCount(atual);
            for (var i = 0; i < filhos; i++)
                fila.Enqueue(VisualTreeHelper.GetChild(atual, i));
        }

        return null;
    }

    /// <summary>
    /// Copia com algumas tentativas — a área de transferência é recurso ÚNICO da máquina
    /// e outro programa pode estar segurando-a (a lição do Copiavel, parcela 57). E falha
    /// NUNCA aparece como sucesso: dizer "copiado" e a pessoa colar o conteúdo anterior é
    /// pior do que não ter o recurso.
    /// </summary>
    private void Copiar(string texto)
    {
        Exception? falha = null;
        for (var i = 0; i < Tentativas; i++)
        {
            try
            {
                Clipboard.SetDataObject(texto, copy: true);
                Avisar("Copiado!");
                return;
            }
            catch (Exception ex)
            {
                falha = ex;
                System.Threading.Thread.Sleep(60);
            }
        }

        Clinica.Application.Diagnostico.Registrar(
            "Cartão de KPI — não foi possível copiar", falha!);
        Avisar("Não deu para copiar — outro programa está usando a área de transferência.");
    }

    /// <summary>Confirma NO LUGAR DO CLIQUE — quem clicou está olhando para cá.</summary>
    private void Avisar(string texto)
    {
        var dica = new ToolTip
        {
            Content = texto,
            PlacementTarget = this,
            Placement = PlacementMode.Bottom,
            IsOpen = true,
            StaysOpen = false
        };

        var relogio = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.6) };
        relogio.Tick += (_, _) =>
        {
            relogio.Stop();
            dica.IsOpen = false;
        };
        relogio.Start();
    }
}
