namespace Clinica.Domain.Entities;

/// <summary>
/// Quanto vale um paciente recuperado (out/2026, pedido da clínica): a atendente assume
/// quem sumiu, o paciente volta, conclui o atendimento e a guia nasce — e só então a
/// comissão existe. É a mesma ideia da regra de repasse do profissional, aplicada a quem
/// trouxe o paciente de volta.
///
/// Tem VIGÊNCIA, como o repasse e o preço: reajustar hoje não reescreve o mês já validado —
/// o valor é COPIADO na validação (<see cref="ComissaoRecuperacao.Valor"/>). A mais
/// ESPECÍFICA ganha (fila + modalidade vence só a fila, que vence a geral), senão a clínica
/// cadastraria a exceção e continuaria vendo o valor geral.
/// </summary>
public class RegraComissaoRecuperacao
{
    public int Id { get; set; }

    /// <summary>A fila (recall ou novos BSV). Null = as duas.</summary>
    public TipoAcompanhamento? Tipo { get; set; }

    /// <summary>A modalidade da sessão de retorno. Null = todas.</summary>
    public ModalidadeAtendimento? Modalidade { get; set; }

    /// <summary>O valor por paciente recuperado.</summary>
    public decimal Valor { get; set; }

    public DateOnly VigenteDe { get; set; }

    public DateOnly? VigenteAte { get; set; }

    public bool Ativa { get; set; } = true;

    public string? Observacao { get; set; }

    public DateTime CriadaEm { get; set; }

    public string CriadaPor { get; set; } = string.Empty;
}

/// <summary>A decisão da gestão sobre uma recuperação: validar (com o valor copiado) ou recusar (com o motivo).</summary>
public enum DecisaoComissaoRecuperacao { Validada, Recusada }

/// <summary>
/// A VERIFICAÇÃO de uma recuperação pela gestão — uma linha por SESSÃO DE RETORNO
/// (<see cref="AtendimentoId"/>): o mesmo paciente pode sumir e voltar de novo, e cada volta é
/// um caso. Não se apaga: desfazer é CANCELAR com motivo, e a recuperação volta à verificação.
/// Quem assumiu, quando, e o que a regra dizia ficam COPIADOS — a conferência do mês passado
/// não muda porque alguém reajustou o valor ou trocou o responsável do caso depois.
/// </summary>
public class ComissaoRecuperacao
{
    public int Id { get; set; }

    public int AcompanhamentoPacienteId { get; set; }
    public AcompanhamentoPaciente? Acompanhamento { get; set; }

    /// <summary>A sessão em que o paciente voltou — é ela que fecha o fluxo.</summary>
    public int AtendimentoId { get; set; }

    public int PacienteId { get; set; }

    /// <summary>A atendente que assumiu o caso antes do retorno.</summary>
    public int UsuarioId { get; set; }
    public UsuarioSistema? Usuario { get; set; }

    public DateTime AssumidoEm { get; set; }

    public DateOnly RetornoEm { get; set; }

    public DecisaoComissaoRecuperacao Decisao { get; set; }

    /// <summary>O valor pago pela recuperação — copiado da regra na validação; zero na recusa.</summary>
    public decimal Valor { get; set; }

    /// <summary>Como o valor foi decidido ("R$ 30,00 — recall, acupuntura"), congelado na validação.</summary>
    public string? RegraDescricao { get; set; }

    /// <summary>A justificativa da recusa (obrigatória) ou a observação da validação.</summary>
    public string? Observacao { get; set; }

    public DateTime DecididaEm { get; set; }

    public string DecididaPor { get; set; } = string.Empty;

    public DateTime? CanceladaEm { get; set; }

    public string? CanceladaPor { get; set; }

    public string? MotivoCancelamento { get; set; }
}
