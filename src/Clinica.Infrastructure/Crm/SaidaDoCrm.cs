using Clinica.Application.Abstracoes;

namespace Clinica.Infrastructure.Crm;

/// <summary>
/// A saída da pessoa do CRM quando ela sai do app (out/2026, PR 5 do WhatsApp nativo,
/// docs/whatsapp-nativo-implementacao.md). O ponto único é o FECHAR da janela do app
/// (<c>JanelaDoApp.Closed</c>): "Sair" fecha a janela, e "Trocar usuário" REABRE o processo
/// (parcela 45) — as duas saídas passam por ali, e nenhuma troca a sessão em memória.
///
/// Por que existe e não é só <c>EsquecerSessao</c>: o aviso ao CRM corre numa tarefa de
/// fundo, e o processo termina logo depois de a janela fechar. Sem esperar, o POST de logout
/// morria com o processo quase sempre, e a sessão da pessoa continuava valendo no servidor do
/// CRM até vencer (2 h) — a chamada existiria e não faria o que promete. A espera tem teto: o CRM fora do
/// ar não pode segurar o fechamento do app. A janela já sumiu da tela quando isto roda; quem
/// troca de usuário já tem o app novo abrindo.
/// </summary>
public static class SaidaDoCrm
{
    /// <summary>O máximo que o fechamento do app espera pelo aviso de saída.</summary>
    public static readonly TimeSpan Espera = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Esquece a sessão do CRM de quem estava logado e espera o aviso por até <paramref name="espera"/>.
    /// Nunca lança. Devolve se havia alguém para tirar do CRM.
    /// </summary>
    /// <param name="crm">O cliente do processo (nulo quando o app não o registra).</param>
    /// <param name="usuarioId">Quem estava logado; 0 = ninguém entrou (desistiu na entrada).</param>
    public static bool AoFecharOApp(IClienteDoCrm? crm, int usuarioId, TimeSpan espera)
    {
        if (crm is null || usuarioId <= 0) return false;
        try
        {
            crm.EsquecerSessao(usuarioId);
            // SairAsync nunca falha (registra no Diagnostico e engole): o Wait só limita o tempo.
            if (crm is ClienteDoCrmHttp http) http.UltimaSaida.Wait(espera);
            return true;
        }
        catch (Exception e)
        {
            Clinica.Application.Diagnostico.Registrar("CRM — saída no fechamento do app (a sessão vence sozinha)", e);
            return false;
        }
    }
}
