namespace Clinica.Domain.Entities;

/// <summary>
/// Cota de sessões liberada pelo convênio para um paciente (a "senha"/autorização).
///
/// Existe para evitar a glosa <c>2006 — quantidade executada acima da autorizada</c>:
/// o sistema já sabia registrar essa glosa depois do prejuízo, mas não avisava antes.
/// O saldo é contado pelos atendimentos do paciente dentro da vigência da autorização
/// (ver <c>AutorizacaoService</c>), então não é preciso amarrar cada atendimento a uma
/// senha na hora do lançamento.
/// </summary>
public class AutorizacaoSessoes
{
    public int Id { get; set; }

    public int PacienteId { get; set; }
    public Paciente? Paciente { get; set; }

    /// <summary>Número da senha/autorização emitida pelo convênio (vai na guia).</summary>
    public string? Numero { get; set; }

    /// <summary>Convênio que autorizou (registrado no momento da emissão).</summary>
    public Convenio Convenio { get; set; }

    /// <summary>Código do convênio no catálogo. Null = convênio embutido.</summary>
    public string? ConvenioCodigo { get; set; }

    public DateOnly DataEmissao { get; set; }

    /// <summary>Último dia em que a autorização pode ser usada.</summary>
    public DateOnly DataValidade { get; set; }

    /// <summary>Quantas sessões o convênio liberou.</summary>
    public int QuantidadeAutorizada { get; set; }

    /// <summary>
    /// Sessões já consumidas antes de o controle existir (ou fora do sistema).
    /// Entra no saldo junto com os atendimentos contados na vigência.
    /// </summary>
    public int QuantidadeUtilizadaManual { get; set; }

    public string? Observacoes { get; set; }

    /// <summary>Encerrada manualmente (substituída por outra senha, cancelada pelo convênio…).</summary>
    public bool Encerrada { get; set; }

    /// <summary>Vigente na data: não encerrada e dentro do intervalo de validade.</summary>
    public bool Vigente(DateOnly referencia)
        => !Encerrada && referencia >= DataEmissao && referencia <= DataValidade;

    /// <summary>Validade já passou.</summary>
    public bool Vencida(DateOnly referencia) => referencia > DataValidade;
}
