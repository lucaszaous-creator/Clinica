using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Application.Abstracoes;

/// <summary>Em que pé está uma recuperação na verificação da gestão.</summary>
public enum SituacaoRecuperacao
{
    /// <summary>Fluxo completo e com valor cadastrado — falta a gestão validar.</summary>
    AValidar,
    /// <summary>Fluxo completo, mas não há valor vigente para a fila/modalidade na data do retorno.</summary>
    SemValor,
    Validada,
    Recusada
}

/// <summary>
/// Uma recuperação apurada: quem assumiu, quando o paciente voltou, a sessão que fechou o
/// fluxo e o que a gestão decidiu. Uma linha por SESSÃO DE RETORNO.
/// </summary>
public sealed record LinhaRecuperacao(
    int AtendimentoId,
    int AcompanhamentoId,
    int PacienteId,
    string Paciente,
    TipoAcompanhamento Tipo,
    ModalidadeAtendimento Modalidade,
    int UsuarioId,
    string Atendente,
    DateTime AssumidoEm,
    DateOnly RetornoEm,
    string Profissional,
    // A atendente registrou contato (WhatsApp, telefone…) antes do retorno — não só assumiu.
    bool TeveContato,
    int Tentativas,
    // A sessão gerou guia para o convênio (falso = particular, sem guia).
    bool GuiaGerada,
    // A sessão foi estornada DEPOIS de decidida — a validação precisa ser revista.
    bool EstornadaDepois,
    SituacaoRecuperacao Situacao,
    decimal? ValorProposto,
    string? RegraProposta,
    int? ComissaoId,
    decimal? ValorDecidido,
    string? Observacao,
    string? DecididaPor,
    DateTime? DecididaEm)
{
    public string TipoTexto => Tipo == TipoAcompanhamento.NovoBsv ? "Novo BSV" : "Recall";
}

public interface IComissaoRecuperacaoService
{
    Task<IReadOnlyList<RegraComissaoRecuperacao>> RegrasAsync(CancellationToken ct = default);

    /// <summary>Cria ou edita a regra de valor (o Id decide). Reajuste é regra NOVA com vigência — o validado não muda.</summary>
    Task<RegraComissaoRecuperacao> SalvarRegraAsync(RegraComissaoRecuperacao dados, string operador, CancellationToken ct = default);

    Task DesativarRegraAsync(int id, string operador, CancellationToken ct = default);

    /// <summary>As recuperações cuja sessão de retorno caiu no período.</summary>
    Task<IReadOnlyList<LinhaRecuperacao>> ApurarAsync(DateOnly inicio, DateOnly fim, CancellationToken ct = default);

    /// <summary>Valida as recuperações (pela sessão de retorno), copiando o valor da regra. Devolve quantas.</summary>
    Task<int> ValidarAsync(IReadOnlyList<int> atendimentoIds, string operador, CancellationToken ct = default);

    /// <summary>Recusa uma recuperação — exige justificativa, e ela fica na verificação.</summary>
    Task RecusarAsync(int atendimentoId, string justificativa, string operador, CancellationToken ct = default);

    /// <summary>Desfaz a decisão (com motivo): a recuperação volta a ser verificada. A linha não se apaga.</summary>
    Task CancelarDecisaoAsync(int comissaoId, string motivo, string operador, CancellationToken ct = default);
}
