using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;

namespace Clinica.Tiss.Conector;

/// <summary>
/// A AUTORIZAÇÃO pelo web service (nov/2026, Amil, TISS 4.03.00). A MESMA transação faz as duas idas:
/// com a autorização <see cref="SituacaoAutorizacaoTiss.Pedida"/>, o pedido (<c>solicitacaoProcedimento</c>);
/// em análise, a pergunta da situação (<c>solicitacao-status-autorizacao</c>) — a fila a devolve com
/// intervalo e teto (<see cref="FilaTiss.TetoDeConsultasDaAutorizacao"/>). O conector lê só a fotografia
/// do pedido guardada na autorização, nunca a ficha do paciente.
/// </summary>
public sealed class ProcessadorDeAutorizacao(
    IClinicaRepositorio repo, ParametrosService parametros, CredenciaisTissService credenciais, Func<DateTime> agora)
    : IProcessadorTransacaoTiss
{
    public TipoTransacaoTiss Tipo => TipoTransacaoTiss.Autorizacao;

    public async Task<PreparoTiss> PrepararAsync(TransacaoTiss transacao, CancellationToken ct)
    {
        if (transacao.AutorizacaoId is not { } id || await repo.ObterAutorizacaoTissAsync(id, ct) is not { } a)
            return PreparoTiss.Falhar("O pedido de autorização veio sem a autorização.");
        if (a.Situacao is not (SituacaoAutorizacaoTiss.Pedida or SituacaoAutorizacaoTiss.EmAnalise))
            return PreparoTiss.Recusar("A autorização não está mais pendente (horário desmarcado ou cancelada): nada foi à operadora.");

        var perguntaSituacao = a.Situacao == SituacaoAutorizacaoTiss.EmAnalise;
        // A pergunta da situação vai ao endereço e na versão DELA (outro endpoint na Amil).
        var paraAbrir = perguntaSituacao ? CopiaComTipo(transacao, TipoTransacaoTiss.StatusAutorizacao) : transacao;
        var (remetente, endereco, senhaMd5, recusa) = await DadosDoConvenio.AbrirAsync(repo, parametros, credenciais, paraAbrir, ct);
        if (recusa is not null) return recusa;
        var momento = agora();
        // Único por ida: a mesma transação vai várias vezes (pedido + perguntas), e a tentativa volta a zero.
        var sequencial = $"{transacao.Id % 100_000_000:D8}{a.ConsultasDeStatus % 100:D2}{transacao.Tentativas % 100:D2}";
        try
        {
            if (perguntaSituacao)
                return PreparoTiss.Enviar(endereco!, TissAutorizacaoWs.MontarEnvelopeStatus(new PedidoStatusAutorizacaoTiss
                {
                    Remetente = remetente!, Sequencial = sequencial, Momento = momento,
                    NumeroGuiaPrestador = a.NumeroGuiaPrestador, NumeroCarteira = a.NumeroCarteira
                }), senhaMd5!);

            var prestador = await parametros.ObterPrestadorAsync(ct);
            return PreparoTiss.Enviar(endereco!, TissAutorizacaoWs.MontarEnvelope(new PedidoAutorizacaoTiss
            {
                Remetente = remetente!, Sequencial = sequencial, Momento = momento,
                NumeroGuiaPrestador = a.NumeroGuiaPrestador, NumeroCarteira = a.NumeroCarteira,
                NomeContratado = prestador.RazaoSocial ?? prestador.NomeFantasia ?? string.Empty,
                Cnes = string.IsNullOrWhiteSpace(prestador.Cnes) ? GuiaAnsNaBaixa.CnesDeQuemNaoTem : prestador.Cnes.Trim(),
                NomeProfissional = a.ProfissionalNome, ConselhoProfissional = a.ConselhoProfissional,
                NumeroConselho = a.NumeroConselho, UfConselho = a.UfConselho, Cbos = a.Cbos,
                CaraterAtendimento = a.CaraterAtendimento, DataSolicitacao = DateOnly.FromDateTime(momento),
                CodigoTabela = a.CodigoTabela, CodigoProcedimento = a.CodigoProcedimento,
                DescricaoProcedimento = a.DescricaoProcedimento, Quantidade = a.Quantidade
            }), senhaMd5!);
        }
        catch (ArgumentException ex)
        {
            return PreparoTiss.Recusar("Pedido de autorização recusado antes do envio: " + ex.Message);
        }
    }

    public DesfechoTiss Ler(TransacaoTiss transacao, RespostaBrutaTissWs bruta)
    {
        var r = TissAutorizacaoWs.LerResposta(bruta);
        // Sem resposta em que confiar: "não se sabe" — nunca "negado".
        if (!r.Autorizada && !r.EmAnalise && !r.Negada && !r.Cancelada) return DesfechoTiss.NaoSeSabe(r.Resumo);
        var situacao = r.Autorizada ? SituacaoAutorizacaoTiss.Autorizada
            : r.EmAnalise ? SituacaoAutorizacaoTiss.EmAnalise
            : r.Cancelada ? SituacaoAutorizacaoTiss.Cancelada
            : SituacaoAutorizacaoTiss.Negada;
        return DesfechoTiss.Concluir(new ConclusaoTransacaoTiss
        {
            Resultado = r.Resumo,
            NumeroGuiaOperadora = r.NumeroGuiaOperadora,
            XmlRecebido = bruta.Corpo,
            Autorizacao = new DesfechoDaAutorizacaoTiss(situacao, r.Senha, r.DataAutorizacao, r.ValidadeSenha, r.NumeroGuiaOperadora,
                situacao is SituacaoAutorizacaoTiss.Negada or SituacaoAutorizacaoTiss.Cancelada ? r.Resumo : null)
        });
    }

    private static TransacaoTiss CopiaComTipo(TransacaoTiss t, TipoTransacaoTiss tipo)
        => new() { Id = t.Id, Tipo = tipo, ConvenioCodigo = t.ConvenioCodigo, Tentativas = t.Tentativas };
}

/// <summary>
/// O horário foi desmarcado com a senha emitida: pede à operadora o cancelamento da GUIA DE SOLICITAÇÃO
/// (<c>cancelaGuia</c> com <c>tipoGuia</c> 1). O XSD 4.03.00 não tem cancelamento pela senha: a guia
/// de solicitação é achada pelo número dela (no prestador e, quando veio, na operadora).
/// </summary>
public sealed class ProcessadorDeCancelamentoDeAutorizacao(
    IClinicaRepositorio repo, ParametrosService parametros, CredenciaisTissService credenciais, Func<DateTime> agora)
    : IProcessadorTransacaoTiss
{
    public TipoTransacaoTiss Tipo => TipoTransacaoTiss.CancelaAutorizacao;

    public async Task<PreparoTiss> PrepararAsync(TransacaoTiss transacao, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(transacao.NumeroGuiaPrestador))
            return PreparoTiss.Falhar("O pedido de cancelamento da autorização veio sem o número da guia de solicitação.");
        var (remetente, endereco, senhaMd5, recusa) = await DadosDoConvenio.AbrirAsync(repo, parametros, credenciais, transacao, ct);
        if (recusa is not null) return recusa;
        try
        {
            return PreparoTiss.Enviar(endereco!, TissCancelaGuiaWs.MontarEnvelope(new PedidoCancelaGuiaTiss
            {
                Remetente = remetente!, Sequencial = DadosDoConvenio.Sequencial(transacao), Momento = agora(),
                Guias =
                [
                    new GuiaACancelarTiss
                    {
                        TipoGuia = TissCancelaGuiaWs.TipoGuiaSolicitacao,
                        NumeroGuiaPrestador = transacao.NumeroGuiaPrestador.Trim(),
                        NumeroGuiaOperadora = string.IsNullOrWhiteSpace(transacao.NumeroGuiaOperadora) ? null : transacao.NumeroGuiaOperadora.Trim()
                    }
                ]
            }), senhaMd5!);
        }
        catch (ArgumentException ex)
        {
            return PreparoTiss.Recusar("Cancelamento da autorização recusado antes do envio: " + ex.Message);
        }
    }

    public DesfechoTiss Ler(TransacaoTiss transacao, RespostaBrutaTissWs bruta)
    {
        var resposta = TissCancelaGuiaWs.LerResposta(bruta);
        return resposta.Cancelado switch
        {
            true => DesfechoTiss.Concluir(new ConclusaoTransacaoTiss
            {
                Resultado = resposta.Resumo, XmlRecebido = bruta.Corpo,
                Autorizacao = new DesfechoDaAutorizacaoTiss(SituacaoAutorizacaoTiss.Cancelada, Motivo: "Horário desmarcado: autorização cancelada na operadora.")
            }),
            false => DesfechoTiss.Falhar(resposta.Resumo),
            null => DesfechoTiss.NaoSeSabe(resposta.Resumo)
        };
    }
}
