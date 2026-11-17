using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;

namespace Clinica.Tiss.Conector;

/// <summary>
/// ENVIO DE LOTE (<c>tissLoteGuias</c>): uma guia de consulta por item da transação, cada uma com
/// o número que a fila reservou para ela (<c>TransacaoTissGuia.NumeroGuiaPrestador</c>) — é por
/// esse número que a resposta acha a guia de volta.
///
/// Como a resposta vira desfecho (as decisões são as das mensagens, <see cref="TissLoteGuiasWs"/>):
/// <list type="bullet">
/// <item><c>Recebido = true</c>: concluída, com o protocolo e o resultado POR GUIA (recebida, ou
/// glosada com a glosa na linha);</item>
/// <item><c>Recebido = false</c> (a operadora devolveu <c>mensagemErro</c> no lugar do protocolo):
/// ela respondeu que NÃO — falha, sem nova tentativa, e as guias ficam livres para um pedido novo;</item>
/// <item><c>Recebido = null</c> (falha de login ou de schema, erro SOAP, página de erro): não se
/// sabe — falha com a frase que manda conferir no portal, e NUNCA reenvio.</item>
/// </list>
/// </summary>
public sealed class ProcessadorDeEnvioDeGuia(
    IClinicaRepositorio repo, ParametrosService parametros, CredenciaisTissService credenciais, Func<DateTime> agora)
    : IProcessadorTransacaoTiss
{
    public TipoTransacaoTiss Tipo => TipoTransacaoTiss.EnvioGuia;

    public async Task<PreparoTiss> PrepararAsync(TransacaoTiss transacao, CancellationToken ct)
    {
        if (transacao.Guias.Count == 0)
            return PreparoTiss.Falhar("O pedido de envio veio sem guia nenhuma.");

        var (remetente, endereco, senhaMd5, recusa) = await DadosDoConvenio.AbrirAsync(repo, parametros, credenciais, transacao, ct);
        if (recusa is not null) return recusa;

        var ids = transacao.Guias.Where(g => g.CodigoId is not null).Select(g => g.CodigoId!.Value).ToList();
        // Só a guia e a conferência dela: a ficha do paciente não é lida pelo conector.
        var codigos = (await repo.GuiasConferidasParaEnvioTissAsync(ids, ct)).ToDictionary(c => c.Id);

        // Entre o pedido e o envio a guia pode ter mudado noutra máquina (baixa manual, estorno da
        // conferência): confere de novo ANTES de qualquer coisa sair. Uma guia fora recusa o lote.
        var guias = new List<System.Xml.Linq.XElement>();
        foreach (var item in transacao.Guias.OrderBy(g => g.Id))
        {
            if (item.CodigoId is not { } id || !codigos.TryGetValue(id, out var codigo))
                return PreparoTiss.Recusar($"A guia {item.NumeroGuiaPrestador} não existe mais no sistema. Nada foi enviado: monte o lote de novo.");
            if (codigo.Baixado)
                return PreparoTiss.Recusar($"A guia {item.NumeroGuiaPrestador} recebeu baixa depois do pedido. Nada foi enviado: monte o lote de novo.");
            if (codigo.GuiaAns is null)
                return PreparoTiss.Recusar($"A guia {item.NumeroGuiaPrestador} está sem a conferência no padrão ANS. Nada foi enviado: confira e monte o lote de novo.");
            try
            {
                guias.Add(TissExportService.GuiaConsultaConferida(codigo, item.NumeroGuiaPrestador));
            }
            catch (ArgumentException ex)
            {
                return PreparoTiss.Falhar("Guia recusada antes do envio: " + ex.Message);
            }
        }

        try
        {
            return PreparoTiss.Enviar(endereco!, TissLoteGuiasWs.MontarEnvelope(new PedidoLoteGuiasTiss
            {
                Remetente = remetente!,
                Sequencial = DadosDoConvenio.Sequencial(transacao),
                Momento = agora(),
                NumeroLote = DadosDoConvenio.NumeroDoLote(transacao),
                Guias = guias
            }), senhaMd5!);
        }
        catch (ArgumentException ex)
        {
            return PreparoTiss.Falhar("Lote recusado antes do envio: " + ex.Message);
        }
    }

    public DesfechoTiss Ler(TransacaoTiss transacao, RespostaBrutaTissWs bruta)
    {
        var resposta = TissLoteGuiasWs.LerResposta(bruta);
        return resposta.Recebido switch
        {
            true => DesfechoTiss.Concluir(new ConclusaoTransacaoTiss
            {
                Resultado = ResultadosPorGuia.Frase(resposta.Resumo, resposta.Guias),
                Protocolo = resposta.Protocolo,
                // Com uma guia só, o número dela na operadora também fica na transação.
                NumeroGuiaOperadora = resposta.Guias.Count == 1 ? resposta.Guias[0].NumeroGuiaOperadora : null,
                XmlRecebido = bruta.Corpo,
                Guias = ResultadosPorGuia.De(resposta.Guias)
            }),
            false => DesfechoTiss.Falhar(resposta.Resumo),
            null => DesfechoTiss.NaoSeSabe(resposta.Resumo)
        };
    }
}

/// <summary>
/// SITUAÇÃO DO PROTOCOLO (<c>tissSolicitacaoStatusProtocolo</c>): pergunta pelo protocolo e, quando a
/// consulta veio de um envio (<c>TransacaoDeEnvioId</c>), a fila atualiza as MESMAS guias do lote.
/// ⚠️ O padrão não traz situação por guia: cada guia volta com os números, os valores e as glosas.
/// Guia citada sem glosa conta como recebida; com glosa, glosada.
/// </summary>
public sealed class ProcessadorDeStatusDeProtocolo(
    IClinicaRepositorio repo, ParametrosService parametros, CredenciaisTissService credenciais, Func<DateTime> agora)
    : IProcessadorTransacaoTiss
{
    public TipoTransacaoTiss Tipo => TipoTransacaoTiss.StatusProtocolo;

    public async Task<PreparoTiss> PrepararAsync(TransacaoTiss transacao, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(transacao.Protocolo))
            return PreparoTiss.Falhar("O pedido de situação veio sem o número do protocolo.");
        var (remetente, endereco, senhaMd5, recusa) = await DadosDoConvenio.AbrirAsync(repo, parametros, credenciais, transacao, ct);
        if (recusa is not null) return recusa;
        try
        {
            return PreparoTiss.Enviar(endereco!, TissStatusProtocoloWs.MontarEnvelope(new PedidoStatusProtocoloTiss
            {
                Remetente = remetente!,
                Sequencial = DadosDoConvenio.Sequencial(transacao),
                Momento = agora(),
                NumeroProtocolo = transacao.Protocolo.Trim()
            }), senhaMd5!);
        }
        catch (ArgumentException ex)
        {
            return PreparoTiss.Falhar("Pedido recusado antes do envio: " + ex.Message);
        }
    }

    public DesfechoTiss Ler(TransacaoTiss transacao, RespostaBrutaTissWs bruta)
    {
        var resposta = TissStatusProtocoloWs.LerResposta(bruta);
        return resposta.Localizado switch
        {
            true => DesfechoTiss.Concluir(new ConclusaoTransacaoTiss
            {
                Resultado = ResultadosPorGuia.Frase(resposta.Resumo, resposta.Guias),
                XmlRecebido = bruta.Corpo,
                Guias = ResultadosPorGuia.De(resposta.Guias)
            }),
            false => DesfechoTiss.Falhar(resposta.Resumo),
            null => DesfechoTiss.NaoSeSabe(resposta.Resumo)
        };
    }
}

/// <summary>
/// CANCELAMENTO DE GUIA (<c>tissCancelaGuia</c>): cancela, na operadora, UMA guia já enviada. Só é
/// "cancelada" com o recibo dizendo isso; recusa é falha; "em processamento" é não se sabe — e, como
/// todo pedido que escreve na operadora, não é repetido sozinho.
/// </summary>
public sealed class ProcessadorDeCancelamentoDeGuia(
    IClinicaRepositorio repo, ParametrosService parametros, CredenciaisTissService credenciais, Func<DateTime> agora)
    : IProcessadorTransacaoTiss
{
    public TipoTransacaoTiss Tipo => TipoTransacaoTiss.CancelamentoGuia;

    public async Task<PreparoTiss> PrepararAsync(TransacaoTiss transacao, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(transacao.NumeroGuiaPrestador))
            return PreparoTiss.Falhar("O pedido de cancelamento veio sem o número da guia.");
        var (remetente, endereco, senhaMd5, recusa) = await DadosDoConvenio.AbrirAsync(repo, parametros, credenciais, transacao, ct);
        if (recusa is not null) return recusa;
        try
        {
            return PreparoTiss.Enviar(endereco!, TissCancelaGuiaWs.MontarEnvelope(new PedidoCancelaGuiaTiss
            {
                Remetente = remetente!,
                Sequencial = DadosDoConvenio.Sequencial(transacao),
                Momento = agora(),
                Guias =
                [
                    new GuiaACancelarTiss
                    {
                        NumeroGuiaPrestador = transacao.NumeroGuiaPrestador.Trim(),
                        NumeroProtocolo = string.IsNullOrWhiteSpace(transacao.Protocolo) ? null : transacao.Protocolo.Trim()
                    }
                ]
            }), senhaMd5!);
        }
        catch (ArgumentException ex)
        {
            return PreparoTiss.Falhar("Pedido recusado antes do envio: " + ex.Message);
        }
    }

    public DesfechoTiss Ler(TransacaoTiss transacao, RespostaBrutaTissWs bruta)
    {
        var resposta = TissCancelaGuiaWs.LerResposta(bruta);
        return resposta.Cancelado switch
        {
            true => DesfechoTiss.Concluir(new ConclusaoTransacaoTiss
            {
                Resultado = resposta.Resumo,
                NumeroGuiaOperadora = resposta.Guias.FirstOrDefault()?.NumeroGuiaOperadora,
                XmlRecebido = bruta.Corpo
            }),
            false => DesfechoTiss.Falhar(resposta.Resumo),
            null => DesfechoTiss.NaoSeSabe(resposta.Resumo)
        };
    }
}

/// <summary>Como a guia que a operadora devolve (no protocolo e na situação) vira o resultado da linha.</summary>
public static class ResultadosPorGuia
{
    public static IReadOnlyList<ResultadoDaGuiaTiss> De(IReadOnlyList<GuiaNoProtocoloTiss> guias)
        => guias.Select(g => new ResultadoDaGuiaTiss(
            g.NumeroGuiaPrestador,
            g.NumeroGuiaOperadora,
            g.Glosada ? SituacaoGuiaTiss.Glosada : SituacaoGuiaTiss.Recebida,
            g.Glosada ? Motivo(g) : null)).ToList();

    /// <summary>A frase do lote com a conta das glosadas, quando há.</summary>
    public static string Frase(string resumo, IReadOnlyList<GuiaNoProtocoloTiss> guias)
    {
        var glosadas = guias.Count(g => g.Glosada);
        return glosadas == 0 ? resumo : $"{resumo} {glosadas} com glosa: confira a linha de cada uma.";
    }

    private static string Motivo(GuiaNoProtocoloTiss guia)
        => string.Join("; ", guia.Glosas.Concat(guia.GlosasDeProcedimento)
            .Select(m => string.IsNullOrWhiteSpace(m.Descricao) ? m.Codigo : $"{m.Codigo} - {m.Descricao}"));
}
