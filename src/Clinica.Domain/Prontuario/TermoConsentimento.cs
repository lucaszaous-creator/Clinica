using Clinica.Domain.Entities;

namespace Clinica.Domain.Prontuario;

/// <summary>
/// Uma decisão do paciente sobre uma finalidade, lida do termo LGPD que ele ASSINOU.
/// </summary>
/// <param name="Finalidade">A finalidade a que a declaração se refere.</param>
/// <param name="Concedido">
/// <c>true</c> = autorizou; <c>false</c> = recusou. Recusar o que estava vigente é uma
/// REVOGAÇÃO, e quem a aplica é <c>ConsentimentoService</c>.
/// </param>
public sealed record DecisaoDeConsentimento(FinalidadeConsentimento Finalidade, bool Concedido);

/// <summary>
/// O TERMO LGPD ASSINADO PELO PACIENTE — a ponte entre o documento e o consentimento
/// (parcela 89).
///
/// Por que ela existe
/// ------------------
/// Até aqui o consentimento era uma CAIXINHA que o balcão marcava, e o termo impresso era
/// só o recibo do que já estava no sistema. A direção decidiu inverter: **o paciente
/// responde as quatro finalidades no termo e ASSINA, e é essa resposta que vale**.
///
/// ⚠️ A inversão não é preferência de leiaute, é a única forma de os dois não divergirem.
/// Com o termo sendo recibo, um paciente podia responder "Não" ao marketing no celular e o
/// sistema continuar mandando campanha, porque a caixinha do balcão seguia marcada — duas
/// verdades sobre o mesmo fato, o defeito recorrente deste projeto na variante que a LGPD
/// pune.
///
/// Por que o vínculo é por CÓDIGO, e não pela ordem nem pelo rótulo
/// ---------------------------------------------------------------
/// <see cref="ItemDocumento.Codigo"/> guarda o nome da finalidade. Casar por <c>Ordem</c>
/// seria o contrato de ÍNDICE que a parcela 41 trocou por NOME: acrescentar uma finalidade
/// no meio empurraria todas as outras e o "Sim" do uso de imagem viraria autorização para
/// compartilhar com o convênio — sem quebrar build nenhum. E casar pelo RÓTULO amarraria a
/// decisão a um texto que a clínica pode reescrever.
///
/// O código é COPIADO na emissão, como todo o resto do documento: o termo assinado no mês
/// passado continua se lendo mesmo que uma finalidade seja renomeada hoje.
/// </summary>
public static class TermoConsentimento
{
    /// <summary>
    /// As decisões que este documento carrega. Vazio quando ele não é um termo LGPD, ou
    /// quando o paciente ainda não respondeu.
    ///
    /// ⚠️ Item sem <see cref="ItemDocumento.Codigo"/> reconhecível é IGNORADO, nunca
    /// adivinhado. Ele existe em dois casos legítimos — o termo emitido antes desta
    /// parcela, e uma finalidade que a versão nova do sistema conhece e esta não —, e nos
    /// dois a resposta certa é não gravar consentimento nenhum. Deduzir pela posição
    /// gravaria a autorização errada, que é pior do que não gravar.
    /// </summary>
    public static IReadOnlyList<DecisaoDeConsentimento> Decisoes(DocumentoClinico documento)
    {
        ArgumentNullException.ThrowIfNull(documento);

        if (documento.Tipo != TipoDocumentoClinico.Consentimento) return [];

        var decisoes = new List<DecisaoDeConsentimento>();

        foreach (var item in documento.Itens.OrderBy(i => i.Ordem))
        {
            if (!Finalidade(item, out var finalidade)) continue;

            // Só resposta EXPLÍCITA vira decisão. Em branco não é "não" — é uma pergunta
            // que ninguém fez, e o `ColherAsync` já recusa assinar assim.
            if (RespostaDeclaracao.EhPositiva(item.Quantidade)) decisoes.Add(new(finalidade, true));
            else if (RespostaDeclaracao.EhNegativa(item.Quantidade)) decisoes.Add(new(finalidade, false));
        }

        return decisoes;
    }

    /// <summary>
    /// Este termo carrega as finalidades que o paciente RESPONDE — ou seja, assiná-lo
    /// produz consentimento.
    ///
    /// ⚠️ Existe por causa do que a clínica encontrou na 1ª rodada desta parcela. Até aqui
    /// o termo de consentimento era o RECIBO da caixinha do balcão: os itens levavam o
    /// rótulo da finalidade e a situação dela ("Nunca perguntado"), e <b>nenhum código</b>.
    /// Ao ligar o portão, esses papéis antigos passaram a satisfazer
    /// <see cref="DocumentoClinico.AguardaAssinaturaDoPaciente"/> — e a coleta reaproveitou
    /// um deles. O paciente assinou as quatro declarações com "Sim", o documento ficou
    /// selado e completo, e <b>nenhum consentimento foi gravado</b>, porque
    /// <see cref="Decisoes"/> não tinha por onde ler a resposta.
    ///
    /// É a garantia aparente que este projeto recusa desde a parcela 3, na forma mais
    /// discreta: nada falha, o papel sai perfeito, e o alerta "sem consentimento LGPD"
    /// continua aceso do outro lado da tela.
    ///
    /// ⚠️ Quem chama precisa ter os <see cref="DocumentoClinico.Itens"/> CARREGADOS. Sem o
    /// <c>Include</c>, a navegação chega vazia em produção e todo termo — inclusive o novo
    /// — responderia "não é respondível", enquanto o teste passaria pelo fixup do EF.
    /// </summary>
    public static bool Respondivel(DocumentoClinico documento)
    {
        ArgumentNullException.ThrowIfNull(documento);

        return documento.Tipo == TipoDocumentoClinico.Consentimento
               && documento.Itens.Any(i => Finalidade(i, out _));
    }

    /// <summary>
    /// A finalidade que a linha declara.
    ///
    /// ⚠️ A conferência é de IDA E VOLTA contra o NOME, e não um <c>Enum.TryParse</c> seco:
    /// ele aceita NÚMERO, e <c>"1"</c> vira uma finalidade de verdade — <c>Enum.IsDefined</c>
    /// não salva, porque 1 É um valor definido. Um código numérico vindo de qualquer lugar
    /// gravaria a autorização de uma finalidade que ninguém escreveu, que é a adivinhação
    /// que esta classe existe para recusar.
    /// </summary>
    private static bool Finalidade(ItemDocumento item, out FinalidadeConsentimento finalidade)
    {
        finalidade = default;

        var codigo = item.Codigo?.Trim();
        if (string.IsNullOrEmpty(codigo)) return false;

        return Enum.TryParse(codigo, ignoreCase: true, out finalidade)
               && finalidade.ToString().Equals(codigo, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>O texto que a declaração de cada finalidade leva no termo.</summary>
    public static string Declarar(FinalidadeConsentimento finalidade) => finalidade switch
    {
        FinalidadeConsentimento.TratamentoDeDados =>
            "Autorizo a clínica a tratar meus dados pessoais e de saúde para me atender.",
        FinalidadeConsentimento.CompartilhamentoComConvenio =>
            "Autorizo o compartilhamento dos meus dados com o meu convênio, para faturamento.",
        FinalidadeConsentimento.UsoDeImagem =>
            "Autorizo o uso da minha imagem (foto do cadastro e fotos clínicas).",
        FinalidadeConsentimento.ComunicacaoEMarketing =>
            "Autorizo receber mensagens de confirmação, retorno e campanhas da clínica.",
        _ => finalidade.ToString()
    };

    /// <summary>
    /// O que cada declaração explica ao paciente ANTES de ele responder.
    ///
    /// ⚠️ A do tratamento de dados diz o que acontece ao responder "Não", e é a única em
    /// que isso importa: sem ela o paciente não é atendido, e escondê-lo faria a recusa
    /// parecer uma escolha sem custo. As outras três são recusáveis sem consequência para
    /// o atendimento — e o texto diz isso, porque consentimento arrancado por medo de
    /// perder a consulta não é consentimento (art. 8º, §4º).
    /// </summary>
    public static string Detalhar(FinalidadeConsentimento finalidade) => finalidade switch
    {
        FinalidadeConsentimento.TratamentoDeDados =>
            "Sem esta autorização a clínica não consegue atendê-lo — ela é a base do "
            + "prontuário e do agendamento.",
        FinalidadeConsentimento.CompartilhamentoComConvenio =>
            "Responder “Não” não impede o atendimento: significa que a sessão será "
            + "particular, porque sem enviar os dados não há como faturar pelo convênio.",
        FinalidadeConsentimento.UsoDeImagem =>
            "Responder “Não” não muda nada no seu atendimento.",
        FinalidadeConsentimento.ComunicacaoEMarketing =>
            "Responder “Não” não muda nada no seu atendimento — você deixa apenas de "
            + "receber as mensagens.",
        _ => string.Empty
    };
}
