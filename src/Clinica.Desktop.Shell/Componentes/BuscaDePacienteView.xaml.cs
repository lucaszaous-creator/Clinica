using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>
/// A BUSCA DE PACIENTE COMO TELA (set/2026 — pedido da direção: <i>"nas telas onde precisa
/// haver busca por pacientes, essa seja a tela inicial, parecida com a tela de
/// atendimento"</i>).
///
/// O que ela unifica
/// -----------------
/// Escolher paciente tinha TRÊS desenhos na suíte, e dois deles o <c>README.md</c> proíbe:
///
/// <list type="bullet">
///   <item>a composição centrada do Novo atendimento (o mockup 05, aprovado pelo cliente);</item>
///   <item>lista de largura inteira → tela do item (Prontuário e Prescrições da Recepção);</item>
///   <item><b>faixa lateral de 320 px</b> com a lista grudada à esquerda para sempre
///   (Prescrições e Prescrição de infusão do Consultório).</item>
/// </list>
///
/// Três desenhos para a MESMA pergunta no mesmo sistema é o que faz a recepcionista achar
/// que abriu outro programa — a reclamação que este projeto já ouviu sete vezes.
///
/// ⚠️ O corpo dela é o bloco do <c>NovoAtendimentoView</c> movido INTEIRO, e não
/// reescrito: ele carrega meia dúzia de lições pagas uma a uma (a coluna de 760 px sem
/// coluna estrela, o campo de 48, os chips que seguem o que ESTÁ na tela, o
/// <c>MaxHeight</c> de número inteiro de linhas, o <c>RodaDaPagina</c>, a ausência de
/// <c>MinHeight</c>). Reescrever teria perdido metade delas em silêncio.
///
/// ⚠️ Ela é SÓ a metade de escolher. A tela dona continua dona do que fazer com o
/// paciente escolhido: este controle não navega, não grava e não conhece nenhum módulo.
/// </summary>
public partial class BuscaDePacienteView : UserControl
{
    public BuscaDePacienteView()
    {
        InitializeComponent();
    }

    /// <summary>A pergunta, grande, no topo — "Quem você vai atender?".</summary>
    public static readonly DependencyProperty PerguntaProperty =
        DependencyProperty.Register(nameof(Pergunta), typeof(string), typeof(BuscaDePacienteView),
            new PropertyMetadata("Quem você vai atender?"));

    public string Pergunta
    {
        get => (string)GetValue(PerguntaProperty);
        set => SetValue(PerguntaProperty, value);
    }

    /// <summary>A linha abaixo da pergunta — diz o que fazer, não o que a tela é.</summary>
    public static readonly DependencyProperty ExplicacaoProperty =
        DependencyProperty.Register(nameof(Explicacao), typeof(string), typeof(BuscaDePacienteView),
            new PropertyMetadata("Digite o nome ou o CPF — ou escolha alguém da agenda de hoje."));

    public string Explicacao
    {
        get => (string)GetValue(ExplicacaoProperty);
        set => SetValue(ExplicacaoProperty, value);
    }

    /// <summary>
    /// O que a tela diz ANTES de alguém pedir alguma coisa (o estado ocioso).
    ///
    /// ⚠️ Ele não é o vazio da busca, e é por isso que são duas frases: "nenhum paciente
    /// encontrado" numa tela recém-aberta seria uma afirmação falsa sobre uma clínica de
    /// 2.238 fichas — e é a frase que faz alguém cadastrar de novo quem já tem ficha
    /// (a lição da parcela 57).
    /// </summary>
    public static readonly DependencyProperty ConviteProperty =
        DependencyProperty.Register(nameof(Convite), typeof(string), typeof(BuscaDePacienteView),
            new PropertyMetadata("Comece digitando o nome ou o CPF."));

    public string Convite
    {
        get => (string)GetValue(ConviteProperty);
        set => SetValue(ConviteProperty, value);
    }

    /// <summary>
    /// Põe o cursor na busca. Quem chega nesta tela vai digitar um nome, e o clique a mais
    /// para chegar ao campo é o atrito que faz a pessoa largar o teclado.
    ///
    /// ⚠️ O <c>Dispatcher</c> não é cerimônia, e ele mora AQUI para o próximo chamador não
    /// precisar saber disso: no instante em que a tela fica visível o <c>TextBox</c> pode
    /// ainda não estar carregado (a aba acabou de ser materializada) e o <c>Focus()</c>
    /// devolve false EM SILÊNCIO. Adiar para depois do Render dá o foco com a árvore
    /// pronta.
    /// </summary>
    public void Focar() => Dispatcher.BeginInvoke(
        System.Windows.Threading.DispatcherPriority.Input,
        new Action(() => Campo.Focus()));

    /// <summary>
    /// O teclado da busca: ↓ desce para a lista, Enter escolhe o primeiro.
    ///
    /// É o gesto que a recepcionista repete quarenta vezes por dia — digitar três letras e
    /// escolher —, e sem isto ele exige tirar a mão do teclado para clicar. Com a lista
    /// vazia as duas teclas não fazem nada, em vez de mover o foco para o vazio.
    ///
    /// ⚠️ `PreviewKeyDown`, e não `KeyDown`: o `TextBox` trata as setas como movimento do
    /// cursor e marca o evento, então no `KeyDown` a seta para baixo nunca chegaria aqui.
    /// </summary>
    private void AoTeclarNaBusca(object sender, KeyEventArgs e)
    {
        if (Lista.Items.Count == 0) return;

        if (e.Key == Key.Down)
        {
            if (Lista.SelectedIndex < 0) Lista.SelectedIndex = 0;
            (Lista.ItemContainerGenerator
                .ContainerFromIndex(Lista.SelectedIndex) as ListBoxItem)?.Focus();
            e.Handled = true;
            return;
        }

        // Enter com UM resultado é a escolha óbvia — quem digitou o CPF inteiro não deve
        // precisar de mais um gesto. Com vários, escolher o primeiro seria chutar; a tecla
        // então só desce para a lista, e a escolha continua sendo de quem está lá.
        if (e.Key == Key.Enter)
        {
            if (Lista.Items.Count == 1) Lista.SelectedIndex = 0;
            else (Lista.ItemContainerGenerator.ContainerFromIndex(0) as ListBoxItem)?.Focus();
            e.Handled = true;
        }
    }
}
