using System.Collections.ObjectModel;
using System.Globalization;
using Clinica.Application.Modelos;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Clinica.Recepcao.ViewModels;

/// <summary>O que o filtro precisa saber de um horário — nada além do que a linha já mostra.</summary>
public readonly record struct HorarioFiltravel(string Paciente, string Modalidade, string Grupo);

/// <summary>Uma pílula de situação com a contagem que ela responde ANTES do clique.</summary>
public sealed class ChipSituacaoAgenda : ObservableObject
{
    private int _contagem;
    private bool _marcado;

    public ChipSituacaoAgenda(string chave, string rotulo) { Chave = chave; Rotulo = rotulo; }
    public string Chave { get; }
    public string Rotulo { get; }
    public int Contagem { get => _contagem; set => SetProperty(ref _contagem, value); }

    /// <summary>
    /// Sempre notifica: o <c>ToggleButton</c> troca o próprio <c>IsChecked</c> no clique, e
    /// clicar de novo na pílula já marcada a deixaria desmarcada na tela com o filtro ainda
    /// ativo. A releitura forçada devolve a pílula ao estado verdadeiro.
    /// </summary>
    public bool Marcado
    {
        get => _marcado;
        set { _marcado = value; OnPropertyChanged(); }
    }
}

/// <summary>
/// A busca e os filtros da agenda (out/2026 — de volta ao WPF depois da versão em
/// navegador embutido). É APRESENTAÇÃO pura: não muda a disponibilidade, não lê o banco e não
/// toca no ViewModel da tela. Quem a usa aplica <see cref="Aceita"/> como filtro da
/// <c>ICollectionView</c> da lista e chama <see cref="Recalcular"/> quando a lista muda.
///
/// As regras são as da versão web, e é isso que a clínica já conhecia: o nome casa
/// termo a termo, sem acento e sem caixa; a modalidade é a escrita na linha; a situação
/// é o grupo de <see cref="SituacaoVisualAgenda"/>; e as contagens das pílulas são sobre
/// o que a BUSCA e a MODALIDADE deixaram, nunca sobre o recorte da própria pílula.
/// </summary>
public sealed partial class FiltroListaAgenda : ObservableObject
{
    public const string TodasModalidades = "Todas as modalidades";
    private static readonly CompareInfo Comparador = CultureInfo.GetCultureInfo("pt-BR").CompareInfo;

    private readonly Func<object, HorarioFiltravel?> _extrair;
    private readonly string _nomeDoPeriodo;
    private string[] _termos = [];

    public FiltroListaAgenda(Func<object, HorarioFiltravel?> extrair, string nomeDoPeriodo)
    {
        _extrair = extrair;
        _nomeDoPeriodo = nomeDoPeriodo;
        Situacoes =
        [
            new("todos", "Todos"), new("pendente", "Pendentes"), new("no-local", "No local"),
            new("em-atendimento", "Em atendimento"), new("atendido", "Atendidos"),
            new("cancelado", "Cancelados"), new("faltou", "Faltas"), new("substituido", "Substituídos")
        ];
        Situacoes[0].Marcado = true;
        NomesDeModalidade.Add(TodasModalidades);
        Resumo = "Nenhum horário carregado";
    }

    /// <summary>Disparado quando um critério muda: a tela refaz a vista.</summary>
    public event Action? Mudou;

    public ObservableCollection<ChipSituacaoAgenda> Situacoes { get; }
    public ObservableCollection<string> NomesDeModalidade { get; } = [];

    [ObservableProperty] private string _busca = "";

    /// <summary>
    /// Nulo vale "todas": um <c>ComboBox</c> cuja lista é limpa escreve NULL aqui, e um
    /// nulo que não fosse "todas" esconderia a agenda inteira por um instante.
    /// </summary>
    [ObservableProperty] private string? _modalidade = TodasModalidades;
    [ObservableProperty] private string _situacao = "todos";
    [ObservableProperty] private string _resumo;
    [ObservableProperty] private bool _temFiltros;

    /// <summary>Há horários no período, e NENHUM passa pelos filtros — a pergunta é outra.</summary>
    [ObservableProperty] private bool _nenhumCorresponde;

    private bool ModalidadeAtiva => !string.IsNullOrEmpty(Modalidade) && Modalidade != TodasModalidades;

    partial void OnBuscaChanged(string value)
    {
        _termos = (value ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        Avisar();
    }
    partial void OnModalidadeChanged(string? value) => Avisar();
    partial void OnSituacaoChanged(string value)
    {
        foreach (var chip in Situacoes) chip.Marcado = chip.Chave == value;
        Avisar();
    }

    private void Avisar()
    {
        TemFiltros = _termos.Length > 0 || ModalidadeAtiva || Situacao != "todos";
        Mudou?.Invoke();
    }

    [RelayCommand]
    private void EscolherSituacao(ChipSituacaoAgenda? chip)
    {
        if (chip is null) return;
        if (Situacao == chip.Chave)
        {
            // O clique na pílula já marcada não desmarca: devolve o estado à tela.
            foreach (var c in Situacoes) c.Marcado = c.Chave == Situacao;
            return;
        }
        Situacao = chip.Chave;
    }

    [RelayCommand]
    private void Limpar()
    {
        Busca = "";
        Modalidade = TodasModalidades;
        Situacao = "todos";
    }

    private bool PassaBuscaEModalidade(HorarioFiltravel h) =>
        (!ModalidadeAtiva || h.Modalidade == Modalidade)
        && _termos.All(t => Comparador.IndexOf(h.Paciente, t,
            CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0);

    /// <summary>O predicado da vista. Item que não é horário (o vão livre) sempre passa.</summary>
    public bool Aceita(object item) =>
        _extrair(item) is not { } h || (PassaBuscaEModalidade(h) && (Situacao == "todos" || h.Grupo == Situacao));

    /// <summary>Refaz modalidades, contagens e resumo a partir da lista INTEIRA do período.</summary>
    public void Recalcular(IEnumerable<object> todos, bool carregando)
    {
        var horarios = todos.Select(_extrair).Where(h => h is not null).Select(h => h!.Value).ToList();

        var nomes = horarios.Select(h => h.Modalidade).Where(m => !string.IsNullOrWhiteSpace(m))
            .Distinct().OrderBy(m => m, StringComparer.Create(CultureInfo.GetCultureInfo("pt-BR"), false)).ToList();
        // A escolha atual fica na lista mesmo sem horário dela no período: sumir com a
        // opção escolhida faria o combo devolver nulo e trocar o filtro sozinho.
        if (ModalidadeAtiva && !nomes.Contains(Modalidade!)) nomes.Add(Modalidade!);
        var desejadas = new List<string> { TodasModalidades };
        desejadas.AddRange(nomes);
        if (!desejadas.SequenceEqual(NomesDeModalidade))
        {
            var escolhida = Modalidade;
            // Remove e acrescenta só o que mudou — `Clear()` levaria o combo a escrever nulo.
            for (var i = NomesDeModalidade.Count - 1; i >= 0; i--)
                if (!desejadas.Contains(NomesDeModalidade[i])) NomesDeModalidade.RemoveAt(i);
            for (var i = 0; i < desejadas.Count; i++)
            {
                if (i < NomesDeModalidade.Count && NomesDeModalidade[i] == desejadas[i]) continue;
                if (NomesDeModalidade.Contains(desejadas[i])) NomesDeModalidade.Remove(desejadas[i]);
                NomesDeModalidade.Insert(i, desejadas[i]);
            }
            if (Modalidade != escolhida) Modalidade = escolhida;
        }

        var pesquisadas = horarios.Where(PassaBuscaEModalidade).ToList();
        foreach (var chip in Situacoes)
            chip.Contagem = chip.Chave == "todos" ? pesquisadas.Count : pesquisadas.Count(h => h.Grupo == chip.Chave);
        var visiveis = Situacao == "todos" ? pesquisadas.Count : pesquisadas.Count(h => h.Grupo == Situacao);

        NenhumCorresponde = !carregando && horarios.Count > 0 && visiveis == 0 && TemFiltros;
        Resumo = carregando ? "Atualizando agenda…" : $"{visiveis} de {horarios.Count} horários {_nomeDoPeriodo}";
    }
}
