using System.Globalization;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;

namespace Clinica.Application.Formatacao;

/// <summary>Em que estado a linha do extrato está na tela — decide o selo e as ações.</summary>
public enum EstadoLinhaExtrato
{
    /// <summary>Um lançamento bate. O sistema propõe; a pessoa confirma.</summary>
    Casada,

    /// <summary>O depósito é o lote completo da maquininha naquele dia.</summary>
    LoteDaMaquininha,

    /// <summary>Mais de um candidato — a pessoa escolhe qual.</summary>
    EscolhaQual,

    /// <summary>O dinheiro se moveu no banco e não há lançamento no sistema.</summary>
    SoNoBanco,

    /// <summary>Conciliada numa importação anterior: dá para DESFAZER, não para conciliar de novo.</summary>
    JaConferida,

    /// <summary>O OFX não trouxe o FITID: não há como conciliar sem o identificador do banco.</summary>
    SemIdentificador
}

/// <summary>
/// O que a tela "Extrato do banco" AFIRMA sobre cada linha e sobre o arquivo (out/2026,
/// migrado do <c>ExtratoBancoViewModel</c> do WPF para a tela Blazor).
///
/// Mora aqui, e não no Razor, porque são decisões e não desenho: qual frase descreve a
/// linha, se ela pode ser conferida, se pede escolha, o que vai junto no clique. As duas
/// telas (a WPF, enquanto existir, e a Blazor) não podem responder diferente.
///
/// A regra de ouro continua a do serviço: o sistema PROPÕE e a pessoa confirma. A proposta
/// só vem pré-escolhida quando não há dúvida (<see cref="Proposta"/>); na linha ambígua o
/// campo fica em branco, porque escolher "o primeiro" seria decidir por quem não sabe.
/// </summary>
public static class ExtratoDoBancoNaTela
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private static string Dinheiro(decimal v) => v.ToString("C2", PtBr);

    public static EstadoLinhaExtrato Estado(LinhaConciliada l)
    {
        if (!l.Extrato.IdentidadeBancariaInformada) return EstadoLinhaExtrato.SemIdentificador;
        return l.Situacao switch
        {
            SituacaoConciliacao.Casada => EstadoLinhaExtrato.Casada,
            SituacaoConciliacao.DepositoCartao => EstadoLinhaExtrato.LoteDaMaquininha,
            SituacaoConciliacao.Ambigua => EstadoLinhaExtrato.EscolhaQual,
            SituacaoConciliacao.JaConciliada => EstadoLinhaExtrato.JaConferida,
            _ => EstadoLinhaExtrato.SoNoBanco
        };
    }

    /// <summary>Rótulo curto do selo da linha.</summary>
    public static string Selo(EstadoLinhaExtrato e) => e switch
    {
        EstadoLinhaExtrato.Casada => "Casada",
        EstadoLinhaExtrato.LoteDaMaquininha => "Lote da maquininha",
        EstadoLinhaExtrato.EscolhaQual => "Escolha qual",
        EstadoLinhaExtrato.JaConferida => "Já conferida",
        EstadoLinhaExtrato.SemIdentificador => "Sem FITID",
        _ => "Só no banco"
    };

    /// <summary>
    /// O TOM do selo pela GRAVIDADE: verde é "pronto para confirmar", âmbar é "falta uma
    /// decisão sua", vermelho é "o extrato não tem como ser conciliado", neutro é "já feito".
    /// </summary>
    public static string Tom(EstadoLinhaExtrato e) => e switch
    {
        EstadoLinhaExtrato.Casada or EstadoLinhaExtrato.LoteDaMaquininha => "sucesso",
        EstadoLinhaExtrato.EscolhaQual or EstadoLinhaExtrato.SoNoBanco => "aviso",
        EstadoLinhaExtrato.SemIdentificador => "perigo",
        _ => "neutro"
    };

    /// <summary>A frase que explica a linha — a mesma do WPF, com o dinheiro em pt-BR fixo.</summary>
    public static string Situacao(LinhaConciliada l) => Estado(l) switch
    {
        EstadoLinhaExtrato.SemIdentificador =>
            "OFX sem identificador bancário (FITID). Solicite um extrato completo ao banco.",
        EstadoLinhaExtrato.Casada => "Casada — confirme",
        EstadoLinhaExtrato.LoteDaMaquininha =>
            $"Depósito de {l.Candidatos[0].Adquirente}: {l.Candidatos.Count} créditos, bruto "
            + $"{Dinheiro(l.Candidatos.Sum(c => c.Valor))}, taxas "
            + $"{Dinheiro(l.Candidatos.Sum(c => c.ValorTaxa ?? 0))} — confirme o lote",
        EstadoLinhaExtrato.EscolhaQual => $"{l.Candidatos.Count} candidatos — escolha qual",
        EstadoLinhaExtrato.JaConferida => "Já conferida",
        _ => "Não há lançamento correspondente no sistema"
    };

    /// <summary>
    /// A frase AO LADO do selo. Onde a <see cref="Situacao"/> só repetiria o selo ("Casada —
    /// confirme" ao lado de "Casada"), ela diz o que o selo não diz; no resto é a mesma.
    /// </summary>
    public static string Detalhe(LinhaConciliada l) => Estado(l) switch
    {
        EstadoLinhaExtrato.Casada => "o sistema propõe o lançamento abaixo — confira e confirme",
        EstadoLinhaExtrato.JaConferida => "já conciliada com o sistema — desfaça se o par estiver errado",
        _ => Situacao(l)
    };

    /// <summary>Há o que confirmar nesta linha.</summary>
    public static bool PodeConferir(LinhaConciliada l)
        => l.Situacao != SituacaoConciliacao.JaConciliada && l.Candidatos.Count > 0;

    /// <summary>Já conciliada antes: o botão é "Desfazer".</summary>
    public static bool PodeDesfazer(LinhaConciliada l)
        => l.Situacao == SituacaoConciliacao.JaConciliada && l.Candidatos.Count > 0;

    /// <summary>O seletor aparece só quando há mais de uma opção — senão é ruído.</summary>
    public static bool PedeEscolha(LinhaConciliada l)
        => l.Situacao != SituacaoConciliacao.JaConciliada
           && l.Situacao != SituacaoConciliacao.DepositoCartao
           && l.Candidatos.Count > 1;

    /// <summary>A proposta pré-escolhida: SÓ quando há exatamente um candidato.</summary>
    public static ItemConciliacao? Proposta(LinhaConciliada l) => l.Unico;

    /// <summary>
    /// O que vai junto no clique de "Conferir". O lote da maquininha vai inteiro; a linha
    /// comum vai com o escolhido. Null quando falta escolher — e a tela DIZ isso (a guarda
    /// com voz da parcela 41: sair calado faria o botão parecer quebrado).
    /// </summary>
    public static IReadOnlyCollection<ItemConciliacao>? ItensAConferir(
        LinhaConciliada l, ItemConciliacao? escolhido)
    {
        if (l.Situacao == SituacaoConciliacao.DepositoCartao) return l.Candidatos.ToArray();
        return escolhido is null ? null : [escolhido];
    }

    /// <summary>A frase do clique sem escolha.</summary>
    public const string FaltaEscolher =
        "Escolha primeiro com qual lançamento esta linha do extrato casa.";

    /// <summary>O resumo do arquivo, acima da lista.</summary>
    public static string Resumo(ResultadoConciliacao r)
        => r.Linhas.Count == 0
            ? "Este arquivo não tem nenhuma transação. Confira se é o extrato OFX que "
              + "o banco exporta — não o PDF nem o CSV."
            : $"{r.Linhas.Count} linha(s) no extrato · {r.Casadas} já casam · "
              + $"{r.Pendentes} pedem atenção · {r.SoNoSistema.Count} no sistema sem "
              + "correspondência no banco"
              + (r.Inicio is { } de && r.Fim is { } ate
                  ? $" · período de {de.ToString("dd/MM/yyyy", PtBr)} a {ate.ToString("dd/MM/yyyy", PtBr)}"
                  : string.Empty);

    /// <summary>
    /// Os bytes do arquivo como texto. O padrão é Latin-1, como no WPF: o OFX 1.x dos bancos
    /// brasileiros costuma vir em ISO-8859-1, e lê-lo como UTF-8 estraga os acentos do MEMO —
    /// justamente o texto pelo qual a pessoa reconhece a transação. Só vira UTF-8 quando o
    /// próprio arquivo DIZ que é (BOM, <c>CHARSET:UTF-8</c> do 1.x ou <c>encoding="UTF-8"</c>
    /// do 2.x) — e aí lê-lo como Latin-1 é que estragaria os acentos.
    /// </summary>
    public static string Decodificar(byte[] conteudo)
    {
        if (conteudo.Length >= 3 && conteudo[0] == 0xEF && conteudo[1] == 0xBB && conteudo[2] == 0xBF)
            return System.Text.Encoding.UTF8.GetString(conteudo, 3, conteudo.Length - 3);

        var cabecalho = System.Text.Encoding.Latin1.GetString(conteudo, 0, Math.Min(conteudo.Length, 600));
        var declaraUtf8 = System.Text.RegularExpressions.Regex.IsMatch(cabecalho,
            @"CHARSET\s*:\s*UTF-?8|ENCODING\s*:\s*UTF-?8|encoding\s*=\s*[""']utf-?8",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return declaraUtf8
            ? System.Text.Encoding.UTF8.GetString(conteudo)
            : System.Text.Encoding.Latin1.GetString(conteudo);
    }

    /// <summary>Como um candidato aparece no seletor: data, valor no banco e descrição.</summary>
    public static string Candidato(ItemConciliacao c)
        => $"{c.Data.ToString("dd/MM", PtBr)} · {Dinheiro(c.ValorBancario)} · {c.Descricao}";
}
