using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Modelos;

/// <summary>
/// O que a escolha "CONVÊNIO ou PARTICULAR" afirma nas telas de marcar e de lançar (nov/2026,
/// pedido das médicas: "a paciente tem plano, mas eu não atendo o plano dela — precisa
/// sinalizar já no agendamento que a consulta foi particular").
///
/// Mora aqui, e não no Razor, pela regra da casa: são DUAS telas (o formulário do horário e o
/// Marcar/Lançar do balcão) fazendo a mesma pergunta, e duas telas com dois textos
/// divergiriam na primeira correção. A decisão em si é do domínio
/// (<see cref="SessaoParticular"/>); aqui fica só o que a tela ESCREVE.
/// </summary>
public static class FormaDeAtendimentoNaTela
{
    public const string Rotulo = "Atendimento por";
    public const string RotuloParticular = "Particular";

    /// <summary>
    /// A escolha só aparece quando HÁ o que escolher: a ficha tem convênio que fatura (ou
    /// ainda não tem convênio). Quando a ficha já é particular, a sessão é particular por
    /// construção — oferecer o botão seria uma pergunta com uma resposta só.
    /// </summary>
    public static bool OfereceEscolha(Paciente? paciente)
        => paciente is not null && (paciente.ConvenioADefinir || !SessaoParticular.PelaFicha(paciente));

    /// <summary>O botão do convênio leva o NOME do plano da ficha — é ele que a recepção reconhece.</summary>
    public static string RotuloConvenio(Paciente paciente)
        => paciente.ConvenioADefinir ? "Convênio (a definir)" : paciente.ConvenioNome;

    /// <summary>A linha sob a escolha: o que muda ao marcar particular, dito antes de gravar.</summary>
    public static string Explicacao(Paciente paciente, bool particular)
        => !particular
            ? string.Empty
            : paciente.ConvenioADefinir
                ? "Sem guia: a cobrança é no caixa, e a sessão entra nos relatórios como particular."
                : $"Sem guia do {paciente.ConvenioNome}: a cobrança é no caixa, e a sessão entra nos "
                  + "relatórios como particular. O convênio da ficha não muda.";

    /// <summary>Os alertas que são do CONVÊNIO — e que não dizem nada de uma sessão particular.</summary>
    private static readonly HashSet<ImpedimentoElegibilidade> DoConvenio =
    [
        ImpedimentoElegibilidade.ConvenioADefinir,
        ImpedimentoElegibilidade.CarteirinhaVencida,
        ImpedimentoElegibilidade.CarteirinhaAVencer,
        ImpedimentoElegibilidade.CotaEsgotada,
        ImpedimentoElegibilidade.CotaQuaseNoFim,
        ImpedimentoElegibilidade.ConsultaVencida,
        ImpedimentoElegibilidade.ConsultaARenovar,
        ImpedimentoElegibilidade.SemAutorizacaoVigente
    ];

    /// <summary>
    /// Os alertas de elegibilidade que valem para ESTA sessão. Marcada como particular, saem
    /// carteirinha, cota, consulta e autorização: são conferências do plano que não vai ser
    /// usado, e "carteirinha vencida" em vermelho sobre uma consulta particular ensinaria a
    /// recepção a ignorar o vermelho. Débito, sessão sem receita, pacote e termo ficam.
    /// </summary>
    public static IReadOnlyList<AlertaElegibilidade> AlertasDaSessao(
        IEnumerable<AlertaElegibilidade> alertas, bool particular)
        => (particular ? alertas.Where(a => !DoConvenio.Contains(a.Motivo)) : alertas).ToList();
}
