using Clinica.Domain.Entities;
namespace Clinica.Application.Servicos;

public sealed partial class ContasService
{
    public async Task<IReadOnlyList<LancamentoFinanceiro>> HistoricoObrigacaoAsync(int lancamentoId, CancellationToken ct = default)
    {
        var conta = await _repo.ObterLancamentoAsync(lancamentoId, ct)
            ?? throw new InvalidOperationException("Conta não encontrada.");
        return conta.GrupoObrigacao is { } grupo ? await _repo.LancamentosDaObrigacaoAsync(grupo, ct) : [conta];
    }

    // Chamado dentro da transação do pagamento. O lançamento recebido permanece como
    // prova do valor pago; o saldo vira outra previsão, vinculada à mesma obrigação.
    internal async Task PrepararBaixaParcialAsync(LancamentoFinanceiro conta, decimal valorPago,
        DateOnly? vencimentoSaldo, string? operador, CancellationToken ct)
    {
        if (conta.Status != StatusLancamento.Previsto || valorPago <= 0 || valorPago > conta.Valor || decimal.Round(valorPago, 2) != valorPago)
            throw new InvalidOperationException("O valor da baixa deve ser positivo, em reais e centavos, e não pode superar o saldo em aberto.");
        if (valorPago == conta.Valor) return;
        var vencimento = vencimentoSaldo ?? conta.DataVencimento
            ?? throw new InvalidOperationException("Informe o vencimento do saldo que continuará em aberto.");
        var anterior = conta.Valor;
        conta.GrupoObrigacao ??= Guid.NewGuid();
        conta.ValorOriginalObrigacao ??= anterior;
        var restante = CopiarSaldo(conta, anterior - valorPago, vencimento, operador);
        // Retenções já previstas são divididas em centavos. A taxa de cartão da baixa
        // é recalculada pelo recebimento sobre o montante efetivamente pago.
        if (conta.ValorImposto is { } imposto)
        {
            conta.ValorImposto = decimal.Round(imposto * valorPago / anterior, 2, MidpointRounding.AwayFromZero);
            restante.ValorImposto = imposto - conta.ValorImposto;
        }
        if (conta.ValorTaxa is { } taxa)
        {
            conta.ValorTaxa = decimal.Round(taxa * valorPago / anterior, 2, MidpointRounding.AwayFromZero);
            restante.ValorTaxa = taxa - conta.ValorTaxa;
        }
        conta.Valor = valorPago;
        await _repo.AdicionarLancamentoAsync(restante, ct);
        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria {
            Acao = "ContaBaixadaParcialmente", Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            PacienteId = conta.PacienteId,
            Detalhe = $"Conta #{conta.Id}: saldo anterior {anterior:C2}; baixa {valorPago:C2}; restante {restante.Valor:C2}, vencimento {vencimento:dd/MM/yyyy}."
        }, ct);
    }

    internal async Task ReabrirBaixaDesdobradaAsync(LancamentoFinanceiro pago, string? operador, CancellationToken ct)
    {
        if (pago.Status != StatusLancamento.Realizado || pago.GrupoObrigacao is null) return;
        var reaberta = CopiarSaldo(pago, pago.Valor, pago.DataVencimento ?? pago.Data, operador);
        await _repo.AdicionarLancamentoAsync(reaberta, ct);
        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria {
            Acao = "BaixaEstornadaNaObrigacao", Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            PacienteId = pago.PacienteId,
            Detalhe = $"Pagamento #{pago.Id} estornado: {pago.Valor:C2} voltou a ficar em aberto na obrigação {pago.GrupoObrigacao}."
        }, ct);
    }

    private static LancamentoFinanceiro CopiarSaldo(LancamentoFinanceiro origem, decimal valor, DateOnly vencimento, string? operador)
        => new() {
            GrupoObrigacao = origem.GrupoObrigacao, ValorOriginalObrigacao = origem.ValorOriginalObrigacao,
            OrigemDesdobramentoId = origem.Id, Data = origem.Data, DataVencimento = vencimento,
            Tipo = origem.Tipo, Descricao = origem.Descricao, Valor = valor, Status = StatusLancamento.Previsto,
            CategoriaFinanceiraId = origem.CategoriaFinanceiraId, PacienteId = origem.PacienteId,
            AtendimentoId = origem.AtendimentoId, CodigoFaturamentoId = origem.CodigoFaturamentoId,
            PacotePacienteId = origem.PacotePacienteId, Convenio = origem.Convenio, ConvenioCodigo = origem.ConvenioCodigo,
            Contraparte = origem.Contraparte, DocumentoReferencia = origem.DocumentoReferencia,
            NumeroParcelaConta = origem.NumeroParcelaConta, TotalParcelasConta = origem.TotalParcelasConta,
            // Chaves de geração identificam somente a obrigação original. O saldo a
            // referencia por OrigemDesdobramentoId, sem criar outra parcela contratual.
            FormaPagamento = origem.FormaPagamento, AliquotaImposto = origem.AliquotaImposto,
            ValorImposto = origem.ValorImposto, DetalheImposto = origem.DetalheImposto,
            Observacoes = origem.Observacoes, CriadoPor = operador
        };
}
