namespace Clinica.Application.Modelos;

/// <summary>
/// A ÚNICA linha de contexto acima da folha do atendimento (set/2026, "uma folha, dois
/// lados"). Eram quatro linhas de texto suave — os sinais vitais, a procedência deles, o
/// resumo da dor e a última sessão —, e as quatro empurravam a folha para baixo da dobra
/// no monitor de 768 do consultório. O resumo da dor saiu (é o assunto da seção
/// Acompanhamento); o que fica é a resposta às duas perguntas que se fazem antes de
/// escrever: "por que este paciente está aqui hoje" e "o que a enfermagem aferiu".
///
/// Três estados para a enfermagem, e só dois aparecem: aferido (o número e quem mediu),
/// e "não foi possível conferir" (o terceiro estado, escrito). Sem aferição no dia a
/// metade SOME — a linha é contexto, não a resposta de um campo, e "Sem aferição" em toda
/// consulta sem passagem viraria paisagem antes do dia em que importa. A falha continua
/// escrita porque é a que muda conduta se for confundida com ausência (parcela 76).
///
/// Mora na Application pela regra de sempre: o que decide o que a tela AFIRMA precisa
/// morar onde o <c>dotnet test</c> alcança.
/// </summary>
public static class ContextoDaFolha
{
    public const string Separador = "   ·   ";

    /// <param name="ultimaSessao">A linha da última sessão (<see cref="ResumoSessaoAnterior.ContextoDaUltima"/>); vazia na primeira.</param>
    /// <param name="sinaisVitais">"PA 120x80 · FC 78 · T 36,4 °C" quando a enfermagem aferiu; nulo/vazio quando não.</param>
    /// <param name="procedencia">"às 09:12, por Joana (COREN-SP 999999)"; vazia sem aferição.</param>
    /// <param name="leituraFalhou">A leitura dos sinais vitais não respondeu — o terceiro estado.</param>
    public static string Montar(
        string? ultimaSessao, string? sinaisVitais, string? procedencia, bool leituraFalhou)
    {
        var partes = new List<string>(2);

        if (!string.IsNullOrWhiteSpace(ultimaSessao))
            partes.Add(ultimaSessao.Trim());

        if (leituraFalhou)
            partes.Add("Enfermagem hoje: não foi possível conferir os sinais vitais");
        else if (!string.IsNullOrWhiteSpace(sinaisVitais))
            partes.Add(string.IsNullOrWhiteSpace(procedencia)
                ? $"Enfermagem hoje: {sinaisVitais.Trim()}"
                : $"Enfermagem hoje: {sinaisVitais.Trim()} ({procedencia.Trim()})");

        return string.Join(Separador, partes);
    }
}
