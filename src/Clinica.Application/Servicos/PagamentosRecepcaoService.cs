using Clinica.Application.Abstracoes;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>Recebimento de cobranças do paciente no balcão, sobre o mesmo caixa do Financeiro.</summary>
public sealed class PagamentosRecepcaoService(IClinicaRepositorio repo, TaxaService taxas)
{
    public Task<IReadOnlyList<LancamentoFinanceiro>> DoPacienteAsync(int pacienteId, CancellationToken ct = default)
        => repo.CobrancasDoPacienteAsync(pacienteId, ct);

    public Task<LancamentoFinanceiro> ReceberAsync(int pacienteId, int lancamentoId, decimal valorConferido,
        DateOnly data, FormaPagamento forma, string? operador = null,
        string? adquirente = null, string? bandeira = null, int parcelas = 1, CancellationToken ct = default)
        => repo.ExecutarGestaoAtomicaAsync(async () =>
        {
            if (!Enum.IsDefined(forma) || forma == FormaPagamento.Convenio)
                throw new InvalidOperationException("Escolha a forma usada pelo paciente. Recebimento de operadora pertence ao Financeiro.");
            if (parcelas < 1 || parcelas > 36 || (forma != FormaPagamento.CartaoCredito && parcelas != 1))
                throw new InvalidOperationException("Informe de 1 a 36 parcelas; parcelamento só se aplica ao cartão de crédito.");
            if (data > DateOnly.FromDateTime(DateTime.Today))
                throw new InvalidOperationException("Um pagamento futuro deve permanecer a receber.");
            var l = await repo.ObterLancamentoAsync(lancamentoId, ct)
                ?? throw new InvalidOperationException("Cobrança não encontrada.");
            if (l.PacienteId != pacienteId || l.Tipo != TipoLancamento.Entrada || l.CodigoFaturamentoId is not null
                || l.FormaPagamento == FormaPagamento.Convenio)
                throw new InvalidOperationException("Esta cobrança não é um pagamento do paciente selecionado.");
            if (l.Status != StatusLancamento.Previsto)
                throw new InvalidOperationException("Esta cobrança já foi recebida ou cancelada. Atualize a lista antes de continuar.");
            if (valorConferido != l.Valor)
                throw new InvalidOperationException("O valor foi alterado. Confira o total atualizado da cobrança antes de receber.");
            var d = await taxas.CalcularPagamentoPacienteAsync(l.Valor, data, forma, adquirente, bandeira, parcelas, ct);
            if (d.Total > l.Valor)
                throw new InvalidOperationException("As deduções superam o valor do recebimento.");
            await new FechamentoCaixaService(repo).ExigirDiaAbertoAsync(data, forma, ct);
            l.Status = StatusLancamento.Realizado;
            l.DataPagamento = data;
            l.FormaPagamento = forma;
            l.Adquirente = TaxaService.ModalidadeDe(forma) is null ? null : adquirente?.Trim();
            l.Bandeira = TaxaService.ModalidadeDe(forma) is null ? null : bandeira?.Trim();
            l.ModalidadeCartao = TaxaService.ModalidadeDe(forma, parcelas);
            l.Parcelas = l.ModalidadeCartao is null ? null : parcelas;
            l.TaxaPercentual = d.TaxaPercentual;
            l.ValorTaxa = d.ValorTaxa;
            l.AliquotaImposto = d.AliquotaImposto;
            l.ValorImposto = d.ValorImposto;
            l.DetalheImposto = d.DetalheImposto;
            l.PrevisaoRecebimento = d.PrevisaoRecebimento;
            await repo.RegistrarAuditoriaAsync(new EventoAuditoria
            {
                Acao = "PagamentoRecebidoNaRecepcao", PacienteId = pacienteId,
                Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
                Detalhe = $"Cobrança {l.Id}: {l.Valor:C2} em {forma}, {data:dd/MM/yyyy}; líquido {l.ValorLiquido:C2}."
            }, ct);
            await repo.SalvarAsync(ct);
            await new CalendarioCartaoService(repo).CriarAsync(l, d.LiquidacaoMensal, ct);
            return l;
        }, ct);
}
