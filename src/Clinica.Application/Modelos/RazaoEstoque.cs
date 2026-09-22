using Clinica.Domain.Entities;

namespace Clinica.Application.Modelos;

/// <summary>Reconstituição cronológica do estoque. Compras futuras não reprecificam consumos anteriores.</summary>
public sealed class RazaoEstoque
{
    public sealed class LoteDisponivel
    {
        public required MovimentoEstoque Entrada { get; init; }
        public decimal Quantidade { get; set; }
    }

    public List<LoteDisponivel> Lotes { get; } = [];
    public Dictionary<int, decimal?> CustosDosMovimentos { get; } = [];
    public decimal Saldo => Lotes.Sum(l => l.Quantidade);
    public decimal? CustoMedio { get; private set; }

    public static RazaoEstoque Calcular(IEnumerable<MovimentoEstoque> movimentos)
    {
        var razao = new RazaoEstoque();
        foreach (var m in movimentos.OrderBy(m => m.Data).ThenBy(m => m.Id))
        {
            var custo = m.Tipo == TipoMovimentoEstoque.Entrada ? m.CustoUnitario : m.CustoUnitario ?? razao.CustoMedio;
            razao.CustosDosMovimentos[m.Id] = custo;
            if (m.Delta > 0)
            {
                var saldo = razao.Saldo;
                razao.CustoMedio = saldo == 0 ? custo
                    : razao.CustoMedio is { } medio && custo is { } novo
                        ? Math.Round((saldo * medio + m.Quantidade * novo) / (saldo + m.Quantidade), 4)
                        : null;
                razao.Lotes.Add(new LoteDisponivel { Entrada = m, Quantidade = m.Quantidade });
            }
            else
            {
                var restante = m.Quantidade;
                // Movimentos antigos sem lote são alocados por validade e ordem de entrada.
                foreach (var lote in razao.Lotes
                    .Where(l => string.IsNullOrWhiteSpace(m.Lote) || l.Entrada.Lote == m.Lote)
                    .OrderBy(l => l.Entrada.Validade ?? DateOnly.MaxValue).ThenBy(l => l.Entrada.Id))
                {
                    var baixa = Math.Min(restante, lote.Quantidade);
                    lote.Quantidade -= baixa;
                    restante -= baixa;
                    if (restante == 0) break;
                }
                if (razao.Saldo == 0) razao.CustoMedio = null;
            }
        }
        return razao;
    }
}
