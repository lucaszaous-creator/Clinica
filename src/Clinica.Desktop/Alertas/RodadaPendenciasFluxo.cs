using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Clinica.Application.Servicos;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.Alertas;

/// <summary>
/// Orquestra a rodada de pendências na UI: lista as guias que passaram do prazo (por guia), abre a
/// janela de decisão e aplica as baixas e as não conformidades linha a linha. Compartilhado entre o
/// painel (botão "Rodar pendências") e a abertura do app (aviso bloqueante quando há guia vencida).
/// </summary>
internal static class RodadaPendenciasFluxo
{
    /// <summary>
    /// Executa a rodada sobre as guias que já passaram do prazo. <paramref name="bloqueante"/> = true
    /// trava a janela até que toda guia tenha decisão (baixa ou não conformidade). Retorna true se
    /// alguma decisão foi aplicada (o painel deve recarregar).
    /// </summary>
    public static async Task<bool> ExecutarAsync(IServiceScopeFactory scopeFactory, Window? owner, bool bloqueante)
    {
        using var scope = scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;
        var rodada = sp.GetRequiredService<RodadaPendenciasService>();
        var faturamento = sp.GetRequiredService<FaturamentoService>();
        var hoje = DateOnly.FromDateTime(DateTime.Today);

        var status = await rodada.ObterStatusAsync(hoje);
        var vencidas = await rodada.GuiasParaDecisaoAsync(hoje);

        // Nenhuma guia passou do prazo: nada a decidir.
        if (vencidas.Count == 0)
            return false;

        var janela = new RodadaPendenciasWindow(vencidas, status, bloqueante) { Owner = owner };
        if (janela.ShowDialog() != true)
            return false;

        foreach (var l in janela.Linhas)
        {
            if (!string.IsNullOrWhiteSpace(l.NumeroGuia))
                await faturamento.DarBaixaAsync(l.CodigoId, janela.DataBaixa, l.NumeroGuia!.Trim(),
                    Environment.UserName, "baixa na rodada de pendências");
            else if (!string.IsNullOrWhiteSpace(l.Justificativa))
                await rodada.MarcarNaoConformidadeAsync(l.CodigoId, l.Justificativa!.Trim(), Environment.UserName);
        }

        return true;
    }
}
