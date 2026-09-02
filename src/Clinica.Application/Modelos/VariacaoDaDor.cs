namespace Clinica.Application.Modelos;

/// <summary>
/// A frase que a régua de dor escreve enquanto a pessoa mexe nela — "aliviou 5 pontos",
/// "piorou 2", "sem mudança", ou o pedido para medir o par.
///
/// Existia dentro da ViewModel da janela de evolução do balcão; quando a tela de
/// Atendimento do Consultório ganhou a mesma régua (set/2026), a alternativa era uma
/// segunda cópia do <c>switch</c> — e duas frases sobre a MESMA medida divergem na
/// primeira correção. Mora na Application porque decisão em projeto WPF não é alcançada
/// pelo <c>dotnet test</c>.
/// </summary>
public static class VariacaoDaDor
{
    public static string Descrever(int? antes, int? depois) => (antes, depois) switch
    {
        (null, _) or (_, null) => "Meça antes e depois para saber se aliviou.",
        var (a, d) when a > d => $"Aliviou {a - d} ponto(s) nesta sessão.",
        var (a, d) when a < d => $"Piorou {d - a} ponto(s) nesta sessão.",
        _ => "Sem mudança nesta sessão."
    };
}
