using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

public sealed record MaterialConsumidoProcedimento(int ItemId, decimal Quantidade, string? Lote = null);
public sealed record PedidoConsumoProcedimento(IReadOnlyList<MaterialConsumidoProcedimento> Materiais, bool SemConsumo = false);

public sealed partial class EstoqueService
{
    public Task<ConferenciaConsumoProcedimento?> ConferenciaDoProcedimentoAsync(int atendimentoId, CancellationToken ct = default)
        => _repo.ConferenciaConsumoAsync(atendimentoId, ct);

    /// <summary>Ou confirma todas as baixas, ou nenhuma. Reenvio não repete consumo.</summary>
    public Task<ConferenciaConsumoProcedimento> ConfirmarConsumoProcedimentoAsync(
        int atendimentoId, PedidoConsumoProcedimento pedido, string operador, CancellationToken ct = default)
        => _repo.ExecutarGestaoAtomicaAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(operador) || operador.Trim().Length > 80)
            throw new InvalidOperationException("Identifique o responsável pela conferência dos materiais.");
        var materiais = pedido.Materiais ?? throw new InvalidOperationException("Informe os materiais usados.");
        if (materiais.Count == 0 && !pedido.SemConsumo || materiais.Count > 0 && pedido.SemConsumo)
            throw new InvalidOperationException("Informe os materiais usados ou declare explicitamente que não houve consumo.");
        if (materiais.Any(m => m.ItemId <= 0 || m.Quantidade <= 0 || decimal.Round(m.Quantidade, 3) != m.Quantidade))
            throw new InvalidOperationException("Informe quantidades positivas com até três casas decimais.");
        var agrupados = materiais.GroupBy(m => (m.ItemId, Lote: Limpar(m.Lote)))
            .Select(g => new MaterialConsumidoProcedimento(g.Key.ItemId, g.Sum(m => m.Quantidade), g.Key.Lote))
            .OrderBy(m => m.ItemId).ThenBy(m => m.Lote, StringComparer.Ordinal).ToArray();
        static string Resumo(IEnumerable<MaterialConsumidoProcedimento> itens)
            => JsonSerializer.Serialize(itens.Select(m => new { m.ItemId, Quantidade = m.Quantidade.ToString("G29", CultureInfo.InvariantCulture), m.Lote }));
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Resumo(agrupados))));
        var anterior = await _repo.ConferenciaConsumoAsync(atendimentoId, ct);
        if (anterior is not null)
        {
            if (anterior.Pedido != hash || anterior.SemConsumo != pedido.SemConsumo)
                throw new InvalidOperationException("Esta sessão já tem materiais conferidos com outros dados. Consulte o histórico de consumo.");
            return anterior;
        }
        var atendimento = await _repo.ObterAtendimentoAsync(atendimentoId, ct)
            ?? throw new InvalidOperationException("Atendimento não encontrado.");
        var movimentos = (await _repo.MovimentosDoAtendimentoAsync(atendimentoId, ct))
            .Where(m => m.Tipo == TipoMovimentoEstoque.Saida).ToList();
        if (movimentos.Count > 0)
        {
            var anteriores = movimentos.GroupBy(m => (m.ItemEstoqueId, Lote: Limpar(m.Lote)))
                .Select(g => new MaterialConsumidoProcedimento(g.Key.ItemEstoqueId, g.Sum(m => m.Quantidade), g.Key.Lote))
                .OrderBy(m => m.ItemId).ThenBy(m => m.Lote, StringComparer.Ordinal);
            if (Resumo(anteriores) != Resumo(agrupados))
                throw new InvalidOperationException("Já há baixas nesta sessão. Confira os materiais registrados antes de confirmar; eles não serão descontados novamente.");
        }
        else
        {
            foreach (var material in agrupados)
            {
                var item = await _repo.ObterItemEstoqueAsync(material.ItemId, ct)
                    ?? throw new InvalidOperationException("Material não encontrado.");
                if ((item.ExigirLote || item.Grupo == GrupoEstoque.Medicamento) && material.Lote is null)
                    throw new InvalidOperationException($"Informe o lote utilizado de {item.Nome}.");
                await MovimentarAsync(new MovimentoEstoque {
                    ItemEstoqueId = material.ItemId, Tipo = TipoMovimentoEstoque.Saida,
                    Quantidade = material.Quantidade, Lote = material.Lote,
                    AtendimentoId = atendimento.Id, PacienteId = atendimento.PacienteId,
                    Data = atendimento.Data, Observacao = "Materiais conferidos no procedimento",
                    DestinoConsumo = DestinoConsumoEstoque.Procedimento
                }, operador, ct);
            }
        }
        var conferencia = new ConferenciaConsumoProcedimento {
            AtendimentoId = atendimentoId, SemConsumo = pedido.SemConsumo,
            Pedido = hash, ConferidoEm = DateTime.Now, ConferidoPor = operador.Trim()
        };
        await _repo.AdicionarConferenciaConsumoAsync(conferencia, ct);
        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria {
            Acao = "MateriaisProcedimentoConferidos", Operador = operador.Trim(), PacienteId = atendimento.PacienteId,
            Detalhe = pedido.SemConsumo ? $"Atendimento #{atendimentoId}: sem consumo de materiais declarado."
                : $"Atendimento #{atendimentoId}: {agrupados.Length} item(ns)/lote(s) conferido(s)."
        }, ct);
        await _repo.SalvarAsync(ct);
        return conferencia;
    }, ct);
}
