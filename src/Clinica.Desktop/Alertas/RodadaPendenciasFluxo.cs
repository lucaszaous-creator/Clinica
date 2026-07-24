using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Clinica.Application.Servicos;
using Clinica.Desktop.Configuracao;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.Alertas;

/// <summary>
/// Orquestra a rodada de pendências na UI: abre a janela de decisão e aplica as baixas e as não
/// conformidades linha a linha. Compartilhado entre o painel (botão "Rodar pendências", que mostra
/// TODAS as pendências) e a abertura do app (aviso BLOQUEANTE com as guias cujo prazo desde o
/// atendimento venceu — precisam de decisão antes de o sistema liberar).
/// </summary>
internal static class RodadaPendenciasFluxo
{
    /// <summary>
    /// Executa a rodada. <paramref name="bloqueante"/> = true lista apenas as guias com prazo vencido
    /// (atendimento + N dias) e trava a janela até que toda guia tenha uma decisão (baixa ou não
    /// conformidade); false lista todas as pendentes (cobrança proativa). Retorna true se foi concluída.
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
        var itens = bloqueante
            ? await rodada.GuiasVencidasParaDecisaoAsync(hoje) // só as vencidas bloqueiam
            : await pendencias.CodigosPendentesAsync(hoje);     // manual: todas as pendentes

        // Nada a decidir: encerra sem abrir a janela.
        if (itens.Count == 0)
            return true;

        var janela = new RodadaPendenciasWindow(itens, status, bloqueante) { Owner = owner };
        if (janela.ShowDialog() != true)
            return false;

        // Aplica linha a linha, mas UMA falha (nº de guia inválido, concorrência etc.) não pode abortar
        // as demais nem passar despercebida: registra a falha e segue; ao fim, resume o que não aplicou.
        var falhas = new List<string>();
        foreach (var l in janela.Linhas)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(l.NumeroGuia))
                    await faturamento.DarBaixaAsync(l.CodigoId, janela.DataBaixa, l.NumeroGuia!.Trim(),
                        Environment.UserName, "baixa na rodada de pendências");
                else if (!string.IsNullOrWhiteSpace(l.Justificativa))
                    await rodada.MarcarNaoConformidadeAsync(l.CodigoId, l.Justificativa!.Trim(), Environment.UserName);
            }
            catch (Exception ex)
            {
                LogErros.Registrar($"Rodada de pendências — falha ao aplicar guia {l.CodigoId}", ex);
                falhas.Add($"• {l.Descricao}: {ex.Message}");
            }
        }

        if (falhas.Count > 0)
            MessageBox.Show(
                $"{falhas.Count} guia(s) não puderam ser aplicadas e continuam pendentes:\n\n" +
                string.Join("\n", falhas) +
                "\n\nO restante foi processado. Reveja essas guias no painel.",
                "Rodar pendências", MessageBoxButton.OK, MessageBoxImage.Warning);

        return true;
    }
}
