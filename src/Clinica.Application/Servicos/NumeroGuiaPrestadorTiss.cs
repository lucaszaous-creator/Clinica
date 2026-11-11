using System.Globalization;

namespace Clinica.Application.Servicos;

/// <summary>
/// O <c>numeroGuiaPrestador</c> das guias enviadas pelo web service TISS: um número ÚNICO por
/// guia do sistema, que não repete entre lotes e que deixa achar a guia de volta na resposta da
/// operadora. No padrão 4.01.00 o campo é obrigatório e tem de 1 a 20 caracteres
/// (<c>st_texto20</c>); quem o gera, o padrão não diz.
/// <para>
/// A regra: prefixo + Id do código de faturamento — "C1234". O Id é a chave da guia no banco,
/// então nunca se repete e a volta é direta. Reenviar a MESMA guia depois de um cancelamento
/// pede número novo (a operadora pode recusar número já apresentado): aí entra o sufixo de
/// reenvio, "C1234R2".
/// </para>
/// <para>
/// ⚠️ Não está provado que a operadora aceita letras no número (no portal é ela quem numera).
/// Se ela exigir só dígitos, o que muda é esta classe, e os testes dizem o que quebra.
/// </para>
/// </summary>
public static class NumeroGuiaPrestadorTiss
{
    /// <summary>O prefixo das guias geradas pelo sistema: "C", de código de faturamento.</summary>
    public const string Prefixo = "C";

    /// <summary>O tamanho máximo do campo no XSD (<c>st_texto20</c>).</summary>
    public const int TamanhoMaximo = 20;

    private const char MarcaDeReenvio = 'R';

    /// <summary>
    /// O número da guia para um código de faturamento. <paramref name="reenvio"/> é 1 no primeiro
    /// envio (sem sufixo) e cresce a cada reenvio da mesma guia: 2 dá "C1234R2".
    /// </summary>
    public static string Gerar(int codigoId, int reenvio = 1)
    {
        if (codigoId <= 0) throw new ArgumentException("A guia só ganha número depois de gravada: o Id do código tem de ser positivo.");
        if (reenvio < 1) throw new ArgumentException("O envio começa em 1.");

        var numero = Prefixo + codigoId.ToString(CultureInfo.InvariantCulture)
                     + (reenvio == 1 ? string.Empty : MarcaDeReenvio + reenvio.ToString(CultureInfo.InvariantCulture));
        // Com Id e reenvio inteiros o teto real é 22 caracteres; passar de 20 pede bilhões de
        // reenvios, mas o campo é da operadora e a conta fica explícita.
        if (numero.Length > TamanhoMaximo)
            throw new ArgumentException($"O número da guia passaria de {TamanhoMaximo} caracteres.");
        return numero;
    }

    /// <summary>
    /// A volta: de "C1234" ou "C1234R2" para o Id do código de faturamento (e o envio). Devolve
    /// <c>false</c> para qualquer número que não tenha saído de <see cref="Gerar"/> — guia digitada
    /// no portal, número da operadora, texto vazio —, sem lançar.
    /// </summary>
    public static bool TentarLer(string? numeroGuiaPrestador, out int codigoId, out int reenvio)
    {
        codigoId = 0;
        reenvio = 0;
        var texto = numeroGuiaPrestador?.Trim();
        if (string.IsNullOrEmpty(texto) || texto.Length > TamanhoMaximo || !texto.StartsWith(Prefixo, StringComparison.Ordinal))
            return false;

        var resto = texto[Prefixo.Length..];
        var marca = resto.IndexOf(MarcaDeReenvio);
        var parteId = marca < 0 ? resto : resto[..marca];
        var parteReenvio = marca < 0 ? null : resto[(marca + 1)..];

        if (!LerInteiro(parteId, out var id) || id <= 0) return false;
        var envio = 1;
        // "R1" não sai de Gerar (o primeiro envio não leva sufixo): aceitar seria ter dois textos
        // para a mesma guia.
        if (parteReenvio is not null && (!LerInteiro(parteReenvio, out envio) || envio < 2)) return false;

        codigoId = id;
        reenvio = envio;
        return true;
    }

    /// <summary>Só dígitos, sem sinal, sem espaço e sem zero à esquerda — exatamente o que <see cref="Gerar"/> escreve.</summary>
    private static bool LerInteiro(string texto, out int valor)
    {
        valor = 0;
        return texto.Length > 0 && texto.All(char.IsAsciiDigit) && texto[0] != '0'
               && int.TryParse(texto, NumberStyles.None, CultureInfo.InvariantCulture, out valor);
    }
}
