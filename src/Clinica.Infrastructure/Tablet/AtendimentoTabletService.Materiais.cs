using Clinica.Application.Tablet;
using Clinica.Domain.Entities;
namespace Clinica.Infrastructure.Tablet;

public sealed partial class AtendimentoTabletService
{
    public async Task<MateriaisProcedimentoTablet> MateriaisAsync(SessaoTablet sessao, int agendamentoId, CancellationToken ct)
    {
        var usuario = await AutorizarAsync(sessao, ct);
        var horario = await Horario(usuario, agendamentoId, ct);
        var conferencia = horario.AtendimentoId is { } id ? await repo.ConferenciaConsumoAsync(id, ct) : null;
        var itens = await repo.ItensEstoqueAsync(true, ct);
        var saldos = await repo.SaldosEstoqueAsync(ct);
        var linhas = itens.Where(i => i.Uso != UsoEstoque.Rotina).OrderBy(i => i.Nome)
            .Select(i => new MaterialProcedimentoTablet(i.Id, i.Nome, i.CodigoInterno, i.Unidade,
                saldos.GetValueOrDefault(i.Id), i.ExigirLote || i.Grupo == GrupoEstoque.Medicamento)).ToList();
        if (horario.AtendimentoId is { } atendimento)
        {
            var movimentos = await repo.MovimentosDoAtendimentoAsync(atendimento, ct);
            foreach (var grupo in movimentos.Where(m => m.Tipo == TipoMovimentoEstoque.Saida)
                .GroupBy(m => (m.ItemEstoqueId, m.Lote)))
            {
                var indice = linhas.FindIndex(l => l.ItemId == grupo.Key.ItemEstoqueId && l.QuantidadeUtilizada is null);
                if (indice >= 0)
                    linhas[indice] = linhas[indice] with { QuantidadeUtilizada = grupo.Sum(m => m.Quantidade), Lote = grupo.Key.Lote };
                else
                {
                    var item = await repo.ObterItemEstoqueAsync(grupo.Key.ItemEstoqueId, ct)
                        ?? throw new InvalidOperationException("O material registrado não foi localizado.");
                    linhas.Add(new(item.Id, item.Nome, item.CodigoInterno, item.Unidade, saldos.GetValueOrDefault(item.Id),
                        item.ExigirLote || item.Grupo == GrupoEstoque.Medicamento, grupo.Sum(m => m.Quantidade), grupo.Key.Lote));
                }
            }
        }
        return new(conferencia is not null, conferencia?.SemConsumo == true, linhas);
    }
}
