using Clinica.Domain.Entities;

namespace Clinica.Application.Modelos;

/// <summary>
/// Uma guia de um lote que a operadora RECEBEU e que ainda não tem baixa no sistema: o que a
/// baixa em lote tem a fazer depois de um envio.
/// </summary>
/// <param name="ItemId">A linha do lote (<c>TransacaoTissGuia.Id</c>).</param>
/// <param name="CodigoId">A guia do sistema.</param>
/// <param name="NumeroGuiaPrestador">O número que a clínica mandou.</param>
/// <param name="NumeroGuiaOperadora">O número devolvido pela operadora, quando ela devolve.</param>
/// <param name="Protocolo">O protocolo do lote.</param>
public sealed record GuiaTissRecebidaSemBaixa(
    int ItemId, int CodigoId, string NumeroGuiaPrestador, string? NumeroGuiaOperadora, string? Protocolo);

/// <summary>Uma guia conferida, sem baixa e fora de qualquer envio que a prenda: pode entrar num lote.</summary>
public sealed record GuiaProntaParaEnvioTiss(
    int CodigoId, int PacienteId, string PacienteNome, DateOnly DataAtendimento, Clinica.Domain.TipoCodigo Tipo,
    string CodigoProcedimento, decimal ValorProcedimento, string ConferidaPor, DateTime ConferidaEm);

/// <summary>Em que pé está o envio de uma guia, para o selo da linha.</summary>
public enum EstadoDoEnvioDaGuia
{
    NaoEnviada,
    NaFila,
    /// <summary>Protocolada, ainda sem resposta da operadora sobre ESTA guia.</summary>
    Enviada,
    Recebida,
    Glosada,
    Falhou,
    Cancelada
}

/// <summary>
/// O selo de envio de uma guia: o estado, a frase pronta e o que a tela precisa para abrir o lote.
/// A frase mora aqui, e não na tela: é o que o sistema AFIRMA sobre a guia.
/// </summary>
public sealed record SeloDeEnvioTiss(EstadoDoEnvioDaGuia Estado, string Texto, string? Protocolo = null, int? TransacaoId = null)
{
    public static readonly SeloDeEnvioTiss NaoEnviada = new(EstadoDoEnvioDaGuia.NaoEnviada, "Não enviada");

    /// <summary>O selo a partir do item do ÚLTIMO envio da guia (com a transação dele). Nulo = nunca enviada.</summary>
    public static SeloDeEnvioTiss Do(TransacaoTissGuia? item)
    {
        if (item?.Transacao is not { } envio) return NaoEnviada;
        var protocolo = string.IsNullOrWhiteSpace(envio.Protocolo) ? null : envio.Protocolo;
        var motivo = string.IsNullOrWhiteSpace(item.Motivo) ? null : item.Motivo;

        SeloDeEnvioTiss Selo(EstadoDoEnvioDaGuia estado, string texto) => new(estado, texto, protocolo, envio.Id);
        return (envio.Estado, item.Situacao) switch
        {
            (EstadoTransacaoTiss.Pendente or EstadoTransacaoTiss.EmEnvio, _) => Selo(EstadoDoEnvioDaGuia.NaFila, "Na fila de envio"),
            (_, SituacaoGuiaTiss.Recebida) => Selo(EstadoDoEnvioDaGuia.Recebida, $"Recebida pela operadora, protocolo {protocolo ?? "não informado"}"),
            (_, SituacaoGuiaTiss.Glosada) => Selo(EstadoDoEnvioDaGuia.Glosada, "Glosada: " + (motivo ?? "a operadora não informou o motivo")),
            (_, SituacaoGuiaTiss.Cancelada) => Selo(EstadoDoEnvioDaGuia.Cancelada, motivo ?? "Cancelada na operadora"),
            (EstadoTransacaoTiss.Concluida, SituacaoGuiaTiss.Pendente) => Selo(EstadoDoEnvioDaGuia.Enviada, $"Enviada, protocolo {protocolo ?? "não informado"}: sem resposta sobre esta guia"),
            // Envio cancelado antes de sair não é falha: a guia simplesmente não foi.
            (EstadoTransacaoTiss.Cancelada, _) => new(EstadoDoEnvioDaGuia.NaoEnviada, "Não enviada: " + (motivo ?? "o envio foi cancelado"), null, envio.Id),
            _ => Selo(EstadoDoEnvioDaGuia.Falhou, "Falhou: " + (motivo ?? "o envio não se completou"))
        };
    }
}

/// <summary>O que aconteceu com UMA guia na baixa das recebidas.</summary>
/// <param name="NumeroUsado">O número com que a baixa foi (ou seria) dada.</param>
/// <param name="Falha">Nulo = baixada. Senão, a frase do que impediu — a guia continua pendente.</param>
public sealed record BaixaDeGuiaEnviada(int CodigoId, string NumeroUsado, string? Falha)
{
    public bool Baixada => Falha is null;
}

/// <summary>O desfecho da baixa das guias recebidas de um envio.</summary>
public sealed record BaixaDasGuiasEnviadas(IReadOnlyList<BaixaDeGuiaEnviada> Guias)
{
    public int Baixadas => Guias.Count(g => g.Baixada);
    public IReadOnlyList<BaixaDeGuiaEnviada> Falhas => Guias.Where(g => !g.Baixada).ToList();

    /// <summary>Uma frase para a tela: quantas foram e quantas não, com o motivo de cada uma.</summary>
    public string Resumo => Guias.Count == 0
        ? "Não há guia recebida pela operadora aguardando baixa neste envio."
        : Falhas.Count == 0
            ? $"{Baixadas} guia(s) baixada(s)."
            : $"{Baixadas} guia(s) baixada(s); {Falhas.Count} NÃO: " + string.Join(" ", Falhas.Select(f => $"[{f.CodigoId}] {f.Falha}"));
}

/// <summary>O que a operadora disse de UMA guia do lote, na resposta do envio ou do status.</summary>
/// <param name="NumeroGuiaPrestador">O número que a clínica mandou: é por ele que a guia é achada.</param>
public sealed record ResultadoDaGuiaTiss(
    string NumeroGuiaPrestador, string? NumeroGuiaOperadora, SituacaoGuiaTiss Situacao, string? Motivo = null);

/// <summary>
/// A conferência de um pedido de envio, ANTES de ele entrar na fila: o que impede o lote
/// inteiro e o que impede cada guia. A tela mostra a frase na linha da guia, antes do clique.
/// </summary>
public sealed record ConferenciaDeEnvioTiss
{
    /// <summary>O que impede o lote como um todo (vazio, mais de 100…). Nulo quando não há.</summary>
    public string? Impedimento { get; init; }

    /// <summary>A recusa de cada guia, pela guia. Guia que pode ir não aparece aqui.</summary>
    public IReadOnlyDictionary<int, string> PorGuia { get; init; } = new Dictionary<int, string>();

    public bool PodeEnviar => Impedimento is null && PorGuia.Count == 0;

    /// <summary>Tudo numa frase só, para quem não mostra por linha.</summary>
    public string Resumo => Impedimento
        ?? (PorGuia.Count == 0 ? "O lote pode ser enviado."
            : $"{PorGuia.Count} guia(s) não podem ir neste envio: " + string.Join(" ", PorGuia.Select(p => $"[{p.Key}] {p.Value}")));
}

/// <summary>
/// O pedido de envio foi recusado. A mensagem é o resumo; <see cref="Conferencia"/> traz a
/// recusa de cada guia para a tela mostrar por linha. Nada entrou na fila.
/// </summary>
public sealed class EnvioTissRecusadoException(ConferenciaDeEnvioTiss conferencia) : InvalidOperationException(conferencia.Resumo)
{
    public ConferenciaDeEnvioTiss Conferencia { get; } = conferencia;
}
