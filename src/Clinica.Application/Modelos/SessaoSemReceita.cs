using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Modelos;

/// <summary>
/// Sessão PARTICULAR que aconteceu e ainda não tem dinheiro registrado — nem receita,
/// nem conta a receber, nem sessão de pacote debitada (set/2026).
///
/// É o par do <c>GuiaSemLancamento</c> para quem não tem convênio. A Conciliação respondia
/// "que guia efetivada ainda não virou dinheiro?" e não tinha como responder a mesma
/// pergunta sobre o particular: <c>CodigosNoPeriodoAsync</c> exclui o
/// <see cref="StatusCodigo.NaoAplicavel"/> por construção, e o particular só tem código
/// assim. A sessão ficava registrada, a guia não existia (certo) e o dinheiro não existia
/// (errado) — sem nada, em tela nenhuma, dizendo isso.
///
/// Uma linha aqui é o que a Recepção deixou de resolver no dia (o Finalizar sem registrar
/// como o paciente paga) ou o que foi combinado "para depois" e ninguém anotou. Ela SOME
/// quando passa a ter lançamento (recebido OU a receber) ou sessão de pacote — nunca
/// porque alguém a marcou.
/// </summary>
public sealed record SessaoSemReceita(
    int AtendimentoId,
    int PacienteId,
    string Paciente,
    DateOnly Data,
    ModalidadeAtendimento Modalidade,
    string? ModalidadeCodigo,
    Especialidade? Especialidade,
    string? EspecialidadeCodigo,
    Convenio Convenio,
    string? ConvenioCodigo,
    TipoCodigo Tipo)
{
    /// <summary>Código da modalidade como a tabela do particular o conhece (família quando não há código).</summary>
    public string CodigoDaModalidade => ModalidadeCodigo ?? Modalidade.ToString();

    /// <summary>Idem para a especialidade; nulo quando a sessão não tem uma.</summary>
    public string? CodigoDaEspecialidade => EspecialidadeCodigo ?? Especialidade?.ToString();

    /// <summary>Nome do CATÁLOGO, nunca o enum (a lição da parcela 41).</summary>
    public string ModalidadeNome => CatalogoModalidades.Nome(ModalidadeCodigo, Modalidade);

    /// <summary>"Particular", ou o nome que a clínica deu ao cadastro sem guia.</summary>
    public string ConvenioNome => CatalogoConvenios.Nome(ConvenioCodigo, Convenio);

    /// <summary>Código do convênio como a tabela de preço o conhece (família quando não há código).</summary>
    public string CodigoParaTabela => ConvenioCodigo ?? Convenio.ToString();
}
