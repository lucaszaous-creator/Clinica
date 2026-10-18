using System.Globalization;
using Clinica.Application.Modelos;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>Um evento da trilha, como aparece na lista (e no CSV).</summary>
public sealed record LinhaDaTrilha(string Quando, string Acao, string Operador, string Detalhe, string Referencias);

/// <summary>Uma linha do resumo — ação ou operador — com a contagem e a fração da barra.</summary>
public sealed record ContagemDaTrilha(string Rotulo, string Vezes, double Fracao);

/// <summary>O que a pessoa escolheu nos filtros da tela.</summary>
public sealed record FiltrosDaTrilha(
    int? PacienteId,
    bool SomenteAcessos,
    string? Acao,
    string? Operador,
    string? Termo,
    DateOnly? Inicio,
    DateOnly? Fim,
    int Limite);

/// <summary>A frase do resumo e se o resultado bateu no limite.</summary>
public sealed record ResumoDaTrilha(string Texto, bool NoLimite);

/// <summary>
/// O que a tela da TRILHA DE AUDITORIA do Gerente AFIRMA (out/2026, etapa 6 — a tela saiu do
/// WPF para o Blazor). Era o <c>AuditoriaViewModel</c> + <c>LinhaAuditoria</c>: a montagem do
/// filtro (o atalho "só quem ABRIU" vence o combo de ação), a linha com o NOME do paciente
/// (parcela 52), as contagens por ação e por operador, a frase do resumo — que DIZ quando o
/// resultado está cortado no limite — e o CSV. Nada aqui lê o banco: a tela lê com o MESMO
/// <see cref="AuditoriaService"/>.
///
/// A trilha é somente leitura, e isso é decisão: registro de auditoria que se pode editar ou
/// apagar não é auditoria, é rascunho. Não há (e não deve haver) nada aqui que altere evento.
/// </summary>
public static class TrilhaDeAuditoriaNaTela
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>Os limites do combo, como no WPF. O corte é no BANCO.</summary>
    public static IReadOnlyList<int> Limites { get; } = [100, 300, 1000];

    public const int LimitePadrao = 300;

    /// <summary>Quantas ações e quantos operadores o resumo mostra.</summary>
    public const int TopoDoResumo = 12;

    /// <summary>O filtro com que a tela abre e para o qual o "Limpar" volta: os últimos 30 dias.</summary>
    public static FiltrosDaTrilha Padrao(DateOnly hoje)
        => new(null, false, null, null, null, hoje.AddDays(-30), hoje, LimitePadrao);

    /// <summary>
    /// O filtro que vai ao banco. O atalho "só quem ABRIU o prontuário" VENCE o combo de ação:
    /// quem marcou pediu isso — e achar os acessos sem ele exigiria saber que a ação se chama
    /// "ProntuarioAcessado".
    /// </summary>
    public static FiltroAuditoria Filtro(FiltrosDaTrilha f) => new()
    {
        PacienteId = f.PacienteId,
        Acao = f.SomenteAcessos ? AcessoProntuarioService.PrefixoAcao : f.Acao,
        Operador = f.Operador,
        Termo = f.Termo,
        Inicio = f.Inicio,
        Fim = f.Fim,
        Limite = f.Limite
    };

    /// <summary>
    /// A linha. O nome do paciente entra quando há paciente filtrado (parcela 52): a trilha
    /// mostrava "paciente #123", e ninguém investiga um acesso indevido sabendo o número da
    /// pessoa. Com HORA: num balcão em que duas pessoas dividem a máquina, a hora é o que
    /// separa um turno do outro.
    /// </summary>
    public static LinhaDaTrilha Linha(EventoAuditoria e, string? nomePaciente = null) => new(
        Quando: e.DataHora.ToString("dd/MM/yyyy HH:mm", PtBr),
        Acao: e.Acao,
        Operador: string.IsNullOrWhiteSpace(e.Operador) ? "(não informado)" : e.Operador,
        Detalhe: e.Detalhe ?? "—",
        Referencias: string.Join(" · ", new[]
        {
            e.CodigoId is { } c ? $"guia #{c}" : null,
            e.PacienteId is { } p
                ? (string.IsNullOrWhiteSpace(nomePaciente) ? $"paciente #{p}" : nomePaciente)
                : null,
            e.LoteTissId is { } l ? $"lote #{l}" : null
        }.Where(x => x is not null)));

    /// <summary>As primeiras doze, com a fração sobre o total do RESULTADO (não da base).</summary>
    public static IReadOnlyList<ContagemDaTrilha> Contagens(IEnumerable<(string Rotulo, int Vezes)> grupos, int total)
        => grupos.Take(TopoDoResumo)
            .Select(g => new ContagemDaTrilha(g.Rotulo, $"{g.Vezes}x", total > 0 ? (double)g.Vezes / total : 0d))
            .ToList();

    /// <summary>
    /// Bater no limite = o resultado está CORTADO, e a frase precisa dizer. Um "300 eventos" que
    /// na verdade são 300 dos 4.000 faria a direção concluir que o período teve pouca
    /// movimentação.
    /// </summary>
    public static ResumoDaTrilha Resumo(ResumoAuditoria r, int eventosLidos, int limite)
    {
        var noLimite = eventosLidos >= limite;
        var texto = r.Vazio
            ? "Nenhum evento no filtro atual."
            : noLimite
                ? $"{r.Eventos} evento(s) — LIMITE ATINGIDO, há mais no período. Estreite o filtro ou aumente o limite."
                : $"{r.Eventos} evento(s) no filtro atual.";
        return new ResumoDaTrilha(texto, noLimite);
    }

    /// <summary>
    /// O CSV — o que se anexa quando a pergunta sai da clínica (contestação de convênio, dúvida
    /// do contador). Mesmas colunas do WPF, na ordem da lista.
    /// </summary>
    public static (IReadOnlyList<string> Cabecalho, IReadOnlyList<IReadOnlyList<string>> Linhas) Csv(IEnumerable<LinhaDaTrilha> linhas)
        => (["Quando", "Acao", "Operador", "Detalhe", "Referencias"],
            linhas.Select(l => (IReadOnlyList<string>)new[] { l.Quando, l.Acao, l.Operador, l.Detalhe, l.Referencias }).ToList());

    public static string NomeDoCsv(DateOnly hoje) => $"auditoria-{hoje.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.csv";
}
