using Clinica.Application.Servicos;

namespace Clinica.Application.Abstracoes;

/// <summary>Uma linha da lista do chat: um canal ou uma pessoa (a direta com ela).</summary>
/// <param name="PessoaAtiva">Falso na direta com quem saiu da equipe: a conversa fica, só para leitura.</param>
public sealed record ConversaNaLista(string Conversa, string Titulo, string Subtitulo, int NaoLidas,
    string? UltimaPrevia, DateTime? UltimaEm, bool EhCanal, int? PessoaId = null, string Iniciais = "",
    PresencaChat Presenca = PresencaChat.Fora, bool PessoaAtiva = true);

/// <summary>Uma mensagem que chegou depois do último número visto — a prévia da bolinha e o som.</summary>
public sealed record NovidadeChat(int Id, string Conversa, string Titulo, string AutorNome, string Previa,
    DateTime EnviadaEm, SomChat Som);

/// <summary>
/// A lista do chat numa leitura só. <paramref name="Completo"/> falso (a bolinha fechada) traz só
/// as conversas e as não lidas, sem nomes nem presença — é o que o número da bolinha precisa.
/// </summary>
/// <param name="Sistema">A conversa da pessoa com o Sistema SemDor (sempre presente; out/2026).</param>
public sealed record PainelChat(IReadOnlyList<ConversaNaLista> Canais, IReadOnlyList<ConversaNaLista> Diretas,
    int TotalNaoLidas, int Online, bool Completo = true, ConversaNaLista? Sistema = null);

/// <summary>
/// "Chegou alguma coisa para mim depois de <c>desdeId</c>?" — a pergunta da bolinha a cada batida,
/// numa ida ao banco. <paramref name="UltimaMensagemId"/> é o <c>desdeId</c> da próxima.
/// </summary>
public sealed record PulsoChat(int UltimaMensagemId, IReadOnlyList<NovidadeChat> Novidades);

/// <param name="TamanhoTexto">
/// O tamanho do texto INTEIRO. A conversa traz só o começo das mensagens longas
/// (<see cref="ChatEquipeRegras.TextoNaConversa"/>); o resto vem por <see cref="IChatEquipeService.TextoAsync"/>.
/// Nulo quando <paramref name="Texto"/> já é o texto inteiro.
/// </param>
public sealed record MensagemNaConversa(int Id, int AutorId, string AutorNome, string Iniciais, string Texto,
    DateTime EnviadaEm, DateTime? EditadaEm, bool Minha, bool MeCita,
    int? PacienteId, string? PacienteNome, int? DocumentoId, string? DocumentoRotulo,
    string? ArquivoNome, long? ArquivoTamanho, string? LidaPor, int? TamanhoTexto = null,
    bool DoSistema = false, CartaoChat? Cartao = null)
{
    /// <summary>O texto que está aqui é o inteiro?</summary>
    public bool TextoCompleto => TamanhoTexto is null || TamanhoTexto <= Texto.Length;

    public int Tamanho => TamanhoTexto ?? Texto.Length;
}

/// <summary>Quem está na conversa (para a sugestão de @).</summary>
public sealed record PessoaDaConversa(int Id, string Nome, string Iniciais);

/// <param name="Instante">A hora da leitura — é o <c>editadasDesde</c> da próxima <see cref="IChatEquipeService.AtualizarAsync"/>.</param>
/// <param name="Pessoas">Quem pode ser citado com @ (as outras pessoas da conversa, ativas).</param>
/// <param name="SoLeitura">A direta com quem saiu da equipe: dá para ler, não para escrever.</param>
public sealed record ConversaAberta(string Conversa, string Titulo, string Subtitulo, int? PessoaId,
    IReadOnlyList<MensagemNaConversa> Mensagens, int UltimaLidaAntes, IReadOnlyList<string> Escrevendo, bool TemAnteriores,
    DateTime Instante = default, IReadOnlyList<PessoaDaConversa>? Pessoas = null, bool SoLeitura = false);

/// <summary>Até onde cada pessoa leu a conversa — a tela refaz o "Lida por" das mensagens que já tem.</summary>
public sealed record LeituraNaConversa(int UsuarioId, int UltimaLidaId);

/// <summary>
/// O que mudou na conversa aberta desde a última leitura: a releitura de 4 s não traz de novo
/// as mensagens (e os laudos) que já estão na tela.
/// </summary>
/// <param name="HaMaisNovas">Chegaram mais que o limite: a tela pede a próxima página.</param>
/// <param name="Editadas">Mensagens que já estavam na conversa e foram corrigidas.</param>
/// <param name="Subtitulo">A presença da pessoa na direta; nulo no canal (não muda de 4 em 4 s).</param>
public sealed record AtualizacaoDaConversa(IReadOnlyList<MensagemNaConversa> Novas, bool HaMaisNovas,
    IReadOnlyList<MensagemNaConversa> Editadas, IReadOnlyList<LeituraNaConversa> Leituras,
    IReadOnlyList<string> Escrevendo, string? Subtitulo, DateTime Instante);

/// <summary>Um resultado da busca: a mensagem, onde ela está e o trecho com a palavra.</summary>
public sealed record ResultadoDaBusca(int MensagemId, string Conversa, string Titulo, string AutorNome,
    DateTime EnviadaEm, string Trecho);

/// <summary>O que se manda. Texto vazio só vale com anexo.</summary>
public sealed record NovaMensagemEquipe(string Texto, int? PacienteId = null, int? DocumentoClinicoId = null,
    string? ArquivoNome = null, byte[]? ArquivoConteudo = null);

/// <summary>
/// O chat da equipe. Cada método diz quantas idas ao banco custa: com o banco a ~250 ms do posto,
/// é o NÚMERO de idas que decide o tempo, não o tamanho da consulta (docs/chat-da-equipe.md).
/// </summary>
public interface IChatEquipeService
{
    /// <summary>
    /// A batida da bolinha: o que chegou para mim depois de <paramref name="desdeId"/>, com a
    /// prévia e o som. UMA ida ao banco, pela chave primária — não lê o histórico.
    /// </summary>
    Task<PulsoChat> PulsoAsync(int usuarioId, int desdeId, PreferenciasSomChat prefs, CancellationToken ct = default);

    /// <summary>
    /// A lista (canais e diretas) e as não lidas. Duas idas; com <paramref name="completo"/>, quatro
    /// (nomes, presença e quem está em atendimento). Só roda quando algo mudou.
    /// </summary>
    Task<PainelChat> PainelAsync(int usuarioId, bool completo = true, CancellationToken ct = default);

    /// <summary>
    /// As últimas mensagens da conversa (ou as anteriores a <paramref name="antesDe"/>). Sem
    /// <paramref name="antesDe"/>, marca a conversa como lida na mesma chamada. Três a quatro idas.
    /// </summary>
    Task<ConversaAberta> AbrirAsync(int usuarioId, string conversa, int? antesDe = null, int limite = 40, CancellationToken ct = default);

    /// <summary>
    /// O que mudou depois de <paramref name="depoisDe"/> (a última mensagem na tela) e as correções
    /// feitas depois de <paramref name="editadasDesde"/> (o <c>Instante</c> da leitura anterior). Marca
    /// as novas como lidas. Duas a quatro idas.
    /// </summary>
    Task<AtualizacaoDaConversa> AtualizarAsync(int usuarioId, string conversa, int depoisDe, DateTime editadasDesde,
        int limite = 60, CancellationToken ct = default);

    /// <summary>Envia. Duas idas (mais uma por anexo de paciente ou documento).</summary>
    Task<int> EnviarAsync(int usuarioId, string conversa, NovaMensagemEquipe dados, CancellationToken ct = default);

    /// <summary>Corrige o próprio texto; a versão anterior fica guardada.</summary>
    Task EditarAsync(int usuarioId, int mensagemId, string texto, CancellationToken ct = default);

    /// <summary>O texto INTEIRO de uma mensagem longa ("Ver inteiro", "Copiar", "Corrigir").</summary>
    Task<string> TextoAsync(int usuarioId, int mensagemId, CancellationToken ct = default);

    Task MarcarLidaAsync(int usuarioId, string conversa, int ultimaId, CancellationToken ct = default);

    /// <summary>"Estou aqui" e, opcionalmente, "estou escrevendo nesta conversa". Uma ida.</summary>
    Task SinalizarAsync(int usuarioId, string? escrevendoEm, CancellationToken ct = default);

    /// <summary>
    /// Procura as palavras nas conversas que a pessoa pode ver (ou só em <paramref name="conversa"/>),
    /// da mais recente para a mais antiga. No Postgres vai ao índice de texto; duas idas.
    /// </summary>
    Task<IReadOnlyList<ResultadoDaBusca>> BuscarAsync(int usuarioId, string termo, string? conversa = null, int limite = 30,
        CancellationToken ct = default);

    /// <summary>O arquivo anexado, para quem pode ver a conversa; nulo quando não há.</summary>
    Task<(string Nome, byte[] Conteudo)?> ArquivoAsync(int usuarioId, int mensagemId, CancellationToken ct = default);
}
