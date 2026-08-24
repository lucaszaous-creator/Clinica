namespace Clinica.Application.Modelos;

/// <summary>
/// Os sinais vitais que a ENFERMAGEM aferiu no dia desta sessão, para a tela de quem
/// atende (parcela 76).
///
/// ⚠️ Existe porque a clínica disse que <b>todo paciente passa pela enfermagem</b>: a PA, a
/// FC e a temperatura são colhidas minutos antes da consulta, e quem prescreve escrevia a
/// sessão sem elas na frente — ou saía da tela para procurá-las. É o defeito recorrente do
/// projeto na variante "o leitor existe, mas não onde a decisão acontece".
///
/// É LEITURA, nunca coleta: colher aqui daria dois lugares para gravar a mesma aferição,
/// e a tela de atendimento não é formulário de enfermagem.
/// </summary>
/// <param name="Resumo">"PA 120x80 · FC 78 · T 36,4 °C" — montado pela entidade.</param>
/// <param name="Hora">A hora do FATO, informada por quem aferiu (nunca o relógio).</param>
/// <param name="Autor">Quem aferiu.</param>
/// <param name="Conselho">O COREN de quem aferiu, quando há.</param>
public sealed record SinaisVitaisDaSessao(
    string Resumo, TimeOnly Hora, string Autor, string? Conselho)
{
    /// <summary>"às 09:12, por Joana Técnica (COREN-SP 999999)".</summary>
    public string Procedencia => string.IsNullOrWhiteSpace(Conselho)
        ? $"\u00E0s {Hora:HH\\:mm}, por {Autor}"
        : $"\u00E0s {Hora:HH\\:mm}, por {Autor} ({Conselho})";
}
