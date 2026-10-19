using System.Globalization;

namespace Clinica.Application.Modelos;

/// <summary>Um horário como o filtro da agenda do posto o enxerga: nome, modalidade e grupo da situação.</summary>
public sealed record ItemDaAgendaDoPosto(string Paciente, string Modalidade, string Grupo);

/// <summary>
/// O filtro escolhido. Nulo/vazio na modalidade é "todas"; "todos" na situação é sem recorte.
/// </summary>
public sealed record FiltroAgendaDoPosto(string Busca = "", string? Modalidade = null, string Situacao = "todos")
{
    /// <summary>A modalidade não recorta nada.</summary>
    public bool ModalidadeLivre => string.IsNullOrEmpty(Modalidade) || Modalidade == FiltroDaAgendaNaTela.TodasAsModalidades;

    /// <summary>Busca, modalidade ou situação escolhidos — o que liga o "Limpar filtros".</summary>
    public bool TemFiltros => Termos.Length > 0 || !ModalidadeLivre || Situacao != "todos";

    internal string[] Termos => (Busca ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
}

/// <summary>Uma pílula de situação, com a contagem visível ANTES do clique.</summary>
public sealed record OpcaoSituacaoDoPosto(string Chave, string Rotulo, int Contagem, bool Marcada)
{
    public string Texto => $"{Rotulo} {Contagem}";
}

/// <summary>O que a barra de busca desenha depois do recorte.</summary>
public sealed record RecorteAgendaDoPosto(
    IReadOnlyList<string> Modalidades,
    IReadOnlyList<OpcaoSituacaoDoPosto> Situacoes,
    int Visiveis,
    int Total,
    string Resumo,
    bool SemResultado);

/// <summary>
/// A BUSCA DA AGENDA do posto — Meu dia e Minha semana (out/2026: era o <c>FiltroDaAgenda</c>
/// do ViewModel WPF, e a regra desceu para cá quando as duas telas viraram Blazor, para a
/// mesma pergunta ter a mesma resposta nas duas e o <c>dotnet test</c> alcançá-la).
///
/// É filtro de APRESENTAÇÃO: não muda disponibilidade, ordem nem dado clínico. As regras são
/// as do WPF, sem mudança: cada palavra digitada precisa aparecer no nome (sem acento, sem
/// caixa); a modalidade é o rótulo exibido; a situação é o grupo de
/// <see cref="SituacaoVisualAgenda"/>; e as contagens das pílulas são sobre o que a busca e a
/// modalidade deixaram — a distribuição do que o filtro de situação vai recortar.
/// </summary>
public static class FiltroDaAgendaNaTela
{
    public const string TodasAsModalidades = "Todas as modalidades";

    /// <summary>As oito pílulas, na ordem da agenda em produção.</summary>
    public static readonly IReadOnlyList<(string Chave, string Rotulo)> Situacoes =
    [
        ("todos", "Todos"), ("pendente", "Pendentes"), ("no-local", "No local"),
        ("em-atendimento", "Em atendimento"), ("atendido", "Atendidos"),
        ("cancelado", "Cancelados"), ("faltou", "Faltas"), ("substituido", "Substituídos")
    ];

    private static readonly CompareInfo Comparador = CultureInfo.GetCultureInfo("pt-BR").CompareInfo;

    private static bool CasaBuscaEModalidade(FiltroAgendaDoPosto f, string[] termos, string paciente, string modalidade)
        => (f.ModalidadeLivre || modalidade == f.Modalidade)
           && termos.All(t => Comparador.IndexOf(paciente ?? string.Empty, t,
               CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0);

    /// <summary>O horário fica na VISTA? (a coleção continua inteira para quem conta o dia).</summary>
    public static bool Aceitar(FiltroAgendaDoPosto f, string paciente, string modalidade, string grupo)
        => CasaBuscaEModalidade(f, f.Termos, paciente, modalidade) && (f.Situacao == "todos" || grupo == f.Situacao);

    /// <summary>
    /// Clicar na pílula já marcada a MANTÉM marcada (o comportamento do editor web): "Todos" é a
    /// pílula de limpar.
    /// </summary>
    public static FiltroAgendaDoPosto EscolherSituacao(FiltroAgendaDoPosto f, string chave)
        => f.Situacao == chave ? f : f with { Situacao = chave };

    /// <summary>
    /// Refaz modalidades, contagens e resumo a partir dos horários carregados — numa passada só.
    /// A modalidade escolhida que sumiu do período continua na lista: senão o combo esvaziaria a
    /// escolha e o filtro mudaria sozinho.
    /// </summary>
    public static RecorteAgendaDoPosto Recortar(IReadOnlyList<ItemDaAgendaDoPosto> itens, FiltroAgendaDoPosto f)
    {
        var modalidades = itens.Select(i => i.Modalidade)
            .Where(m => !string.IsNullOrWhiteSpace(m)).Distinct()
            .OrderBy(m => m, StringComparer.Create(CultureInfo.GetCultureInfo("pt-BR"), true))
            .ToList();
        if (!f.ModalidadeLivre && !modalidades.Contains(f.Modalidade!)) modalidades.Add(f.Modalidade!);
        modalidades.Insert(0, TodasAsModalidades);

        var termos = f.Termos;
        var pesquisadas = itens.Where(i => CasaBuscaEModalidade(f, termos, i.Paciente, i.Modalidade)).ToList();
        var opcoes = Situacoes.Select(s => new OpcaoSituacaoDoPosto(
            s.Chave, s.Rotulo,
            s.Chave == "todos" ? pesquisadas.Count : pesquisadas.Count(i => i.Grupo == s.Chave),
            s.Chave == f.Situacao)).ToList();

        var visiveis = f.Situacao == "todos" ? pesquisadas.Count : pesquisadas.Count(i => i.Grupo == f.Situacao);
        var resumo = itens.Count == 0
            ? string.Empty
            : f.TemFiltros
                ? $"{visiveis} de {itens.Count} horário(s) com o filtro"
                : $"{itens.Count} horário(s) no período";

        return new RecorteAgendaDoPosto(modalidades, opcoes, visiveis, itens.Count, resumo,
            SemResultado: f.TemFiltros && itens.Count > 0 && visiveis == 0);
    }
}
