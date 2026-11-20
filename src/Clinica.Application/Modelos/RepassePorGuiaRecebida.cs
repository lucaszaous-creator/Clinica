using Clinica.Domain.Entities;
using Clinica.Domain;

namespace Clinica.Application.Modelos;

// ============================================================================================
// REPASSE POR GUIA RECEBIDA (nov/2026) — o contrato entre o serviço e a tela.
//
// A regra da clínica: o repasse do médico só conta DEPOIS de a guia ser conciliada, isto é,
// depois de o financeiro ou o gerente afirmar na Conciliação que o convênio PAGOU (receita
// de entrada REALIZADA, viva, ligada à guia). Guia faturada e não paga aparece como
// "aguardando o convênio" — informada, nunca somada.
// ============================================================================================

/// <summary>Por que um item recebido ainda não pode entrar no fechamento.</summary>
public enum BloqueioItemRepasse
{
    /// <summary>Pronto para fechar.</summary>
    Nenhum,

    /// <summary>Não há regra de repasse vigente que alcance este convênio/tipo/modalidade.</summary>
    SemRegra,

    /// <summary>
    /// Sessão particular dentro de um período já apurado no modelo antigo
    /// (<see cref="RepasseApurado"/>): pagá-la de novo seria pagar duas vezes.
    /// </summary>
    ApuradoNoModeloAntigo,

    /// <summary>Não se descobriu quem atendeu (horário sem profissional).</summary>
    SemProfissional
}

/// <summary>
/// Um recebimento que dá (ou tira) direito a repasse, ainda NÃO fechado. Mesma forma do
/// <see cref="ItemRepasse"/> gravado, para a prévia e o demonstrativo falarem a mesma língua.
/// </summary>
public sealed record ItemRepasseElegivel(
    string Chave,
    OrigemItemRepasse Origem,
    NaturezaItemRepasse Natureza,
    int ProfissionalId,
    int AtendimentoId,
    int? CodigoFaturamentoId,
    int? LancamentoReceitaId,
    int? ItemEstornadoId,
    int PacienteId,
    string Paciente,
    DateOnly DataAtendimento,
    string ConvenioCodigo,
    string ConvenioNome,
    TipoCodigo? TipoGuia,
    string Descricao,
    string? NumeroGuia,
    string? ReferenciaPagamento,
    DateOnly DataRecebimento,
    decimal ValorRecebido,
    int? RegraRepasseId,
    string RegraDescricao,
    decimal Valor,
    BloqueioItemRepasse Bloqueio)
{
    public bool Pronto => Bloqueio == BloqueioItemRepasse.Nenhum;
}

/// <summary>Guia do profissional já faturada (baixada) cujo pagamento ainda não foi conciliado.</summary>
public sealed record GuiaAguardandoConvenio(
    int CodigoFaturamentoId,
    int AtendimentoId,
    string Paciente,
    DateOnly DataAtendimento,
    DateOnly? DataBaixa,
    string ConvenioNome,
    string Descricao,
    string? NumeroGuia,
    /// <summary>Receita lançada mas PREVISTA (a receber) — conciliada sem confirmar o pagamento.</summary>
    bool ReceitaPrevista,
    bool Glosada);

/// <summary>O que um profissional tem a fechar numa competência.</summary>
public sealed record PreviaRepasseProfissional(
    int ProfissionalId,
    string Profissional,
    string? Registro,
    IReadOnlyList<ItemRepasseElegivel> Itens,
    IReadOnlyList<GuiaAguardandoConvenio> Aguardando,
    /// <summary>Fechamentos vivos deste profissional nesta competência (já fechados).</summary>
    IReadOnlyList<RepasseFechamento> JaFechados)
{
    public IEnumerable<ItemRepasseElegivel> Prontos => Itens.Where(i => i.Pronto);
    public IEnumerable<ItemRepasseElegivel> Bloqueados => Itens.Where(i => !i.Pronto);
    public decimal ValorAFechar => Prontos.Sum(i => i.Valor);
    public int QuantidadeAFechar => Prontos.Count(i => i.Natureza == NaturezaItemRepasse.Credito);
    public bool PodeFechar => Prontos.Any() && ValorAFechar > 0;
}

/// <summary>Uma linha do resumo do demonstrativo: o convênio, quantos, quanto por um, total.</summary>
public sealed record ResumoConvenioNoDemonstrativo(
    string Convenio,
    int Quantidade,
    /// <summary>Valor unitário quando todos os itens do convênio têm o mesmo; senão nulo ("variado").</summary>
    decimal? ValorUnitario,
    decimal Total);

/// <summary>Um bloco do anexo do demonstrativo: convênio + referência do pagamento (GRD/conta/lote).</summary>
public sealed record BlocoDoDemonstrativo(
    string Convenio,
    string? ReferenciaPagamento,
    IReadOnlyList<ItemRepasse> Itens,
    decimal Total);

/// <summary>
/// O demonstrativo de repasse médico, montado de um fechamento GRAVADO (nunca da prévia: o
/// papel que vai ao médico precisa ser o que foi conferido). Forma copiada do modelo que a
/// clínica já usava (Demonstrativo_Repasse_*_09-2026.pdf).
/// </summary>
public sealed record DemonstrativoRepasse(
    int FechamentoId,
    string Clinica,
    string Profissional,
    string? Registro,
    DateOnly Competencia,
    DateOnly RecebidosAte,
    DateTime ConferidoEm,
    string ConferidoPor,
    string Situacao,
    IReadOnlyList<ResumoConvenioNoDemonstrativo> Resumo,
    /// <summary>"Amil: R$ 65,00 por consulta" — a tabela de repasse como foi aplicada.</summary>
    IReadOnlyList<string> RegrasAplicadas,
    IReadOnlyList<BlocoDoDemonstrativo> Blocos,
    IReadOnlyList<ItemRepasse> Estornos,
    int Quantidade,
    decimal Total,
    string? Observacoes);
