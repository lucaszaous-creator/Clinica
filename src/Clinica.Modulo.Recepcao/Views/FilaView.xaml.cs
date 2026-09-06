using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Clinica.Domain.Entities;
using Clinica.Recepcao.ViewModels;

namespace Clinica.Recepcao.Views;

/// <summary>
/// A agenda do dia do balcão, em lista (set/2026).
///
/// A View liga e desliga o relógio que envelhece os tempos de espera e relê o dia — é
/// por ele que a chamada feita no consultório chega ao balcão (parcela 38). O shell
/// constrói uma tela nova a cada navegação, e um <c>DispatcherTimer</c> rodando manteria
/// vivo cada ViewModel já trocado (e faria vários irem ao banco pela mesma coisa).
///
/// O menu "⋯" mora aqui, em código, e não em XAML, de propósito: um <c>ContextMenu</c>
/// declarado vive num <c>Popup</c>, fora da árvore visual desta tela — os comandos
/// precisariam de <c>PlacementTarget.Tag</c> para chegar ao ViewModel, e binding que erra
/// o caminho falha em RUNTIME, calado (a categoria que nenhuma rede local pega). Em
/// código, o <c>compilar-sombra</c> pega.
///
/// O arrasto entre raias (parcelas 58 e 87) saiu junto das raias: numa lista ordenada
/// pela hora não há para onde arrastar, e o passo seguinte é o botão de cada linha.
/// </summary>
public partial class FilaView : UserControl
{
    public FilaView()
    {
        InitializeComponent();

        Loaded += (_, _) => (DataContext as FilaViewModel)?.AoEntrarEmCena();
        Unloaded += (_, _) => (DataContext as FilaViewModel)?.AoSairDeCena();
    }

    // ==================== O "⋯" da linha ====================

    /// <summary>
    /// As ações de exceção da linha: o termo, o fechamento, voltar etapa, falta,
    /// cancelamento — e a porta para a GRADE, onde mora a janela do horário (remarcar,
    /// reabrir, comprovante, WhatsApp).
    ///
    /// ⚠️ A visibilidade de cada item é ESTADO **e** PERMISSÃO. O bloco não segue um
    /// `IsEnabled` só porque os atos pedem bits diferentes — colher o termo é
    /// `ColherAssinaturaPaciente` (a técnica de enfermagem o tem e não tem o da agenda),
    /// mover a fila é `EditarAgenda` OU `MovimentarFila`, e falta/cancelamento são do
    /// balcão, `EditarAgenda` estrito. Sem esta metade, o item aparecia aceso e a recusa
    /// só chegava depois do clique.
    /// </summary>
    private void AoAbrirMenuDaLinha(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement botao) return;
        if (botao.DataContext is not CartaoFila cartao) return;
        if (DataContext is not FilaViewModel vm) return;

        var menu = new ContextMenu
        {
            PlacementTarget = botao,
            Placement = PlacementMode.Bottom
        };

        void Acrescentar(string rotulo, System.Windows.Input.ICommand comando, bool visivel)
        {
            if (!visivel) return;
            menu.Items.Add(new MenuItem { Header = rotulo, Command = comando, CommandParameter = cartao });
        }

        void Separar()
        {
            if (menu.Items.Count > 0 && menu.Items[^1] is not Separator) menu.Items.Add(new Separator());
        }

        // A PORTA do termo (parcela 66). Vem primeira quando há termo pendente: o alerta do
        // check-in diz que falta assinar, e alerta sem porta no mesmo app é pior que alerta
        // nenhum — ele ensina a pessoa a ignorá-lo (a lição da parcela 48).
        Acrescentar("Colher o termo do procedimento…", vm.ColherTermoCommand,
            cartao.TemTermoPendente && vm.PodeColherTermo);

        // A PORTA do dinheiro depois que o médico concluiu (parcela 95). Ela fica AQUI, e
        // não como botão da linha, porque só o PACOTE é anunciado como pendência: insumo e
        // caixa a lista não sabe prever em lote, e um botão aceso em toda sessão concluída
        // do dia ensinaria a ignorar a coluna. O que se faz de vez em quando mora no "⋯".
        Acrescentar("Fechar sessão (pacote, insumo, caixa)…", vm.FecharSessaoCommand,
            cartao.PodeFechar && vm.PodeFecharSessao);

        Separar();

        Acrescentar("Voltar uma etapa", vm.VoltarEtapaCommand,
            cartao.PodeVoltar && vm.PodeEditarAgenda);
        Acrescentar("Entrou (pular a chamada)", vm.IniciarAtendimentoCommand,
            cartao.PodeIniciar && cartao.Etapa != EtapaFila.Chamado && vm.PodeEditarAgenda);

        Separar();

        // O bit dos ITENS casa com a guarda dos comandos (`EditarAgenda` estrito) — a
        // metade visível não pode ser mais estreita que a guarda, senão tirar
        // `LancarAtendimento` de alguém esconde daqui a falta e o cancelamento, que
        // não geram guia nenhuma.
        Acrescentar("Marcar falta", vm.MarcarFaltaCommand,
            cartao.EmAberto && vm.PodeMarcarFaltaOuCancelar);
        Acrescentar("Cancelar o horário", vm.CancelarCommand,
            cartao.EmAberto && vm.PodeMarcarFaltaOuCancelar);

        Separar();

        // A GRADE é onde moram remarcar, reabrir o cancelado (a parcela 69), o comprovante
        // e o WhatsApp — a janela do horário. Sempre disponível: é leitura, e para a linha
        // cancelada ou de falta é a ÚNICA ação que sobra, com razão.
        Acrescentar(cartao.ForaDaFila ? "Abrir na grade (reabrir ou remarcar)…" : "Abrir na grade…",
            vm.AbrirNaGradeCommand, true);

        // Separador no fim é lixo visual: tira se ficou órfão.
        if (menu.Items[^1] is Separator) menu.Items.RemoveAt(menu.Items.Count - 1);

        menu.IsOpen = true;
    }
}
