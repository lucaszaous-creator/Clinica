using Clinica.Application.Servicos;

namespace Clinica.Application.Abstracoes;

/// <summary>Uma linha da lista do chat: um canal ou uma pessoa (a direta com ela).</summary>
public sealed record ConversaNaLista(string Conversa, string Titulo, string Subtitulo, int NaoLidas,
    string? UltimaPrevia, DateTime? UltimaEm, bool EhCanal, int? PessoaId = null, string Iniciais = "",
    PresencaChat Presenca = PresencaChat.Fora);

/// <summary>Uma mensagem que chegou depois do último número visto — a prévia da bolinha e o som.</summary>
public sealed record NovidadeChat(int Id, string Conversa, string Titulo, string AutorNome, string Previa,
    DateTime EnviadaEm, SomChat Som);

/// <summary>O que a bolinha e a lista precisam numa leitura só.</summary>
public sealed record PainelChat(IReadOnlyList<ConversaNaLista> Canais, IReadOnlyList<ConversaNaLista> Diretas,
    int TotalNaoLidas, int Online, int UltimaMensagemId, IReadOnlyList<NovidadeChat> Novidades);

public sealed record MensagemNaConversa(int Id, int AutorId, string AutorNome, string Iniciais, string Texto,
    DateTime EnviadaEm, DateTime? EditadaEm, bool Minha, bool MeCita,
    int? PacienteId, string? PacienteNome, int? DocumentoId, string? DocumentoRotulo,
    string? ArquivoNome, long? ArquivoTamanho, string? LidaPor);

/// <param name="Instante">A hora da leitura — é o <c>editadasDesde</c> da próxima <see cref="IChatEquipeService.AtualizarAsync"/>.</param>
public sealed record ConversaAberta(string Conversa, string Titulo, string Subtitulo, int? PessoaId,
    IReadOnlyList<MensagemNaConversa> Mensagens, int UltimaLidaAntes, IReadOnlyList<string> Escrevendo, bool TemAnteriores,
    DateTime Instante = default);

/// <summary>Até onde cada pessoa leu a conversa — a tela refaz o "Lida por" das mensagens que já tem.</summary>
public sealed record LeituraNaConversa(int UsuarioId, int UltimaLidaId);

/// <summary>
/// O que mudou na conversa aberta desde a última leitura: a releitura de 4 s não traz de novo
/// as mensagens (e os laudos) que já estão na tela.
/// </summary>
/// <param name="HaMaisNovas">Chegaram mais que o limite: a tela relê a conversa inteira.</param>
/// <param name="Editadas">Mensagens que já estavam na conversa e foram corrigidas.</param>
/// <param name="Subtitulo">A presença da pessoa na direta; nulo no canal (não muda de 4 em 4 s).</param>
public sealed record AtualizacaoDaConversa(IReadOnlyList<MensagemNaConversa> Novas, bool HaMaisNovas,
    IReadOnlyList<MensagemNaConversa> Editadas, IReadOnlyList<LeituraNaConversa> Leituras,
    IReadOnlyList<string> Escrevendo, string? Subtitulo, DateTime Instante);

/// <summary>O que se manda. Texto vazio só vale com anexo.</summary>
public sealed record NovaMensagemEquipe(string Texto, int? PacienteId = null, int? DocumentoClinicoId = null,
    string? ArquivoNome = null, byte[]? ArquivoConteudo = null);

public interface IChatEquipeService
{
    /// <summary>A lista (canais e pessoas), as não lidas e o que chegou depois de <paramref name="desdeId"/>.</summary>
    Task<PainelChat> PainelAsync(int usuarioId, int desdeId, PreferenciasSomChat prefs, CancellationToken ct = default);

    /// <summary>As últimas mensagens da conversa (ou as anteriores a <paramref name="antesDe"/>).</summary>
    Task<ConversaAberta> AbrirAsync(int usuarioId, string conversa, int? antesDe = null, int limite = 60, CancellationToken ct = default);

    /// <summary>
    /// O que mudou depois de <paramref name="depoisDe"/> (a última mensagem na tela) e as correções
    /// feitas depois de <paramref name="editadasDesde"/> (o <c>Instante</c> da leitura anterior).
    /// </summary>
    Task<AtualizacaoDaConversa> AtualizarAsync(int usuarioId, string conversa, int depoisDe, DateTime editadasDesde,
        int limite = 60, CancellationToken ct = default);

    Task<int> EnviarAsync(int usuarioId, string conversa, NovaMensagemEquipe dados, CancellationToken ct = default);

    /// <summary>Corrige o próprio texto; a versão anterior fica guardada.</summary>
    Task EditarAsync(int usuarioId, int mensagemId, string texto, CancellationToken ct = default);

    Task MarcarLidaAsync(int usuarioId, string conversa, int ultimaId, CancellationToken ct = default);

    /// <summary>"Estou aqui" e, opcionalmente, "estou escrevendo nesta conversa".</summary>
    Task SinalizarAsync(int usuarioId, string? escrevendoEm, CancellationToken ct = default);

    /// <summary>O arquivo anexado, para quem pode ver a conversa; nulo quando não há.</summary>
    Task<(string Nome, byte[] Conteudo)?> ArquivoAsync(int usuarioId, int mensagemId, CancellationToken ct = default);
}
