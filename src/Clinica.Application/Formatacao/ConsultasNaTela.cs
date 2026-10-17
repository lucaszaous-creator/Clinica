using System.Globalization;
using Clinica.Application.Modelos;
using Clinica.Domain.Regras;

namespace Clinica.Application.Formatacao;

/// <summary>Uma linha da lista de consultas renováveis, com a situação e o convênio JÁ RESOLVIDOS.</summary>
public sealed record LinhaConsultaBalcao(
    StatusConsultaPaciente Status, string Paciente, string Convenio, string UltimaEmissao,
    string Vencimento, string Situacao, string SituacaoFiltro)
{
    public bool EhVermelha => Status.Urgencia == NivelUrgencia.Vermelho;
    public bool EhAmarela => Status.Urgencia == NivelUrgencia.Amarelo;
    /// <summary>Convênio sem consulta renovável não tem o que renovar — o botão fica apagado.</summary>
    public bool TemRenovacao => Status.UsaConsulta;
}

/// <summary>
/// O que a tela "Consultas de convênio" AFIRMA (out/2026, migrado do
/// <c>ConsultasViewModel</c> WPF). A situação como opção de filtro deriva do MESMO estado que
/// pinta o semáforo e escreve a frase — uma segunda classificação divergiria da primeira.
/// </summary>
public static class ConsultasNaTela
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public const string TodasSituacoes = "Todas as situações";
    public const string SituacaoPrecisaRenovar = "Precisa renovar";
    public const string SituacaoVenceEmBreve = "Vence em breve";
    public const string SituacaoEmDia = "Em dia";
    public const string SituacaoNuncaEmitiu = "Nunca emitiu";
    public const string SituacaoNaoUsa = "Não usa consulta";
    public const string TodosConvenios = "Todos os convênios";

    public static readonly IReadOnlyList<string> OpcoesSituacao =
    [
        TodasSituacoes, SituacaoPrecisaRenovar, SituacaoVenceEmBreve, SituacaoEmDia, SituacaoNuncaEmitiu, SituacaoNaoUsa
    ];

    public static string SituacaoFiltro(StatusConsultaPaciente s) =>
        !s.UsaConsulta ? SituacaoNaoUsa
        : s.PrecisaRenovar ? SituacaoPrecisaRenovar
        : s.Urgencia == NivelUrgencia.Amarelo ? SituacaoVenceEmBreve
        : s.UltimaEmissao is null ? SituacaoNuncaEmitiu
        : SituacaoEmDia;

    /// <summary>
    /// A frase do modelo (<c>AvisoRenovacao</c>) é a mesma da agenda e do balcão; quando não há
    /// o que renovar, a linha diz o estado.
    /// </summary>
    public static string Situacao(StatusConsultaPaciente s) => s.AvisoRenovacao
        ?? (!s.UsaConsulta ? "O convênio não usa consulta renovável."
            : s.UltimaEmissao is null ? "Nunca emitiu consulta."
            : "Em dia.");

    public static LinhaConsultaBalcao Linha(StatusConsultaPaciente s) => new(
        s,
        s.PacienteNome,
        CatalogoConvenios.Nome(s.ConvenioCodigo, s.Convenio),
        s.UltimaEmissao?.ToString("dd/MM/yyyy", PtBr) ?? "nunca emitida",
        s.Vencimento?.ToString("dd/MM/yyyy", PtBr) ?? "—",
        Situacao(s),
        SituacaoFiltro(s));

    /// <summary>Quem precisa renovar primeiro: a lista existe para agir.</summary>
    public static IReadOnlyList<LinhaConsultaBalcao> Ordenar(IEnumerable<StatusConsultaPaciente> lista)
        => lista.OrderByDescending(c => c.PrecisaRenovar)
            .ThenBy(c => c.DiasParaVencer ?? int.MaxValue)
            .ThenBy(c => c.PacienteNome, StringComparer.Create(PtBr, true))
            .Select(Linha).ToList();

    public static bool FiltroAtivo(string situacao, string convenio, string? paciente)
        => situacao != TodasSituacoes || convenio != TodosConvenios || !string.IsNullOrWhiteSpace(paciente);

    public static bool Passa(LinhaConsultaBalcao l, string situacao, string convenio, string? paciente)
        => (situacao == TodasSituacoes || l.SituacaoFiltro == situacao)
           && (convenio == TodosConvenios || l.Convenio == convenio)
           && Busca.Casa(l.Paciente, paciente);

    /// <summary>As operadoras da CARTEIRA carregada — oferecer as sem paciente daria filtro que só leva a vazio.</summary>
    public static IReadOnlyList<string> Convenios(IEnumerable<LinhaConsultaBalcao> linhas)
        => [TodosConvenios, .. linhas.Select(l => l.Convenio).Distinct().OrderBy(n => n, StringComparer.Create(PtBr, false))];

    /// <summary>O resumo DIZ que está filtrado: "12 pacientes" e "12 de 90 no filtro" respondem perguntas diferentes.</summary>
    public static string Resumo(int mostradas, int total, int precisamRenovar, bool filtroAtivo)
        => total == 0
            ? "Nenhum paciente de convênio com consulta renovável."
            : filtroAtivo
                ? $"{mostradas} de {total} paciente(s) no filtro · {precisamRenovar} precisam renovar no total."
                : precisamRenovar == 0
                    ? $"{total} paciente(s) — nenhum precisa renovar hoje."
                    : $"{precisamRenovar} de {total} paciente(s) precisam renovar a consulta.";

    /// <summary>"Nenhum paciente com consulta" e "nenhum bate com o filtro" são respostas diferentes (parcela 25).</summary>
    public static string VazioDescricao(bool filtroAtivo) => filtroAtivo
        ? "Nenhum paciente bate com o filtro — limpe-o para ver a carteira inteira."
        : "Nenhum paciente de convênio com consulta renovável foi encontrado.";
}
