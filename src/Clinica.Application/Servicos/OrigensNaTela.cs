using System.Globalization;

namespace Clinica.Application.Servicos;

/// <summary>Uma origem na tabela, já formatada para a tela.</summary>
/// <param name="Fracao">Fração da BASE. "—" sem base: 0% de zero pacientes seria alvo batido falso.</param>
/// <param name="EhSemResposta">A linha "não perguntado" vem MARCADA: quando ela encabeça a tabela,
/// o achado do relatório é o balcão ter parado de perguntar — e a direção precisa ler isso como
/// tarefa (cobrar a pergunta no cadastro), não como categoria de marketing.</param>
public sealed record LinhaDeOrigem(string Rotulo, int NaBase, int Estreias, string Fracao, bool EhSemResposta);

/// <summary>Quem indicou, no ranking.</summary>
public sealed record LinhaDeIndicador(string Nome, string Quantos);

/// <summary>Tudo o que a tela "De onde vêm os pacientes" mostra.</summary>
public sealed record PainelDeOrigens(
    string Resumo,
    IReadOnlyList<LinhaDeOrigem> Linhas,
    IReadOnlyList<LinhaDeIndicador> Indicadores);

/// <summary>
/// O que a tela "DE ONDE VÊM OS PACIENTES" do Gerente AFIRMA (out/2026, etapa 6 — a tela saiu
/// do WPF para o Blazor). Era o <c>OrigensViewModel</c>: a fração da base, o selo do "não
/// perguntado", o ranking de quem indica e a frase do resumo. Quem CALCULA continua sendo o
/// <see cref="OrigemPacientesService"/>; aqui só se formata.
///
/// O período é o MESMO das outras telas da direção (<see cref="ResumoFaturamentoNaTela.Periodos"/>),
/// com o padrão "Este ano": a pergunta desta tela ("vale manter o anúncio?") se decide olhando
/// meses, e um mês de estreias tem meia dúzia de linhas — ruído com cara de tendência.
/// </summary>
public static class OrigensNaTela
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>O período com que a tela abre.</summary>
    public const string PeriodoPadrao = ResumoFaturamentoNaTela.EsteAno;

    public static PainelDeOrigens Montar(ResumoOrigemPacientes resumo)
    {
        var total = resumo.TotalPacientes;
        var linhas = resumo.Linhas.Select(l => new LinhaDeOrigem(
            l.Rotulo,
            l.TotalNaBase,
            l.EstreiasNoPeriodo,
            total > 0 ? ((double)l.TotalNaBase / total).ToString("P0", PtBr) : "—",
            l.Origem is null)).ToList();

        var indicadores = resumo.QuemMaisIndica
            .Select(i => new LinhaDeIndicador(i.Nome, i.Indicados == 1 ? "1 paciente" : $"{i.Indicados} pacientes"))
            .ToList();

        return new PainelDeOrigens(Resumo(resumo), linhas, indicadores);
    }

    /// <summary>
    /// "12 paciente(s) na base · 3 sem a pergunta respondida · estreias de 01/01/2026 a 18/10/2026".
    /// Sem base, diz que não há paciente — e não "0 paciente(s)".
    /// </summary>
    public static string Resumo(ResumoOrigemPacientes resumo)
        => resumo.TotalPacientes == 0
            ? "Nenhum paciente cadastrado ainda."
            : $"{resumo.TotalPacientes} paciente(s) na base · {resumo.SemResposta} sem a pergunta respondida"
              + $" · estreias de {resumo.Inicio.ToString("dd/MM/yyyy", PtBr)} a {resumo.Fim.ToString("dd/MM/yyyy", PtBr)}";
}
