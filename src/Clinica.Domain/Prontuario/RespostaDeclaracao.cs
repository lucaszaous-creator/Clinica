using Clinica.Domain.Entities;

namespace Clinica.Domain.Prontuario;

/// <summary>
/// Como o paciente respondeu uma declaração do termo.
///
/// É texto e não booleano porque mora em <see cref="ItemDocumento.Quantidade"/>, que é o
/// campo genérico que as sete impressões já usam — e porque a resposta precisa sair
/// IMPRESSA com essa palavra na via que o paciente leva. Um booleano viraria "True" no
/// papel se alguém esquecesse de traduzir.
///
/// ⚠️ Ele DESCEU da Application para o Domínio na parcela 89, e não foi arrumação: o termo
/// LGPD precisa ler a mesma resposta (<see cref="TermoConsentimento.Decisoes"/>) e o
/// Domínio não enxerga a Application. Uma segunda cópia de "isto é um sim?" divergiria na
/// primeira correção — e aqui divergir significa o paciente responder “Não” ao marketing e
/// o sistema gravar outra coisa, sem nada falhar.
/// </summary>
public static class RespostaDeclaracao
{
    public const string Sim = "Sim";
    public const string Nao = "Não";
    public const string CodigoAlergiasTablet = "ALERGIAS_TABLET";

    /// <summary>
    /// A AUTORIZAÇÃO DE MENSAGENS (out/2026, fase 3 Recepção×CRM): a declaração opcional que
    /// os termos do BSV levam e que, com "Sim", registra o consentimento de
    /// <see cref="FinalidadeConsentimento.ComunicacaoEMarketing"/> — o que o recall do CRM
    /// exige. É o NOME da finalidade, a mesma convenção do termo LGPD
    /// (<see cref="TermoConsentimento"/>).
    /// </summary>
    public const string CodigoAutorizacaoMensagens = nameof(FinalidadeConsentimento.ComunicacaoEMarketing);

    // A pergunta direta sobre alergia tem polaridade diferente das declarações
    // afirmativas do TCLE: "sim, tenho alergia" precisa de atenção clínica.
    // A autorização de mensagens não pede atenção em resposta NENHUMA: "Não" é uma
    // escolha do paciente, não um sinal clínico, e acender vermelho no balcão por ela
    // ensinaria a ignorar o vermelho.
    public static bool RequerAtencao(ItemDocumento item)
        => item.Codigo == CodigoAutorizacaoMensagens ? false
            : item.Codigo == CodigoAlergiasTablet
            ? EhPositiva(item.Quantidade)
            : EhNegativa(item.Quantidade);

    /// <summary>
    /// A resposta é um "não". Compara sem acento e sem caixa porque o valor pode ter sido
    /// gravado por versões diferentes da tela — e uma comparação estrita faria uma
    /// declaração negada deixar de acender o alerta, que é a falha que custa caro.
    /// </summary>
    public static bool EhNegativa(string? resposta)
        => resposta is not null
           && (resposta.Trim().Equals(Nao, StringComparison.OrdinalIgnoreCase)
               || resposta.Trim().Equals("Nao", StringComparison.OrdinalIgnoreCase));

    public static bool EhPositiva(string? resposta)
        => resposta is not null && resposta.Trim().Equals(Sim, StringComparison.OrdinalIgnoreCase);
}
