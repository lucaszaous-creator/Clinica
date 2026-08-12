namespace Clinica.Domain.Entities;

/// <summary>Ciclo de vida de um lote TISS enviado à operadora.</summary>
public enum StatusLoteTiss
{
    Gerado,     // XML exportado; ainda não entregue à operadora
    Enviado,    // entregue (portal/webservice); aguardando análise
    Processado  // demonstrativo de análise registrado (retorno da operadora)
}

/// <summary>
/// Lote TISS: registro permanente de cada exportação de guias à operadora.
/// Fecha o ciclo lote → envio (protocolo) → demonstrativo de análise (retorno),
/// evitando reenvio duplicado de guias e dando lastro auditável ao faturamento.
/// FATURAMENTO apenas: não há nenhum campo de valor.
/// </summary>
public class LoteTiss
{
    public int Id { get; set; }

    /// <summary>Número sequencial do lote (exigência do padrão TISS).</summary>
    public int Numero { get; set; }

    public DateOnly DataGeracao { get; set; }

    /// <summary>Registro ANS da operadora de destino no momento da geração.</summary>
    public string? RegistroAnsOperadora { get; set; }

    /// <summary>
    /// Código do convênio (catálogo) cujas guias este lote carrega (parcela 60). O lote
    /// é POR OPERADORA: antes desta coluna, um "lote do período" engolia as guias de
    /// todas as operadoras num XML endereçado a UMA — e as demais nunca mais entravam em
    /// lote, porque a guarda de duplicidade as via como já exportadas. Nulo nos lotes
    /// anteriores à coluna (gerados no modo antigo) — histórico, não se reescreve.
    /// </summary>
    public string? ConvenioCodigo { get; set; }

    public StatusLoteTiss Status { get; set; } = StatusLoteTiss.Gerado;

    // ---------- Envio ----------

    public DateOnly? DataEnvio { get; set; }

    /// <summary>Protocolo devolvido pela operadora no recebimento do lote.</summary>
    public string? ProtocoloOperadora { get; set; }

    // ---------- Retorno (demonstrativo de análise de conta) ----------

    public DateOnly? DataRetorno { get; set; }
    public string? ObservacaoRetorno { get; set; }

    /// <summary>Guias incluídas neste lote.</summary>
    public List<CodigoFaturamento> Codigos { get; set; } = new();

    /// <summary>Marca o lote como entregue à operadora.</summary>
    public void MarcarEnviado(DateOnly data, string? protocolo)
    {
        if (Status == StatusLoteTiss.Processado)
            throw new InvalidOperationException("O lote já foi processado — não é possível alterar o envio.");
        Status = StatusLoteTiss.Enviado;
        DataEnvio = data;
        ProtocoloOperadora = protocolo;
    }

    /// <summary>Registra o retorno (demonstrativo) da operadora.</summary>
    public void RegistrarRetorno(DateOnly data, string? observacao)
    {
        if (Status == StatusLoteTiss.Gerado)
            throw new InvalidOperationException("Marque o lote como enviado antes de registrar o retorno.");
        Status = StatusLoteTiss.Processado;
        DataRetorno = data;
        ObservacaoRetorno = observacao;
    }
}
