using Clinica.Domain.Entities;

namespace Clinica.Tiss.Conector;

/// <summary>
/// Envio de guia, status de protocolo e cancelamento AINDA NÃO falam com a operadora.
///
/// As mensagens deles são escritas noutra frente (branch <c>tiss/b-mensagens</c>:
/// <c>TissLoteGuiasWs</c>, <c>TissStatusProtocoloWs</c>, <c>TissCancelaGuiaWs</c>), que não
/// existia quando este arquivo foi escrito. Até a ligação, cada um responde "ainda não ligado"
/// — a transação termina como falha, com a frase, e NADA sai para a operadora.
///
/// TODO(tiss-b-mensagens): trocar cada classe abaixo por um processador de verdade, no molde do
/// <see cref="ProcessadorDeElegibilidade"/>:
///   PrepararAsync → credencial + DadosDoConvenio.LerAsync(…, Tipo, …) + TissLoteGuiasWs.MontarEnvelope(PedidoLoteGuiasTiss)
///   Ler           → TissLoteGuiasWs.LerResposta(xml): Recebido == true → DesfechoTiss.Concluir com o
///                   Protocolo e, em ConclusaoTransacaoTiss.Guias, UM ResultadoDaGuiaTiss por guia da
///                   resposta (NumeroGuiaPrestador, NumeroGuiaOperadora, Recebida ou Glosada + a glosa);
///                   senão DesfechoTiss.Falhar(Falha).
/// O envio é um LOTE: transacao.Guias traz de 1 a 100 itens, cada um com o NumeroGuiaPrestador que
/// tem de ir na guia daquele item — é por esse número que a fila casa a resposta com a guia.
/// A consulta de status devolve a mesma lista em Guias, e a fila atualiza os itens do envio
/// consultado (TransacaoDeEnvioId).
/// O executor já carimba o envio antes da rede, guarda o XML sem a senha e aplica as duas
/// chaves de envio: o processador novo não precisa repetir nada disso.
/// </summary>
public abstract class ProcessadorAindaNaoLigado : IProcessadorTransacaoTiss
{
    public abstract TipoTransacaoTiss Tipo { get; }

    public static string Frase(TipoTransacaoTiss tipo)
        => $"{Nome(tipo)} pelo web service ainda não foi ligado nesta versão do conector. Nada foi enviado à operadora.";

    public Task<PreparoTiss> PrepararAsync(TransacaoTiss transacao, CancellationToken ct)
        => Task.FromResult(PreparoTiss.Falhar(Frase(Tipo)));

    public DesfechoTiss Ler(TransacaoTiss transacao, string xmlDaResposta) => DesfechoTiss.Falhar(Frase(Tipo));

    private static string Nome(TipoTransacaoTiss tipo) => tipo switch
    {
        TipoTransacaoTiss.EnvioGuia => "O envio de guia",
        TipoTransacaoTiss.StatusProtocolo => "A consulta de protocolo",
        TipoTransacaoTiss.CancelamentoGuia => "O cancelamento de guia",
        _ => "Esta transação"
    };
}

public sealed class ProcessadorDeEnvioDeGuia : ProcessadorAindaNaoLigado
{
    public override TipoTransacaoTiss Tipo => TipoTransacaoTiss.EnvioGuia;
}

public sealed class ProcessadorDeStatusDeProtocolo : ProcessadorAindaNaoLigado
{
    public override TipoTransacaoTiss Tipo => TipoTransacaoTiss.StatusProtocolo;
}

public sealed class ProcessadorDeCancelamentoDeGuia : ProcessadorAindaNaoLigado
{
    public override TipoTransacaoTiss Tipo => TipoTransacaoTiss.CancelamentoGuia;
}
