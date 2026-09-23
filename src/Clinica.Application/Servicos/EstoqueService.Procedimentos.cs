using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Clinica.Domain.Entities;
using Clinica.Application.Modelos;

namespace Clinica.Application.Servicos;

public sealed record MaterialConsumidoProcedimento(int ItemId, decimal Quantidade, string? Lote = null);
public sealed record PedidoConsumoProcedimento(IReadOnlyList<MaterialConsumidoProcedimento> Materiais, bool SemConsumo = false);

public sealed partial class EstoqueService
{
    public Task<ConferenciaConsumoProcedimento?> ConferenciaDoProcedimentoAsync(int atendimentoId, CancellationToken ct = default)
        => _repo.ConferenciaConsumoAsync(atendimentoId, ct);

    /// <summary>Operação separada: nunca conclui, reabre ou relança atendimento/guias.</summary>
    public Task<ConferenciaConsumoProcedimento> RegistrarMateriaisAsync(int agendamentoId, int usuarioId,
        PedidoConsumoProcedimento pedido, CancellationToken ct = default)
        => _repo.ExecutarGestaoAtomicaAsync(async () =>
        {
            var horario = await new PoliticaMateriaisService(_repo).ExigirRegistroAsync(agendamentoId, usuarioId, ct);
            var usuario = (await _repo.ObterUsuarioAsync(usuarioId, ct))!;
            return await ConfirmarConsumoProcedimentoAsync(horario.AtendimentoId!.Value, pedido, usuario.Login, ct);
        }, ct);

    public static IReadOnlyList<MaterialConsumidoProcedimento> MateriaisRegistrados(ConferenciaConsumoProcedimento registro)
        => JsonSerializer.Deserialize<MaterialConsumidoProcedimento[]>(registro.MateriaisJson) ?? [];

    /// <summary>Registra o relato; baixa tudo ou deixa tudo pendente. Reenvio não duplica consumo.</summary>
    public Task<ConferenciaConsumoProcedimento> ConfirmarConsumoProcedimentoAsync(
        int atendimentoId, PedidoConsumoProcedimento pedido, string operador, CancellationToken ct = default)
        => _repo.ExecutarGestaoAtomicaAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(operador) || operador.Trim().Length > 80)
            throw new InvalidOperationException("Identifique o responsável pela conferência dos materiais.");
        var materiais = pedido.Materiais ?? throw new InvalidOperationException("Informe os materiais usados.");
        if (materiais.Count == 0 && !pedido.SemConsumo || materiais.Count > 0 && pedido.SemConsumo)
            throw new InvalidOperationException("Informe os materiais usados ou declare explicitamente que não houve consumo.");
        if (materiais.Count > 300 || materiais.Any(m => m is null || m.ItemId <= 0 || m.Quantidade <= 0
            || m.Quantidade >= 100000000000m || decimal.Round(m.Quantidade, 3) != m.Quantidade || m.Lote?.Length > 60))
            throw new InvalidOperationException("Confira os materiais, lotes e quantidades positivas com até três casas decimais.");
        var agrupados = materiais.GroupBy(m => (m.ItemId, Lote: Limpar(m.Lote)))
            .Select(g => new MaterialConsumidoProcedimento(g.Key.ItemId, g.Sum(m => m.Quantidade), g.Key.Lote))
            .OrderBy(m => m.ItemId).ThenBy(m => m.Lote, StringComparer.Ordinal).ToArray();
        if (agrupados.Any(m => m.Quantidade >= 100000000000m)) throw new InvalidOperationException("Quantidade acima do limite permitido.");
        static string Resumo(IEnumerable<MaterialConsumidoProcedimento> itens)
            => JsonSerializer.Serialize(itens.Select(m => new { m.ItemId, Quantidade = m.Quantidade.ToString("G29", CultureInfo.InvariantCulture), m.Lote }));
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Resumo(agrupados))));
        var anterior = await _repo.ConferenciaConsumoAsync(atendimentoId, ct);
        if (anterior is not null)
        {
            if (anterior.Pedido != hash || anterior.SemConsumo != pedido.SemConsumo)
                throw new InvalidOperationException("Esta sessão já tem materiais registrados com outros dados. Consulte o histórico de consumo.");
            if (anterior.BaixadoEm is not null) return anterior;
        }
        var atendimento = await _repo.ObterAtendimentoAsync(atendimentoId, ct)
            ?? throw new InvalidOperationException("Atendimento não encontrado.");
        var movimentos = (await _repo.MovimentosDoAtendimentoAsync(atendimentoId, ct))
            .Where(m => m.Tipo == TipoMovimentoEstoque.Saida).ToList();
        string? pendencia = null;
        var agora = PoliticaMateriaisService.Agora;
        // O consumo pertence à sessão original, mas a baixa tardia entra no razão de hoje.
        var dataBaixa = DateOnly.FromDateTime(agora);
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
            // Valida o conjunto inteiro antes da primeira baixa, sob a mesma trava de estoque.
            var saldos = await _repo.SaldosEstoqueAsync(ct);
            foreach (var grupo in agrupados.GroupBy(m => m.ItemId))
            {
                var item = await _repo.ObterItemEstoqueAsync(grupo.Key, ct)
                    ?? throw new InvalidOperationException("Material não encontrado.");
                if (item.Uso == UsoEstoque.Rotina) throw new InvalidOperationException("Use materiais destinados a procedimentos.");
                if (grupo.Any(m => (item.ExigirLote || item.Grupo == GrupoEstoque.Medicamento) && m.Lote is null))
                    throw new InvalidOperationException($"Informe o lote utilizado de {item.Nome}.");
                // Misturar uma baixa sem lote com outra por lote torna a alocação ambígua.
                if (grupo.Count() > 1 && grupo.Any(m => m.Lote is null))
                    throw new InvalidOperationException($"Informe o lote em todas as linhas de {item.Nome}.");
                var anteriores = await _repo.MovimentosDoItemAsync(item.Id, ct);
                var razao = RazaoEstoque.Calcular(anteriores);
                if (!item.Ativo) pendencia ??= $"Reative o material {item.Nome} antes de baixar.";
                if (anteriores.Any(m => m.Data > dataBaixa)) pendencia ??= $"Confira os movimentos futuros de {item.Nome}.";
                if (grupo.Sum(m => m.Quantidade) > saldos.GetValueOrDefault(item.Id))
                    pendencia ??= $"Saldo insuficiente de {item.Nome}. Registre a entrada ou confira o inventário.";
                foreach (var m in grupo)
                {
                    var lotes = razao.Lotes.Where(l => m.Lote is null || l.Entrada.Lote == m.Lote).ToList();
                    if (lotes.Sum(l => l.Quantidade) < m.Quantidade) pendencia ??= $"Saldo insuficiente no lote de {item.Nome}.";
                    if (lotes.Any(l => l.Quantidade > 0 && l.Entrada.Validade < dataBaixa))
                        pendencia ??= $"Confira o lote vencido de {item.Nome} antes da baixa.";
                }
            }
            if (pendencia is null)
                foreach (var material in agrupados)
                    await MovimentarInternoAsync(new MovimentoEstoque {
                        ItemEstoqueId = material.ItemId, Tipo = TipoMovimentoEstoque.Saida,
                        Quantidade = material.Quantidade, Lote = material.Lote,
                        AtendimentoId = atendimento.Id, PacienteId = atendimento.PacienteId,
                        Data = dataBaixa, Observacao = $"Materiais da sessão de {atendimento.Data:dd/MM/yyyy}",
                        DestinoConsumo = DestinoConsumoEstoque.Procedimento
                    }, operador, ct, baixaDaConferencia: true);
        }
        var conferencia = anterior ?? new ConferenciaConsumoProcedimento {
            AtendimentoId = atendimentoId, SemConsumo = pedido.SemConsumo, Pedido = hash,
            MateriaisJson = JsonSerializer.Serialize(agrupados), ConferidoEm = agora, ConferidoPor = operador.Trim()
        };
        conferencia.BaixadoEm = pendencia is null ? agora : null;
        conferencia.MotivoPendencia = pendencia;
        if (anterior is null) await _repo.AdicionarConferenciaConsumoAsync(conferencia, ct);
        else await _repo.AtualizarBaixaConferenciaAsync(conferencia.Id, conferencia.BaixadoEm, pendencia, ct);
        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria {
            Acao = pendencia is null ? "MateriaisProcedimentoConferidos" : "MateriaisProcedimentoPendentes",
            Operador = operador.Trim(), PacienteId = atendimento.PacienteId,
            Detalhe = $"Atendimento #{atendimentoId}: " + (pendencia ?? (pedido.SemConsumo
                ? "sem consumo declarado." : $"{agrupados.Length} item(ns)/lote(s) com baixa registrada."))
        }, ct);
        await _repo.SalvarAsync(ct);
        return conferencia;
    }, ct);
}
