using System.Collections;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;

namespace Clinica.Desktop.Controls;

/// <summary>
/// O cabeçalho de uma raia do kanban: rótulo, contagem e o traço da raia vazia.
///
/// Por que existe
/// --------------
/// Os dois quadros da suíte (a fila do balcão e o dia do consultório) escreviam o rótulo
/// da coluna à mão, e **nenhum dos dois mostrava a contagem por coluna**. O número existia
/// só numa frase de resumo acima do quadro — "3 aguardando · 0 na recepção · …" —, ou
/// seja: os mesmos cinco números repetidos numa faixa de texto que empurrava o quadro
/// para baixo, e que o olho tinha de casar com a coluna certa pela ordem.
///
/// A regra do traço
/// ----------------
/// Raia vazia mostra <b>—</b>, nunca vão branco. Num dia já terminado, quatro raias vazias
/// sem marca nenhuma viram um buraco de mil pixels ao lado da única com conteúdo, e o
/// quadro deixa de se ler como quadro: parece uma lista solta num canto e uma tela
/// quebrada. O traço custa uma linha e devolve as cinco faixas ao olho.
///
/// A contagem acompanha a coleção
/// ------------------------------
/// Assina <see cref="INotifyCollectionChanged"/> e reconta: as colunas são limpas e
/// repovoadas a cada leitura, e um número preso no primeiro carregamento mentiria a cada
/// releitura de fundo do relógio de um minuto.
/// </summary>
public class CabecalhoRaia : Control
{
    static CabecalhoRaia()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(CabecalhoRaia), new FrameworkPropertyMetadata(typeof(CabecalhoRaia)));
    }

    public CabecalhoRaia() => IsTabStop = false;

    /// <summary>O rótulo da raia ("AGUARDANDO", "EM ATENDIMENTO").</summary>
    public static readonly DependencyProperty TituloProperty =
        DependencyProperty.Register(nameof(Titulo), typeof(string), typeof(CabecalhoRaia),
            new PropertyMetadata(string.Empty));

    public string Titulo
    {
        get => (string)GetValue(TituloProperty);
        set => SetValue(TituloProperty, value);
    }

    /// <summary>A coleção da raia. O componente conta sozinho.</summary>
    public static readonly DependencyProperty ItensProperty =
        DependencyProperty.Register(nameof(Itens), typeof(IEnumerable), typeof(CabecalhoRaia),
            new PropertyMetadata(null, AoTrocarColecao));

    public IEnumerable? Itens
    {
        get => (IEnumerable?)GetValue(ItensProperty);
        set => SetValue(ItensProperty, value);
    }

    private static readonly DependencyPropertyKey QuantidadeKey =
        DependencyProperty.RegisterReadOnly(nameof(Quantidade), typeof(int), typeof(CabecalhoRaia),
            new PropertyMetadata(0));

    public static readonly DependencyProperty QuantidadeProperty = QuantidadeKey.DependencyProperty;

    /// <summary>Quantos cartões há na raia.</summary>
    public int Quantidade
    {
        get => (int)GetValue(QuantidadeProperty);
        private set => SetValue(QuantidadeKey, value);
    }

    private static readonly DependencyPropertyKey VaziaKey =
        DependencyProperty.RegisterReadOnly(nameof(Vazia), typeof(bool), typeof(CabecalhoRaia),
            new PropertyMetadata(true));

    public static readonly DependencyProperty VaziaProperty = VaziaKey.DependencyProperty;

    /// <summary>A raia não tem cartão nenhum — é o que acende o traço.</summary>
    public bool Vazia
    {
        get => (bool)GetValue(VaziaProperty);
        private set => SetValue(VaziaKey, value);
    }

    private static void AoTrocarColecao(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not CabecalhoRaia cabecalho) return;

        // Trocar a coleção sem largar a anterior deixaria o componente vivo preso a uma
        // lista que ninguém mais mostra — e recontando por causa dela.
        if (e.OldValue is INotifyCollectionChanged antiga)
            antiga.CollectionChanged -= cabecalho.AoMudarColecao;

        if (e.NewValue is INotifyCollectionChanged nova)
            nova.CollectionChanged += cabecalho.AoMudarColecao;

        cabecalho.Recontar();
    }

    private void AoMudarColecao(object? remetente, NotifyCollectionChangedEventArgs e) => Recontar();

    private void Recontar()
    {
        var total = Itens switch
        {
            null => 0,
            ICollection colecao => colecao.Count,
            var itens => itens.Cast<object?>().Count()
        };

        Quantidade = total;
        Vazia = total == 0;
    }
}
