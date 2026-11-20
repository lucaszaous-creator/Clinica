using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// Repasse por GUIA RECEBIDA (nov/2026). Ver <see cref="RepasseFechamento"/> para o porquê.
///
/// ⚠️ CONTRATO FIXADO — a tela é escrita contra estas assinaturas. Mudar uma delas é
/// combinar com quem escreve a tela.
/// </summary>
public sealed class FechamentoRepasseService
{
    private readonly IClinicaRepositorio _repo;

    public FechamentoRepasseService(IClinicaRepositorio repo) => _repo = repo;

    /// <summary>
    /// Prévia (não grava nada): por profissional, os recebimentos ainda não fechados com
    /// data de recebimento até o fim da <paramref name="competencia"/> (meses anteriores
    /// INCLUSIVE — o que ficou para trás entra no próximo fechamento), os estornos pendentes,
    /// as guias aguardando o convênio e os fechamentos já feitos nesta competência.
    /// Profissional sem nada a fechar e sem fechamento na competência não aparece.
    /// </summary>
    public Task<IReadOnlyList<PreviaRepasseProfissional>> PreviaAsync(
        DateOnly competencia, CancellationToken ct = default)
        => throw new NotImplementedException();

    /// <summary>
    /// Fecha (confere) o repasse: grava o fechamento com os itens PRONTOS da prévia, cria a
    /// saída PREVISTA no caixa e a auditoria — tudo no mesmo SaveChanges. Recusa (com
    /// <see cref="InvalidOperationException"/> e frase para a tela) quando não há item pronto,
    /// quando o total é zero ou negativo, ou quando outro posto fechou os mesmos itens antes.
    /// </summary>
    public Task<RepasseFechamento> FecharAsync(
        int profissionalId, DateOnly competencia, string operador,
        string? observacoes = null, int? categoriaId = null, CancellationToken ct = default)
        => throw new NotImplementedException();

    /// <summary>Fechamentos (com itens e lançamento), do mais novo ao mais antigo.</summary>
    public Task<IReadOnlyList<RepasseFechamento>> FechamentosAsync(
        DateOnly? competencia = null, int? profissionalId = null, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<RepasseFechamento?> ObterAsync(int fechamentoId, CancellationToken ct = default)
        => throw new NotImplementedException();

    /// <summary>Realiza a saída do caixa do fechamento (o médico foi pago).</summary>
    public Task RegistrarPagamentoAsync(
        int fechamentoId, DateOnly dataPagamento, FormaPagamento forma, string operador,
        CancellationToken ct = default)
        => throw new NotImplementedException();

    /// <summary>
    /// Cancela com motivo obrigatório: o fechamento fica (marcado), os itens voltam a ser
    /// elegíveis e a saída prevista é cancelada junto. Fechamento PAGO é recusado.
    /// </summary>
    public Task CancelarAsync(
        int fechamentoId, string motivo, string operador, CancellationToken ct = default)
        => throw new NotImplementedException();

    /// <summary>Monta o demonstrativo de um fechamento gravado.</summary>
    public Task<DemonstrativoRepasse> DemonstrativoAsync(int fechamentoId, CancellationToken ct = default)
        => throw new NotImplementedException();
}
