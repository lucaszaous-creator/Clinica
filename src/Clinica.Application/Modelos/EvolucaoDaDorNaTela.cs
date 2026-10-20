using System.Globalization;
using Clinica.Application.Servicos;

namespace Clinica.Application.Modelos;

/// <summary>
/// O que a aba "Dor" do Acompanhamento afirma (out/2026, subido da EvolucaoDorViewModel WPF
/// na migração da tela do paciente para Blazor): os quatro números, o resumo, a tendência e
/// as linhas da tabela. Puro, com teste — a tela só desenha.
/// </summary>
public sealed record EvolucaoDaDorNaTela(
    string DorInicial,
    string DorAtual,
    string GanhoAcumulado,
    string AlivioMedio,
    string Resumo,
    string Tendencia,
    bool TendenciaPreocupa,
    bool TemCurva,
    IReadOnlyList<EvolucaoDaDorNaTela.Linha> Linhas,
    IReadOnlyList<PontoDaSerie> CurvaAntes,
    IReadOnlyList<PontoDaSerie> CurvaDepois)
{
    /// <summary>Sessões medidas para falar em tendência — menos que isso, cada ponto é só um dia.</summary>
    public const int SessoesParaTendencia = 3;

    /// <summary>Uma sessão medida, como a tabela a escreve.</summary>
    public sealed record Linha(string Data, int Antes, int Depois, string Variacao, bool Aliviou);

    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static EvolucaoDaDorNaTela Montar(EvolucaoDaDor dor)
    {
        var linhas = dor.Pontos.Select(p => new Linha(
            p.Data.ToString("dd/MM/yyyy", PtBr), p.Antes, p.Depois,
            p.Variacao switch { > 0 => $"−{p.Variacao}", < 0 => $"+{-p.Variacao}", _ => "sem mudança" },
            p.Variacao > 0)).ToList();
        var antes = dor.Pontos.Select(p => new PontoDaSerie(p.Data.ToString("dd/MM", PtBr), p.Antes)).ToList();
        var depois = dor.Pontos.Select(p => new PontoDaSerie(p.Data.ToString("dd/MM", PtBr), p.Depois)).ToList();

        if (dor.SessoesComMedida == 0)
        {
            var resumo = dor.SessoesRegistradas == 0
                ? "Nenhuma sessão registrada no prontuário ainda."
                : $"{dor.SessoesRegistradas} sessão(ões) registradas e nenhuma com o par EVA "
                  + "(antes e depois). Sem o par não há curva: uma medida solta não diz se a dor melhorou.";
            return new("—", "—", "—", "—", resumo, string.Empty, false, false, linhas, antes, depois);
        }

        var ganho = dor.GanhoAcumulado is { } g ? (g >= 0 ? $"−{g} pontos" : $"+{-g} pontos") : "—";
        var (tendencia, preocupa) = LerTendencia(dor);
        return new(
            $"{dor.DorInicial}/10", $"{dor.DorAtual}/10", ganho,
            $"{dor.AlivioMedioPorSessao.ToString("0.#", PtBr)} por sessão",
            $"{dor.SessoesComMedida} de {dor.SessoesRegistradas} sessão(ões) com EVA medida.",
            tendencia, preocupa, true, linhas, antes, depois);
    }

    /// <summary>
    /// A tendência olha a dor de CHEGADA das últimas sessões: é ela que diz se o alívio se
    /// sustenta entre uma sessão e outra.
    /// </summary>
    private static (string Frase, bool Preocupa) LerTendencia(EvolucaoDaDor dor)
    {
        if (dor.Pontos.Count < SessoesParaTendencia)
            return ($"São necessárias ao menos {SessoesParaTendencia} sessões medidas "
                    + "para falar em tendência; até lá, cada ponto é só um dia.", false);

        var ultimas = dor.Pontos.TakeLast(SessoesParaTendencia).ToList();
        var variacao = ultimas[0].Antes - ultimas[^1].Antes;
        var frase = variacao switch
        {
            > 0 => $"Nas últimas {SessoesParaTendencia} sessões a dor de CHEGADA caiu "
                   + $"{variacao} ponto(s) — o alívio está se sustentando entre as sessões.",
            0 => $"Nas últimas {SessoesParaTendencia} sessões a dor de CHEGADA não mudou: "
                 + "a sessão alivia, e o alívio não chega à seguinte. Vale revisar a conduta.",
            _ => $"Nas últimas {SessoesParaTendencia} sessões a dor de CHEGADA subiu "
                 + $"{-variacao} ponto(s). Vale revisar a conduta e investigar o que mudou."
        };
        return (frase, variacao <= 0);
    }
}
