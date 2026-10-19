using System.Globalization;
using Clinica.Application.Formatacao;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>Uma linha da tabela do particular, já escrita para a tela.</summary>
public sealed record LinhaDePrecoParticular(
    int PrecoId,
    string Modalidade,
    string Especialidade,
    string Valor,
    string Vigencia,
    /// <summary>Vigente HOJE — é o valor que o Finalizar e a Conciliação propõem agora.</summary>
    bool ValendoAgora,
    /// <summary>A situação em palavras: valendo agora, ainda não vale, vigência encerrada ou inativo.</summary>
    string Situacao);

/// <summary>Uma opção do formulário (código + nome do catálogo). O código nulo é "qualquer especialidade".</summary>
public sealed record OpcaoDeCatalogo(string? Codigo, string Nome);

/// <summary>
/// O que a tela PREÇOS DO PARTICULAR afirma (out/2026 — saiu do WPF para o Blazor). Era o
/// <c>PrecosParticularViewModel</c> com a <c>LinhaPrecoParticular</c> e o
/// <c>PrecoParticularEdicaoViewModel</c>: o filtro "só o que vale hoje", o resumo, a frase do
/// vazio, as opções do formulário e a leitura do valor digitado. Quem lê e grava é a tela, com o
/// MESMO <see cref="PrecoParticularService"/> que o Finalizar e a Conciliação leem.
///
/// O preço é chaveado pelo que a clínica FAZ: <b>modalidade</b> (obrigatória) + <b>especialidade
/// atendida</b> (opcional — separa a consulta de psiquiatria da de geriatria). O mais específico
/// ganha, tem vigência e é PROPOSTA (o desconto é dado no clique).
///
/// A permissão é <c>VenderPacote</c> OU <c>EditarFinanceiro</c> — o mesmo corte da venda de
/// pacote: combinar preço é do balcão, com o paciente na frente.
/// </summary>
public static class PrecosParticularNaTela
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public const Permissao QuemEdita =
        Permissao.VenderPacote | Permissao.EditarFinanceiro;

    public const string QualquerEspecialidade = "qualquer especialidade";

    public const string VazioSemTabela =
        "Nenhum preço cadastrado. Sem tabela, o Finalizar do particular e a Conciliação pedem o valor digitado — o sistema não inventa um valor.";

    public const string VazioComFiltro =
        "Nenhum preço vale hoje — desmarque o filtro para ver a tabela inteira.";

    public const string Cadastrado = "Preço cadastrado — o Finalizar do particular já vai propor esse valor.";

    public const string Atualizado = "Preço atualizado. Vale para as sessões novas; o que já foi lançado não muda.";

    public const string Excluido = "Preço excluído.";

    public static LinhaDePrecoParticular LinhaDe(PrecoParticular p, DateOnly hoje) => new(
        PrecoId: p.Id,
        Modalidade: p.ModalidadeNome,
        // "qualquer" escrito, e não célula vazia: vazio se lê como "não preenchido".
        Especialidade: string.IsNullOrWhiteSpace(p.EspecialidadeCodigo) ? QualquerEspecialidade : p.EspecialidadeNome,
        Valor: p.Valor.ToString("C", PtBr),
        Vigencia: p.Vigencia,
        ValendoAgora: p.VigenteEm(hoje),
        Situacao: Situacao(p, hoje));

    /// <summary>
    /// Fora de vigência, a tela diz POR QUÊ: o reajuste que começa mês que vem, o preço antigo já
    /// encerrado e o desligado são respostas diferentes (o WPF só pintava a linha).
    /// </summary>
    public static string Situacao(PrecoParticular p, DateOnly hoje)
    {
        if (!p.Ativo) return "inativo";
        if (p.VigenteDe is { } de && hoje < de) return "ainda não vale";
        if (p.VigenteAte is { } ate && hoje > ate) return "vigência encerrada";
        return "valendo agora";
    }

    public static IReadOnlyList<LinhaDePrecoParticular> Filtrar(
        IEnumerable<LinhaDePrecoParticular> todas, bool soValendoHoje)
        => todas.Where(p => !soValendoHoje || p.ValendoAgora).ToList();

    /// <summary>O resumo DIZ que está filtrado; o "valendo hoje" é sempre o do TOTAL.</summary>
    public static string Resumo(IReadOnlyCollection<LinhaDePrecoParticular> todas, int noFiltro, bool soValendoHoje)
    {
        var vigentes = todas.Count(p => p.ValendoAgora);
        return todas.Count == 0
            ? "Nenhum preço cadastrado — o Finalizar e a Conciliação continuam pedindo o valor digitado."
            : soValendoHoje
                ? $"{noFiltro} de {todas.Count} preço(s) valendo hoje."
                : $"{noFiltro} preço(s) cadastrado(s) · {vigentes} valendo hoje.";
    }

    public static string Vazio(bool soValendoHoje) => soValendoHoje ? VazioComFiltro : VazioSemTabela;

    public static string Concluido(bool novo) => novo ? Cadastrado : Atualizado;

    public static string PerguntaDeExcluir(LinhaDePrecoParticular l)
        => $"Apagar o preço de {l.Modalidade} ({l.Especialidade})? Se ele já propôs valor a "
           + "alguma sessão, prefira ENCERRAR pela vigência: os lançamentos guardam o valor "
           + "copiado, mas apagar a linha apaga a explicação de por que aquela sessão valeu aquilo.";

    // ---------------------------------------------------------------- formulário

    /// <summary>
    /// As opções de um catálogo (modalidade ou especialidade): as ATIVAS — e, na edição, também a
    /// do próprio preço quando ela foi desligada depois. O WPF oferecia só as ativas, e o preço de
    /// uma modalidade desligada abria com o combo em branco e recusava o Salvar, sem jeito de
    /// corrigir nem de encerrar a vigência dele.
    /// </summary>
    public static IReadOnlyList<OpcaoDeCatalogo> Opcoes(
        IEnumerable<(string Codigo, string Nome, bool Ativo)> catalogo, string? codigoAtual)
        => catalogo.Where(c => c.Ativo
                               || (codigoAtual is not null
                                   && string.Equals(c.Codigo, codigoAtual, StringComparison.OrdinalIgnoreCase)))
            .Select(c => new OpcaoDeCatalogo(c.Codigo, c.Ativo ? c.Nome : $"{c.Nome} (desativada)"))
            .ToList();

    /// <summary>O código do catálogo que casa com o gravado (caixa ignorada), ou nulo.</summary>
    public static string? Casar(IEnumerable<OpcaoDeCatalogo> opcoes, string? codigo)
        => codigo is null
            ? null
            : opcoes.FirstOrDefault(o => string.Equals(o.Codigo, codigo, StringComparison.OrdinalIgnoreCase))?.Codigo;

    /// <summary>O valor gravado de volta ao campo, em pt-BR ("180", "62,5").</summary>
    public static string ParaCampo(decimal valor) => valor.ToString("0.##", PtBr);

    /// <summary>
    /// Confere o formulário na ORDEM do WPF (modalidade, depois valor) e lê o valor pela MESMA
    /// <see cref="LeituraDeValores.TentarLerDecimal"/> — o zero é recusado: preço zero é o mesmo
    /// que não ter tabela. Falso = <paramref name="erro"/> diz o quê.
    /// </summary>
    public static bool Conferir(string? modalidadeCodigo, string? valorDigitado, out decimal valor, out string? erro)
    {
        valor = 0m;
        if (string.IsNullOrWhiteSpace(modalidadeCodigo))
        {
            erro = "Escolha a modalidade (acupuntura, consulta, BSV…).";
            return false;
        }
        if (!LeituraDeValores.TentarLerDecimal(valorDigitado, out valor) || valor <= 0m)
        {
            erro = "Informe quanto a clínica cobra por esta sessão (ex.: 180,00).";
            return false;
        }
        erro = null;
        return true;
    }
}
