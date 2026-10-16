using System.Globalization;
using Clinica.Application.Abstracoes;
using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>O resumo por atendente — a "folha" da comissão do período.</summary>
public sealed record ResumoAtendenteRecuperacao(int UsuarioId, string Atendente, int Recuperados,
    int AValidar, int SemValor, int Validados, int Recusados, decimal ValorValidado, decimal ValorAValidar);

/// <summary>
/// O que decide a comissão de recuperação e não depende do banco (out/2026): qual regra de
/// valor vale, e as contas da verificação. Puro, para o <c>dotnet test</c> alcançar.
/// </summary>
public static class ComissaoRecuperacaoRegras
{
    private static readonly CultureInfo PtBr = new("pt-BR");

    /// <summary>
    /// A regra que vale para a recuperação: ativa, vigente na data do RETORNO (não na de
    /// hoje — o mês validado não muda com o reajuste) e a mais específica. Empate na mesma
    /// especificidade: a vigência mais recente, depois o Id — sem o desempate, duas leituras
    /// do mesmo dado poderiam propor valores diferentes.
    /// </summary>
    public static RegraComissaoRecuperacao? Vigente(IEnumerable<RegraComissaoRecuperacao> regras,
        TipoAcompanhamento tipo, ModalidadeAtendimento modalidade, DateOnly data)
        => regras
            .Where(r => r.Ativa && r.VigenteDe <= data && (r.VigenteAte is null || r.VigenteAte >= data)
                && (r.Tipo is null || r.Tipo == tipo) && (r.Modalidade is null || r.Modalidade == modalidade))
            .OrderByDescending(r => (r.Tipo is null ? 0 : 1) + (r.Modalidade is null ? 0 : 2))
            .ThenByDescending(r => r.VigenteDe)
            .ThenByDescending(r => r.Id)
            .FirstOrDefault();

    /// <summary>A frase da regra — vai COPIADA na validação, para o mês passado continuar dizendo o que valeu.</summary>
    public static string Descrever(RegraComissaoRecuperacao r)
        => $"{r.Valor.ToString("C", PtBr)} por paciente — "
           + string.Join(", ", new[]
           {
               r.Tipo switch { TipoAcompanhamento.NovoBsv => "novos BSV", TipoAcompanhamento.Recall => "recall", _ => "recall e novos BSV" },
               r.Modalidade is { } m ? RotulosEnum.De(m).ToLower(PtBr) : "todas as modalidades"
           })
           + $" · desde {r.VigenteDe.ToString("dd/MM/yyyy", PtBr)}"
           + (r.VigenteAte is { } ate ? $" até {ate.ToString("dd/MM/yyyy", PtBr)}" : "");

    /// <summary>A regra só é aceita com valor positivo e vigência coerente — nulo = pode gravar.</summary>
    public static string? Impedimento(RegraComissaoRecuperacao r)
    {
        if (r.Valor <= 0) return "Informe o valor por paciente recuperado (maior que zero).";
        if (r.Valor > 100_000) return "O valor por paciente parece digitado errado — confira.";
        if (r.VigenteAte is { } ate && ate < r.VigenteDe) return "O fim da vigência é anterior ao início.";
        if (r.Tipo is { } t && !Enum.IsDefined(t)) return "Escolha uma fila válida.";
        if (r.Modalidade is { } m && !Enum.IsDefined(m)) return "Escolha uma modalidade válida.";
        return null;
    }

    /// <summary>
    /// Quem recebe não valida a própria comissão: é a separação que dá valor à verificação
    /// (o mesmo princípio de quem prescreve não ser quem executa).
    /// </summary>
    public static string? ImpedimentoDeValidar(LinhaRecuperacao l, string loginDeQuemValida, string loginDaAtendente)
    {
        if (string.Equals(loginDeQuemValida, loginDaAtendente, StringComparison.OrdinalIgnoreCase))
            return $"{l.Paciente}: a comissão é sua — outra pessoa da gestão precisa validar.";
        return l.Situacao switch
        {
            SituacaoRecuperacao.Validada => $"{l.Paciente}: já está validada.",
            SituacaoRecuperacao.Recusada => $"{l.Paciente}: foi recusada — desfaça a recusa antes.",
            SituacaoRecuperacao.SemValor => $"{l.Paciente}: não há valor cadastrado para a fila e a modalidade na data do retorno.",
            _ => null
        };
    }

    /// <summary>O resumo por atendente, do que mais tem a validar para o que menos tem.</summary>
    public static IReadOnlyList<ResumoAtendenteRecuperacao> PorAtendente(IEnumerable<LinhaRecuperacao> linhas)
        => linhas.GroupBy(l => (l.UsuarioId, l.Atendente))
            .Select(g => new ResumoAtendenteRecuperacao(g.Key.UsuarioId, g.Key.Atendente, g.Count(),
                g.Count(l => l.Situacao == SituacaoRecuperacao.AValidar),
                g.Count(l => l.Situacao == SituacaoRecuperacao.SemValor),
                g.Count(l => l.Situacao == SituacaoRecuperacao.Validada),
                g.Count(l => l.Situacao == SituacaoRecuperacao.Recusada),
                g.Where(l => l.Situacao == SituacaoRecuperacao.Validada).Sum(l => l.ValorDecidido ?? 0),
                g.Where(l => l.Situacao == SituacaoRecuperacao.AValidar).Sum(l => l.ValorProposto ?? 0)))
            .OrderByDescending(r => r.AValidar).ThenByDescending(r => r.Recuperados)
            .ThenBy(r => r.Atendente, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    /// <summary>A situação escrita na linha.</summary>
    public static (string Texto, string Tom) Selo(LinhaRecuperacao l) => l.Situacao switch
    {
        SituacaoRecuperacao.Validada when l.EstornadaDepois => ("Validada — sessão estornada depois", "perigo"),
        SituacaoRecuperacao.Validada => ("Validada", "sucesso"),
        SituacaoRecuperacao.Recusada => ("Recusada", "neutro"),
        SituacaoRecuperacao.SemValor => ("Sem valor cadastrado", "aviso"),
        _ => ("A validar", "info")
    };
}
