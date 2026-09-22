using Clinica.Domain.Entities;

namespace Clinica.Application.Modelos;

/// <summary>Uma movimentação no banco: pagamento integral ou crédito de uma parcela da adquirente.</summary>
public sealed record ItemConciliacao(LancamentoFinanceiro Lancamento, ParcelaRecebivelCartao? Parcela = null)
{
    public int Id => Lancamento.Id;
    public int? ParcelaId => Parcela?.Id;
    public string Chave => Parcela is null ? $"venda:{Id}" : $"parcela:{Parcela.Id}";
    public string Descricao => Parcela is null ? Lancamento.Descricao : $"{Lancamento.Descricao} · parcela {Parcela.Numero}/{Lancamento.Parcelas}";
    public decimal Valor => Parcela?.Bruto ?? Lancamento.Valor;
    public decimal? ValorTaxa => Parcela?.Taxa ?? Lancamento.ValorTaxa;
    public TipoLancamento Tipo => Lancamento.Tipo;
    public StatusLancamento Status => Lancamento.Status;
    public string? Adquirente => Lancamento.Adquirente;
    public ModalidadeCartao? ModalidadeCartao => Lancamento.ModalidadeCartao;
    public bool Conciliado => Parcela is null ? Lancamento.Conciliado : Parcela.ConciliadoEm is not null;
    public string? IdBancario => Parcela is null ? Lancamento.IdBancario : Parcela.IdBancario;
    public string? ContaBancariaConciliacao => Parcela is null ? Lancamento.ContaBancariaConciliacao : Parcela.ContaBancaria;
    public DateOnly Data => Parcela is null
        ? Lancamento.RecebimentoConfirmadoEm ?? Lancamento.PrevisaoRecebimento ?? Lancamento.DataPagamento ?? Lancamento.Data
        : Parcela.RecebidoEm ?? Parcela.Previsao;
    public decimal ValorBancario => Parcela?.Liquido ?? (Tipo != TipoLancamento.Entrada ? Valor
        : Lancamento.FormaPagamento == FormaPagamento.Convenio || Lancamento.CodigoFaturamentoId is not null
            ? Lancamento.ValorLiquido : Valor - (ValorTaxa ?? 0));
}
