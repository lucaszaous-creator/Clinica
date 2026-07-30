namespace Clinica.Application.Modelos;

/// <summary>
/// Filtro da trilha de auditoria (parcela 21).
///
/// Existe como objeto — e não como uma pilha de parâmetros — porque os campos entram no
/// SQL juntos, e uma assinatura de seis argumentos opcionais acabaria chamada com os
/// valores na ordem errada em algum ponto.
/// </summary>
public sealed class FiltroAuditoria
{
    /// <summary>Ação executada, casada por PREFIXO ("Conta" acha ContaCriada e ContaReagendada").</summary>
    public string? Acao { get; set; }

    /// <summary>Quem executou. Casa por trecho — o operador é digitado, não escolhido.</summary>
    public string? Operador { get; set; }

    /// <summary>Trecho do detalhe (nº de guia, motivo, valor escrito).</summary>
    public string? Termo { get; set; }

    public DateOnly? Inicio { get; set; }

    public DateOnly? Fim { get; set; }

    /// <summary>Trilha de um paciente específico.</summary>
    public int? PacienteId { get; set; }

    /// <summary>Trilha de uma guia específica.</summary>
    public int? CodigoId { get; set; }

    /// <summary>
    /// Corte no BANCO. A trilha cresce para sempre — é a tabela que mais cresce no sistema,
    /// porque toda ação que mexe em dinheiro escreve nela —, e trazer tudo para descartar
    /// em memória é exatamente o que a convenção do projeto proíbe.
    /// </summary>
    public int Limite { get; set; } = 300;
}
