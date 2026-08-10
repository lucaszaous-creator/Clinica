using System.Windows;
using System.Windows.Controls;
using Clinica.Desktop.Shell.Modulos;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>
/// Hospeda várias telas existentes como abas de um item de menu só (parcela 55).
///
/// Ele NÃO reimplementa tela nenhuma: recebe uma fábrica por aba e chama a que for
/// escolhida. Quem monta a fábrica é o <c>ShellViewModel</c>, que é o único que sabe qual
/// módulo constrói cada chave — é isso que permite compor telas de módulos diferentes sem
/// que um passe a conhecer o outro.
/// </summary>
public partial class TelaComAbas : UserControl
{
    private readonly List<Func<object?>> _fabricas = [];

    /// <summary>
    /// Ligado enquanto as abas estão sendo adicionadas.
    ///
    /// O `TabControl` seleciona sozinho a primeira aba assim que ela entra na coleção, e
    /// isso dispara o `SelectionChanged` no meio do construtor. Sem esta guarda, abrir
    /// "Caixa" já na aba "Fechamento" — que é o que `NavegacaoSuite.Ir("fechamento-caixa")`
    /// faz — montaria as DUAS telas, cada uma com a sua ida ao banco, para mostrar uma.
    /// </summary>
    private bool _montando = true;

    /// <summary>
    /// <paramref name="abas"/> na ordem em que aparecem; <paramref name="inicial"/> é a
    /// aba já aberta — é por ela que <c>NavegacaoSuite.Ir("fechamento-caixa")</c>
    /// continua caindo na tela certa depois que ela virou aba de "Caixa".
    /// </summary>
    public TelaComAbas(IReadOnlyList<(string Rotulo, Func<object?> Criar)> abas, int inicial = 0)
    {
        InitializeComponent();

        foreach (var (rotulo, criar) in abas)
        {
            _fabricas.Add(criar);
            Abas.Items.Add(new TabItem { Header = rotulo });
        }

        _montando = false;
        if (Abas.Items.Count == 0) return;

        // Fora da faixa cai na primeira: aba pedida que não existe neste executável
        // (o módulo dela não foi carregado) não pode deixar a tela em branco.
        var alvo = inicial >= 0 && inicial < Abas.Items.Count ? inicial : 0;

        Abas.SelectedIndex = alvo;
        Materializar(alvo);
    }

    /// <summary>Abre uma aba já existente — usada quando se navega para a mesma tela composta.</summary>
    public void Selecionar(int indice)
    {
        if (indice >= 0 && indice < Abas.Items.Count) Abas.SelectedIndex = indice;
    }

    private void AoTrocarAba(object sender, SelectionChangedEventArgs e)
    {
        // ⚠️ SelectionChanged é um evento ROTEADO e BORBULHA. Toda ListBox, DataGrid,
        // ComboBox ou TabControl dentro do conteúdo da aba dispara este mesmo handler ao
        // trocar de linha — e sem esta guarda o clique numa linha de tabela seria lido
        // como troca de aba, remontando a tela por baixo de quem está usando.
        if (_montando) return;
        if (!ReferenceEquals(e.OriginalSource, Abas)) return;

        Materializar(Abas.SelectedIndex);
    }

    /// <summary>
    /// Cria o conteúdo da aba na PRIMEIRA vez que ela é aberta.
    ///
    /// Preguiçoso de propósito: o banco é remoto, e montar as três telas de "Caixa" ao
    /// abrir a primeira dispararia três consultas para mostrar uma. Depois de criada a
    /// aba é guardada, então voltar a ela não recarrega nem perde o que foi digitado.
    /// </summary>
    private void Materializar(int indice)
    {
        if (indice < 0 || indice >= _fabricas.Count) return;
        if (Abas.Items[indice] is not TabItem item || item.Content is not null) return;

        try
        {
            item.Content = _fabricas[indice]();
        }
        catch (Exception ex)
        {
            // Degradação com rastro (regra do projeto): uma aba que falhou não pode
            // derrubar as outras nem sumir em silêncio — a tela diz que falhou e o log
            // guarda o porquê.
            Configuracao.LogSuite.Registrar(
                $"TelaComAbas — falha ao montar a aba '{(item.Header as string) ?? indice.ToString()}'", ex);

            item.Content = new TextBlock
            {
                Text = "Não foi possível abrir esta aba. Tente de novo; "
                       + "se continuar, avise o suporte com a data e a hora.",
                Margin = new Thickness(24),
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Left
            };
        }
    }
}
