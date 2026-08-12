using System.Globalization;
using System.Text.RegularExpressions;

namespace Clinica.Domain;

/// <summary>Uma linha do extrato do banco, como o OFX a entrega.</summary>
/// <param name="Id">
/// O <c>FITID</c> — identificador que o BANCO dá à transação. É a chave de idempotência
/// da conciliação: importar o mesmo extrato duas vezes não pode conciliar nada duas vezes.
/// </param>
/// <param name="Data">Quando o dinheiro se moveu na conta (<c>DTPOSTED</c>).</param>
/// <param name="Valor">Positivo = entrou; negativo = saiu. É o sinal do próprio OFX.</param>
/// <param name="Descricao">O <c>MEMO</c> — "PIX RECEBIDO MARIA", "PAGTO ALUGUEL".</param>
public sealed record LinhaExtrato(string Id, DateOnly Data, decimal Valor, string Descricao)
{
    public bool Entrada => Valor > 0;

    /// <summary>Quanto se moveu, sem sinal — é o que casa com o valor do lançamento.</summary>
    public decimal ValorAbsoluto => Math.Abs(Valor);
}

/// <summary>O extrato inteiro: as linhas e o período que o arquivo cobre.</summary>
public sealed record Extrato(
    IReadOnlyList<LinhaExtrato> Linhas, DateOnly? Inicio, DateOnly? Fim, string? Conta);

/// <summary>
/// LEITOR DE OFX (parcela 63) — o extrato que o banco exporta.
///
/// Até aqui o extrato era conferido A OLHO contra a tela de recebíveis: a pessoa abria o
/// internet banking de um lado, o sistema do outro, e comparava linha por linha. É o
/// último lugar do financeiro em que a conferência do mês depende de alguém não se
/// distrair.
///
/// <b>Por que um leitor à mão e não uma biblioteca:</b> os cinco apps se auto-atualizam
/// por Velopack, e dependência nova é risco desproporcional — a mesma decisão que fez os
/// gráficos serem desenhados com os tokens. O OFX 1.x é SGML simples, e o que a
/// conciliação precisa dele são cinco campos.
///
/// ⚠️ <b>O OFX 1.x tem tags que NÃO FECHAM</b> (<c>&lt;TRNAMT&gt;-150.00</c> e ponto), e é
/// por isso que ler isto com um parser de XML não funciona: o arquivo não é XML bem
/// formado. O valor de um campo vai da tag até a próxima tag ou até o fim da linha.
/// O OFX 2.x é XML de verdade e passa pelo mesmo caminho, porque as tags fechadas também
/// terminam numa tag ("&lt;/TRNAMT&gt;").
/// </summary>
public static class LeitorOfx
{
    private static readonly Regex Transacao = new(
        @"<STMTTRN>(?<corpo>.*?)</STMTTRN>",
        RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Um campo do OFX: da tag até a próxima "&lt;" ou o fim da linha. Cobre as duas
    /// versões do formato sem precisar saber qual está lendo.
    /// </summary>
    private static string? Campo(string corpo, string tag)
    {
        var m = Regex.Match(
            corpo, $@"<{tag}>(?<v>[^<\r\n]*)", RegexOptions.IgnoreCase);

        var v = m.Success ? m.Groups["v"].Value.Trim() : null;
        return string.IsNullOrWhiteSpace(v) ? null : v;
    }

    /// <summary>
    /// Lê o extrato. Linha sem data ou sem valor é IGNORADA em silêncio — o arquivo tem
    /// blocos de cabeçalho e de saldo que não são transação, e tratá-los como erro faria
    /// um extrato perfeitamente válido ser recusado.
    ///
    /// O que NÃO é ignorado é o arquivo inteiro sem uma transação: quem chamou precisa
    /// distinguir "extrato vazio" de "escolhi o arquivo errado", e é a contagem zero que
    /// permite a tela dizer isso.
    /// </summary>
    public static Extrato Ler(string conteudo)
    {
        if (string.IsNullOrWhiteSpace(conteudo))
            return new Extrato([], null, null, null);

        var linhas = new List<LinhaExtrato>();

        foreach (Match m in Transacao.Matches(conteudo))
        {
            var corpo = m.Groups["corpo"].Value;

            if (LerData(Campo(corpo, "DTPOSTED")) is not { } data) continue;
            if (LerValor(Campo(corpo, "TRNAMT")) is not { } valor) continue;

            // Sem FITID o banco não deu identificador — cai para um sintético com os
            // dados da própria linha. Não é tão bom (dois PIX iguais no mesmo dia
            // colidem), mas é melhor do que descartar a transação por falta de um campo
            // que alguns bancos simplesmente não mandam.
            var id = Campo(corpo, "FITID")
                     ?? $"{data:yyyyMMdd}|{valor.ToString(CultureInfo.InvariantCulture)}|{linhas.Count}";

            var descricao = Campo(corpo, "MEMO")
                            ?? Campo(corpo, "NAME")
                            ?? Campo(corpo, "TRNTYPE")
                            ?? "—";

            linhas.Add(new LinhaExtrato(id, data, valor, descricao));
        }

        return new Extrato(
            linhas,
            LerData(Campo(conteudo, "DTSTART")) ?? linhas.MinBy(l => l.Data)?.Data,
            LerData(Campo(conteudo, "DTEND")) ?? linhas.MaxBy(l => l.Data)?.Data,
            Campo(conteudo, "ACCTID"));
    }

    /// <summary>
    /// A data do OFX é <c>AAAAMMDD</c> com hora e fuso opcionais
    /// (<c>20260805120000[-3:BRT]</c>). Só os oito primeiros dígitos importam: o que a
    /// conciliação casa é o DIA em que o dinheiro se moveu, e o fuso do arquivo do banco
    /// não muda isso.
    /// </summary>
    private static DateOnly? LerData(string? texto)
    {
        if (texto is null || texto.Length < 8) return null;

        return DateOnly.TryParseExact(
            texto[..8], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d
            : null;
    }

    /// <summary>
    /// O valor do OFX é SEMPRE com ponto decimal (o formato é internacional), e o sinal
    /// diz a direção.
    ///
    /// ⚠️ Lido com <see cref="CultureInfo.InvariantCulture"/> de propósito: numa máquina
    /// em pt-BR, "-150.00" lido na cultura local viraria <b>-15000</b>, e a conciliação
    /// apontaria uma diferença de cem vezes sem ninguém entender por quê. É a mesma
    /// armadilha que o <c>ParametrosService</c> já evita ao ler número que precisa voltar
    /// a ser número.
    /// </summary>
    private static decimal? LerValor(string? texto)
        => texto is not null
           && decimal.TryParse(
               texto, NumberStyles.Number | NumberStyles.AllowLeadingSign,
               CultureInfo.InvariantCulture, out var v)
            ? v
            : null;
}
