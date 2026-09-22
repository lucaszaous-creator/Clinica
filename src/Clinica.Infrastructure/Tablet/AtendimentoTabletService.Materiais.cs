using Clinica.Application.Tablet;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
namespace Clinica.Infrastructure.Tablet;

public sealed partial class AtendimentoTabletService
{
    private async Task<bool> MateriaisHabilitadosAsync(Agendamento horario, CancellationToken ct)
    {
        var politica = await new PoliticaMateriaisService(repo).ObterAsync(ct);
        return politica.Modo == ModoMateriais.Equipe && horario.Status == StatusAgendamento.Realizado
            && horario.FimAtendimentoEm is not null && horario.AtendimentoId is not null
            && (politica.Abrange(horario) || horario.AtendimentoId is { } id && await repo.ConferenciaConsumoAsync(id, ct) is not null);
    }

    public async Task<MateriaisProcedimentoTablet> MateriaisAsync(SessaoTablet sessao, int agendamentoId, CancellationToken ct)
    {
        var usuario = await AutorizarAsync(sessao, ct);
        var horario = await Horario(usuario, agendamentoId, ct);
        if (!await MateriaisHabilitadosAsync(horario, ct)) throw new RecursoClinicoIndisponivel();
        var conferencia = await repo.ConferenciaConsumoAsync(horario.AtendimentoId!.Value, ct);
        var itens = await repo.ItensEstoqueAsync(true, ct);
        var saldos = await repo.SaldosEstoqueAsync(ct);
        var linhas = itens.Where(i => i.Uso != UsoEstoque.Rotina).OrderBy(i => i.Nome)
            .Select(i => new MaterialProcedimentoTablet(i.Id, i.Nome, i.CodigoInterno, i.Unidade,
                saldos.GetValueOrDefault(i.Id), i.ExigirLote || i.Grupo == GrupoEstoque.Medicamento)).ToList();
        var registrados = conferencia is not null ? EstoqueService.MateriaisRegistrados(conferencia) : [];
        if (registrados.Count == 0 && conferencia?.SemConsumo != true)
            registrados = (await repo.MovimentosDoAtendimentoAsync(horario.AtendimentoId.Value, ct))
                .Where(m => m.Tipo == TipoMovimentoEstoque.Saida).GroupBy(m => (m.ItemEstoqueId, m.Lote))
                .Select(g => new MaterialConsumidoProcedimento(g.Key.ItemEstoqueId, g.Sum(m => m.Quantidade), g.Key.Lote)).ToArray();
        foreach (var material in registrados)
        {
            var indice = linhas.FindIndex(l => l.ItemId == material.ItemId && l.QuantidadeUtilizada is null);
            if (indice >= 0) linhas[indice] = linhas[indice] with { QuantidadeUtilizada = material.Quantidade, Lote = material.Lote };
            else
            {
                var item = await repo.ObterItemEstoqueAsync(material.ItemId, ct)
                    ?? throw new InvalidOperationException("O material registrado não foi localizado.");
                linhas.Add(new(item.Id, item.Nome, item.CodigoInterno, item.Unidade, saldos.GetValueOrDefault(item.Id),
                    item.ExigirLote || item.Grupo == GrupoEstoque.Medicamento, material.Quantidade, material.Lote));
            }
        }
        return new(conferencia?.BaixadoEm is not null, conferencia?.SemConsumo == true, linhas,
            conferencia is not null, conferencia?.MotivoPendencia);
    }

    public Task<ResultadoMateriaisTablet> RegistrarMateriaisAsync(SessaoTablet sessao, int id, RegistrarMateriaisTablet pedido, CancellationToken ct)
        => Escrever(sessao, id, pedido.Idempotencia, pedido, async (u, horario) =>
        {
            if (!await MateriaisHabilitadosAsync(horario, ct)) throw new RecursoClinicoIndisponivel();
            if (pedido.Consumo is null) throw new InvalidOperationException("Informe os materiais.");
            var registro = await new EstoqueService(repo).RegistrarMateriaisAsync(id, u.Id, pedido.Consumo, ct);
            return new ResultadoMateriaisTablet(registro.BaixadoEm is not null, registro.MotivoPendencia is { } motivo
                ? "Consumo registrado; baixa pendente no estoque. " + motivo + " O atendimento e as guias permanecem concluídos."
                : registro.SemConsumo ? "Ausência de consumo registrada." : "Materiais registrados e baixados do estoque.");
        }, ct);
}
