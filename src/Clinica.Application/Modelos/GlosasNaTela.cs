using Clinica.Application.Formatacao;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Modelos;

/// <summary>
/// Uma glosa na tela de Glosas e recursos, com o prazo de recurso já calculado. Subiu do
/// <c>GlosaLinha</c> do <c>GlosasViewModel</c> WPF na migração para Blazor (out/2026), onde
/// o `dotnet test` alcança — e ganhou duas respostas que o WPF não dava: SE a guia ainda
/// pode ser reapresentada e SE ainda pode ser marcada como recuperada. No WPF os dois
/// botões ficavam acesos em toda linha: "Reapresentar" numa guia já reapresentada levava
/// a recusa do domínio depois do clique, e "Recuperada" numa já recuperada não fazia nada
/// (o botão que não faz nada da parcela 41).
/// </summary>
public sealed record GlosaNaTela(
    CodigoFaturamento Codigo,
    string MotivoAns,
    DateOnly? PrazoRecurso,
    int? DiasParaFimPrazo,
    NivelUrgencia Urgencia)
{
    public string PacienteNome => Codigo.Atendimento?.Paciente?.Nome ?? "(paciente desconhecido)";

    /// <summary>A OPERADORA (catálogo), nunca a família do enum — parcela 50.</summary>
    public string ConvenioNome => Codigo.Atendimento?.Paciente?.ConvenioNome ?? "—";

    public int? PacienteId => Codigo.Atendimento?.PacienteId;

    /// <summary>"12/11/2026 (vencido)" / "12/11/2026 (4 d)"; vazio sem glosa em aberto (como no WPF).</summary>
    public string PrazoRotulo => PrazoRecurso is null || DiasParaFimPrazo is null
        ? string.Empty
        : DiasParaFimPrazo < 0
            ? $"{PrazoRecurso:dd/MM/yyyy} (vencido)"
            : $"{PrazoRecurso:dd/MM/yyyy} ({DiasParaFimPrazo} d)";

    /// <summary>O código ANS existe no catálogo de motivos (o guia "Entenda" tem o que dizer sobre ele).</summary>
    public bool MotivoCatalogado => MotivosGlosa.Obter(Codigo.MotivoGlosaCodigo) is not null;

    /// <summary>
    /// A linha principal do motivo: a descrição do catálogo; o código cru quando ele não está
    /// no catálogo ("Código ANS 1801"); e, sem código, o complemento que a pessoa escreveu —
    /// o WPF deixava a coluna em branco e o texto só aparecia na coluna ao lado.
    /// </summary>
    public string MotivoPrincipal => MotivoCatalogado
        ? MotivoAns
        : !string.IsNullOrWhiteSpace(Codigo.MotivoGlosaCodigo)
            ? $"Código ANS {Codigo.MotivoGlosaCodigo}"
            : string.IsNullOrWhiteSpace(Codigo.MotivoGlosa) ? "Motivo não informado" : Codigo.MotivoGlosa!;

    /// <summary>O complemento por baixo — só quando ele já não virou a linha principal.</summary>
    public string? MotivoComplemento =>
        string.IsNullOrWhiteSpace(Codigo.MotivoGlosaCodigo) || string.IsNullOrWhiteSpace(Codigo.MotivoGlosa)
            ? null
            : Codigo.MotivoGlosa;

    /// <summary>Prazo de recorrer já passou — a glosa virou decisão, não fila.</summary>
    public bool PrazoVencido => DiasParaFimPrazo < 0;

    /// <summary>A situação por extenso, com maiúscula (o enum cru "Reapresentada" vazava na coluna do WPF).</summary>
    public string Situacao => Codigo.Glosa switch
    {
        StatusGlosa.Glosada => "Glosada",
        StatusGlosa.Reapresentada => "Reapresentada",
        StatusGlosa.Recuperada => "Recuperada",
        _ => "Sem glosa"
    };

    /// <summary>O domínio só reapresenta o que está GLOSADO (<c>CodigoFaturamento.Reapresentar</c>).</summary>
    public bool PodeReapresentar => Codigo.Glosa == StatusGlosa.Glosada;

    /// <summary>Recuperar só muda algo numa glosa ainda em aberto (glosada ou reapresentada).</summary>
    public bool PodeRecuperar => Codigo.GlosaEmAberto;
}

/// <summary>Um motivo que se repete nas glosas da clínica, com quantas vezes apareceu.</summary>
public sealed record MotivoDeGlosaRecorrente(string Codigo, string Descricao, int Ocorrencias, string ComoEvitar)
{
    /// <summary>Código fora do catálogo: não há guia para "Entenda" (abriria em outro motivo).</summary>
    public bool Catalogado => MotivosGlosa.Obter(Codigo) is not null;

    public string Rotulo => $"{Ocorrencias}× · {Codigo} — {Descricao}";
}

/// <summary>
/// O filtro da tela de glosas. O "em aberto" é o recorte PADRÃO, com caixinha própria; os
/// outros três são os filtros que acendem o "Limpar" (a mesma divisão do WPF).
/// </summary>
public sealed record FiltroGlosas(
    bool SomenteEmAberto = true,
    string Paciente = "",
    string Guia = "",
    bool SoPrazoVencido = false)
{
    public bool Ativo =>
        SoPrazoVencido
        || !string.IsNullOrWhiteSpace(Paciente)
        || !string.IsNullOrWhiteSpace(Guia);

    public bool Casa(GlosaNaTela g)
        => (!SomenteEmAberto || g.Codigo.GlosaEmAberto)
           // null < 0 é falso: glosa sem prazo calculado não é "vencida".
           && (!SoPrazoVencido || g.PrazoVencido)
           && Busca.Casa(g.PacienteNome, Paciente)
           && Busca.Casa(g.Codigo.NumeroGuiaReal, Guia);
}

/// <summary>
/// O que a tela de Glosas e recursos AFIRMA (out/2026, do <c>GlosasViewModel</c> WPF).
/// A carga traz TODAS as glosas e o recorte é em memória: é isso que dá o total honesto do
/// resumo — a caixinha "em aberto", ligada por padrão, escondia o resto sem dizer.
/// </summary>
public static class GlosasNaTela
{
    /// <summary>Até 3 dias (ou vencido) é vermelho; até 7, amarelo; o resto e sem prazo, verde.</summary>
    public static NivelUrgencia Urgencia(int? dias)
        => dias is null ? NivelUrgencia.Verde
            : dias <= 3 ? NivelUrgencia.Vermelho
            : dias <= 7 ? NivelUrgencia.Amarelo
            : NivelUrgencia.Verde;

    public static GlosaNaTela Linha(CodigoFaturamento c, DateOnly hoje)
    {
        var dias = c.DiasParaFimRecurso(hoje);
        return new GlosaNaTela(c, MotivosGlosa.Descricao(c.MotivoGlosaCodigo), c.DataLimiteRecurso, dias, Urgencia(dias));
    }

    public static IReadOnlyList<GlosaNaTela> Montar(IEnumerable<CodigoFaturamento> codigos, DateOnly hoje)
        => codigos.Select(c => Linha(c, hoje)).ToList();

    public static IReadOnlyList<GlosaNaTela> Filtrar(IEnumerable<GlosaNaTela> todas, FiltroGlosas filtro)
        => todas.Where(filtro.Casa).ToList();

    /// <summary>
    /// O resumo diz que a lista é PARCIAL: "5 em aberto de 40 no total" e "40 glosas"
    /// respondem perguntas diferentes.
    /// </summary>
    public static string Resumo(int mostradas, int total, FiltroGlosas filtro)
        => filtro.Ativo
            ? $"{mostradas} de {total} glosa(s) no filtro"
            : filtro.SomenteEmAberto
                ? $"{mostradas} glosa(s) em aberto de {total} no total"
                : $"{mostradas} glosa(s)";

    /// <summary>"Nenhuma glosa" e "nenhuma bate com o filtro" pedem ações opostas.</summary>
    public static (string Titulo, string Descricao) Vazio(int total, FiltroGlosas filtro)
    {
        if (filtro.Ativo)
            return ("Nenhuma glosa bate com o filtro", "Limpe o filtro para ver as outras.");
        if (filtro.SomenteEmAberto && total > 0)
            return ("Nenhuma glosa em aberto", $"As {total} glosa(s) registradas já foram recuperadas — desmarque \"só em aberto\" para vê-las.");
        return ("Nenhuma glosa registrada", "Quando o convênio recusar uma guia baixada, registre a glosa na tela de guias faturadas — ela aparece aqui com o prazo de recurso.");
    }

    /// <summary>
    /// Os três motivos que mais glosam esta clínica. Sobre o recorte "em aberto", NUNCA
    /// sobre o texto digitado: o padrão é da CLÍNICA, e não pode mudar porque alguém
    /// procurou a Maria.
    /// </summary>
    public static IReadOnlyList<MotivoDeGlosaRecorrente> MotivosRecorrentes(IEnumerable<GlosaNaTela> todas, bool somenteEmAberto)
        => todas
            .Where(g => !somenteEmAberto || g.Codigo.GlosaEmAberto)
            .Where(g => !string.IsNullOrWhiteSpace(g.Codigo.MotivoGlosaCodigo))
            .GroupBy(g => g.Codigo.MotivoGlosaCodigo!)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .Take(3)
            .Select(grupo =>
            {
                var motivo = MotivosGlosa.Obter(grupo.Key);
                return new MotivoDeGlosaRecorrente(grupo.Key, motivo?.Descricao ?? "código fora do catálogo", grupo.Count(),
                    motivo?.ComoEvitar ?? string.Empty);
            })
            .ToList();

    /// <summary>
    /// As guias que entram no XML de recurso: as glosas EM ABERTO da lista que está na tela
    /// (o recorte do filtro), como no WPF. Recuperada não se recorre.
    /// </summary>
    public static IReadOnlyList<CodigoFaturamento> ParaRecurso(IEnumerable<GlosaNaTela> mostradas)
        => mostradas.Where(l => l.Codigo.GlosaEmAberto).Select(l => l.Codigo).ToList();

    /// <summary>Em aberto, com prazo vencido e vencendo em até 7 dias — sobre TODAS, nunca sobre o filtro.</summary>
    public static (int EmAberto, int Vencidas, int VencendoEmSete) Contagem(IEnumerable<GlosaNaTela> todas)
    {
        var abertas = todas.Where(g => g.Codigo.GlosaEmAberto).ToList();
        return (abertas.Count,
            abertas.Count(g => g.PrazoVencido),
            abertas.Count(g => g.DiasParaFimPrazo is >= 0 and <= 7));
    }

    /// <summary>
    /// A busca do guia "Por que as guias são glosadas": código, descrição, significado ou
    /// como evitar — sem acento e sem caixa (a regra de filtro da casa, <see cref="Busca"/>).
    /// </summary>
    public static IReadOnlyList<MotivoGlosa> FiltrarMotivos(string? termo)
        => string.IsNullOrWhiteSpace(termo)
            ? MotivosGlosa.Todos
            : MotivosGlosa.Todos.Where(m =>
                Busca.Casa(m.Codigo, termo) || Busca.Casa(m.Descricao, termo)
                || Busca.Casa(m.Significado, termo) || Busca.Casa(m.ComoEvitar, termo)).ToList();
}
