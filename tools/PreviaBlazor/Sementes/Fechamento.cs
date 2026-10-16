// Dados FICTÍCIOS da prévia — nenhuma pessoa real.
using Clinica.Domain.Entities;
using Clinica.Infrastructure;

namespace PreviaBlazor.Sementes;

/// <summary>
/// A gaveta dos últimos dez dias: recebimentos e despesas em ESPÉCIE (só eles entram na
/// conferência), alguns dias já conferidos — um que bateu, um com sobra justificada, um
/// reaberto — e o resto pendente, que é o que o painel de "não conferidos" mostra.
/// </summary>
public sealed class SementeFechamento : ISemente
{
    public int Ordem => 5;

    public void Semear(ClinicaDbContext db)
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var rnd = new Random(11);
        var categoriaParticular = db.CategoriasFinanceiras.FirstOrDefault(c => c.Nome == "Particular")?.Id;
        for (var i = 9; i >= 0; i--)
        {
            var dia = hoje.AddDays(-i);
            if (dia.DayOfWeek == DayOfWeek.Sunday) continue;
            var sessoes = 2 + rnd.Next(4);
            for (var s = 0; s < sessoes; s++)
                db.Add(new LancamentoFinanceiro
                {
                    Data = dia, DataPagamento = dia, Tipo = TipoLancamento.Entrada, Status = StatusLancamento.Realizado,
                    Valor = 180m + 20m * rnd.Next(4), FormaPagamento = FormaPagamento.Dinheiro,
                    Descricao = "Sessão avulsa paga em dinheiro", CategoriaFinanceiraId = categoriaParticular
                });
            if (i % 3 == 0)
                db.Add(new LancamentoFinanceiro
                {
                    Data = dia, DataPagamento = dia, Tipo = TipoLancamento.Saida, Status = StatusLancamento.Realizado,
                    Valor = 45m, FormaPagamento = FormaPagamento.Dinheiro, Descricao = "Café e água para a recepção"
                });
        }
        db.SaveChanges();

        // Três dias já conferidos. O ValorSistema é COPIADO no fechamento (parcela 14).
        void Conferido(int diasAtras, decimal diferenca, string? justificativa, bool reaberto = false)
        {
            var dia = hoje.AddDays(-diasAtras);
            var lanc = db.Lancamentos.Where(l => l.Data == dia && l.FormaPagamento == FormaPagamento.Dinheiro).ToList();
            if (lanc.Count == 0) return;
            var entradas = lanc.Where(l => l.Tipo == TipoLancamento.Entrada).Sum(l => l.Valor);
            var saidas = lanc.Where(l => l.Tipo == TipoLancamento.Saida).Sum(l => l.Valor);
            db.Add(new FechamentoCaixa
            {
                Data = dia, ValorSistema = entradas, SaidasEspecie = saidas, ValorContado = entradas - saidas + diferenca,
                QuantidadeLancamentos = lanc.Count, Justificativa = justificativa, ConferidoPor = "bianca.moraes",
                ConferidoEm = dia.ToDateTime(new TimeOnly(18, 40)), Reaberto = reaberto,
                MotivoReabertura = reaberto ? "Recontar: troco de R$ 20 achado na gaveta de baixo." : null
            });
        }
        Conferido(8, 0m, null);
        Conferido(7, 20m, "Sessão das 16h paga em dinheiro e lançada como PIX — corrigido.");
        Conferido(6, -10m, "Troco dado sem registro.", reaberto: true);
        db.SaveChanges();
    }
}
