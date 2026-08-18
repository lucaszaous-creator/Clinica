using System.Collections;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;

namespace Clinica.Desktop.Controls;

/// <summary>
/// Os três estados de uma lista, num componente só.
///
/// Por que existe
/// --------------
/// Vinte e oito ViewModels da suíte mantêm uma propriedade `Carregando`, e **uma única
/// tela a exibia**. O design system tem `Spinner`, `Skeleton` e o controle
/// <see cref="EmptyState"/> — nenhum dos três com um consumidor sequer. Cada tela que
/// queria dizer "não há nada aqui" escrevia o seu próprio `TextBlock`, e as que não
/// escreviam simplesmente mostravam uma área em branco.
///
/// O problema não é estético. O banco é REMOTO e, desde que o retry foi ligado, uma
/// consulta numa conexão ruim pode levar quinze segundos. Durante esse tempo a tela
/// mostrava **uma lista vazia — visualmente idêntica a "não existe nada"**. A secretária
/// conclui que o paciente sumiu do sistema, ou que o dia não tem ninguém marcado, e age
/// a partir disso.
///
/// A ordem dos estados é a regra
/// -----------------------------
/// 1. **Carregando** vence tudo: enquanto a resposta não chegou, não se afirma nada.
/// 2. **Não verificado** vem antes de vazio, e essa é a razão de o componente existir.
///    Leitura que FALHOU não pode ser desenhada como "não há nada" — é o princípio que o
///    projeto aplica no painel da direção, e que na lista estava por conta de cada tela
///    lembrar sozinha.
/// 3. **Vazio** só quando a leitura deu certo e de fato não veio nada.
/// 4. Fora isso o componente some, e a lista aparece.
///
/// Uso
/// ---
/// <code>
/// &lt;ctrl:EstadoDaTela Itens="{Binding Sumidos}"
///                    Carregando="{Binding Carregando}"
///                    NaoVerificado="{Binding NaoVerificado}"
///                    Titulo="Ninguém sem vir há mais de 60 dias"
///                    Descricao="Quem tem horário marcado à frente não entra nesta lista." /&gt;
/// </code>
/// </summary>
public class EstadoDaTela : Control
{
    static EstadoDaTela()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(EstadoDaTela), new FrameworkPropertyMetadata(typeof(EstadoDaTela)));
    }

    public EstadoDaTela()
    {
        IsTabStop = false;
        // O componente ocupa o lugar da lista, então ele não pode aparecer quando há
        // conteúdo — e o estado inicial de uma tela que ainda não carregou é justamente
        // "sem itens", não "vazio de verdade".
        Recalcular();
    }

    // ------------------------------------------------------------ propriedades

    /// <summary>A coleção que a lista mostra. O componente decide sozinho se ela está vazia.</summary>
    public static readonly DependencyProperty ItensProperty =
        DependencyProperty.Register(nameof(Itens), typeof(IEnumerable), typeof(EstadoDaTela),
            new PropertyMetadata(null, AoTrocarItens));

    public IEnumerable? Itens
    {
        get => (IEnumerable?)GetValue(ItensProperty);
        set => SetValue(ItensProperty, value);
    }

    /// <summary>A leitura ainda não voltou.</summary>
    public static readonly DependencyProperty CarregandoProperty =
        DependencyProperty.Register(nameof(Carregando), typeof(bool), typeof(EstadoDaTela),
            new PropertyMetadata(false, AoMudar));

    public bool Carregando
    {
        get => (bool)GetValue(CarregandoProperty);
        set => SetValue(CarregandoProperty, value);
    }

    /// <summary>
    /// A leitura FALHOU. É o terceiro estado do projeto, e o motivo de este componente
    /// existir: sem ele, falha de leitura se desenha como ausência de dado.
    /// </summary>
    public static readonly DependencyProperty NaoVerificadoProperty =
        DependencyProperty.Register(nameof(NaoVerificado), typeof(bool), typeof(EstadoDaTela),
            new PropertyMetadata(false, AoMudar));

    public bool NaoVerificado
    {
        get => (bool)GetValue(NaoVerificadoProperty);
        set => SetValue(NaoVerificadoProperty, value);
    }

    /// <summary>
    /// Diz explicitamente se está vazio, ignorando <see cref="Itens"/>.
    /// </summary>
    /// <remarks>
    /// Saída para tela COMPOSTA, e não atalho. A fila é um quadro de quatro colunas que
    /// carregam da mesma consulta: quatro componentes dariam quatro giros na tela para
    /// uma espera só, e o quadro só está vazio quando as quatro estão. `Itens` continua
    /// sendo o caminho normal — quem tem uma lista só não precisa deste campo.
    ///
    /// Nulo = decide pelo `Itens`. É por isso que é `bool?` e não `bool`: com `bool`, o
    /// valor padrão `false` afirmaria "não está vazio" em toda tela que não o preenchesse.
    /// </remarks>
    public static readonly DependencyProperty VazioProperty =
        DependencyProperty.Register(nameof(Vazio), typeof(bool?), typeof(EstadoDaTela),
            new PropertyMetadata(null, AoMudar));

    public bool? Vazio
    {
        get => (bool?)GetValue(VazioProperty);
        set => SetValue(VazioProperty, value);
    }

    /// <summary>
    /// O componente está VALENDO. Falso = some, qualquer que seja o estado.
    /// </summary>
    /// <remarks>
    /// ⚠️ Existe para que ninguém precise amarrar <c>Visibility</c> neste controle — e
    /// amarrar era o que várias telas faziam.
    ///
    /// <b>Não funciona, e falha de um jeito traiçoeiro.</b> O componente DECIDE a própria
    /// <c>Visibility</c> em <c>Recalcular()</c>, atribuindo um valor LOCAL — e valor local
    /// atribuído por código <b>substitui o binding</b>. O XAML liga a visibilidade a
    /// "estou mostrando o prontuário"; na primeira mudança de `Itens`, `Carregando` ou
    /// `NaoVerificado`, o `Recalcular` sobrescreve e o binding morre. Daí em diante o vazio
    /// aparece quando a LISTA está vazia, e não quando a tela dele está aberta: foi assim
    /// que "Nenhuma sessão registrada" ficou escrito por cima da lista de pacientes, em
    /// produção.
    ///
    /// Com <c>Ativo</c> a condição entra no cálculo em vez de brigar com ele.
    /// </remarks>
    public static readonly DependencyProperty AtivoProperty =
        DependencyProperty.Register(nameof(Ativo), typeof(bool), typeof(EstadoDaTela),
            new PropertyMetadata(true, AoMudar));

    public bool Ativo
    {
        get => (bool)GetValue(AtivoProperty);
        set => SetValue(AtivoProperty, value);
    }

    /// <summary>Ícone do estado vazio (fonte de ícones do design system).</summary>
    public static readonly DependencyProperty GlifoProperty =
        DependencyProperty.Register(nameof(Glifo), typeof(string), typeof(EstadoDaTela),
            new PropertyMetadata(""));

    public string Glifo
    {
        get => (string)GetValue(GlifoProperty);
        set => SetValue(GlifoProperty, value);
    }

    /// <summary>
    /// O que dizer quando não há nada. Escreva a FRASE da clínica, não "Nenhum registro":
    /// "ninguém espera" e "ninguém serve para este horário" são respostas diferentes, e a
    /// tela que não distingue as duas faz a pessoa desistir dela.
    /// </summary>
    public static readonly DependencyProperty TituloProperty =
        DependencyProperty.Register(nameof(Titulo), typeof(string), typeof(EstadoDaTela),
            new PropertyMetadata("Nada por aqui"));

    public string Titulo
    {
        get => (string)GetValue(TituloProperty);
        set => SetValue(TituloProperty, value);
    }

    public static readonly DependencyProperty DescricaoProperty =
        DependencyProperty.Register(nameof(Descricao), typeof(string), typeof(EstadoDaTela),
            new PropertyMetadata(string.Empty));

    public string Descricao
    {
        get => (string)GetValue(DescricaoProperty);
        set => SetValue(DescricaoProperty, value);
    }

    /// <summary>Texto exibido enquanto carrega. Trocável porque nem toda espera é igual.</summary>
    public static readonly DependencyProperty TextoCarregandoProperty =
        DependencyProperty.Register(nameof(TextoCarregando), typeof(string), typeof(EstadoDaTela),
            new PropertyMetadata("Carregando…"));

    public string TextoCarregando
    {
        get => (string)GetValue(TextoCarregandoProperty);
        set => SetValue(TextoCarregandoProperty, value);
    }

    /// <summary>
    /// Texto do estado de falha. O padrão já diz as duas coisas que importam: que a lista
    /// está vazia POR FALHA, e que isso não significa ausência de dado.
    /// </summary>
    public static readonly DependencyProperty TextoNaoVerificadoProperty =
        DependencyProperty.Register(nameof(TextoNaoVerificado), typeof(string), typeof(EstadoDaTela),
            new PropertyMetadata(
                "Não foi possível ler esta lista. Ela está vazia por falha de leitura, "
                + "não porque não haja registros."));

    public string TextoNaoVerificado
    {
        get => (string)GetValue(TextoNaoVerificadoProperty);
        set => SetValue(TextoNaoVerificadoProperty, value);
    }

    // ------------------------------------------------------------ estado atual

    private static readonly DependencyPropertyKey EstadoKey =
        DependencyProperty.RegisterReadOnly(nameof(Estado), typeof(EstadoDeLista),
            typeof(EstadoDaTela), new PropertyMetadata(EstadoDeLista.Conteudo));

    public static readonly DependencyProperty EstadoProperty = EstadoKey.DependencyProperty;

    /// <summary>Qual dos quatro estados está valendo agora — é o que o template observa.</summary>
    public EstadoDeLista Estado
    {
        get => (EstadoDeLista)GetValue(EstadoProperty);
        private set => SetValue(EstadoKey, value);
    }

    private static void AoMudar(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((EstadoDaTela)d).Recalcular();

    /// <summary>
    /// ⚠️ A COLEÇÃO muda sem que a PROPRIEDADE mude, e é aí que o componente mentia.
    ///
    /// As telas amarram `Itens` a uma `ObservableCollection` que NUNCA é reatribuída — elas
    /// fazem `Clear()` e `Add()` na mesma instância. Isso dispara `CollectionChanged`, e não
    /// o callback da DependencyProperty: <c>Recalcular</c> rodava uma vez, no instante do
    /// binding, com a lista ainda vazia — e a resposta ficava congelada ali para sempre.
    ///
    /// Quase toda tela escapou por acidente: elas também amarram `Carregando`, que vira
    /// true e depois false a cada leitura, e é ESSE callback que reavaliava a lista. As que
    /// amarraram só `Itens` ficaram com "não há nada aqui" escrito por cima do conteúdo
    /// cheio — a lista de espera da agenda, as autorizações do convênio na ficha e a busca
    /// de CID. Nada falhava: o XAML é válido, o binding é válido, e as três redes locais
    /// ficam verdes.
    ///
    /// Depender de uma propriedade IRMÃ mudar é contrato que ninguém consegue lembrar. A
    /// assinatura resolve no componente, que é por onde toda tela passa.
    /// </summary>
    private static void AoTrocarItens(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var alvo = (EstadoDaTela)d;

        if (e.OldValue is INotifyCollectionChanged antiga)
            antiga.CollectionChanged -= alvo.AoMexerNaColecao;

        if (e.NewValue is INotifyCollectionChanged nova)
            nova.CollectionChanged += alvo.AoMexerNaColecao;

        alvo.Recalcular();
    }

    private void AoMexerNaColecao(object? remetente, NotifyCollectionChangedEventArgs e)
        => Recalcular();

    private void Recalcular()
    {
        // A ordem é a regra do componente. Carregando primeiro: enquanto a resposta não
        // chegou, não se afirma nada. Falha antes de vazio: leitura que não rodou não
        // pode ser desenhada como ausência de dado.
        Estado = Carregando ? EstadoDeLista.Carregando
            : NaoVerificado ? EstadoDeLista.NaoVerificado
            : (Vazio ?? Vazia(Itens)) ? EstadoDeLista.Vazio
            : EstadoDeLista.Conteudo;

        // `Ativo` entra AQUI, e não como binding de Visibility: esta atribuição é um
        // valor local e apagaria qualquer binding que a tela tivesse posto.
        Visibility = !Ativo || Estado == EstadoDeLista.Conteudo
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private static bool Vazia(IEnumerable? itens)
    {
        if (itens is null) return true;
        if (itens is ICollection colecao) return colecao.Count == 0;

        var e = itens.GetEnumerator();
        try
        {
            return !e.MoveNext();
        }
        finally
        {
            (e as IDisposable)?.Dispose();
        }
    }
}

/// <summary>Os quatro estados possíveis de uma área de lista.</summary>
public enum EstadoDeLista
{
    /// <summary>Há itens: o componente some e a lista aparece.</summary>
    Conteudo,

    /// <summary>A leitura ainda não voltou.</summary>
    Carregando,

    /// <summary>A leitura falhou — nunca desenhar como vazio.</summary>
    NaoVerificado,

    /// <summary>A leitura deu certo e não veio nada.</summary>
    Vazio
}
