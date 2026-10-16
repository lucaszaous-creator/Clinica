using System.Globalization;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Modelos;

/// <summary>
/// O que a tela "Consultar guias" AFIRMA (out/2026, migração do ConsultaGuiasViewModel e do
/// StatusCodigoConverter do WPF para Blazor). Mora aqui, pura, porque Razor não decide regra
/// e o `dotnet test` não alcança projeto de tela.
///
/// Três decisões que o WPF já tomava e continuam valendo:
/// - a lista filtrada DIZ que está filtrada ("12 guias" e "12 guias de acupuntura em
///   psiquiatria" respondem perguntas diferentes, e quem volta à tela depois do café não
///   lembra o que deixou marcado);
/// - a consulta traz no máximo <see cref="Limite"/> linhas (o <c>Take</c> do repositório),
///   e bater no teto é DITO — "500 guias" que são 500 de 4.000 é a lição da auditoria;
/// - o filtro de convênio é pela FAMÍLIA (parcela 45): a variante responde junto da embutida.
/// </summary>
public static class ConsultaDeGuiasNaTela
{
    /// <summary>O teto do <c>ConsultarCodigosAsync</c> — as mais recentes pelo atendimento.</summary>
    public const int Limite = 500;

    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>Rótulo de cada opção do filtro de situação — o enum nunca aparece cru.</summary>
    public static string Rotulo(FiltroStatusGuia status) => status switch
    {
        FiltroStatusGuia.Aberto => "Em aberto",
        FiltroStatusGuia.Baixado => "Baixadas",
        FiltroStatusGuia.Glosado => "Com glosa",
        _ => "Todas"
    };

    /// <summary>
    /// Nome da FAMÍLIA de convênio no filtro. Personalizado sai neutro de propósito
    /// (<see cref="CatalogoConvenios.Nome(Convenio)"/>): a família não sabe qual operadora é.
    /// </summary>
    public static string RotuloConvenio(Convenio familia) => CatalogoConvenios.Nome(familia);

    /// <summary>Algum filtro está valendo — acende o "Limpar" e muda o texto do vazio.</summary>
    public static bool FiltroAtivo(FiltroConsultaGuias f) =>
        !string.IsNullOrWhiteSpace(f.TermoPaciente)
        || !string.IsNullOrWhiteSpace(f.NumeroGuia)
        || !string.IsNullOrWhiteSpace(f.TermoObservacao)
        || f.Inicio is not null || f.Fim is not null
        || f.Status != FiltroStatusGuia.Todos
        || f.Convenio is not null || f.Modalidade is not null || f.Especialidade is not null;

    /// <summary>
    /// "12 guia(s) encontrada(s) — acupuntura · psiquiatria · baixadas". O WPF nomeava só
    /// modalidade e especialidade; aqui o recorte cobre TODO filtro marcado, porque
    /// qualquer um deles esquecido responde "nada encontrado" sobre uma clínica cheia.
    /// </summary>
    public static string Resumo(FiltroConsultaGuias f, int encontradas)
    {
        var recorte = new List<string>();
        if (f.Status != FiltroStatusGuia.Todos) recorte.Add(Rotulo(f.Status).ToLower(PtBr));
        if (f.Convenio is { } c) recorte.Add(RotuloConvenio(c));
        if (f.Modalidade is { } m) recorte.Add(ModalidadeInfo.NomeExibicao(m));
        if (f.Especialidade is { } e) recorte.Add(EspecialidadeInfo.NomeExibicao(e));
        if (!string.IsNullOrWhiteSpace(f.TermoPaciente)) recorte.Add($"paciente “{f.TermoPaciente.Trim()}”");
        if (!string.IsNullOrWhiteSpace(f.NumeroGuia)) recorte.Add($"guia “{f.NumeroGuia.Trim()}”");
        if (!string.IsNullOrWhiteSpace(f.TermoObservacao)) recorte.Add($"observação “{f.TermoObservacao.Trim()}”");
        var periodo = Periodo(f.Inicio, f.Fim);
        if (periodo is not null) recorte.Add(periodo);

        return $"{encontradas} guia(s) encontrada(s)"
               + (recorte.Count > 0 ? $" — {string.Join(" · ", recorte)}" : string.Empty)
               + (encontradas >= Limite ? $" (mostrando as {Limite} mais recentes — estreite o filtro)" : string.Empty);
    }

    private static string? Periodo(DateOnly? inicio, DateOnly? fim) => (inicio, fim) switch
    {
        ({ } i, { } f) => $"atendidas de {D(i)} a {D(f)}",
        ({ } i, null) => $"atendidas desde {D(i)}",
        (null, { } f) => $"atendidas até {D(f)}",
        _ => null
    };

    /// <summary>A data final antes da inicial não devolve nada — a tela avisa em vez de buscar.</summary>
    public static bool PeriodoInvertido(DateOnly? inicio, DateOnly? fim)
        => inicio is { } i && fim is { } f && f < i;

    private static string D(DateOnly d) => d.ToString("dd/MM/yyyy", PtBr);

    /// <summary>
    /// A situação da guia em uma frase e o tom do selo. É o <c>StatusCodigoConverter</c> do
    /// WPF com uma correção: lá a NÃO CONFORMIDADE caía no ramo de "Aberto — faturar em …",
    /// afirmando como pendente uma guia que a rodada justificou e silenciou.
    /// </summary>
    public static (string Texto, string Tom) Situacao(CodigoFaturamento c)
    {
        if (c.Baixado)
            // O número da guia já tem coluna própria; repeti-lo no selo só o faria quebrar linha.
            return ($"Baixada em {D(c.DataBaixa!.Value)}", "sucesso");

        return c.Status switch
        {
            StatusCodigo.NaoAplicavel => ("Não aplicável", "neutro"),
            StatusCodigo.NaoConformidade => ("Não conformidade (justificada)", "neutro"),
            _ => ($"A faturar em {D(c.DataPrevistaFaturamento)}", "aviso")
        };
    }

    /// <summary>Glosa da guia: nulo quando não há (a coluna mostra um traço), e o tom do selo.</summary>
    public static (string Texto, string Tom)? Glosa(StatusGlosa glosa) => glosa switch
    {
        StatusGlosa.Glosada => ("Glosada", "perigo"),
        StatusGlosa.Reapresentada => ("Reapresentada", "aviso"),
        StatusGlosa.Recuperada => ("Recuperada", "sucesso"),
        _ => null
    };

    /// <summary>O que a guia É: "Acupuntura · 1º código".</summary>
    public static string Codigo(CodigoFaturamento c) => $"{RotulosEnum.De(c.Tipo)} · {RotulosEnum.De(c.Ordem)}";

    /// <summary>Modalidade do atendimento pelo CATÁLOGO (a variante cadastrada pelo nome dela).</summary>
    public static string Modalidade(Atendimento? a)
        => a is null ? "—" : CatalogoModalidades.Nome(a.ModalidadeCodigo, a.Modalidade);

    /// <summary>
    /// Especialidade da GUIA, com o atendimento como caminho de baixo — a MESMA regra do
    /// filtro do repositório: a coluna não pode discordar do filtro que a trouxe.
    /// </summary>
    public static string? Especialidade(CodigoFaturamento c)
    {
        if (!string.IsNullOrWhiteSpace(c.EspecialidadeCodigo)) return CatalogoEspecialidades.Nome(c.EspecialidadeCodigo);
        if (c.Especialidade is { } e) return EspecialidadeInfo.NomeExibicao(e);
        if (!string.IsNullOrWhiteSpace(c.Atendimento?.EspecialidadeConsultaCodigo))
            return CatalogoEspecialidades.Nome(c.Atendimento.EspecialidadeConsultaCodigo);
        return c.Atendimento?.EspecialidadeConsulta is { } ea ? EspecialidadeInfo.NomeExibicao(ea) : null;
    }

    /// <summary>Convênio da linha: a OPERADORA, pelo código do paciente (nunca a família sozinha).</summary>
    public static string Convenio(CodigoFaturamento c)
        => c.Atendimento?.Paciente?.ConvenioNome ?? CatalogoConvenios.Nome(default(Convenio));

    /// <summary>
    /// De que guia é o histórico / a glosa. O número REAL vem primeiro: é por ele que a
    /// operadora e o faturista se referem à guia; o Id interno não diz nada fora do banco.
    /// </summary>
    public static string Descrever(CodigoFaturamento guia)
    {
        var partes = new List<string>();
        if (!string.IsNullOrWhiteSpace(guia.NumeroGuiaReal)) partes.Add($"Guia {guia.NumeroGuiaReal}");
        if (!string.IsNullOrWhiteSpace(guia.Descricao)) partes.Add(guia.Descricao!);
        partes.Add($"prevista para {D(guia.DataPrevistaFaturamento)}");
        if (guia.Atendimento?.Paciente?.Nome is { } paciente) partes.Add(paciente);
        return string.Join(" · ", partes);
    }
}
