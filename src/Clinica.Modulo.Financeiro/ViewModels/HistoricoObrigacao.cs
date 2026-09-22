using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
namespace Clinica.Financeiro.ViewModels;

internal static class HistoricoObrigacao
{
    public static async Task MostrarAsync(IServiceScopeFactory escopos, IDialogoService dialogo, int id)
    {
        SessaoUsuario.Atual.Exigir(Permissao.VerFinanceiro, "consultar histórico da obrigação");
        using var escopo = escopos.CreateScope();
        var linhas = await escopo.ServiceProvider.GetRequiredService<ContasService>().HistoricoObrigacaoAsync(id);
        var original = linhas.First().ValorOriginalObrigacao ?? linhas.First().Valor;
        var pago = linhas.Where(l => l.Status == StatusLancamento.Realizado).Sum(l => l.Valor);
        var aberto = linhas.Where(l => l.Status == StatusLancamento.Previsto).Sum(l => l.Valor);
        var texto = $"{linhas.First().Descricao}\nValor original: {original:C2}\nPago/recebido: {pago:C2}\nEm aberto: {aberto:C2}\n\n";
        texto += string.Join("\n", linhas.Select(l =>
            $"#{l.Id} · {RotulosEnum.De(l.Status)} · {l.Valor:C2} · vence {l.DataVencimento:dd/MM/yyyy}"
            + (l.DataPagamento is { } data ? $" · baixa {data:dd/MM/yyyy}" : "")
            + (l.OrigemDesdobramentoId is { } origem ? $" · origem #{origem}" : "")));
        dialogo.Aviso("Histórico da obrigação", texto);
    }
}
