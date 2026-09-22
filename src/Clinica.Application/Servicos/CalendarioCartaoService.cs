using Clinica.Application.Abstracoes;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

public sealed class CalendarioCartaoService(IClinicaRepositorio repo)
{
    /// <summary>Chamado dentro da transação do pagamento. Preserva centavos no último crédito.</summary>
    public async Task CriarAsync(LancamentoFinanceiro l, bool mensal, CancellationToken ct = default)
    {
        if (!mensal || l.Status != StatusLancamento.Realizado || l.Tipo != TipoLancamento.Entrada) return;
        if (l.PrevisaoRecebimento is not { } inicio || l.Parcelas is not { } parcelas || parcelas < 2)
            throw new InvalidOperationException("O contrato mensal exige prazo do primeiro depósito e número de parcelas.");
        if ((await repo.ParcelasCartaoDoLancamentoAsync(l.Id, ct)).Count > 0)
            throw new InvalidOperationException("A agenda de recebíveis deste pagamento já existe.");
        var brutoBase = decimal.Floor(l.Valor * 100 / parcelas) / 100;
        var taxaTotal = l.ValorTaxa ?? 0;
        var taxaBase = decimal.Floor(taxaTotal * 100 / parcelas) / 100;
        var creditos = Enumerable.Range(1, parcelas).Select(numero => new ParcelaRecebivelCartao
        {
            LancamentoFinanceiroId = l.Id, Numero = numero, Previsao = inicio.AddMonths(numero - 1),
            Bruto = numero == parcelas ? l.Valor - brutoBase * (parcelas - 1) : brutoBase, Taxa = taxaBase
        }).ToList();
        // Centavos residuais vão ao fim, respeitando o bruto de cada crédito.
        var restante = taxaTotal - taxaBase * parcelas;
        foreach (var p in creditos.AsEnumerable().Reverse())
        {
            var acrescimo = Math.Min(restante, p.Bruto - p.Taxa);
            p.Taxa += acrescimo;
            restante -= acrescimo;
        }
        if (restante != 0 || creditos.Any(p => p.Liquido < 0))
            throw new InvalidOperationException("As taxas superam o valor dos créditos do contrato.");
        foreach (var p in creditos) await repo.AdicionarParcelaCartaoAsync(p, ct);
        await repo.SalvarAsync(ct);
    }

    public async Task AtualizarConfirmacaoDaVendaAsync(int lancamentoId, CancellationToken ct = default)
    {
        var parcelas = await repo.ParcelasCartaoDoLancamentoAsync(lancamentoId, ct);
        if (parcelas.Count == 0) return;
        var l = await repo.ObterLancamentoAsync(lancamentoId, ct)
            ?? throw new InvalidOperationException("Venda do cartão não encontrada.");
        l.RecebimentoConfirmadoEm = parcelas.All(p => p.RecebidoEm is not null)
            ? parcelas.Max(p => p.RecebidoEm) : null;
    }
}
