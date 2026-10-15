using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Clinica.Clinico.ViewModels;

/// <summary>Uma pílula de situação, com a contagem visível ANTES do clique.</summary>
public sealed class OpcaoSituacaoAgenda
{
    public required string Chave { get; init; }
    public required string Rotulo { get; init; }
    public required int Contagem { get; init; }
    public required bool Marcada { get; init; }
    public string Texto => $"{Rotulo} {Contagem}";
}

/// <summary>
/// A busca da agenda do profissional — por paciente, modalidade e situação (out/2026).
///
/// Nasceu na tela web embutida e voltou ao WPF sem mudar o que faz: é filtro de APRESENTAÇÃO, não
/// altera disponibilidade, ordem nem dado clínico. As linhas que não casam somem da vista
/// da coleção; a coleção continua inteira (quem conta o dia, a fila e as pendências lê a
/// coleção, nunca a vista).
///
/// As regras são as do editor web, para a mesma pergunta ter a mesma resposta:
/// cada palavra digitada precisa aparecer no nome (sem acento, sem caixa); a modalidade é
/// o rótulo exibido; a situação é o grupo visual de <c>SituacaoVisualAgenda</c>; e as
/// contagens das pílulas são sobre o que a busca e a modalidade deixaram — a distribuição
/// do que o filtro de situação vai recortar.
/// </summary>
public sealed partial class FiltroDaAgenda : ObservableObject
{
    public const string TodasAsModalidades = "Todas as modalidades";

    private static readonly (string Chave, string Rotulo)[] Situacoes =
    [
        ("todos", "Todos"), ("pendente", "Pendentes"), ("no-local", "No local"),
        ("em-atendimento", "Em atendimento"), ("atendido", "Atendidos"),
        ("cancelado", "Cancelados"), ("faltou", "Faltas"), ("substituido", "Substituídos")
    ];

    private static readonly CompareInfo Comparador = CultureInfo.GetCultureInfo("pt-BR").CompareInfo;

    private IReadOnlyList<(string Paciente, string Modalidade, string Grupo)> _itens = [];
    private string[] _termos = [];
    private bool _remontando;

    /// <summary>Disparado quando a pessoa muda um critério — quem tem a vista a refaz.</summary>
    public event Action? Mudou;

    [ObservableProperty] private string _busca = string.Empty;
    [ObservableProperty] private string? _modalidade = TodasAsModalidades;
    [ObservableProperty] private string _situacao = "todos";

    [ObservableProperty] private string _resumo = string.Empty;
    [ObservableProperty] private bool _temFiltros;

    /// <summary>Há horários e nenhum casa com o filtro — o vazio que pede "limpar", e não
    /// "ninguém marcou".</summary>
    [ObservableProperty] private bool _semResultado;

    public ObservableCollection<string> OpcoesDeModalidade { get; } = [TodasAsModalidades];
    public ObservableCollection<OpcaoSituacaoAgenda> OpcoesSituacao { get; } = [];

    public FiltroDaAgenda() => Recalcular([]);

    partial void OnBuscaChanged(string value)
    {
        _termos = (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        Avisar();
    }

    // ComboBox cuja ItemsSource é LIMPA escreve NULL no SelectedItem: nulo é "todas", e
    // durante a remontagem a mudança não é da pessoa — não refiltra.
    partial void OnModalidadeChanged(string? value)
    {
        if (_remontando) return;
        Avisar();
    }

    partial void OnSituacaoChanged(string value) => Avisar();

    private void Avisar()
    {
        Mudou?.Invoke();
        Recalcular(_itens);
    }

    private bool ModalidadeLivre => string.IsNullOrEmpty(Modalidade) || Modalidade == TodasAsModalidades;

    private bool CasaBuscaEModalidade(string paciente, string modalidade) =>
        (ModalidadeLivre || modalidade == Modalidade)
        && _termos.All(t => Comparador.IndexOf(paciente ?? string.Empty, t,
               CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0);

    /// <summary>A linha fica na vista?</summary>
    public bool Aceitar(string paciente, string modalidade, string grupo) =>
        CasaBuscaEModalidade(paciente, modalidade) && (Situacao == "todos" || grupo == Situacao);

    [RelayCommand]
    private void EscolherSituacao(OpcaoSituacaoAgenda? opcao)
    {
        // Clicar na pílula já marcada a mantém marcada (o comportamento do editor web): a
        // reconstrução abaixo devolve o estado certo à pílula que o clique desmarcou.
        if (opcao is null || opcao.Chave == Situacao) { Recalcular(_itens); return; }
        Situacao = opcao.Chave;
    }

    [RelayCommand]
    private void Limpar()
    {
        Busca = string.Empty;
        Modalidade = TodasAsModalidades;
        Situacao = "todos";
    }

    /// <summary>
    /// Refaz modalidades, contagens e resumo a partir das linhas carregadas. Chamado pelo
    /// dono depois de cada leitura — numa passada só, sem await (parcela 62).
    /// </summary>
    public void Recalcular(IReadOnlyList<(string Paciente, string Modalidade, string Grupo)> itens)
    {
        _itens = itens;

        var modalidades = itens.Select(i => i.Modalidade)
            .Where(m => !string.IsNullOrWhiteSpace(m)).Distinct()
            .OrderBy(m => m, StringComparer.Create(CultureInfo.GetCultureInfo("pt-BR"), true))
            .ToList();
        // A escolhida que sumiu do período continua na lista: senão o combo esvaziaria a
        // escolha e o filtro mudaria sozinho.
        if (!ModalidadeLivre && !modalidades.Contains(Modalidade!)) modalidades.Add(Modalidade!);
        if (!OpcoesDeModalidade.Skip(1).SequenceEqual(modalidades))
        {
            var atual = Modalidade;
            _remontando = true;
            try
            {
                OpcoesDeModalidade.Clear();
                OpcoesDeModalidade.Add(TodasAsModalidades);
                foreach (var m in modalidades) OpcoesDeModalidade.Add(m);
            }
            finally { _remontando = false; }
            if (Modalidade != atual) Modalidade = atual;
        }

        var pesquisadas = itens.Where(i => CasaBuscaEModalidade(i.Paciente, i.Modalidade)).ToList();
        OpcoesSituacao.Clear();
        foreach (var (chave, rotulo) in Situacoes)
        {
            OpcoesSituacao.Add(new OpcaoSituacaoAgenda
            {
                Chave = chave,
                Rotulo = rotulo,
                Contagem = chave == "todos" ? pesquisadas.Count : pesquisadas.Count(i => i.Grupo == chave),
                Marcada = chave == Situacao
            });
        }

        var visiveis = Situacao == "todos" ? pesquisadas.Count : pesquisadas.Count(i => i.Grupo == Situacao);
        TemFiltros = _termos.Length > 0 || !ModalidadeLivre || Situacao != "todos";
        SemResultado = TemFiltros && itens.Count > 0 && visiveis == 0;
        Resumo = itens.Count == 0
            ? string.Empty
            : TemFiltros
                ? $"{visiveis} de {itens.Count} horário(s) com o filtro"
                : $"{itens.Count} horário(s) no período";
    }
}
