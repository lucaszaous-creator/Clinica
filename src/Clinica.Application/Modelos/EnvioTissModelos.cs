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
