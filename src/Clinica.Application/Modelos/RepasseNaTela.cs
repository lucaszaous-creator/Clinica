using System.Globalization;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Modelos;

// ============================================================================================
// O QUE AS TELAS DO REPASSE AFIRMAM (nov/2026) — "Repasses" (fechar e pagar, no Financeiro) e
// "Repasse médico" (a tabela de regras, no Gerente e no Financeiro).
//
// Razor não decide: as frases, os totais do topo, a situação de cada linha e o que acende cada
// botão moram aqui, puros e com teste (RepasseNaTelaTests). O serviço GARANTE (recusa com
// frase); a tela só EXPLICA de antemão por que um botão está apagado.
//
// Dinheiro e data em pt-BR FIXO: a tela roda no Windows da clínica e na prévia em Linux, onde a
// cultura do processo é a invariante.
// ============================================================================================

/// <summary>Os cartões do topo da tela de Repasses, numa competência.</summary>
public sealed record TotaisDoRepasse(
    /// <summary>Soma dos itens PRONTOS (créditos menos estornos) de quem pode fechar.</summary>
    decimal AFechar,
    int ItensAFechar,
    int ProfissionaisAFechar,
    /// <summary>Guias faturadas que o convênio ainda não pagou (ou cuja receita está prevista). Contadas, nunca somadas.</summary>
    int Aguardando,
    int AguardandoComReceitaPrevista,
    /// <summary>Soma dos fechamentos VIVOS da competência (conferidos e pagos).</summary>
    decimal Fechado,
    int Fechamentos,
    decimal Pago,
    int Pagos,
    int SemRegra,
    int SemProfissional,
    int ApuradoNoModeloAntigo)
{
    public int Bloqueados => SemRegra + SemProfissional + ApuradoNoModeloAntigo;
}

/// <summary>Um bloco da lista expandida de um profissional: o convênio e o pagamento da operadora.</summary>
public sealed record GrupoDeItensRepasse(
    string Convenio,
    string? ReferenciaPagamento,
    IReadOnlyList<ItemRepasseElegivel> Itens,
    /// <summary>Só o que está PRONTO soma — o bloqueado aparece, mas não conta.</summary>
    decimal Total);

/// <summary>Uma linha da tabela de repasse ("Repasse médico"), já escrita.</summary>
public sealed record LinhaDaRegraRepasse(
    RegraRepasse Regra,
    string Profissional,
    string Convenio,
    string TipoGuia,
    string Modalidade,
    string Valor,
    string Vigencia,
    string Situacao,
    /// <summary>Tom do selo: sucesso (vale hoje), info (começa depois), neutro (encerrada/desativada).</summary>
    string Tom,
    bool ValendoHoje);

/// <summary>Convênios em que um profissional tem guia PAGA sem regra que a alcance.</summary>
public sealed record ConveniosSemRegra(
    int ProfissionalId,
    string Profissional,
    IReadOnlyList<ConvenioSemRegra> Convenios);

/// <summary>Um convênio (+ tipo de guia) sem regra, com quantas guias pagas esperam por ela.</summary>
public sealed record ConvenioSemRegra(string ConvenioCodigo, string Convenio, TipoCodigo? TipoGuia, int Guias);

public static class RepasseNaTela
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public const string TodosOsProfissionais = "Todos os profissionais";
    public const string TodosOsConvenios = "Todos os convênios";

    /// <summary>A frase de rodapé da tela: a regra inteira, numa frase só.</summary>
    public const string RegraDoRepasse =
        "Só entra no repasse o que o convênio PAGOU e foi conciliado (receita realizada ligada à guia); "
        + "cada guia entra uma única vez, no primeiro fechamento depois do pagamento — mesmo que o atendimento seja de outro mês.";

    /// <summary>O que o cartão "Aguardando o convênio" quer dizer — informado, nunca somado.</summary>
    public const string NotaDoAguardando =
        "Guias faturadas ainda sem pagamento conciliado. Informado, nunca somado: entra no repasse quando o convênio pagar.";

    /// <summary>Formas com que a clínica paga o profissional. Cartão e "convênio" não pagam médico.</summary>
    public static IReadOnlyList<FormaPagamento> FormasDePagamento { get; } =
        [FormaPagamento.Pix, FormaPagamento.Transferencia, FormaPagamento.Dinheiro, FormaPagamento.Boleto, FormaPagamento.Outro];

    // ==================== Totais e situação ====================

    public static TotaisDoRepasse Totais(
        IReadOnlyList<PreviaRepasseProfissional> previas, IReadOnlyList<RepasseFechamento> fechamentosDaCompetencia)
    {
        var podem = previas.Where(p => p.PodeFechar).ToList();
        var vivos = fechamentosDaCompetencia.Where(f => !f.Cancelado).ToList();
        var bloqueados = previas.SelectMany(p => p.Bloqueados).ToList();
        return new TotaisDoRepasse(
            podem.Sum(p => p.ValorAFechar),
            podem.Sum(p => p.QuantidadeAFechar),
            podem.Count,
            previas.Sum(p => p.Aguardando.Count),
            previas.Sum(p => p.Aguardando.Count(a => a.ReceitaPrevista)),
            vivos.Sum(f => f.Valor),
            vivos.Count,
            vivos.Where(f => f.Pago).Sum(f => f.Valor),
            vivos.Count(f => f.Pago),
            bloqueados.Count(i => i.Bloqueio == BloqueioItemRepasse.SemRegra),
            bloqueados.Count(i => i.Bloqueio == BloqueioItemRepasse.SemProfissional),
            bloqueados.Count(i => i.Bloqueio == BloqueioItemRepasse.ApuradoNoModeloAntigo));
    }

    /// <summary>A nota do cartão "A fechar".</summary>
    public static string NotaDoAFechar(TotaisDoRepasse t) => t.ProfissionaisAFechar == 0
        ? "Nada pronto para fechar nesta competência"
        : $"{Plural(t.ItensAFechar, "guia paga", "guias pagas")} de {Plural(t.ProfissionaisAFechar, "profissional", "profissionais")}, já com a regra aplicada";

    public static string NotaDoFechado(TotaisDoRepasse t) => t.Fechamentos == 0
        ? "Nenhum fechamento conferido nesta competência"
        : $"{Plural(t.Fechamentos, "fechamento conferido", "fechamentos conferidos")} (cancelados não contam)";

    public static string NotaDoPago(TotaisDoRepasse t) => t.Fechamentos == 0
        ? "O pagamento se registra no fechamento conferido"
        : t.Pagos == t.Fechamentos
            ? "Todos os fechamentos da competência pagos"
            : $"{Plural(t.Fechamentos - t.Pagos, "fechamento ainda a pagar", "fechamentos ainda a pagar")}";

    public static string NotaDoAguardandoComPrevista(TotaisDoRepasse t) => t.AguardandoComReceitaPrevista == 0
        ? NotaDoAguardando
        : $"{Plural(t.AguardandoComReceitaPrevista, "guia já tem", "guias já têm")} receita lançada como A RECEBER — "
          + "entra quando o recebimento for confirmado. Informado, nunca somado.";

    /// <summary>O aviso do topo quando há item pago que ainda não pode entrar. Null = nada bloqueado.</summary>
    public static string? AvisoDosBloqueados(TotaisDoRepasse t)
    {
        if (t.Bloqueados == 0) return null;
        var partes = new List<string>();
        if (t.SemRegra > 0) partes.Add($"{Plural(t.SemRegra, "sem regra de repasse", "sem regra de repasse")}");
        if (t.SemProfissional > 0) partes.Add($"{t.SemProfissional} sem profissional no horário");
        if (t.ApuradoNoModeloAntigo > 0) partes.Add($"{t.ApuradoNoModeloAntigo} já pago(s) na apuração antiga");
        return $"{Plural(t.Bloqueados, "recebimento não pode", "recebimentos não podem")} entrar ainda: {string.Join(", ", partes)}. "
               + "Eles aparecem marcados na lista de cada profissional e não somam no fechamento até serem resolvidos.";
    }

    /// <summary>A situação do profissional na competência (texto, tom do selo).</summary>
    public static (string Texto, string Tom) Situacao(PreviaRepasseProfissional p)
    {
        if (p.PodeFechar) return ("A fechar", "info");
        if (p.Prontos.Any()) return ("Saldo negativo", "perigo");
        if (p.Bloqueados.Any()) return ("Bloqueado", "aviso");
        if (p.JaFechados.Count > 0)
            return p.JaFechados.All(f => f.Pago) ? ("Pago", "sucesso") : ("Conferido", "sucesso");
        return p.Aguardando.Count > 0 ? ("Aguardando o convênio", "neutro") : ("Nada a fechar", "neutro");
    }

    /// <summary>A linha secundária do profissional na lista ("12 guias · 2 bloqueadas · 3 aguardando").</summary>
    public static string Detalhe(PreviaRepasseProfissional p)
    {
        var partes = new List<string> { Plural(p.QuantidadeAFechar, "guia a fechar", "guias a fechar") };
        var bloqueados = p.Bloqueados.Count();
        if (bloqueados > 0) partes.Add(Plural(bloqueados, "bloqueada", "bloqueadas"));
        var estornos = p.Prontos.Count(i => i.Natureza == NaturezaItemRepasse.Estorno);
        if (estornos > 0) partes.Add(Plural(estornos, "estorno", "estornos"));
        if (p.Aguardando.Count > 0) partes.Add($"{p.Aguardando.Count} aguardando o convênio");
        if (p.JaFechados.Count > 0) partes.Add(Plural(p.JaFechados.Count, "fechamento já feito", "fechamentos já feitos"));
        return string.Join(" · ", partes);
    }

    /// <summary>
    /// A dica do "Fechar e conferir": por que está apagado, ou o que ele vai fazer. A metade
    /// VISÍVEL da recusa do <c>FechamentoRepasseService.FecharAsync</c>.
    /// </summary>
    public static string DicaDeFechar(PreviaRepasseProfissional p, bool podeEditar)
    {
        if (!podeEditar) return "Seu acesso não permite fechar repasses.";
        if (!p.Prontos.Any())
            return p.Bloqueados.Any()
                ? "Os recebimentos deste profissional estão bloqueados — resolva o motivo de cada um para fechar."
                : "Nada pronto para fechar: nenhuma guia paga e conciliada desde o último fechamento.";
        if (!p.PodeFechar)
            return "Os estornos superam os créditos — o saldo fica para o próximo fechamento, quando houver guia paga.";
        var bloqueados = p.Bloqueados.Count();
        return $"Confere {Plural(p.QuantidadeAFechar, "guia", "guias")} ({Dinheiro(p.ValorAFechar)}) e cria a saída prevista no caixa."
               + (bloqueados > 0 ? $" {Plural(bloqueados, "item bloqueado fica", "itens bloqueados ficam")} para depois." : "");
    }

    /// <summary>A frase do formulário de fechamento — o que vai ser gravado, em voz alta.</summary>
    public static string ResumoDoFechamento(PreviaRepasseProfissional p, DateOnly competencia)
    {
        var estornos = p.Prontos.Where(i => i.Natureza == NaturezaItemRepasse.Estorno).ToList();
        var texto = $"{Plural(p.QuantidadeAFechar, "guia paga", "guias pagas")} de {p.Profissional}, competência {Competencia(competencia)}";
        if (estornos.Count > 0)
            texto += $", já descontados {Plural(estornos.Count, "estorno", "estornos")} ({Dinheiro(estornos.Sum(i => i.Valor))})";
        return texto + ".";
    }

    /// <summary>Por que um item recebido ainda não pode entrar. Vazio quando está pronto.</summary>
    public static string MotivoDoBloqueio(BloqueioItemRepasse b) => b switch
    {
        BloqueioItemRepasse.SemRegra => "Sem regra de repasse para este convênio e tipo de guia — cadastre a regra para ele entrar.",
        BloqueioItemRepasse.ApuradoNoModeloAntigo => "Período já pago na apuração antiga (por período) — não entra de novo.",
        BloqueioItemRepasse.SemProfissional => "Horário sem profissional — corrija o horário na agenda para ele entrar.",
        _ => string.Empty
    };

    /// <summary>O rótulo curto do bloqueio, para o selo da linha.</summary>
    public static string RotuloDoBloqueio(BloqueioItemRepasse b) => b switch
    {
        BloqueioItemRepasse.SemRegra => "Sem regra",
        BloqueioItemRepasse.ApuradoNoModeloAntigo => "Já apurado",
        BloqueioItemRepasse.SemProfissional => "Sem profissional",
        _ => "Pronto"
    };

    /// <summary>
    /// Os créditos (prontos e bloqueados) agrupados como no demonstrativo da clínica: por
    /// convênio e, dentro dele, por pagamento da operadora (GRD/conta/lote). Os estornos ficam
    /// FORA — linha própria, negativa (<see cref="Estornos"/>).
    /// </summary>
    public static IReadOnlyList<GrupoDeItensRepasse> Grupos(IEnumerable<ItemRepasseElegivel> itens)
        => itens.Where(i => i.Natureza == NaturezaItemRepasse.Credito)
            .GroupBy(i => (i.ConvenioNome, Referencia: string.IsNullOrWhiteSpace(i.ReferenciaPagamento) ? null : i.ReferenciaPagamento.Trim()))
            .OrderBy(g => g.Key.ConvenioNome, StringComparer.Create(PtBr, true))
            .ThenBy(g => g.Key.Referencia is null ? 1 : 0)
            .ThenBy(g => g.Key.Referencia, StringComparer.Ordinal)
            .Select(g => new GrupoDeItensRepasse(
                g.Key.ConvenioNome,
                g.Key.Referencia,
                g.OrderBy(i => i.DataRecebimento).ThenBy(i => i.DataAtendimento)
                    .ThenBy(i => i.Paciente, StringComparer.Create(PtBr, true)).ToList(),
                g.Where(i => i.Pronto).Sum(i => i.Valor)))
            .ToList();

    public static IReadOnlyList<ItemRepasseElegivel> Estornos(IEnumerable<ItemRepasseElegivel> itens)
        => itens.Where(i => i.Natureza == NaturezaItemRepasse.Estorno).OrderBy(i => i.DataRecebimento).ToList();

    /// <summary>"Pagamento GRD 4471" ou "Sem referência do pagamento".</summary>
    public static string RotuloDaReferencia(string? referencia)
        => string.IsNullOrWhiteSpace(referencia) ? "Sem referência do pagamento" : $"Pagamento {referencia.Trim()}";

    // ==================== Fechamentos ====================

    /// <summary>"Conferido", "Pago em 05/10/2026" ou "Cancelado em 12/10/2026".</summary>
    public static string SituacaoDoFechamento(RepasseFechamento f)
    {
        if (f.CanceladoEm is { } cancelado) return $"Cancelado em {cancelado.ToString("dd/MM/yyyy", PtBr)}";
        if (f.Pago)
            return f.Lancamento?.DataPagamento is { } dia ? $"Pago em {Data(dia)}" : "Pago";
        return "Conferido";
    }

    public static string TomDoFechamento(RepasseFechamento f) => f.Cancelado ? "neutro" : f.Pago ? "sucesso" : "info";

    /// <summary>Registrar pagamento: só no conferido vivo, ainda não pago, com a saída prevista de pé.</summary>
    public static bool PodeRegistrarPagamento(RepasseFechamento f)
        => !f.Cancelado && !f.Pago && f.Lancamento?.Status != StatusLancamento.Cancelado;

    /// <summary>Cancelar: só o conferido vivo e NÃO pago (o serviço recusa o pago de novo).</summary>
    public static bool PodeCancelar(RepasseFechamento f) => !f.Cancelado && !f.Pago;

    public static string? DicaDePagamento(RepasseFechamento f, bool podeEditar)
    {
        if (!podeEditar) return "Seu acesso não permite registrar pagamento de repasse.";
        if (f.Cancelado) return "Fechamento cancelado — não há o que pagar.";
        if (f.Pago) return "Este repasse já foi pago.";
        if (f.Lancamento?.Status == StatusLancamento.Cancelado) return "A saída deste fechamento foi cancelada no Caixa.";
        return "Registra que o profissional foi pago: a saída prevista no caixa passa a realizada.";
    }

    public static string? DicaDeCancelar(RepasseFechamento f, bool podeEditar)
    {
        if (!podeEditar) return "Seu acesso não permite cancelar fechamento de repasse.";
        if (f.Cancelado) return "Já cancelado.";
        if (f.Pago) return "Fechamento pago não se cancela: estorne o pagamento no Caixa antes.";
        return "Cancela com motivo: o fechamento fica registrado, as guias voltam a ficar prontas e a saída prevista é cancelada junto.";
    }

    public static string PerguntaDeCancelar(RepasseFechamento f)
        => $"Por que o fechamento de {f.ProfissionalNome} ({Competencia(f.Competencia)}, {Dinheiro(f.Valor)}) está sendo cancelado? "
           + "O fechamento continua registrado com o motivo, as guias voltam a ficar prontas para um novo fechamento e a saída prevista no caixa é cancelada junto.";

    /// <summary>"09/2026" — a competência como a clínica a escreve no demonstrativo.</summary>
    public static string Competencia(DateOnly competencia) => competencia.ToString("MM/yyyy", PtBr);

    // ==================== Tabela de repasse (regras) ====================

    public static LinhaDaRegraRepasse LinhaDe(RegraRepasse r, DateOnly hoje)
    {
        var (situacao, tom) = SituacaoDaRegra(r, hoje);
        return new LinhaDaRegraRepasse(
            r,
            r.Profissional?.Rotulo ?? $"Profissional {r.ProfissionalId}",
            string.IsNullOrWhiteSpace(r.ConvenioCodigo) ? TodosOsConvenios : CatalogoConvenios.NomeOuCodigo(r.ConvenioCodigo),
            r.TipoGuia is { } t ? RotulosEnum.De(t) : "Todos os tipos",
            string.IsNullOrWhiteSpace(r.ModalidadeCodigo) ? "Todas" : CatalogoModalidades.Nome(r.ModalidadeCodigo),
            ValorDaRegra(r),
            LeituraDosRepasses.Vigencia(r.VigenteDe, r.VigenteAte),
            situacao, tom,
            r.VigenteEm(hoje));
    }

    /// <summary>"R$ 65,00 por guia" ou "40% do recebido" — como se lê na conversa com o médico.</summary>
    public static string ValorDaRegra(RegraRepasse r) => r.Base == BaseRepasse.PercentualDaReceita
        ? string.Format(PtBr, "{0:0.##}% do recebido", r.Percentual ?? 0m)
        : string.Format(PtBr, "{0:C} por guia", r.ValorPorAtendimento ?? 0m);

    public static (string Texto, string Tom) SituacaoDaRegra(RegraRepasse r, DateOnly hoje)
    {
        if (!r.Ativa) return ("Desativada", "neutro");
        // As datas já estão na coluna Vigência: o selo diz só o estado (cabe em 1024).
        if (r.VigenteAte is { } ate && ate < hoje) return ("Encerrada", "neutro");
        if (r.VigenteDe is { } de && de > hoje) return ("Ainda não vale", "info");
        return ("Vale hoje", "sucesso");
    }

    /// <summary>Ordem de leitura: médico, convênio, tipo; o que vale hoje antes do histórico.</summary>
    public static IReadOnlyList<LinhaDaRegraRepasse> Ordenar(IEnumerable<LinhaDaRegraRepasse> linhas)
    {
        var cmp = StringComparer.Create(PtBr, true);
        return linhas.OrderBy(l => l.Profissional, cmp)
            .ThenBy(l => l.Convenio == TodosOsConvenios ? 1 : 0).ThenBy(l => l.Convenio, cmp)
            .ThenBy(l => l.TipoGuia, cmp)
            .ThenBy(l => l.ValendoHoje ? 0 : 1)
            .ThenByDescending(l => l.Regra.VigenteDe ?? DateOnly.MinValue)
            .ToList();
    }

    public static IReadOnlyList<string> OpcoesDeProfissional(IEnumerable<LinhaDaRegraRepasse> linhas)
        => [TodosOsProfissionais, .. linhas.Select(l => l.Profissional).Distinct().OrderBy(n => n, StringComparer.Create(PtBr, true))];

    public static IReadOnlyList<string> OpcoesDeConvenio(IEnumerable<LinhaDaRegraRepasse> linhas)
        => [TodosOsConvenios, .. linhas.Select(l => l.Convenio).Where(c => c != TodosOsConvenios).Distinct()
            .OrderBy(n => n, StringComparer.Create(PtBr, true))];

    /// <summary>
    /// O filtro da tabela. O convênio escolhido traz também as regras de "todos os convênios"
    /// daquele médico — são elas que valem para aquele convênio quando não há uma específica.
    /// </summary>
    public static IReadOnlyList<LinhaDaRegraRepasse> Filtrar(
        IEnumerable<LinhaDaRegraRepasse> linhas, string profissional, string convenio, bool soVigentes)
        => linhas.Where(l => profissional == TodosOsProfissionais || l.Profissional == profissional)
            .Where(l => convenio == TodosOsConvenios || l.Convenio == convenio || l.Convenio == TodosOsConvenios)
            .Where(l => !soVigentes || l.ValendoHoje)
            .ToList();

    /// <summary>
    /// Por profissional, os convênios (e tipos de guia) em que ele tem guia PAGA e conciliada
    /// esperando por uma regra — lido da prévia do repasse, a mesma conta da tela de fechamento.
    /// </summary>
    public static IReadOnlyList<ConveniosSemRegra> SemRegra(IEnumerable<PreviaRepasseProfissional> previas)
        => previas
            .Select(p => new ConveniosSemRegra(p.ProfissionalId, p.Profissional,
                p.Itens.Where(i => i.Bloqueio == BloqueioItemRepasse.SemRegra && i.Natureza == NaturezaItemRepasse.Credito)
                    .GroupBy(i => (i.ConvenioCodigo, i.ConvenioNome, i.TipoGuia))
                    .Select(g => new ConvenioSemRegra(g.Key.ConvenioCodigo, g.Key.ConvenioNome, g.Key.TipoGuia, g.Count()))
                    .OrderBy(c => c.Convenio, StringComparer.Create(PtBr, true))
                    .ToList()))
            .Where(p => p.Convenios.Count > 0)
            .OrderBy(p => p.Profissional, StringComparer.Create(PtBr, true))
            .ToList();

    /// <summary>"3 valendo hoje · 5 no total · 2 no filtro".</summary>
    public static string ResumoDaTabela(IReadOnlyCollection<LinhaDaRegraRepasse> todas, int noFiltro, bool filtroAtivo)
        => $"{todas.Count(l => l.ValendoHoje).ToString(PtBr)} valendo hoje · {Plural(todas.Count, "regra", "regras")} no total"
           + (filtroAtivo ? $" · {noFiltro.ToString(PtBr)} no filtro" : "");

    public static string PerguntaDeDesativar(LinhaDaRegraRepasse l)
        => $"Desativar a regra de {l.Profissional} ({l.Convenio} · {l.TipoGuia} · {l.Valor})? Ela deixa de pagar as guias que ainda não "
           + "foram fechadas; o que já foi fechado não muda. Dá para reativar depois.";

    /// <summary>"Amil · Consulta (3 guias)".</summary>
    public static string RotuloSemRegra(ConvenioSemRegra c)
        => $"{c.Convenio}{(c.TipoGuia is { } t ? $" · {RotulosEnum.De(t)}" : "")} ({Plural(c.Guias, "guia", "guias")})";

    /// <summary>A pergunta do reajuste, que deixa claro o que ele NÃO muda.</summary>
    public static string ExplicacaoDoReajuste(RegraRepasse r, DateOnly aPartirDe)
        => $"A regra atual ({ValorDaRegra(r)}) passa a valer até {Data(aPartirDe.AddDays(-1))} e a nova começa em {Data(aPartirDe)}. "
           + "O que já foi fechado não muda: cada item fechado guarda a regra que valia quando entrou.";

    // ==================== Conciliação: a referência do pagamento ====================

    /// <summary>
    /// A referência do pagamento (GRD / conta / lote) como vai ser gravada: aparada, e nula
    /// quando vazia. Limite de 60 caracteres — é um número de documento, não uma observação.
    /// </summary>
    public static string? ReferenciaParaGravar(string? digitada)
    {
        if (string.IsNullOrWhiteSpace(digitada)) return null;
        var limpa = string.Join(' ', digitada.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return limpa.Length <= 60 ? limpa : limpa[..60];
    }

    // ==================== apoio ====================

    private static string Dinheiro(decimal v) => v.ToString("C", PtBr);
    private static string Data(DateOnly d) => d.ToString("dd/MM/yyyy", PtBr);
    private static string Plural(int n, string um, string varios) => $"{n.ToString(PtBr)} {(n == 1 ? um : varios)}";
}
