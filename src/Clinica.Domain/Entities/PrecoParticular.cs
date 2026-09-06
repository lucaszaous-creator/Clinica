using Clinica.Domain.Regras;

namespace Clinica.Domain.Entities;

/// <summary>
/// Quanto a clínica cobra do PARTICULAR por especialidade atendida (set/2026).
///
/// O pedido da direção foi literal: <i>"um cadastro de preço por tipo de especialidade
/// atendida quando o paciente for particular"</i>. Até aqui o particular não tinha preço de
/// lista em lugar nenhum: o fechamento sugeria "o que este paciente pagou da última vez"
/// (nada, na primeira vez) e a conciliação pedia o valor digitado. A tabela por convênio
/// (<see cref="PrecoConvenio"/>) não serve para isto — ela é chaveada pelo TIPO DE GUIA,
/// que é conceito do faturamento, e o particular não tem guia.
///
/// A chave é o que a clínica FAZ, nas palavras dela:
/// <list type="bullet">
///   <item><b>Modalidade</b> (código do catálogo — acupuntura simples, com eletro, BSV,
///   consulta…), obrigatória: é o procedimento.</item>
///   <item><b>Especialidade</b> (código do catálogo), opcional: é o que diferencia a
///   consulta de psiquiatria da de geriatria. Em branco = vale para qualquer especialidade
///   daquela modalidade — o caso normal da acupuntura, que é a especialidade da casa.</item>
/// </list>
///
/// A mais ESPECÍFICA ganha (a lição da tabela por convênio, parcela 20): preço com
/// especialidade vence o genérico da modalidade; código de variante da modalidade vence o
/// da família. Tem VIGÊNCIA — reajuste é linha nova, e a sessão de março segue valendo o
/// preço de março. E é PROPOSTA: o valor é COPIADO no lançamento; reajustar a tabela não
/// reescreve o que o paciente já pagou, e o balcão pode dar desconto no clique.
/// </summary>
public class PrecoParticular
{
    public int Id { get; set; }

    /// <summary>
    /// Código da modalidade no catálogo (<c>CatalogoModalidades</c>). Para as embutidas é o
    /// nome do enum ("AcupunturaComEletro"); para as variantes da clínica, o código delas.
    /// </summary>
    public string ModalidadeCodigo { get; set; } = string.Empty;

    /// <summary>
    /// Código da especialidade no catálogo (<c>CatalogoEspecialidades</c>), quando o valor
    /// depende dela. Null = qualquer especialidade desta modalidade.
    /// </summary>
    public string? EspecialidadeCodigo { get; set; }

    /// <summary>Quanto a clínica cobra do particular por esta sessão.</summary>
    public decimal Valor { get; set; }

    public DateOnly? VigenteDe { get; set; }

    public DateOnly? VigenteAte { get; set; }

    public bool Ativo { get; set; } = true;

    public string? Observacoes { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.Now;

    public string? CriadoPor { get; set; }

    /// <summary>O preço vale no dia informado?</summary>
    public bool VigenteEm(DateOnly dia)
        => Ativo
           && (VigenteDe is null || dia >= VigenteDe)
           && (VigenteAte is null || dia <= VigenteAte);

    /// <summary>pt-BR FIXO — este texto é copiado para a procedência do lançamento e fica gravado.</summary>
    private static readonly System.Globalization.CultureInfo Brasil = new("pt-BR");

    /// <summary>Nome da modalidade pelo catálogo — nunca o identificador do enum (parcela 41).</summary>
    public string ModalidadeNome => CatalogoModalidades.Nome(ModalidadeCodigo);

    /// <summary>Nome da especialidade pelo catálogo; vazio quando vale para todas.</summary>
    public string EspecialidadeNome => string.IsNullOrWhiteSpace(EspecialidadeCodigo)
        ? string.Empty
        : CatalogoEspecialidades.Nome(EspecialidadeCodigo);

    /// <summary>"Consulta · Psiquiatria — R$ 400,00" ou "Acupuntura + eletro — R$ 180,00".</summary>
    public string Descricao => string.Format(
        Brasil, "{0}{1} — {2:C}",
        ModalidadeNome,
        string.IsNullOrWhiteSpace(EspecialidadeCodigo) ? string.Empty : $" · {EspecialidadeNome}",
        Valor);

    /// <summary>Vigência escrita, para a lista.</summary>
    public string Vigencia => (VigenteDe, VigenteAte) switch
    {
        (null, null) => "sem prazo",
        ({ } de, null) => string.Format(Brasil, "desde {0:dd/MM/yyyy}", de),
        (null, { } ate) => string.Format(Brasil, "até {0:dd/MM/yyyy}", ate),
        var (de, ate) => string.Format(Brasil, "{0:dd/MM/yyyy} a {1:dd/MM/yyyy}", de, ate)
    };
}
