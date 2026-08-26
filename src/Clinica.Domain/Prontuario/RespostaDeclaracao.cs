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
