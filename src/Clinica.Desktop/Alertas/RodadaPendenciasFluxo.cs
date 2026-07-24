using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Clinica.Application.Servicos;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.Alertas;

/// <summary>
/// Orquestra a rodada de pendências na UI: abre a janela de decisão, aplica as baixas e as não
/// conformidades linha a linha e conclui a rodada (carimba a data). Compartilhado entre o painel
/// (botão "Rodar pendências") e a abertura do app (aviso bloqueante quando a rodada vence).
/// </summary>
internal static class RodadaPendenciasFluxo
{
    /// <summary>
    /// Executa a rodada. <paramref name="bloqueante"/> = true trava a janela até que toda guia tenha
    /// uma decisão (baixa ou não conformidade). Retorna true se a rodada foi concluída.
    /// </summary>
    public static async Task<bool> ExecutarAsync(IServiceScopeFactory scopeFactory, Window? owner, bool bloqueante)
    {
        using var scope = scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;
        var rodada = sp.GetRequiredService<RodadaPendenciasService>();
        var pendencias = sp.GetRequiredService<PendenciaService>();
        var faturamento = sp.GetRequiredService<FaturamentoService>();
        var hoje = DateOnly.FromDateTime(DateTime.Today);

        var status = await rodada.ObterStatusAsync(hoje);
        var pendentes = await pendencias.CodigosPendentesAsync(hoje);

        // Nada a decidir: conclui direto (zera o prazo do próximo ciclo).
        if (pendentes.Count == 0)
        {
            await rodada.ConcluirRodadaAsync(hoje, Environment.UserName);
            return true;
        }

        var janela = new RodadaPendenciasWindow(pendentes, status, bloqueante) { Owner = owner };
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

        await rodada.ConcluirRodadaAsync(hoje, Environment.UserName);
        return true;
    }
}
