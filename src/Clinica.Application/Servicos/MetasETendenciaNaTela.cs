using System.Globalization;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>Como a linha da meta se pinta: a cor repete o que o número diz.</summary>
public enum TomDaMeta { Neutro, Sucesso, Aviso, Perigo }

/// <summary>Uma meta do mês na tela, com o realizado e a projeção já escritos.</summary>
public sealed record LinhaMetaNaTela(
    int MetaId, int Ano, int Mes, IndicadorMeta Indicador, decimal Alvo,
    int? ProfissionalId, string ModalidadeCodigo, string EspecialidadeCodigo, string? Observacoes,
    string Rotulo, string Dono, string Recorte,
    string AlvoTexto, string RealizadoTexto, string ProjecaoTexto,
    double? Fracao, double? FracaoProjecao,
    string Situacao, TomDaMeta Tom, string? Nota,
    bool Atingida, bool EmRisco);

/// <summary>
/// O QUE A TELA DE METAS AFIRMA (out/2026, migração do MetasView do WPF para Blazor). Mora
/// aqui, pura, porque as frases e as contas decidem o que a direção lê — e o que decide o
/// que a tela afirma precisa morar onde o <c>dotnet test</c> alcança.
///
/// Três correções em relação ao WPF, as três da auditoria de linha:
/// <list type="bullet">
/// <item>a apuração casa com a meta pelo <b>Id</b> (<see cref="MetaApurada.MetaId"/>): o WPF
/// casava por indicador + mês + profissional, e com o recorte por modalidade duas metas de
/// sessões do mesmo mês recebiam o realizado uma da outra;</item>
/// <item>o alvo <b>zero</b> é aceito (<see cref="TentarLerAlvo"/>): o próprio diálogo de excluir
/// manda "editar o valor para zero" em vez de apagar, e a leitura de dinheiro do WPF recusava
/// o zero — a instrução mandava fazer o que a tela impedia;</item>
/// <item>qualquer mês é apurado, não só o corrente: mês fechado é apurado até o último dia
/// (a projeção é o próprio realizado), e só o mês FUTURO fica sem realizado.</item>
/// </list>
/// </summary>
public static class MetasNaTela
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>
    /// Até que dia o mês é apurado: hoje no mês em curso, o último dia num mês que passou, e
    /// <c>null</c> num mês que ainda não começou — realizado de mês futuro seria "zero", e zero
    /// faria todo mês planejado parecer fracasso.
    /// </summary>
    public static DateOnly? ReferenciaDeApuracao(DateOnly mes, DateOnly hoje)
    {
        var inicio = new DateOnly(mes.Year, mes.Month, 1);
        var fim = inicio.AddMonths(1).AddDays(-1);
        if (inicio > hoje) return null;
        return hoje < fim ? hoje : fim;
    }

    /// <summary>O valor na unidade do indicador, em pt-BR FIXO (a tela também roda em Linux).</summary>
    public static string Formatar(IndicadorMeta indicador, decimal valor) => indicador switch
    {
        IndicadorMeta.Faturamento => valor.ToString("C", PtBr),
        IndicadorMeta.Ocupacao => valor.ToString("0.#", PtBr) + "%",
        _ => valor.ToString("0.#", PtBr) + (valor == 1 ? " sessão" : " sessões")
    };

    /// <summary>O recorte escrito como a direção fala; sem recorte, "Todas as modalidades".</summary>
    public static string Recorte(IndicadorMeta indicador, string? modalidadeCodigo, string? especialidadeCodigo)
    {
        if (indicador == IndicadorMeta.Ocupacao) return "Agenda inteira";
        var seg = MetaService.DescreverSegmento(modalidadeCodigo, especialidadeCodigo).TrimStart(' ', '·').Trim();
        return seg.Length > 0 ? seg : "Todas as modalidades";
    }

    /// <summary>
    /// Lê o alvo digitado. Aceita o ZERO (meta zero é decisão; meta ausente é "não decidimos"),
    /// recusa o negativo e a ocupação acima de 100 — as mesmas recusas do serviço, ditas antes
    /// do clique. <paramref name="erro"/> é a frase para o campo.
    /// </summary>
    public static bool TentarLerAlvo(string? texto, IndicadorMeta indicador, out decimal valor, out string? erro)
    {
        valor = 0;
        erro = null;
        if (string.IsNullOrWhiteSpace(texto)) { erro = "Informe o valor da meta."; return false; }
        var limpo = texto.Trim().TrimEnd('%').Trim();
        if (!Formatacao.LeituraDeValores.TentarLerDecimal(limpo, out valor))
        {
            // TentarLerDecimal só devolve verdadeiro para valor POSITIVO; o zero e o negativo
            // são lidos aqui para a frase dizer o que houve.
            if (!decimal.TryParse(limpo.Replace("R$", string.Empty).Trim(), NumberStyles.Number | NumberStyles.AllowLeadingSign, PtBr, out valor)
                && !decimal.TryParse(limpo, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out valor))
            {
                erro = "Não entendi o valor. Use números, como 120 ou 45.000,00.";
                return false;
            }
            if (valor < 0) { erro = "Meta negativa não significa nada — use zero."; return false; }
        }
        if (indicador == IndicadorMeta.Ocupacao && valor > 100)
        {
            erro = "Ocupação é percentual: o máximo é 100.";
            return false;
        }
        return true;
    }

    /// <summary>
    /// A meta que já ocupa a mesma CHAVE (mês, indicador, dono e recorte). O serviço substitui
    /// o alvo dela em vez de criar outra — a tela precisa dizer isso antes do clique, senão
    /// "Nova meta" apaga em silêncio o alvo que a direção tinha declarado.
    /// </summary>
    public static MetaMensal? MesmaChave(
        IEnumerable<MetaMensal> doAno, int mes, IndicadorMeta indicador, int? profissionalId,
        string? modalidadeCodigo, string? especialidadeCodigo, int metaIdEditando)
    {
        var mod = (modalidadeCodigo ?? string.Empty).Trim();
        var esp = (especialidadeCodigo ?? string.Empty).Trim();
        return doAno.FirstOrDefault(m => m.Id != metaIdEditando
            && m.Mes == mes && m.Indicador == indicador && m.ProfissionalId == profissionalId
            && string.Equals(m.ModalidadeCodigo, mod, StringComparison.OrdinalIgnoreCase)
            && string.Equals(m.EspecialidadeCodigo, esp, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Monta as linhas das metas do mês. <paramref name="apuradas"/> vazio com
    /// <paramref name="apuracaoFalhou"/> é o terceiro estado: a linha diz que não foi possível
    /// apurar, em vez de mostrar "—" com cara de mês sem movimento.
    /// </summary>
    public static IReadOnlyList<LinhaMetaNaTela> Montar(
        IReadOnlyList<MetaMensal> doMes, IReadOnlyList<MetaApurada> apuradas,
        DateOnly mes, DateOnly hoje, bool apuracaoFalhou)
    {
        var futuro = ReferenciaDeApuracao(mes, hoje) is null;
        var emCurso = mes.Year == hoje.Year && mes.Month == hoje.Month;

        return doMes
            .OrderBy(m => m.Indicador)
            .ThenBy(m => m.ProfissionalId is null && m.ModalidadeCodigo.Length == 0 && m.EspecialidadeCodigo.Length == 0 ? 0 : 1)
            .ThenBy(m => m.Profissional?.Rotulo, StringComparer.Ordinal)
            .ThenBy(m => Recorte(m.Indicador, m.ModalidadeCodigo, m.EspecialidadeCodigo), StringComparer.Ordinal)
            .Select(m =>
            {
                var a = apuradas.FirstOrDefault(x => x.MetaId == m.Id);
                var realizado = a?.Realizado;
                var projecao = a?.Projecao;

                double? fracao = realizado is { } r
                    ? (m.Valor > 0 ? Math.Min((double)(r / m.Valor), 1.0) : 1.0)
                    : null;
                // O marcador da projeção só faz sentido no mês em curso: num mês fechado a
                // projeção É o realizado, e desenhá-la de novo seria a mesma resposta duas vezes.
                double? fracaoProj = emCurso && projecao is { } p && m.Valor > 0
                    ? Math.Min((double)(p / m.Valor), 1.0)
                    : null;

                var (situacao, tom) = Situacao(a, futuro, apuracaoFalhou, emCurso);
                string? nota = m.Indicador == IndicadorMeta.Ocupacao && m.ProfissionalId is not null
                    ? "A ocupação apurada é a da clínica inteira — a jornada não se recorta por pessoa."
                    : null;

                return new LinhaMetaNaTela(
                    m.Id, m.Ano, m.Mes, m.Indicador, m.Valor,
                    m.ProfissionalId, m.ModalidadeCodigo, m.EspecialidadeCodigo, m.Observacoes,
                    MetaMensal.Rotular(m.Indicador),
                    m.Profissional?.Rotulo ?? "Clínica",
                    Recorte(m.Indicador, m.ModalidadeCodigo, m.EspecialidadeCodigo),
                    Formatar(m.Indicador, m.Valor),
                    // "—" e não "0": sem apuração não há realizado nenhum.
                    realizado is { } rr ? Formatar(m.Indicador, rr) : "—",
                    !emCurso ? "—" : projecao is { } pp ? Formatar(m.Indicador, pp) : "—",
                    fracao, fracaoProj, situacao, tom, nota,
                    a?.Atingida ?? false, emCurso && (a?.EmRisco ?? false));
            })
            .ToList();
    }

    private static (string Texto, TomDaMeta Tom) Situacao(MetaApurada? a, bool futuro, bool falhou, bool emCurso)
    {
        if (futuro) return ("ainda não começou", TomDaMeta.Neutro);
        if (falhou) return ("não foi possível apurar", TomDaMeta.Perigo);
        if (a is null) return ("não apurada", TomDaMeta.Neutro);
        if (a.Atingida) return ("meta batida", TomDaMeta.Sucesso);
        if (a.Realizado is null) return ("sem base de cálculo", TomDaMeta.Neutro);
        if (a.EmRisco && a.DesvioProjetado is { } d)
            return emCurso
                ? ($"em risco: no ritmo atual fecha {Math.Abs(d).ToString("0.#", PtBr)}% abaixo", TomDaMeta.Aviso)
                : ($"não batida: fechou {Math.Abs(d).ToString("0.#", PtBr)}% abaixo", TomDaMeta.Perigo);
        if (a.PercentualAtingido is { } pct)
            return emCurso && a.Projecao is null
                ? ($"{pct.ToString("0.#", PtBr)}% do alvo · projeção a partir do dia {MetaService.DiasMinimosParaProjetar}", TomDaMeta.Neutro)
                : ($"{pct.ToString("0.#", PtBr)}% do alvo", TomDaMeta.Neutro);
        return ("sem base de cálculo", TomDaMeta.Neutro);
    }

    /// <summary>A frase do topo. Sem meta, diz o que isso significa — não "0 metas".</summary>
    public static string Resumo(IReadOnlyList<LinhaMetaNaTela> linhas, DateOnly mes, bool apuracaoFalhou)
    {
        var nome = mes.ToString("MMMM 'de' yyyy", PtBr);
        if (linhas.Count == 0)
            return $"Nenhuma meta para {nome}. Sem alvo, o painel mostra variação — não desempenho.";
        var partes = new List<string> { linhas.Count == 1 ? "1 meta" : $"{linhas.Count} metas" };
        if (apuracaoFalhou) partes.Add("apuração não verificada");
        else
        {
            var batidas = linhas.Count(l => l.Atingida);
            var risco = linhas.Count(l => l.EmRisco);
            if (batidas > 0) partes.Add(batidas == 1 ? "1 batida" : $"{batidas} batidas");
            if (risco > 0) partes.Add(risco == 1 ? "1 em risco" : $"{risco} em risco");
        }
        return $"{nome}: " + string.Join(" · ", partes) + ".";
    }
}

/// <summary>
/// O QUE A TELA DE TENDÊNCIAS AFIRMA — rótulos, valores e o CSV, em pt-BR fixo e com teste.
/// </summary>
public static class TendenciaNaTela
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>
    /// QUEM PODE LER. Sessões é contagem — lê quem vê os indicadores, o financeiro ou o
    /// faturamento (o faturista já conta guias por modalidade). Receita é DINHEIRO: só
    /// indicadores ou financeiro — "faturamento não é recebível", e o bit do faturamento
    /// nunca deu acesso a valor recebido. Bits num OU (<c>PodeAlgum</c>), nunca num E.
    /// </summary>
    public static Permissao Leitura(MetricaTendencia metrica) => metrica == MetricaTendencia.Receita
        ? Permissao.VerIndicadores | Permissao.VerFinanceiro
        : Permissao.VerIndicadores | Permissao.VerFinanceiro | Permissao.VerFaturamento;

    public static string Rotulo(SituacaoTendencia s) => s switch
    {
        SituacaoTendencia.EmAlta => "Em alta",
        SituacaoTendencia.EmBaixa => "Em baixa",
        SituacaoTendencia.Estavel => "Estável",
        SituacaoTendencia.Nova => "Nova",
        SituacaoTendencia.Sumiu => "Sem movimento",
        _ => "Pouca base"
    };

    /// <summary>A explicação da situação, para a dica — sobretudo do "pouca base".</summary>
    public static string Dica(SituacaoTendencia s, MetricaTendencia m) => s switch
    {
        SituacaoTendencia.PoucaBase => m == MetricaTendencia.Receita
            ? $"Movimento abaixo de {TendenciaAtendimentos.DiferencaMinimaReceita.ToString("C0", PtBr)} — pequeno demais para chamar de tendência."
            : $"Menos de {TendenciaAtendimentos.DiferencaMinimaSessoes} sessões — pequeno demais para chamar de tendência.",
        SituacaoTendencia.Estavel => $"Variou menos de {TendenciaAtendimentos.LimiarPercentual:0}% ou pouco em números absolutos.",
        SituacaoTendencia.Nova => "Não houve movimento no período anterior.",
        SituacaoTendencia.Sumiu => "Houve movimento no período anterior e nenhum neste.",
        _ => $"Variou {TendenciaAtendimentos.LimiarPercentual:0}% ou mais, e também em números absolutos."
    };

    public static string Dimensao(DimensaoTendencia d) => d switch
    {
        DimensaoTendencia.Modalidade => "Categoria",
        DimensaoTendencia.Especialidade => "Especialidade",
        DimensaoTendencia.Convenio => "Convênio",
        _ => "Profissional"
    };

    public static string Valor(decimal valor, MetricaTendencia m) => m == MetricaTendencia.Receita
        ? valor.ToString("C", PtBr)
        : valor.ToString("N0", PtBr);

    public static string Unidade(MetricaTendencia m) => m == MetricaTendencia.Receita ? "receita" : "sessões";

    /// <summary>"▲ 32%" do total, ou "—" sem base anterior.</summary>
    public static string Variacao(double? v) => v is { } x
        ? (x > 0 ? "▲ " : x < 0 ? "▼ " : "") + Math.Abs(x).ToString("0.#", PtBr) + "%"
        : "—";

    /// <summary>Só modalidade e especialidade viram meta — é o recorte que a meta aceita.</summary>
    public static bool AceitaMeta(DimensaoTendencia d, LinhaTendencia linha)
        => d is DimensaoTendencia.Modalidade or DimensaoTendencia.Especialidade && linha.Chave.Length > 0;

    public static IndicadorMeta IndicadorDaMetrica(MetricaTendencia m)
        => m == MetricaTendencia.Receita ? IndicadorMeta.Faturamento : IndicadorMeta.Sessoes;

    /// <summary>
    /// O mês da meta criada a partir de uma linha: o do fim do período, mas nunca um mês que
    /// já passou — meta para trás não muda conduta nenhuma, então cai no mês corrente.
    /// </summary>
    public static DateOnly MesSugeridoParaMeta(DateOnly fimDoPeriodo, DateOnly hoje)
    {
        var mesFim = new DateOnly(fimDoPeriodo.Year, fimDoPeriodo.Month, 1);
        var mesHoje = new DateOnly(hoje.Year, hoje.Month, 1);
        return mesFim < mesHoje ? mesHoje : mesFim;
    }

    /// <summary>A referência que vai ao lado do campo de alvo — o sistema não chuta o alvo.</summary>
    public static string Referencia(LinhaTendencia l, MetricaTendencia m)
        => $"No período: {Valor(l.Atual, m)} · no anterior: {Valor(l.Anterior, m)}.";

    public static readonly IReadOnlyList<string> CabecalhoCsv =
        ["Grupo", "Atual", "Anterior", "Variação", "Situação", "Participação"];

    /// <summary>As linhas do CSV, em pt-BR fixo (abre certo no Excel em português).</summary>
    public static IReadOnlyList<IReadOnlyList<string>> Csv(PainelTendencia p)
    {
        var m = p.Filtro.Metrica;
        var linhas = p.Linhas.Select(l => (IReadOnlyList<string>)[
            l.Rotulo, Valor(l.Atual, m), Valor(l.Anterior, m),
            l.Variacao is null ? "—" : Variacao(l.Variacao), Rotulo(l.Situacao),
            l.FracaoDoTotal.ToString("P0", PtBr)]).ToList();
        linhas.Add(["Total", Valor(p.TotalAtual, m), Valor(p.TotalAnterior, m), Variacao(p.VariacaoTotal), "", ""]);
        return linhas;
    }
}
