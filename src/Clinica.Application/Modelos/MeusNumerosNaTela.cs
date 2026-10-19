using System.Globalization;

namespace Clinica.Application.Modelos;

/// <summary>
/// Os oito números da tela "Meus números", já escritos como a tela os mostra. Métrica sem base é
/// "—", nunca zero, e a barra (<c>Tem…</c>) segue a MESMA condição do número.
/// </summary>
public sealed record NumerosDoProfissional(
    bool SemMovimento,
    string Atendidos, string Pacientes, string Faltas, string NoShow, string Ocupacao,
    string Completude, string Evolucoes, string MelhoraDor,
    double? NoShowFracao, double? OcupacaoFracao, double? CompletudeFracao,
    string LeituraCompletude,
    VariacaoKpi? VariacaoAtendidos, VariacaoKpi? VariacaoPacientes, VariacaoKpi? VariacaoFaltas,
    VariacaoKpi? VariacaoNoShow, VariacaoKpi? VariacaoOcupacao, VariacaoKpi? VariacaoCompletude,
    VariacaoKpi? VariacaoEvolucoes, VariacaoKpi? VariacaoMelhoraDor);

/// <summary>
/// "MEUS NÚMEROS" — a produtividade de quem atende, na tela DELE (out/2026; era o
/// <c>MeusNumerosViewModel</c> WPF, parcela 39). Mostra só quem está logado e não compara com
/// colegas; sem vínculo, soma a clínica inteira e a tela DIZ isso.
/// </summary>
public static class MeusNumerosNaTela
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public const string RotuloDelta = "vs. período anterior";

    /// <summary>Os períodos oferecidos. Mês corrente é o padrão: é o ciclo da clínica.</summary>
    public static readonly IReadOnlyList<string> Periodos = ["Este mês", "Mês passado", "Últimos 90 dias", "Este ano"];

    public const string PeriodoPadrao = "Este mês";

    public const string TodosOsProfissionais = "Todos os profissionais";

    public const string FraseSemPermissaoPendentes =
        "A lista de sessões sem evolução é prontuário, e o seu acesso não tem essa permissão. A direção libera em Acessos.";

    /// <summary>O intervalo de cada período (o mês passado inteiro; os outros até hoje).</summary>
    public static (DateOnly Inicio, DateOnly Fim) Intervalo(string periodo, DateOnly hoje) => periodo switch
    {
        "Mês passado" => (new DateOnly(hoje.Year, hoje.Month, 1).AddMonths(-1), new DateOnly(hoje.Year, hoje.Month, 1).AddDays(-1)),
        "Últimos 90 dias" => (hoje.AddDays(-89), hoje),
        "Este ano" => (new DateOnly(hoje.Year, 1, 1), hoje),
        _ => (new DateOnly(hoje.Year, hoje.Month, 1), hoje)
    };

    public static string TextoDoIntervalo(DateOnly inicio, DateOnly fim)
        => $"{inicio.ToString("dd/MM/yyyy", PtBr)} a {fim.ToString("dd/MM/yyyy", PtBr)}";

    /// <summary>A dica do delta: o trecho exato contra o qual se compara.</summary>
    public static string DetalheDoTrecho((DateOnly Inicio, DateOnly Fim)? trecho)
        => trecho is { } t ? $"Trecho anterior equivalente: {TextoDoIntervalo(t.Inicio, t.Fim)}" : string.Empty;

    /// <summary>
    /// A linha da pessoa: com vínculo, a dela; sem vínculo, a SOMA da clínica. O anterior é
    /// escolhido pela MESMA régua — comparar o meu mês com a soma da casa seria delta entre
    /// duas perguntas diferentes.
    /// </summary>
    public static ProdutividadeProfissional? Escolher(IReadOnlyList<ProdutividadeProfissional> todos, int? profissionalId)
        => profissionalId is { } id ? todos.FirstOrDefault(p => p.ProfissionalId == id) : Somar(todos);

    public static string NomeDoProfissional(int? profissionalId, ProdutividadeProfissional? meu)
        => profissionalId is null ? TodosOsProfissionais : meu?.Nome ?? "Profissional";

    /// <summary>
    /// Soma da clínica. As TAXAS não se somam: são recalculadas sobre os totais (o registro as
    /// deriva), senão a média de percentuais daria um número que não é leitura nenhuma.
    /// Pacientes distintos por profissional se somam com duplicação — por isso a tela não os
    /// apresenta como exatos sem vínculo.
    /// </summary>
    public static ProdutividadeProfissional? Somar(IReadOnlyList<ProdutividadeProfissional> todos)
    {
        if (todos.Count == 0) return null;
        var evas = todos.Where(p => p.MelhoraMediaEva is not null).ToList();
        return new ProdutividadeProfissional(
            ProfissionalId: null,
            Nome: TodosOsProfissionais,
            Agendados: todos.Sum(p => p.Agendados),
            Atendidos: todos.Sum(p => p.Atendidos),
            Faltas: todos.Sum(p => p.Faltas),
            Cancelados: todos.Sum(p => p.Cancelados),
            Encaixes: todos.Sum(p => p.Encaixes),
            PacientesDistintos: todos.Sum(p => p.PacientesDistintos),
            MinutosOcupados: todos.Sum(p => p.MinutosOcupados),
            DiasComAgenda: todos.Sum(p => p.DiasComAgenda),
            MinutosDisponiveis: todos.Sum(p => p.MinutosDisponiveis),
            Evolucoes: todos.Sum(p => p.Evolucoes),
            MelhoraMediaEva: evas.Count == 0 ? null : evas.Average(p => p.MelhoraMediaEva!.Value));
    }

    private static string Pct(double v) => v.ToString("0.#", PtBr) + "%";

    /// <summary>
    /// Os números. Sem linha no período a pessoa não trabalhou nele — e isso é "—", nunca zero:
    /// 0% de ocupação e "não houve agenda" são coisas diferentes.
    /// </summary>
    public static NumerosDoProfissional Montar(ProdutividadeProfissional? p, ProdutividadeProfissional? anterior, string detalheDelta)
    {
        var semMovimento = p is null || p.Agendados == 0;
        if (p is null)
            return new NumerosDoProfissional(semMovimento,
                "—", "—", "—", "—", "—", "—", "—", "—",
                null, null, null, string.Empty,
                null, null, null, null, null, null, null, null);

        var temNoShow = p.Fechados > 0;
        string completude; double? completudeFracao; string leitura;
        if (p.CompletudeProntuario is { } c)
        {
            completude = Pct(c);
            completudeFracao = Math.Clamp(c / 100.0, 0, 1);
            leitura = c switch
            {
                >= 95 => "Praticamente toda sessão atendida tem registro escrito.",
                >= 80 => "A maior parte tem registro; o que falta ainda dá para escrever.",
                _ => "Boa parte das sessões atendidas ficou sem registro escrito."
            };
        }
        else
        {
            // Nula, nunca 0% — sem sessão atendida, acusar de negligência quem tirou férias.
            completude = "—";
            completudeFracao = null;
            leitura = "Nenhuma sessão atendida no período — não há o que medir.";
        }

        return new NumerosDoProfissional(
            semMovimento,
            Atendidos: p.Atendidos.ToString(PtBr),
            Pacientes: p.PacientesDistintos.ToString(PtBr),
            Faltas: p.Faltas.ToString(PtBr),
            NoShow: temNoShow ? Pct(p.TaxaNoShow) : "—",
            Ocupacao: p.OcupacaoPercentual is { } o ? Pct(o) : "—",
            Completude: completude,
            Evolucoes: p.Evolucoes.ToString(PtBr),
            MelhoraDor: p.MelhoraMediaEva is { } eva ? $"{eva.ToString("0.#", PtBr)} pontos" : "—",
            NoShowFracao: temNoShow ? Math.Clamp(p.TaxaNoShow / 100.0, 0, 1) : null,
            OcupacaoFracao: p.OcupacaoPercentual is { } oc ? Math.Clamp(oc / 100.0, 0, 1) : null,
            CompletudeFracao: completudeFracao,
            LeituraCompletude: leitura,
            VariacaoAtendidos: VariacaoKpi.Relativa(p.Atendidos, anterior?.Atendidos, RotuloDelta, detalheDelta),
            VariacaoPacientes: VariacaoKpi.Relativa(p.PacientesDistintos, anterior?.PacientesDistintos, RotuloDelta, detalheDelta),
            VariacaoFaltas: VariacaoKpi.Relativa(p.Faltas, anterior?.Faltas, RotuloDelta, detalheDelta, melhorQuandoMenor: true),
            VariacaoNoShow: VariacaoKpi.EmPontos(
                p.Fechados > 0 ? p.TaxaNoShow : null,
                anterior is { Fechados: > 0 } ? anterior.TaxaNoShow : null,
                RotuloDelta, detalheDelta, melhorQuandoMenor: true),
            VariacaoOcupacao: VariacaoKpi.EmPontos(p.OcupacaoPercentual, anterior?.OcupacaoPercentual,
                RotuloDelta, detalheDelta, melhorQuandoMenor: false),
            VariacaoCompletude: VariacaoKpi.EmPontos(p.CompletudeProntuario, anterior?.CompletudeProntuario,
                RotuloDelta, detalheDelta, melhorQuandoMenor: false),
            VariacaoEvolucoes: VariacaoKpi.Relativa(p.Evolucoes, anterior?.Evolucoes, RotuloDelta, detalheDelta),
            VariacaoMelhoraDor: VariacaoKpi.EmValor(p.MelhoraMediaEva, anterior?.MelhoraMediaEva,
                "pt", RotuloDelta, detalheDelta, melhorQuandoMenor: false));
    }

    /// <summary>
    /// A dívida é de HOJE, não do período: ela é fila de trabalho, e recortá-la pelo filtro faria
    /// escolher "mês passado" esconder o que está em aberto agora.
    /// </summary>
    public static string Divida(int n)
    {
        var corte = PoliticaRegistroPendente.DataInicio.ToString("dd/MM/yyyy", PtBr);
        return n switch
        {
            0 => "Nenhuma sessão em aberto — prontuário em dia.",
            1 => $"1 sessão desde {corte} continua sem evolução escrita.",
            _ => $"{n} sessões desde {corte} continuam sem evolução escrita."
        };
    }
}
