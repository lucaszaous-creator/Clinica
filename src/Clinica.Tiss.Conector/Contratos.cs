using Clinica.Application.Servicos;
using Clinica.Domain.Entities;

namespace Clinica.Tiss.Conector;

/// <summary>
/// O que o conector precisa saber de um tipo de transação TISS: montar a mensagem e ler a
/// resposta. Um processador por <see cref="TipoTransacaoTiss"/>.
///
/// O processador NÃO abre conexão e NÃO grava na fila: quem carimba o envio, vai à rede e grava
/// o desfecho é o <see cref="ExecutorDeTransacoesTiss"/>, num lugar só. É assim que a regra
/// "envio de guia nunca é reenviado sozinho" não depende de cada processador lembrar dela.
/// </summary>
public interface IProcessadorTransacaoTiss
{
    TipoTransacaoTiss Tipo { get; }

    /// <summary>Monta a mensagem, ou diz por que ela não vai sair. Nada foi enviado ainda.</summary>
    Task<PreparoTiss> PrepararAsync(TransacaoTiss transacao, CancellationToken ct);

    /// <summary>Lê a resposta da operadora. Nunca lança por conteúdo inesperado.</summary>
    DesfechoTiss Ler(TransacaoTiss transacao, string xmlDaResposta);
}

/// <summary>O resultado do preparo: uma mensagem pronta, uma recusa (nada sai) ou uma falha.</summary>
public sealed class PreparoTiss
{
    private PreparoTiss() { }

    public Uri? Endereco { get; private init; }

    /// <summary>O envelope SOAP completo, COM a senha em MD5: só existe em memória, a caminho da rede.</summary>
    public string? Envelope { get; private init; }

    /// <summary>Recusa: a transação termina como cancelada, com esta frase na tela.</summary>
    public string? Recusa { get; private init; }

    /// <summary>Falha antes de enviar: a transação termina como falha, com este motivo.</summary>
    public string? Falha { get; private init; }

    /// <summary>
    /// Textos que não podem aparecer em log, em erro nem em linha da fila (o MD5 da senha).
    /// O executor os troca por asteriscos em tudo o que guarda ou escreve.
    /// </summary>
    public IReadOnlyList<string> Segredos { get; private init; } = [];

    public static PreparoTiss Enviar(Uri endereco, string envelope, params string[] segredos)
        => new() { Endereco = endereco, Envelope = envelope, Segredos = segredos };

    public static PreparoTiss Recusar(string motivo) => new() { Recusa = motivo };

    public static PreparoTiss Falhar(string motivo) => new() { Falha = motivo };
}

/// <summary>O que a resposta da operadora quer dizer para a fila.</summary>
public sealed class DesfechoTiss
{
    private DesfechoTiss() { }

    public ConclusaoTransacaoTiss? Conclusao { get; private init; }

    public string? Falha { get; private init; }

    public static DesfechoTiss Concluir(ConclusaoTransacaoTiss conclusao) => new() { Conclusao = conclusao };

    /// <summary>A operadora respondeu, mas não com o que se pediu (login recusado, glosa da mensagem…).</summary>
    public static DesfechoTiss Falhar(string motivo) => new() { Falha = motivo };
}

/// <summary>A ida à operadora. Trocada por uma rede de mentira nos testes.</summary>
public interface ITransporteTiss
{
    Task<RespostaDoTransporte> EnviarAsync(Uri endereco, string envelope, CancellationToken ct);
}

/// <summary>
/// O que voltou da rede. <see cref="Corpo"/> nulo ou vazio = a operadora não respondeu (queda,
/// prazo, HTTP de erro sem corpo), e <see cref="FalhaDeRede"/> diz o que houve.
/// </summary>
public sealed record RespostaDoTransporte(int? Status, string? Corpo, string? FalhaDeRede)
{
    public bool OperadoraRespondeu => !string.IsNullOrWhiteSpace(Corpo);
}

/// <summary>As chaves do conector, lidas do ambiente na partida (deploy/tiss-conector/conector.env.example).</summary>
public sealed record OpcoesDoConector
{
    /// <summary>
    /// A chave GERAL de envio neste servidor (<c>TISS_ENVIO_HABILITADO</c>). Desligada, nenhum
    /// envio nem cancelamento sai, esteja o convênio ligado ou não. É a primeira das duas chaves.
    /// </summary>
    public bool EnvioHabilitado { get; init; }

    /// <summary>Segundos de espera quando a fila está vazia.</summary>
    public int IntervaloSegundos { get; init; } = 5;

    /// <summary>Como este conector se identifica na reserva.</summary>
    public string Consumidor { get; init; } = Environment.MachineName + ":" + Environment.ProcessId;

    public static OpcoesDoConector DoAmbiente(Microsoft.Extensions.Configuration.IConfiguration configuracao) => new()
    {
        EnvioHabilitado = string.Equals(configuracao["TISS_ENVIO_HABILITADO"], "true", StringComparison.OrdinalIgnoreCase),
        IntervaloSegundos = int.TryParse(configuracao["TISS_CONECTOR_INTERVALO_SEGUNDOS"], out var s) ? Math.Clamp(s, 1, 300) : 5
    };
}
