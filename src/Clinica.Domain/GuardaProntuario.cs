namespace Clinica.Domain;

/// <summary>
/// O PRAZO DE GUARDA do prontuário — Lei 13.787/2018, art. 6º (parcela 52).
///
/// Por que isto é uma classe, e não um número solto na tela
/// -------------------------------------------------------
/// A cliente perguntou, em auditoria de fornecedor, "como você garante a retenção
/// durante 20 anos?". A resposta honesta até esta parcela era "não garanto": o sistema
/// não conhecia o prazo, não o vigiava, e — pior — tinha quatro caminhos que APAGAVAM
/// registro clínico de verdade (evolução, anexo, avaliação e medida), com
/// <c>Remove()</c> no banco.
///
/// Guarda não se cumpre com intenção. Ela precisa de três coisas, e as três moram aqui
/// ou saem daqui:
///
/// 1. <b>Um prazo que o sistema saiba calcular</b> — <see cref="VenceEm"/>.
/// 2. <b>A impossibilidade de apagar antes dele</b> — é o que o
///    <c>ProntuarioService</c> passou a cobrar, trocando exclusão por CANCELAMENTO
///    com motivo (o padrão que o resto do projeto já usava no documento clínico, na
///    não conformidade e na checagem de enfermagem).
/// 3. <b>Uma tela que responda a pergunta</b> — a clínica precisa conseguir mostrar a
///    resposta a quem a audita, e não confiar na palavra do fornecedor.
///
/// A contagem é do ÚLTIMO registro, nunca do primeiro
/// -------------------------------------------------
/// O art. 6º fala em "prazo mínimo de 20 anos a partir do último registro". Contar do
/// cadastro do paciente daria um prazo que vence enquanto ele ainda se trata: quem
/// começou em 2010 e continua vindo teria o prontuário elegível a eliminação em 2030,
/// com sessões de 2029 dentro dele. É o tipo de erro que só aparece no dia da
/// eliminação, quando não tem volta.
///
/// "Último registro" é o mais recente de QUALQUER coisa que o prontuário guarda —
/// sessão, avaliação, medida, documento emitido, prescrição. Quem monta essa data é o
/// <c>GuardaProntuarioService</c>, que enxerga todas as tabelas; aqui fica só a regra.
///
/// O que esta classe NÃO faz
/// -------------------------
/// <b>Ela não elimina nada, e o sistema também não.</b> Vencido o prazo, o prontuário
/// fica ELEGÍVEL — a decisão de eliminar é da clínica, com o CRM e a comissão de
/// revisão que a própria lei prevê (art. 7º), e é irreversível. Um sistema que
/// eliminasse sozinho ao bater o prazo seria a pior leitura possível desta lei: o prazo
/// é um PISO de guarda, não um teto nem um agendamento de destruição.
/// </summary>
public static class GuardaProntuario
{
    /// <summary>
    /// Anos de guarda a partir do último registro (Lei 13.787/2018, art. 6º).
    ///
    /// É <c>const</c> e não configuração de propósito: é prazo LEGAL, não política da
    /// clínica. Deixá-lo editável em Configurações permitiria baixá-lo para 5 anos numa
    /// tela, sem ninguém perceber, e o sistema passaria a chamar de "elegível" o que a
    /// lei ainda protege. Clínica que precise guardar MAIS pode guardar — nada aqui
    /// manda apagar.
    /// </summary>
    public const int AnosDeGuarda = 20;

    /// <summary>
    /// Quando a guarda obrigatória termina, dado o último registro do prontuário.
    /// </summary>
    public static DateOnly VenceEm(DateOnly ultimoRegistro)
        => ultimoRegistro.AddYears(AnosDeGuarda);

    /// <summary>
    /// O prontuário já cumpriu o prazo mínimo e é ELEGÍVEL a eliminação — o que não é
    /// ordem de eliminar, e sim ausência do impedimento legal (ver o resumo da classe).
    /// </summary>
    public static bool CumpriuPrazo(DateOnly ultimoRegistro, DateOnly hoje)
        => hoje >= VenceEm(ultimoRegistro);

    /// <summary>
    /// Quantos dias ainda faltam para vencer a guarda. Zero quando já venceu.
    ///
    /// Devolve DIAS, e a tela converte em anos: um prontuário que vence em 19 anos e 11
    /// meses arredondado para "20 anos" e outro que vence amanhã arredondado para "0
    /// anos" são leituras muito diferentes do mesmo campo, e a conversão é decisão de
    /// quem apresenta.
    /// </summary>
    public static int DiasRestantes(DateOnly ultimoRegistro, DateOnly hoje)
    {
        var vence = VenceEm(ultimoRegistro);
        return hoje >= vence ? 0 : vence.DayNumber - hoje.DayNumber;
    }

    /// <summary>
    /// Frase pronta sobre a situação da guarda, para a tela e para o documento de
    /// conformidade não escreverem duas versões da mesma conta.
    /// </summary>
    public static string Descrever(DateOnly ultimoRegistro, DateOnly hoje)
    {
        var vence = VenceEm(ultimoRegistro);

        if (CumpriuPrazo(ultimoRegistro, hoje))
            return $"Guarda cumprida em {vence:dd/MM/yyyy} "
                 + $"({AnosDeGuarda} anos do último registro, {ultimoRegistro:dd/MM/yyyy}). "
                 + "Elegível a eliminação por decisão da clínica.";

        var anos = DiasRestantes(ultimoRegistro, hoje) / 365;

        return $"Em guarda até {vence:dd/MM/yyyy} "
             + $"({AnosDeGuarda} anos do último registro, {ultimoRegistro:dd/MM/yyyy})"
             + (anos > 0 ? $" — faltam cerca de {anos} ano(s)." : " — vence este ano.");
    }
}
