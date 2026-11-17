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
/// <item>elegibilidade: conferir o TETO DIÁRIO do convênio — batido, a transação volta a pendente
/// com a frase do limite, e nada sai (<see cref="FilaTissService.AdiarPorLimiteDiarioAsync"/>);</item>
/// <item>preparar a mensagem (o processador do tipo);</item>
/// <item>esperar o INTERVALO MÍNIMO desde a última ida à mesma operadora
/// (<see cref="IntervaloEntreChamadasTiss"/>) — antes do carimbo, para nada sair duas vezes por ela;</item>
/// <item>CARIMBAR o envio e guardar o XML sem a senha — antes da rede;</item>
/// <item>ir à rede (e anotar o fim da ida, para o intervalo);</item>
/// <item>ler a resposta e gravar: concluída, falhou ou (só quem apenas pergunta) de volta à fila.</item>
/// </list>
/// O log diz o número da transação, o tipo, o convênio e o desfecho. Nunca carteira, nome,
/// login ou senha.
/// </summary>
public sealed class ExecutorDeTransacoesTiss(
    FilaTissService fila, CredenciaisTissService credenciais, IEnumerable<IProcessadorTransacaoTiss> processadores,
    TissWebserviceCliente transporte, OpcoesDoConector opcoes, ILogger<ExecutorDeTransacoesTiss> log,
    IntervaloEntreChamadasTiss? intervalo = null)
{
    /// <summary>Sem o intervalo registrado (testes que montam o executor à mão), nenhuma espera.</summary>
    private readonly IntervaloEntreChamadasTiss _intervalo = intervalo ?? new IntervaloEntreChamadasTiss(TimeSpan.Zero);

    public const string FraseEnvioDesligadoNoServidor =
        "O envio pelo web service está desligado neste servidor (TISS_ENVIO_HABILITADO). Nada foi enviado à operadora.";

    /// <summary>
    /// A fila de PRODUÇÃO não vai à homologação: com o convênio em homologação, o pedido das telas é
    /// recusado (nada sai). Quem testa a homologação são os comandos <c>testar-*</c> do servidor.
    /// </summary>
    public static string FraseConvenioEmHomologacao(string convenio)
        => $"{convenio} está em HOMOLOGAÇÃO no servidor (comando ambiente): os pedidos das telas não vão à operadora até voltar para produção. Nada foi enviado.";

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
            if (await credenciais.EmHomologacaoAsync(t.ConvenioCodigo, ct))
            {
                await RecusarAsync(t, FraseConvenioEmHomologacao(t.ConvenioCodigo), ct);
                return true;
            }
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

            // A JANELA de envio da operadora (catálogo): lote fora dela volta à fila agendado, sem
            // gastar tentativa e sem sair nada (Amil: do 1º ao 5º dia útil, 7h–19h; recusa 3091).
            if (t.Tipo == TipoTransacaoTiss.EnvioGuia
                && await fila.AgendarNaJanelaAsync(t.Id, await credenciais.OperadoraAsync(t.ConvenioCodigo, ct), ct: ct))
            {
                log.LogInformation("Transação TISS {Id} ({Tipo}, {Convenio}) fora da janela de envio da operadora: agendada.", t.Id, t.Tipo, t.ConvenioCodigo);
                return true;
            }

            if (t.Tipo == TipoTransacaoTiss.Elegibilidade
                && await fila.AdiarPorLimiteDiarioAsync(t.Id, opcoes.LimiteDiarioElegibilidade, ct))
            {
                log.LogWarning("Transação TISS {Id} ({Tipo}, {Convenio}) adiada: teto diário de {Limite} consultas atingido. Fica Pendente.",
                    t.Id, t.Tipo, t.ConvenioCodigo, opcoes.LimiteDiarioElegibilidade);
                return true;
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

            // O intervalo mínimo desde a última ida a esta operadora: a espera vem ANTES do carimbo.
            var operadora = preparo.Endereco.Host;
            await _intervalo.AguardarVezAsync(operadora, ct);

            // A codificação da mensagem é a da operadora no catálogo (Amil: UTF-8; o resto: ISO-8859-1).
            // O XML guardado diz a mesma codificação que foi pela rede.
            var codificacao = OperadorasTiss.CodificacaoDe(await credenciais.OperadoraAsync(t.ConvenioCodigo, ct));
            var envelope = TissWebserviceCliente.ComDeclaracao(preparo.Envelope, TissWebserviceCliente.NomeDa(codificacao));

            // A partir desta linha a mensagem pode ter chegado à operadora.
            await fila.MarcarEnvioIniciadoAsync(t.Id, envelope, ct);
            RespostaBrutaTissWs resposta;
            try { resposta = await transporte.EnviarAsync(preparo.Endereco, envelope, ct, codificacao); }
            finally { _intervalo.RegistrarIda(operadora); }
            if (string.IsNullOrWhiteSpace(resposta.Corpo))
            {
                // HTTP 4xx sem mensagem SOAP (415 de formato, 404, 403): a operadora não leu a
                // mensagem e repetir não muda nada — vira "não foi possível" sem nova tentativa. O
                // motivo leva o status e um trecho do corpo, SEM a carteira (Ocultar) e sem segredo.
                if (FilaTiss.RecusaDefinitivaDoHttp(resposta.StatusHttp))
                {
                    var motivo = (resposta.Falha ?? $"a operadora recusou a mensagem (HTTP {resposta.StatusHttp})") + " " + FilaTiss.FraseRecusaDoHttp;
                    await FalharAsync(t, motivo, podeTentarDeNovo: false, null, [.. segredos, t.NumeroCarteira ?? string.Empty], ct);
                    return true;
                }
                // Rede ou 5xx sem corpo: quem só pergunta tenta de novo; quem escreve, a fila não deixa repetir.
                await FalharAsync(t, resposta.Falha ?? "sem resposta da operadora", podeTentarDeNovo: true, null, segredos, ct);
                return true;
            }

            var desfecho = processador.Ler(t, resposta);
            if (desfecho.RecusaForaDaJanela
                && await fila.AgendarNaJanelaAsync(t.Id, await credenciais.OperadoraAsync(t.ConvenioCodigo, ct), Ocultar(desfecho.Falha ?? "", segredos), ct))
            {
                log.LogWarning("Transação TISS {Id} ({Tipo}, {Convenio}): a operadora recusou fora da janela de envio. Agendada para a próxima, sem gastar tentativa.",
                    t.Id, t.Tipo, t.ConvenioCodigo);
                return true;
            }
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
                // HTTP de erro com página que não é SOAP (502 do gateway…): o log leva o status e um
                // trecho do corpo, sem a carteira e sem segredo — "não é XML" sozinho não deixa diagnosticar.
                if (resposta.StatusHttp >= 400 && !TissWebserviceCliente.PareceSoap(resposta.Corpo!))
                    log.LogWarning("Transação TISS {Id} ({Tipo}, {Convenio}): HTTP {Status} sem mensagem TISS. Corpo: {Trecho}",
                        t.Id, t.Tipo, t.ConvenioCodigo, resposta.StatusHttp,
                        Ocultar(TissWebserviceCliente.Trecho(resposta.Corpo!), [.. segredos, t.NumeroCarteira ?? string.Empty]));
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
