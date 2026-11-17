using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Modelos;

/// <summary>
/// O ATENDIMENTO DE INFERTILIDADE no agendamento (nov/2026 — pedido da direção: "cadastrar o
/// marido para consulta, atendimento de infertilidade, e sinalizar na guia de quem vai passar:
/// geriatria ou psiquiatria. Isso tem que estar sinalizado no sistema para a gente saber para
/// onde está lançando e ficar rastreável — já no agendamento").
///
/// O que esta classe decide: quais guias internas podem ser escolhidas, quando a escolha é
/// obrigatória e o que as telas ESCREVEM. É classificação INTERNA (controle, relatório,
/// separação de repasse): nada daqui entra em regra de guia do convênio — ver o aviso em
/// <see cref="Agendamento.InfertilidadeGuiaDe"/>.
/// </summary>
public static class InfertilidadeNaTela
{
    public const string Rotulo = "Atendimento de infertilidade";

    /// <summary>
    /// A consulta é de INFERTILIDADE? É o tipo de atendimento (a especialidade da consulta) que
    /// pede a guia interna — uma porta só: quem escolhe "Infertilidade" no que foi pedido já
    /// disse o que é, e a tela só pergunta onde lançar.
    /// </summary>
    public static bool EhInfertilidade(bool ehConsulta, string? especialidadeCodigo)
        => ehConsulta && string.Equals(especialidadeCodigo, Clinica.Application.Servicos.EspecialidadeCatalogoService.CodigoInfertilidade,
            StringComparison.OrdinalIgnoreCase);
    public const string RotuloDaGuia = "Lançar na guia de";
    public const string Dica = "Controle interno: diz em qual guia este atendimento é lançado, para o relatório e o repasse.";

    /// <summary>
    /// As guias internas em que o atendimento de infertilidade pode ser lançado: geriatria e
    /// psiquiatria, as duas que a direção nomeou. São códigos do catálogo de especialidades.
    /// </summary>
    public static readonly IReadOnlyList<string> Guias =
        [nameof(Especialidade.Geriatria), nameof(Especialidade.Psiquiatria)];

    /// <summary>As opções do seletor, com o nome que a clínica deu a cada especialidade.</summary>
    public static IReadOnlyList<(string Codigo, string Nome)> Opcoes
        => Guias.Select(g => (g, CatalogoEspecialidades.Nome(g))).ToList();

    /// <summary>
    /// O que vai para a coluna: o código normalizado, ou nulo quando não é infertilidade.
    /// Marcou infertilidade e não disse a guia → recusa, com a frase que diz o que falta:
    /// o pedido é justamente saber PARA ONDE foi lançado.
    /// </summary>
    public static string? Normalizar(bool infertilidade, string? guiaDe)
    {
        if (!infertilidade) return null;
        var escolhida = Guias.FirstOrDefault(g => string.Equals(g, guiaDe?.Trim(), StringComparison.OrdinalIgnoreCase));
        return escolhida ?? throw new InvalidOperationException(RecusaSemGuia);
    }

    public static string RecusaSemGuia
        => $"Atendimento de infertilidade: escolha em qual guia ele é lançado ({string.Join(" ou ", Opcoes.Select(o => o.Nome))}).";

    /// <summary>"Infertilidade · guia Geriatria" — o selo da agenda; nulo quando não é.</summary>
    public static string? Selo(string? guiaDe)
        => string.IsNullOrWhiteSpace(guiaDe) ? null : $"Infertilidade · guia {CatalogoEspecialidades.Nome(guiaDe)}";

    /// <summary>A dica do selo: o que ele quer dizer, e que é interno.</summary>
    public static string? DicaDoSelo(string? guiaDe)
        => string.IsNullOrWhiteSpace(guiaDe) ? null
            : $"Atendimento de infertilidade lançado na guia de {CatalogoEspecialidades.Nome(guiaDe)} — controle interno, para relatório e repasse.";
}
