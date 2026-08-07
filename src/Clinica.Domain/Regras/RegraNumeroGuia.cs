namespace Clinica.Domain.Regras;

/// <summary>
/// O número da guia confere com o que o convênio emite? (parcela 45)
///
/// Por que isto é uma regra de DOMÍNIO e não uma máscara de tela
/// -------------------------------------------------------------
/// A baixa acontece em quatro portas — a tela de baixa do faturamento, a baixa em lote do
/// painel, a rodada de pendências e a fila do Gerente — e a validação vale nas quatro. Uma
/// máscara no <c>TextBox</c> cobre a primeira e deixa as outras três passarem, que é o
/// mesmo defeito recorrente do projeto (capacidade que existe numa porta só) vestido de
/// validação. Por isso quem recusa é <c>FaturamentoService.DarBaixaAsync</c>, no caminho
/// único por onde toda baixa passa; a tela usa a MESMA regra para avisar antes do clique,
/// que é a segunda barreira do projeto — a de cima explica, a de baixo impede.
///
/// O que a regra NÃO faz: conferir tamanho, dígito verificador ou prefixo. Cada operadora
/// tem o seu, eles mudam sem aviso, e uma regra apertada demais recusaria guia legítima —
/// que é pior do que aceitar uma errada, porque trava o faturamento do dia. O que se pega
/// aqui é o erro de digitação que o olho não vê: a letra "O" no lugar do zero.
/// </summary>
public static class RegraNumeroGuia
{
    /// <summary>
    /// Formato que um convênio EMBUTIDO usa, e que a migration semeia no catálogo.
    ///
    /// Serve de semente, não de verdade permanente: a partir do primeiro salvamento quem
    /// manda é o que está no cadastro do convênio (<c>ConvenioCadastro.FormatoNumeroGuia</c>),
    /// porque a operadora pode mudar a numeração sem pedir licença ao nosso release.
    /// </summary>
    public static FormatoNumeroGuia PadraoDaFamilia(Convenio familia) => familia switch
    {
        // A Unimed numera a guia só com dígitos.
        Convenio.UnimedPadrao => FormatoNumeroGuia.SomenteNumeros,
        Convenio.UnimedIntercambio => FormatoNumeroGuia.SomenteNumeros,

        // Petrobras e Amil misturam letra e número.
        Convenio.Petrobras => FormatoNumeroGuia.Alfanumerico,
        Convenio.Amil => FormatoNumeroGuia.Alfanumerico,

        // Personalizado é o convênio que a clínica cadastrou (Sul América entrou por
        // aqui). Não se chuta formato para operadora que o sistema não conhece: a clínica
        // marca em Configurações, e até lá aceita o que vier.
        _ => FormatoNumeroGuia.SemValidacao
    };

    /// <summary>
    /// Critica o número digitado. Devolve a frase a mostrar, ou <c>null</c> quando passa.
    ///
    /// Número EM BRANCO passa em qualquer formato: "ainda não tenho o número" é uma
    /// resposta legítima na fila do Gerente, e é a tela de baixa do faturamento que exige
    /// o preenchimento. Quem valida forma não decide obrigatoriedade.
    /// </summary>
    public static string? Criticar(string? numero, FormatoNumeroGuia formato)
    {
        var valor = (numero ?? string.Empty).Trim();
        if (valor.Length == 0) return null;

        return formato switch
        {
            FormatoNumeroGuia.SomenteNumeros when !valor.All(char.IsDigit) =>
                "Este convênio numera a guia só com números. Confira se não trocou um zero "
                + "por \"O\" — o sistema da operadora recusa, e o erro só apareceria no retorno.",

            // Alfanumérico aceita letra e dígito, e recusa o resto: ponto, barra e espaço
            // são separadores que a pessoa copia do papel junto do número, e a operadora
            // não os reconhece. Esta arma cobre também o número que é só pontuação
            // ("----"), que passaria por qualquer checagem de "tem letra ou dígito".
            FormatoNumeroGuia.Alfanumerico when !valor.All(char.IsLetterOrDigit) =>
                "Este convênio usa letras e números na guia, sem pontuação. Retire espaços, "
                + "pontos e barras que tenham vindo junto.",

            _ => null
        };
    }

    /// <summary>
    /// Frase curta de ajuda ao lado do campo — o que este convênio espera, ANTES de o
    /// usuário errar. Descobrir o requisito errando é o que faz a pessoa desistir da tela.
    /// </summary>
    public static string? Dica(FormatoNumeroGuia formato) => formato switch
    {
        FormatoNumeroGuia.SomenteNumeros => "Este convênio aceita somente números.",
        FormatoNumeroGuia.Alfanumerico => "Este convênio aceita letras e números.",
        _ => null
    };
}
