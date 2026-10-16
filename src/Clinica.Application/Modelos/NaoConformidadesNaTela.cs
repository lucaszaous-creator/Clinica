using Clinica.Application.Formatacao;

namespace Clinica.Application.Modelos;

/// <summary>
/// O que a tela de Não conformidades (NC) do faturamento AFIRMA — subiu do
/// <c>NaoConformidadesViewModel</c> WPF na migração para Blazor (out/2026).
///
/// Um campo só, que casa no PACIENTE e na JUSTIFICATIVA: quem procura tanto lembra o nome
/// quanto lembra o motivo ("portal fora do ar"), e dois campos obrigariam a saber de
/// antemão onde a palavra está.
/// </summary>
public static class NaoConformidadesNaTela
{
    public static bool Casa(NaoConformidadeItem n, string? termo)
        => Busca.Casa(n.PacienteNome, termo) || Busca.Casa(n.Justificativa, termo);

    public static IReadOnlyList<NaoConformidadeItem> Filtrar(IEnumerable<NaoConformidadeItem> todas, string? termo)
        => todas.Where(n => Casa(n, termo)).ToList();

    /// <summary>"3 de 12 no filtro" e "12 NCs" respondem perguntas diferentes.</summary>
    public static string Resumo(int mostradas, int total, string? termo)
        => string.IsNullOrWhiteSpace(termo)
            ? $"{total} não conformidade(s)"
            : $"{mostradas} de {total} não conformidade(s) no filtro";

    /// <summary>"Nada justificado" e "nenhuma bate com o filtro" são respostas diferentes.</summary>
    public static string VazioDescricao(string? termo)
        => string.IsNullOrWhiteSpace(termo)
            ? "Nada justificado como não conformidade até agora."
            : "Nenhuma não conformidade bate com o filtro — limpe-o para ver todas.";

    /// <summary>A justificativa para ler por inteiro (o WPF dizia isso quando estava em branco).</summary>
    public static string Justificativa(NaoConformidadeItem n)
        => string.IsNullOrWhiteSpace(n.Justificativa) ? "(sem justificativa registrada)" : n.Justificativa;
}
