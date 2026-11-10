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

    // ==================== a pergunta obrigatória (nov/2026) ====================
    //
    // A direção, depois de uma semana com a escolha opcional: "em TODA marcação a secretária
    // tem de dizer se é pelo plano ou particular — mesmo o paciente tendo plano. Cada
    // especialidade é credenciada em alguns planos, e quem cala gera a confusão". A escolha
    // deixa de ter resposta padrão: ou a pessoa clicou no seletor, ou o Salvar pergunta.

    public const string TituloDaPergunta = "Plano ou particular?";

    /// <summary>A dica sob o seletor enquanto ninguém respondeu.</summary>
    public const string FaltaResponder = "Obrigatório: diga se este atendimento é pelo plano ou particular.";

    /// <summary>
    /// O Salvar precisa perguntar? Só quando HÁ escolha (a ficha particular não tem o que
    /// responder) e ninguém a fez NESTA marcação. Sugestão do sistema não é resposta.
    /// </summary>
    public static bool PrecisaPerguntar(Paciente? paciente, bool respondida)
        => !respondida && OfereceEscolha(paciente);

    public static string Pergunta(Paciente paciente, Profissional? profissional = null)
        => $"O atendimento de {paciente.Nome} é pelo {RotuloConvenio(paciente)} ou é particular?"
           + (profissional?.NaoAtende(paciente) == true
               ? $"\n\n{profissional.Rotulo} não atende pelo {paciente.ConvenioNome}."
               : string.Empty);

    /// <summary>As duas respostas, na ordem dos botões: [0] pelo plano, [1] particular.</summary>
    public static IReadOnlyList<string> Respostas(Paciente paciente)
        => [$"Pelo {RotuloConvenio(paciente)}", RotuloParticular];

    /// <summary>O índice da resposta "particular" em <see cref="Respostas"/>.</summary>
    public const int RespostaParticular = 1;

    /// <summary>
    /// A resposta do diálogo mudou o que a tela mostrava (preço, guias, trava): a tela aplica
    /// a escolha e NÃO grava — a pessoa confere o resumo novo e confirma de novo.
    /// </summary>
    public const string ConfiraDeNovo = "Escolha registrada. Confira o resumo, que mudou com ela, e confirme de novo.";

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

    /// <summary>
    /// A sugestão do sistema: quem atende NÃO atende pelo convênio da ficha (cadastro do
    /// profissional). É AVISO, não resposta (nov/2026 — a pergunta virou obrigatória): a tela
    /// diz, e quem marca responde.
    /// </summary>
    public static bool SugereParticular(Profissional? profissional, Paciente? paciente)
        => OfereceEscolha(paciente) && profissional?.NaoAtende(paciente) == true;

    public static string PreMarcado(Profissional profissional, Paciente paciente)
        => $"{profissional.Rotulo} não atende pelo {paciente.ConvenioNome} — este atendimento deve ser particular. "
           + "Confirme acima.";

    /// <summary>Os convênios que entram no cadastro do profissional: os que FATURAM (particular e "a definir" não são escolha).</summary>
    public static IReadOnlyList<(string Codigo, string Nome)> ConveniosParaOProfissional(
        IEnumerable<(string Codigo, string Nome, bool GeraGuia, bool Ativo)> catalogo, IEnumerable<string> jaMarcados)
    {
        var marcados = jaMarcados.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return catalogo
            .Where(c => c.GeraGuia && (c.Ativo || marcados.Contains(c.Codigo)))
            .OrderBy(c => c.Nome, StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("pt-BR"), true))
            .Select(c => (c.Codigo, c.Nome))
            .ToList();
    }

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
