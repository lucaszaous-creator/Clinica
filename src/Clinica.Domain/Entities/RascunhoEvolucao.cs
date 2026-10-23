namespace Clinica.Domain.Entities;

/// <summary>
/// O RASCUNHO AUTOMÁTICO da evolução médica (out/2026, pedido da direção: "como no Excel",
/// o texto não se perde). Enquanto o médico escreve, a folha guarda aqui o que está na tela,
/// numa linha só por sessão, ATUALIZADA no lugar — sem versão nova a cada pausa.
///
/// ⚠️ Rascunho NÃO é prontuário: o registro clínico continua nascendo só no "Salvar sessão"
/// (que versiona, Lei 13.787) ou, vencido o prazo da conclusão automática sem ninguém salvar,
/// quando o sistema o grava em nome de quem escreveu e encerra a sessão
/// (<c>ConclusaoAutomaticaService</c>). A linha nunca se apaga: ao ser gravado, o rascunho é
/// ENCERRADO com o desfecho, e o que ele dizia fica para conferência.
/// </summary>
public class RascunhoEvolucao
{
    public int Id { get; set; }

    public int PacienteId { get; set; }
    public Paciente? Paciente { get; set; }

    /// <summary>Quem escreve. Nulo para quem não tem vínculo de profissional (o Gerente).</summary>
    public int? ProfissionalId { get; set; }

    /// <summary>O horário da agenda. Nulo quando o paciente foi aberto pela busca.</summary>
    public int? AgendamentoId { get; set; }

    /// <summary>O dia da sessão — a chave do rascunho quando não há horário.</summary>
    public DateOnly Dia { get; set; }

    /// <summary>A evolução que ele continua, quando ela já foi salva uma vez.</summary>
    public int? EvolucaoId { get; set; }

    /// <summary>O que estava na tela, em JSON (<c>ConteudoDoRascunho</c> da Application).</summary>
    public string Conteudo { get; set; } = string.Empty;

    /// <summary>A última alteração: é dela que conta o prazo da conclusão automática.</summary>
    public DateTime AtualizadoEm { get; set; }

    /// <summary>O login de quem escreveu — o autor da evolução quando o sistema a gravar.</summary>
    public string? AtualizadoPor { get; set; }

    /// <summary>Quando deixou de valer. Nulo = rascunho aberto.</summary>
    public DateTime? EncerradoEm { get; set; }

    /// <summary>Por que encerrou: <see cref="SalvoPeloProfissional"/>, <see cref="SalvoPorPrazo"/>, <see cref="SemConteudo"/> ou <see cref="Superado"/>.</summary>
    public string? Desfecho { get; set; }

    public const string SalvoPeloProfissional = "SalvoPeloProfissional";
    public const string SalvoPorPrazo = "SalvoPorPrazo";
    public const string SemConteudo = "SemConteudo";

    /// <summary>A sessão foi salva DEPOIS da última alteração do rascunho (outra estação, o tablet): ele não regrava nada.</summary>
    public const string Superado = "Superado";
}
