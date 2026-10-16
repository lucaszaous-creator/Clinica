using System.Globalization;
using Clinica.Application.Servicos;

namespace Clinica.Application.Modelos;

/// <summary>
/// O que a tela de RECEBÍVEIS DE CARTÃO afirma (out/2026, na migração para Blazor).
///
/// Estas frases moravam no <c>RecebiveisViewModel</c> do WPF, que o `dotnet test` não
/// alcança. Elas decidem o que a pessoa faz a seguir: "DEVIA TER CAÍDO faz 3 dia(s)" é a
/// única linha da tela que pede ação (ligar para a adquirente), e um "cai amanhã" errado
/// sobre um depósito atrasado esconderia justamente o dinheiro que sumiu.
///
/// Dinheiro e data em pt-BR FIXO: a Application também roda na prévia em Linux, onde a
/// cultura do processo é a invariante.
/// </summary>
public static class RecebiveisNaTela
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>As janelas que o combo "Horizonte" oferece, em dias.</summary>
    public static readonly IReadOnlyList<int> Horizontes = [15, 30, 60, 90];

    /// <summary>A janela de abertura dos previstos.</summary>
    public const int HorizontePadrao = 60;

    /// <summary>Janela dos já creditados: quem procura confirmação errada procura recente.</summary>
    public const int DiasConfirmados = 30;

    /// <summary>
    /// O prazo em palavras, como nas contas: ninguém calcula "20/10 menos hoje". O
    /// atrasado sai em maiúsculas porque é a única linha que pede ação.
    /// </summary>
    public static string DetalheDoEsperado(DepositoEsperado d, DateOnly hoje)
    {
        var dias = d.Previsao.DayNumber - hoje.DayNumber;
        return dias switch
        {
            < 0 => $"{d.Quantidade} crédito(s) · DEVIA TER CAÍDO faz {-dias} dia(s)",
            0 => $"{d.Quantidade} crédito(s) · cai hoje",
            1 => $"{d.Quantidade} crédito(s) · cai amanhã",
            _ => $"{d.Quantidade} crédito(s) · cai em {dias} dia(s)"
        };
    }

    /// <summary>
    /// O atraso de um depósito que já caiu. As duas datas ficam gravadas separadas
    /// justamente para ele continuar visível depois de o dinheiro entrar.
    /// </summary>
    public static string DetalheDoConfirmado(DepositoConfirmado d)
    {
        var atraso = d.DiasDeAtraso switch
        {
            null => "sem previsão registrada",
            0 => "caiu no dia previsto",
            < 0 => $"caiu {-d.DiasDeAtraso} dia(s) adiantado",
            _ => $"caiu {d.DiasDeAtraso} dia(s) depois do previsto"
        };
        return $"{d.Quantidade} crédito(s) · {atraso}";
    }

    /// <summary>Caiu depois do previsto — a linha ganha o tom de aviso.</summary>
    public static bool Atrasou(DepositoConfirmado d) => d.DiasDeAtraso > 0;

    /// <summary>"—" quando não há próximo: não haver depósito e ele ser zero são coisas diferentes.</summary>
    public static string Proximo(ResumoRecebiveis r)
        => r.ProximoDeposito is { } p ? p.ToString("dd/MM/yyyy", PtBr) : "—";

    /// <summary>A frase da aba dos previstos.</summary>
    public static string ResumoDosPrevistos(int depositos, ResumoRecebiveis r)
        => depositos == 0
            ? "Nenhum recebimento de cartão pendente no período."
            : r.TemAtraso
                ? $"{depositos} depósito(s) previsto(s) · {r.QuantidadeAtrasada} crédito(s) que a adquirente JÁ DEVIA TER DEPOSITADO."
                : $"{depositos} depósito(s) previsto(s), nenhum atrasado.";

    /// <summary>A frase da aba dos que já caíram — diz a janela, e quantos atrasaram.</summary>
    public static string ResumoDosConfirmados(IReadOnlyList<DepositoConfirmado> confirmados, int dias)
    {
        if (confirmados.Count == 0) return $"Nenhum depósito confirmado nos últimos {dias} dias.";
        var atrasaram = confirmados.Count(Atrasou);
        return $"{confirmados.Count} depósito(s) confirmado(s) nos últimos {dias} dias"
               + (atrasaram > 0 ? $" · {atrasaram} caiu(íram) depois do previsto." : ".");
    }

    /// <summary>
    /// A data do crédito é INFORMADA, nunca assumida: a clínica confere o extrato na
    /// segunda e o depósito caiu na sexta. Nula = a pessoa apagou o campo.
    /// </summary>
    public static string? ConferirDataDoCredito(DateOnly? dia)
        => dia is null ? "Informe em que dia o dinheiro caiu na conta." : null;

    /// <summary>A pergunta do "Caiu" — diz quem, quanto e quando, antes do clique final.</summary>
    public static string PerguntaDeConfirmar(DepositoEsperado d, DateOnly dia, DateOnly hoje)
        => $"Confirmar que {d.Adquirente} depositou {d.Liquido.ToString("C", PtBr)} "
           + $"em {dia.ToString("dd/MM/yyyy", PtBr)}? ({DetalheDoEsperado(d, hoje)})";

    /// <summary>A pergunta do "Desfazer" — e o que ele NÃO faz (o lançamento fica no caixa).</summary>
    public static string PerguntaDeDesfazer(DepositoConfirmado d)
        => $"Devolver {d.Liquido.ToString("C", PtBr)} de {d.Adquirente} (creditado em {d.Creditado.ToString("dd/MM/yyyy", PtBr)}) "
           + "para a lista do que a adquirente ainda deve depositar? "
           + "O lançamento continua no caixa — só a data do crédito é apagada.";
}
