using System.Globalization;
using Clinica.Application.Formatacao;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Servicos;

/// <summary>Uma linha da tabela de preço, já escrita para a tela.</summary>
public sealed record LinhaDePrecoConvenio(
    int PrecoId,
    string Convenio,
    string Descricao,
    string Valor,
    string Vigencia,
    /// <summary>Vigente HOJE — é o valor que a conciliação está propondo agora.</summary>
    bool ValendoAgora,
    /// <summary>A situação em palavras: valendo agora, ainda não vale, vigência encerrada ou inativo.</summary>
    string Situacao);

/// <summary>
/// O que a tela TABELA DE PREÇO POR CONVÊNIO afirma (out/2026, etapa 6 do Gerente — a tela
/// saiu do WPF para o Blazor). Era o <c>PrecosConvenioViewModel</c> com a <c>LinhaPreco</c> e o
/// <c>PrecoEdicaoViewModel</c>: o filtro (convênio + "só o que vale hoje"), o resumo que DIZ
/// que está filtrado, a frase do vazio, a leitura do valor digitado e as frases de gravar e de
/// excluir. Quem lê e grava é a tela, com o MESMO <see cref="PrecoConvenioService"/>.
///
/// Quem negocia tabela com a operadora é a direção; quem concilia guia é o balcão. O
/// Financeiro lê a mesma tabela que esta tela escreve — nenhuma cópia.
/// </summary>
public static class PrecosConvenioNaTela
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public const string TodosConvenios = "Todos os convênios";

    public const string VazioSemTabela =
        "Nenhum preço cadastrado. Sem tabela, a conciliação continua pedindo o valor digitado — o sistema não inventa um valor de mercado.";

    public const string VazioComFiltro = "Nenhum preço bate com o filtro — limpe-o para ver a tabela inteira.";

    public const string Cadastrado = "Preço cadastrado — a conciliação já vai propor esse valor.";

    public const string Atualizado = "Preço atualizado. Vale para as guias novas; o que já foi lançado não muda.";

    public const string Excluido = "Preço excluído.";

    public static LinhaDePrecoConvenio LinhaDe(PrecoConvenio p, DateOnly hoje) => new(
        PrecoId: p.Id,
        // O NOME da operadora na tela, o código no cadastro: o nome é o que a direção
        // reconhece, o código é a identidade que a conciliação casa.
        Convenio: CatalogoConvenios.Nome(p.ConvenioCodigo),
        Descricao: Descricao(p.Tipo, p.Especialidade),
        // pt-BR fixo: o WPF escrevia na cultura da máquina ("R$145.00" num posto em inglês).
        Valor: p.Valor.ToString("C", PtBr),
        Vigencia: p.Vigencia,
        ValendoAgora: p.VigenteEm(hoje),
        Situacao: Situacao(p, hoje));

    /// <summary>Sem RotulosEnum a tela escreveria "ConsultaEspecialidade" e "ClinicaDaDor".</summary>
    public static string Descricao(TipoCodigo tipo, Especialidade? especialidade)
        => especialidade is { } e
            ? $"{RotulosEnum.De(tipo)} ({RotulosEnum.De(e)})"
            : RotulosEnum.De(tipo);

    /// <summary>
    /// "Valendo agora" é o que responde à pergunta real: o cadastro tem o histórico inteiro, e
    /// sem a marca não dá para saber qual linha a conciliação está propondo hoje. Fora de
    /// vigência, a tela diz POR QUÊ: o reajuste que começa mês que vem e o preço antigo já
    /// encerrado são respostas diferentes — o WPF escrevia "fora de vigência" para os dois, e
    /// também para o preço desligado.
    /// </summary>
    public static string Situacao(PrecoConvenio p, DateOnly hoje)
    {
        if (!p.Ativo) return "inativo";
        if (p.VigenteDe is { } de && hoje < de) return "ainda não vale";
        if (p.VigenteAte is { } ate && hoje > ate) return "vigência encerrada";
        return "valendo agora";
    }

    /// <summary>
    /// As operadoras COM preço cadastrado, em ordem — oferecer as sem linha daria filtro que só
    /// leva a vazio. A primeira opção é sempre <see cref="TodosConvenios"/>.
    /// </summary>
    public static IReadOnlyList<string> OpcoesDeConvenio(IEnumerable<LinhaDePrecoConvenio> todas)
        => [TodosConvenios, .. todas.Select(l => l.Convenio).Distinct()
            .OrderBy(n => n, StringComparer.Create(PtBr, ignoreCase: false))];

    /// <summary>Atualizar não pode desfazer o filtro de quem está trabalhando — a escolha fica, se ainda existir.</summary>
    public static string ManterEscolha(string? escolhido, IReadOnlyList<string> opcoes)
        => escolhido is not null && opcoes.Contains(escolhido) ? escolhido : TodosConvenios;

    public static bool FiltroAtivo(string? convenio, bool soValendoHoje)
        => (convenio ?? TodosConvenios) != TodosConvenios || soValendoHoje;

    /// <summary>O filtro em memória, sobre o que já foi lido (o padrão da tela de Consultas da Recepção).</summary>
    public static IReadOnlyList<LinhaDePrecoConvenio> Filtrar(
        IEnumerable<LinhaDePrecoConvenio> todas, string? convenio, bool soValendoHoje)
    {
        var c = convenio ?? TodosConvenios;
        return todas.Where(p => (c == TodosConvenios || p.Convenio == c)
                                && (!soValendoHoje || p.ValendoAgora)).ToList();
    }

    /// <summary>
    /// O resumo DIZ que está filtrado: "6 preços" e "6 de 30 no filtro" respondem perguntas
    /// diferentes. O "valendo hoje" continua sendo o do TOTAL.
    /// </summary>
    public static string Resumo(IReadOnlyCollection<LinhaDePrecoConvenio> todas, int noFiltro, bool filtroAtivo)
    {
        var vigentes = todas.Count(p => p.ValendoAgora);
        return todas.Count == 0
            ? "Nenhum preço cadastrado — a conciliação continua pedindo o valor digitado."
            : filtroAtivo
                ? $"{noFiltro} de {todas.Count} preço(s) no filtro · {vigentes} valendo hoje no total."
                : $"{noFiltro} preço(s) cadastrado(s) · {vigentes} valendo hoje.";
    }

    /// <summary>"Não há preço" e "nenhum bate com o filtro" são respostas diferentes.</summary>
    public static string Vazio(bool filtroAtivo) => filtroAtivo ? VazioComFiltro : VazioSemTabela;

    public static string Concluido(bool novo) => novo ? Cadastrado : Atualizado;

    /// <summary>A pergunta de excluir: excluir é para o que foi cadastrado ERRADO; o resto se ENCERRA pela vigência.</summary>
    public static string PerguntaDeExcluir(LinhaDePrecoConvenio linha)
        => $"Apagar o preço de {linha.Convenio} ({linha.Descricao})? Se ele já propôs "
           + "valor a alguma guia, prefira ENCERRAR pela vigência: os lançamentos guardam "
           + "o valor copiado, mas apagar a linha apaga a explicação de por que aquela "
           + "guia valeu aquilo.";

    // ---------------------------------------------------------------- formulário

    /// <summary>
    /// Os convênios oferecidos no formulário: os ATIVOS — e, na edição, também o do próprio
    /// preço quando ele foi desligado depois. O WPF oferecia só os ativos, e o preço de uma
    /// operadora desligada abria com o convênio em branco e recusava o Salvar ("Escolha o
    /// convênio"), sem jeito de corrigir nem de encerrar a vigência dele.
    /// </summary>
    public static IReadOnlyList<ConvenioCadastro> ConveniosDoFormulario(
        IEnumerable<ConvenioCadastro> catalogo, string? codigoAtual)
        => catalogo.Where(c => c.Ativo
                               || (codigoAtual is not null
                                   && string.Equals(c.Codigo, codigoAtual, StringComparison.OrdinalIgnoreCase)))
            .ToList();

    /// <summary>O valor gravado de volta ao campo, em pt-BR ("145", "62,5").</summary>
    public static string ParaCampo(decimal valor) => valor.ToString("0.##", PtBr);

    /// <summary>
    /// Confere o formulário na ORDEM do WPF (convênio, depois valor) e lê o valor pela MESMA
    /// <see cref="LeituraDeValores.TentarLerDecimal"/>: aceita "1.250,00" e "1250.00" e recusa
    /// o zero — preço zero é o mesmo que não ter tabela. Falso = <paramref name="erro"/> diz o quê.
    /// </summary>
    public static bool Conferir(string? convenioCodigo, string? valorDigitado, out decimal valor, out string? erro)
    {
        valor = 0m;
        if (string.IsNullOrWhiteSpace(convenioCodigo))
        {
            erro = "Escolha o convênio.";
            return false;
        }
        if (!LeituraDeValores.TentarLerDecimal(valorDigitado, out valor) || valor <= 0m)
        {
            erro = "Informe quanto a operadora paga por esta guia (ex.: 145,00).";
            return false;
        }
        erro = null;
        return true;
    }
}
