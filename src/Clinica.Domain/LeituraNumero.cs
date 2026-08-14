using System.Globalization;

namespace Clinica.Domain;

/// <summary>
/// Lê o número que a clínica DIGITA, sem depender da cultura da máquina.
///
/// ⚠️ <b>Mora no DOMÍNIO, e não no shell, porque é regra que precisa de teste.</b> O leitor
/// anterior (<c>Clinica.Desktop.Controls.Valores</c>) vive num projeto <c>net8.0-windows</c>,
/// que a suíte de testes não alcança — e por isso ninguém percebeu, por muitas parcelas, que
/// ele lia <c>"40.5"</c> como <b>405</b>. É a mesma razão que injetou o relógio na checagem
/// de enfermagem na parcela 42: regra que decide dinheiro e não dá para testar apodrece sem
/// ninguém notar.
///
/// <b>O defeito que isto corrige.</b> A leitura antiga tentava pt-BR e, <i>se falhasse</i>,
/// a cultura invariante. O problema é que ela nunca falha: em pt-BR o ponto é separador de
/// MILHAR, então <c>"50.00"</c> é lido como <b>5.000</b> e <c>"3.9"</c> como <b>39</b> —
/// com sucesso, sem exceção, sem aviso. O caminho invariante era inalcançável, e a
/// documentação do método prometia justamente o que ele não fazia ("aceita 1250.00").
/// O estrago não aparece na tela de quem digitou: aparece no repasse do mês, no fechamento
/// da gaveta e no valor da guia conciliada.
///
/// <b>A regra.</b> Quem decide qual separador é o decimal é a POSIÇÃO, não a cultura:
/// <list type="bullet">
/// <item>tem ponto E vírgula → o <b>último</b> é o decimal ("1.250,00" e "1,250.00");</item>
/// <item>só vírgula, uma só → decimal ("40,5"); mais de uma → milhar ("1,234,567");</item>
/// <item>só ponto, mais de um → milhar ("1.234.567");</item>
/// <item>só ponto, um só → decimal, <b>salvo</b> quando tem exatamente 3 dígitos depois e a
///       parte inteira não é "0" — aí é milhar ("1.250" é mil duzentos e cinquenta, e
///       "0.500" continua sendo meio).</item>
/// </list>
/// A exceção dos 3 dígitos é o único ponto de julgamento, e ela existe porque "1.250" é
/// como o Brasil escreve mil duzentos e cinquenta; ninguém digita três casas decimais num
/// campo de dinheiro. Quem quer 1,25 escreve "1,25" ou "1.25".
/// </summary>
public static class LeituraNumero
{
    /// <summary>
    /// Lê o número digitado. Devolve <c>false</c> para texto vazio ou que não é número —
    /// e NÃO opina sobre sinal nem sobre piso: quem decide se zero vale, ou se o negativo
    /// entra, é quem chama, porque a resposta muda por campo (zero é engano no percentual
    /// do repasse e é a cortesia na venda do pacote).
    /// </summary>
    public static bool TentarLer(string? texto, out decimal valor)
    {
        valor = 0m;
        if (string.IsNullOrWhiteSpace(texto)) return false;

        // "R$", espaço fino e espaço inquebrável: o Windows em pt-BR formata moeda com
        // NBSP, e o texto colado de outra tela volta com ele.
        var limpo = texto
            .Replace("R$", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(" ", string.Empty)
            .Replace(" ", string.Empty)
            .Replace(" ", string.Empty)
            .Trim();

        if (limpo.Length == 0) return false;

        var negativo = limpo.StartsWith('-');
        if (negativo || limpo.StartsWith('+')) limpo = limpo[1..];
        if (limpo.Length == 0) return false;

        // Só dígitos e os dois separadores. Recusar aqui é o que faz "6x" e "12/03" serem
        // devolvidos como não-número em vez de virarem 6 e 12 por leitura parcial.
        foreach (var c in limpo)
            if (!char.IsAsciiDigit(c) && c != '.' && c != ',') return false;

        var pontos = limpo.Count(c => c == '.');
        var virgulas = limpo.Count(c => c == ',');

        char? decimalSep;
        char? milharSep;
        if (pontos > 0 && virgulas > 0)
            (decimalSep, milharSep) = limpo.LastIndexOf('.') > limpo.LastIndexOf(',')
                ? ('.', (char?)',') : (',', '.');
        else if (virgulas == 1)
            (decimalSep, milharSep) = (',', null);
        else if (virgulas > 1)
            (decimalSep, milharSep) = (null, ',');   // "1,234,567" — todos milhar
        else if (pontos == 1)
            (decimalSep, milharSep) = PontoUnicoEhMilhar(limpo)
                ? ((char?)null, (char?)'.') : ((char?)'.', (char?)null);
        else if (pontos > 1)
            (decimalSep, milharSep) = (null, '.');   // "1.234.567" — todos milhar
        else
            (decimalSep, milharSep) = (null, null);  // inteiro puro

        var corte = decimalSep is { } sep ? limpo.LastIndexOf(sep) : limpo.Length;
        var inteiro = limpo[..corte];
        var fracao = decimalSep is not null ? limpo[(corte + 1)..] : string.Empty;

        // A parte da FRAÇÃO é só dígito, e a INTEIRA só pode trazer o separador de milhar,
        // em grupos de três. Sem esta conferência, "1.2,3.4" seria lido como 123,4: o
        // último separador viraria o decimal e o resto seria varrido para fora em silêncio.
        // Leitura parcial de texto que não é número é pior do que recusa — ela grava.
        if (fracao.Any(c => !char.IsAsciiDigit(c))) return false;
        if (!GruposValidos(inteiro, milharSep)) return false;

        inteiro = new string(inteiro.Where(char.IsAsciiDigit).ToArray());

        var normalizado = fracao.Length > 0 ? $"{inteiro}.{fracao}" : inteiro;
        if (normalizado.Length == 0 || normalizado == ".") return false;

        if (!decimal.TryParse(normalizado, NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out valor))
            return false;

        if (negativo) valor = -valor;
        return true;
    }

    /// <summary>
    /// A parte inteira só pode trazer o separador de MILHAR, e em grupos de três:
    /// "1.234.567" vale, "1.23.456" não. Quem não tem separador nenhum passa direto —
    /// "1250" é o jeito mais comum de digitar.
    /// </summary>
    private static bool GruposValidos(string inteiro, char? milharSep)
    {
        if (inteiro.Length == 0) return true;                     // ",50" — fração pura

        if (milharSep is not { } sep)
            return inteiro.All(char.IsAsciiDigit);

        if (inteiro.Any(c => !char.IsAsciiDigit(c) && c != sep)) return false;

        var grupos = inteiro.Split(sep);
        if (grupos.Length == 1) return grupos[0].All(char.IsAsciiDigit);

        if (grupos[0].Length is < 1 or > 3) return false;
        return grupos.Skip(1).All(g => g.Length == 3);
    }

    /// <summary>
    /// "1.250" é mil duzentos e cinquenta; "0.500" é meio; "50.00" é cinquenta.
    /// A conta é a mesma que um brasileiro faz de olho: ponto com exatamente três dígitos
    /// atrás dele separa milhar — a não ser que a parte inteira seja só "0", que é como se
    /// escreve fração no formato invariante.
    /// </summary>
    private static bool PontoUnicoEhMilhar(string texto)
    {
        var i = texto.IndexOf('.');
        var antes = texto[..i];
        var depois = texto[(i + 1)..];

        if (depois.Length != 3) return false;
        if (antes.Length == 0) return false;                 // ".500"
        return antes.TrimStart('0').Length > 0;              // "0.500" continua decimal
    }
}
