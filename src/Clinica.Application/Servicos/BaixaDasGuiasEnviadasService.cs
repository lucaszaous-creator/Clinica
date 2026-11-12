using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// A baixa em lote das guias que a operadora RECEBEU pelo web service (nov/2026, pedido do dono:
/// "se for envio em lote precisamos nos certificar da baixa em lote também").
///
/// Quem chama é a TELA, com quem está logado — nunca o conector, que não tem sessão de ninguém.
/// A baixa é a MESMA de sempre (<see cref="FaturamentoService.DarBaixaAsync"/>): a crítica do
/// formato do número e a exigência da guia ANS valem aqui como em qualquer porta.
///
/// O que este serviço garante:
/// <list type="bullet">
/// <item>só alcança guia <b>recebida</b> pela operadora e ainda <b>sem baixa</b> — glosada, sem
/// resposta, falha e cancelada ficam de fora, à vista, para alguém olhar;</item>
/// <item>chamar duas vezes não baixa duas vezes: a segunda chamada não acha mais as que a
/// primeira baixou;</item>
/// <item>a falha é POR GUIA: uma recusa (o formato do número, outra máquina que baixou antes) não
/// aborta as outras nem passa calada — volta na lista, e a guia continua pendente.</item>
/// </list>
/// </summary>
public sealed class BaixaDasGuiasEnviadasService(IClinicaRepositorio repo, FilaTissService fila, FaturamentoService faturamento)
{
    /// <summary>
    /// Dá baixa em cada guia recebida do envio. O número da baixa é o que a operadora devolveu;
    /// quando ela não devolve, o número que a clínica mandou. A observação diz de onde veio.
    /// </summary>
    public async Task<BaixaDasGuiasEnviadas> BaixarRecebidasAsync(
        int transacaoId, DateOnly dataBaixa, string operador, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(operador))
            throw new InvalidOperationException("A baixa leva quem a fez: faça login antes.");
        var quem = operador.Trim();

        var resultado = new List<BaixaDeGuiaEnviada>();
        foreach (var guia in await fila.GuiasRecebidasSemBaixaAsync(transacaoId, ct))
        {
            var numero = NumeroDaBaixa(guia);
            try
            {
                // A guia ANS já foi conferida ANTES do envio: a baixa a reapresenta como está gravada.
                var codigo = await repo.ObterCodigoAsync(guia.CodigoId, ct)
                    ?? throw new InvalidOperationException("Guia não encontrada: pode ter sido estornada noutra máquina.");
                var guiaAns = codigo.GuiaAns is { } gravada ? GuiaAnsNaBaixa.DaGuia(gravada) : null;
                await faturamento.DarBaixaAsync(guia.CodigoId, dataBaixa, numero, quem, Observacao(guia), ct, guiaAns);
                resultado.Add(new BaixaDeGuiaEnviada(guia.CodigoId, numero, null));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Recusa de regra (formato do número do convênio) ou concorrência: fica na linha.
                Diagnostico.Registrar($"Baixa das guias enviadas — guia do código {guia.CodigoId}", ex);
                // O que a tentativa deixou rastreado não pode ir junto na gravação da próxima guia.
                repo.DescartarAlteracoesPendentes();
                resultado.Add(new BaixaDeGuiaEnviada(guia.CodigoId, numero, ex.Message));
            }
        }
        return new BaixaDasGuiasEnviadas(resultado);
    }

    /// <summary>
    /// O número com que a guia recebe baixa: o da operadora quando ela devolveu, senão o número
    /// que a clínica mandou (a chave com que a operadora acha a guia naquele protocolo).
    /// </summary>
    public static string NumeroDaBaixa(GuiaTissRecebidaSemBaixa guia)
        => string.IsNullOrWhiteSpace(guia.NumeroGuiaOperadora) ? guia.NumeroGuiaPrestador : guia.NumeroGuiaOperadora.Trim();

    /// <summary>A observação da baixa: de onde veio o número e qual o protocolo.</summary>
    public static string Observacao(GuiaTissRecebidaSemBaixa guia)
    {
        var protocolo = string.IsNullOrWhiteSpace(guia.Protocolo) ? "sem número" : guia.Protocolo;
        return string.IsNullOrWhiteSpace(guia.NumeroGuiaOperadora)
            ? $"enviada pelo web service, protocolo {protocolo}; a operadora não devolveu o número dela: baixa com o número enviado ({guia.NumeroGuiaPrestador})"
            : $"enviada pelo web service, protocolo {protocolo}; número enviado {guia.NumeroGuiaPrestador}";
    }
}
