using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Clinica.Tiss.Conector;

/// <summary>
/// Uma volta do conector: reserva UMA transação, leva-a à operadora e grava o desfecho.
///
/// A ordem é o que importa, e por isso ela mora num lugar só:
/// <list type="number">
/// <item>reservar (só um conector fica com a linha);</item>
/// <item>se a transação ESCREVE na operadora, conferir as DUAS chaves de envio — a do servidor
/// e a do convênio. Qualquer uma desligada: recusa com a frase, e nada sai;</item>
/// <item>preparar a mensagem (o processador do tipo);</item>
/// <item>CARIMBAR o envio e guardar o XML sem a senha — antes da rede;</item>
/// <item>ir à rede;</item>
/// <item>ler a resposta e gravar: concluída, falhou ou (só quem apenas pergunta) de volta à fila.</item>
/// </list>
/// O log diz o número da transação, o tipo, o convênio e o desfecho. Nunca carteira, nome,
/// login ou senha.
/// </summary>
public sealed class ExecutorDeTransacoesTiss(
    FilaTissService fila, CredenciaisTissService credenciais, IEnumerable<IProcessadorTransacaoTiss> processadores,
    TissWebserviceCliente transporte, OpcoesDoConector opcoes, ILogger<ExecutorDeTransacoesTiss> log)
{
    public const string FraseEnvioDesligadoNoServidor =
        "O envio pelo web service está desligado neste servidor (TISS_ENVIO_HABILITADO). Nada foi enviado à operadora.";

    public static string FraseEnvioDesligadoNoConvenio(string convenio)
        => $"O envio pelo web service está desligado para {convenio}. Nada foi enviado à operadora.";

    /// <summary>Processa a próxima transação da fila. Falso = a fila está vazia.</summary>
    public async Task<bool> ProcessarProximaAsync(CancellationToken ct = default)
    {
        var t = await fila.ReservarProximaAsync(opcoes.Consumidor, ct);
        if (t is null) return false;

        IReadOnlyList<string> segredos = [];
        try
        {
            if (t.EscreveNaOperadora)
            {
                if (!opcoes.EnvioHabilitado)
                {
                    await RecusarAsync(t, FraseEnvioDesligadoNoServidor, ct);
                    return true;
                }
                if (!await credenciais.EnvioLigadoAsync(t.ConvenioCodigo, ct))
                {
                    await RecusarAsync(t, FraseEnvioDesligadoNoConvenio(t.ConvenioCodigo), ct);
                    return true;
                }
            }

            var processador = processadores.FirstOrDefault(p => p.Tipo == t.Tipo);
            if (processador is null)
            {
                await FalharAsync(t, $"Este conector não sabe processar {t.Tipo}.", podeTentarDeNovo: false, null, segredos, ct);
                return true;
            }

            var preparo = await processador.PrepararAsync(t, ct);
            segredos = preparo.Segredos;
            if (preparo.Recusa is { } recusa)
            {
                await RecusarAsync(t, recusa, ct);
                return true;
            }
            if (preparo.Falha is { } falha || preparo.Envelope is null || preparo.Endereco is null)
            {
                await FalharAsync(t, preparo.Falha ?? "O preparo não produziu mensagem.", podeTentarDeNovo: false, null, segredos, ct);
                return true;
            }

            // A partir desta linha a mensagem pode ter chegado à operadora.
            await fila.MarcarEnvioIniciadoAsync(t.Id, preparo.Envelope, ct);
            var resposta = await transporte.EnviarAsync(preparo.Endereco, preparo.Envelope, ct);
            if (string.IsNullOrWhiteSpace(resposta.Corpo))
            {
                // Rede: quem só pergunta tenta de novo; quem escreve, a fila não deixa repetir.
                await FalharAsync(t, resposta.Falha ?? "sem resposta da operadora", podeTentarDeNovo: true, null, segredos, ct);
                return true;
            }

            var desfecho = processador.Ler(t, resposta);
            if (desfecho.Conclusao is { } conclusao)
            {
                await fila.ConcluirAsync(t.Id, conclusao with
                {
                    Resultado = Ocultar(conclusao.Resultado, segredos),
                    XmlRecebido = conclusao.XmlRecebido ?? resposta.Corpo
                }, ct);
                log.LogInformation("Transação TISS {Id} ({Tipo}, {Convenio}) concluída.", t.Id, t.Tipo, t.ConvenioCodigo);
            }
            else
            {
                // Veio um corpo. Recusa clara ou resposta que não se entende, a mesma mensagem NÃO é
                // repetida: não muda a resposta, e login recusado repetido pode bloquear o acesso da
                // clínica. Quando não se sabe se a operadora fez o que se pediu e a transação ESCREVE
                // nela, a frase manda conferir no portal antes de qualquer novo pedido.
                var motivo = desfecho.Falha ?? "resposta não reconhecida";
                if (desfecho.Incerto && t.EscreveNaOperadora) motivo += " " + FilaTiss.FraseSemNovaTentativa;
                await FalharAsync(t, motivo, podeTentarDeNovo: false, resposta.Corpo, segredos, ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // O serviço está parando: a reserva vence sozinha e a fila decide o que fazer com a linha.
            throw;
        }
        catch (Exception ex)
        {
            // Só o TIPO da exceção vai para o log e para a linha: a mensagem dela pode carregar
            // pedaços do pedido. O detalhe que importa (o campo fora do padrão) vem pelo preparo.
            log.LogError("Transação TISS {Id} ({Tipo}): falha inesperada {Excecao}.", t.Id, t.Tipo, ex.GetType().Name);
            try
            {
                await FalharAsync(t, "Falha inesperada no conector (" + ex.GetType().Name + ").", podeTentarDeNovo: true, null, segredos, ct);
            }
            catch (Exception aoGravar)
            {
                log.LogError("Transação TISS {Id}: o desfecho não pôde ser gravado ({Excecao}). A reserva vence sozinha.", t.Id, aoGravar.GetType().Name);
            }
        }
        return true;
    }

    private async Task RecusarAsync(TransacaoTiss t, string motivo, CancellationToken ct)
    {
        await fila.RecusarAsync(t.Id, motivo, ct);
        log.LogWarning("Transação TISS {Id} ({Tipo}, {Convenio}) recusada antes do envio: {Motivo}", t.Id, t.Tipo, t.ConvenioCodigo, motivo);
    }

    private async Task FalharAsync(TransacaoTiss t, string erro, bool podeTentarDeNovo, string? xmlRecebido,
        IReadOnlyList<string> segredos, CancellationToken ct)
    {
        var limpo = Ocultar(erro, segredos);
        var estado = await fila.FalharAsync(t.Id, limpo, podeTentarDeNovo, xmlRecebido, ct);
        log.LogWarning("Transação TISS {Id} ({Tipo}, {Convenio}) sem sucesso: {Motivo} Fica {Estado}.", t.Id, t.Tipo, t.ConvenioCodigo, limpo, estado);
    }

    /// <summary>Troca por asteriscos qualquer segredo que tenha escapado para um texto.</summary>
    public static string Ocultar(string texto, IReadOnlyList<string> segredos)
    {
        foreach (var segredo in segredos)
            if (!string.IsNullOrEmpty(segredo))
                texto = texto.Replace(segredo, "********", StringComparison.OrdinalIgnoreCase);
        return texto;
    }
}
