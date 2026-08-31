namespace Clinica.Domain.Entities;

/// <summary>
/// Um resultado de exame ESTRUTURADO do paciente (ago/2026 — a tela de referência do
/// handoff mostrava exames com campos tipados, e a direção decidiu construir).
///
/// Até aqui o laudo só existia como ANEXO (o PDF digitalizado) ou como texto dentro da
/// evolução — dava para guardar e para ler, e não dava para PERGUNTAR: "qual era a
/// glicada dele em março?" exigia abrir laudo por laudo. Este registro é o valor que se
/// consulta e se compara; o laudo continua sendo o anexo, e um não substitui o outro.
///
/// As decisões que não são óbvias:
///
/// 1. <b>O valor é TEXTO LIVRE, por desenho.</b> Resultado de laboratório é heterogêneo
///    ("6,1", "não reagente", "&lt; 0,01", "positivo 1/80") e um campo numérico recusaria
///    metade dos laudos reais — a regra apertada demais que o projeto já rejeitou no
///    formato do número da guia. A REFERÊNCIA vem do próprio laudo, copiada, porque cada
///    laboratório tem a sua: inventar uma faixa "normal" aqui seria o sistema opinando
///    sobre o que só o método do laboratório sabe.
///
/// 2. <b>Registro clínico NÃO SE APAGA</b> (Lei 13.787/2018; regra 1 do compromisso de
///    conformidade): não há edição nem exclusão — um valor digitado errado se CANCELA
///    com motivo escrito e se registra de novo. A linha cancelada fica, marcada.
///
/// 3. <b>A data é a do EXAME (coleta/laudo), informada — nunca a de hoje.</b> O resultado
///    chega dias depois da coleta, e é a data clínica que a curva e a guarda usam; o
///    relógio de quem digitou fica em <see cref="CriadoEm"/>, ao lado.
/// </summary>
public class ResultadoExame
{
    public int Id { get; set; }

    public int PacienteId { get; set; }
    public Paciente? Paciente { get; set; }

    /// <summary>Data do exame (coleta ou laudo) — a data CLÍNICA, informada.</summary>
    public DateOnly Data { get; set; }

    /// <summary>O que foi medido — "Hemoglobina glicada", "Hemograma — leucócitos".</summary>
    public string Nome { get; set; } = string.Empty;

    /// <summary>O resultado, como o laudo o escreve.</summary>
    public string Valor { get; set; } = string.Empty;

    public string? Unidade { get; set; }

    /// <summary>A faixa de referência DO LAUDO, copiada — cada laboratório tem a sua.</summary>
    public string? Referencia { get; set; }

    /// <summary>Quem emitiu o laudo (laboratório/serviço) — a procedência do número.</summary>
    public string? Laboratorio { get; set; }

    public string? Observacoes { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.Now;
    public string? CriadoPor { get; set; }

    public DateTime? CanceladoEm { get; set; }
    public string? MotivoCancelamento { get; set; }
    public string? CanceladoPor { get; set; }

    /// <summary>⚠️ Derivada — em consulta traduzida use a COLUNA (CanceladoEm == null).</summary>
    public bool Cancelado => CanceladoEm is not null;

    /// <summary>"6,1 %" — o valor com a unidade, quando há.</summary>
    public string ValorComUnidade => string.IsNullOrWhiteSpace(Unidade)
        ? Valor
        : $"{Valor} {Unidade}";
}
