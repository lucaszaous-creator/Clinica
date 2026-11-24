using Clinica.Crm.Contracts;

namespace Clinica.Application.Servicos;

/// <summary>O que uma opção do "/" põe no campo: uma resposta rápida (texto pronto) ou um modelo aprovado pela Meta.</summary>
public enum TipoNaBarra { Rapida, Modelo }

/// <summary>
/// Uma opção do "/" do campo (F1, nov/2026): o título, o começo do texto e, quando ela não cabe agora, o MOTIVO (a opção
/// aparece apagada com a dica, como no menu). Carrega a resposta ou o modelo de onde veio, para a tela chamar o MESMO
/// handler do popover antigo (<c>UsarRapida</c> / <c>EscolherModelo</c>).
/// </summary>
public sealed record OpcaoDaBarra(TipoNaBarra Tipo, string Titulo, string Texto, string? Motivo = null,
    AppQuickReply? Rapida = null, AppTemplate? Modelo = null)
{
    public bool Habilitada => Motivo is null;
}

/// <summary>
/// A faixa fixada no topo da conversa (o "próximo passo", como a mensagem fixada do WhatsApp): os botões à vista e os que
/// vão para o "Mais". Nenhum botão do aviso some — os que não cabem continuam a um clique.
/// </summary>
public sealed record BotoesDaFaixa(IReadOnlyList<BotaoPedido> AVista, IReadOnlyList<BotaoPedido> NoMais);

/// <summary>
/// O CAMPO E A FAIXA DA CONVERSA COM CARA DE WHATSAPP (nov/2026, F1 — docs da proposta v2 aprovada pelo dono): o "/" que
/// junta respostas rápidas e modelos numa busca só, os emojis do botão 😊 e a divisão dos botões da faixa fixada. A tela
/// só desenha; a regra mora aqui, com teste (<c>CampoComCaraDeWhatsAppTests</c>).
/// </summary>
public static partial class ConversasNaTela
{
    /// <summary>Quantas opções o "/" mostra de uma vez (a regra de 8 do design: mais que isso, a pessoa digita mais uma letra).</summary>
    public const int OpcoesNaBarra = 8;

    /// <summary>Quantos botões a faixa fixada mostra à vista; o resto vai para o "Mais".</summary>
    public const int BotoesAVistaNaFaixa = 2;

    /// <summary>
    /// O termo do "/" quando o campo é uma busca: começa com "/" e ainda é uma linha só ("/rem" → "rem"; "/" → "").
    /// Nulo quando não é: texto comum, ou "/" no meio da frase ("15/11"), ou já com quebra de linha.
    /// </summary>
    public static string? TermoDaBarra(string? texto)
    {
        if (string.IsNullOrEmpty(texto) || texto[0] != '/' || texto.Contains('\n')) return null;
        return texto[1..].Trim();
    }

    /// <summary>
    /// As opções do "/": as respostas rápidas (não arquivadas) primeiro, depois os modelos do número (nunca no SMS, que não
    /// tem modelo), as que casam com o termo no título ou no texto, sem caixa nem acento. No máximo
    /// <see cref="OpcoesNaBarra"/>. O modelo que a Meta não liberou para esta tela vem apagado com o motivo dela.
    /// </summary>
    public static IReadOnlyList<OpcaoDaBarra> OpcoesDaBarra(string termo, IEnumerable<AppQuickReply> rapidas,
        IEnumerable<AppTemplate>? modelos, bool sms)
    {
        var t = termo.Trim();
        bool Casa(string? titulo, string? texto) => t.Length == 0 || Contem(titulo, t) || Contem(texto, t);

        var opcoes = rapidas.Where(q => !q.Archived && Casa(q.Title, q.Text))
            .Select(q => new OpcaoDaBarra(TipoNaBarra.Rapida, q.Title, q.Text, Rapida: q));
        if (!sms && modelos is not null)
            opcoes = opcoes.Concat(modelos.Where(m => Casa(ModeloDoWhatsApp.NomeNaTela(m.Name), m.Text))
                .OrderBy(m => m.Supported ? 0 : 1)
                .Select(m => new OpcaoDaBarra(TipoNaBarra.Modelo, ModeloDoWhatsApp.NomeNaTela(m.Name), ModeloDoWhatsApp.TextoNaLista(m),
                    m.Supported ? null : string.IsNullOrWhiteSpace(m.Reason) ? "Este modelo não pode sair por aqui" : m.Reason, Modelo: m)));
        return opcoes.Take(OpcoesNaBarra).ToList();
    }

    /// <summary>
    /// O que o "/" diz quando não acha nada — separando "não há nada cadastrado" de "o termo não casou", e lembrando que o
    /// SMS não tem modelo.
    /// </summary>
    public static string VazioDaBarra(string termo, bool temRapidas, bool sms)
        => termo.Trim().Length > 0 ? $"Nada com \"{termo.Trim()}\". Apague uma letra ou escreva a mensagem."
            : !temRapidas && sms ? "Nenhuma resposta rápida cadastrada no CRM."
            : "Nenhuma resposta rápida nem modelo para mostrar.";

    /// <summary>Os emojis do botão 😊, os mais usados no balcão — entram no fim do texto, e a pessoa continua escrevendo.</summary>
    public static readonly IReadOnlyList<string> EmojisDoCampo =
        ["😊", "🙂", "😉", "🙏", "👍", "👏", "🤝", "❤️", "💙", "✅", "📅", "⏰", "📍", "📄", "🌿", "☺️"];

    /// <summary>O texto com o emoji no fim: separado por espaço quando o texto já tem conteúdo e não termina em espaço.</summary>
    public static string ComEmoji(string? texto, string emoji)
        => string.IsNullOrEmpty(texto) ? emoji : char.IsWhiteSpace(texto[^1]) ? texto + emoji : $"{texto} {emoji}";

    /// <summary>
    /// Os botões da faixa fixada: até <see cref="BotoesAVistaNaFaixa"/> à vista — os primários primeiro, depois os outros na
    /// ordem do aviso — e o resto no "Mais", na ordem do aviso. Com dois ou menos, todos à vista e o "Mais" não existe.
    /// </summary>
    public static BotoesDaFaixa DividirBotoes(AcaoPedida acao)
    {
        var botoes = acao.Botoes;
        if (botoes.Count <= BotoesAVistaNaFaixa) return new(botoes, []);
        // Por POSIÇÃO, não por igualdade: dois botões iguais (record) não podem cair os dois à vista nem os dois no "Mais".
        var indices = Enumerable.Range(0, botoes.Count);
        var aVista = indices.Where(i => botoes[i].Primario).Concat(indices.Where(i => !botoes[i].Primario))
            .Take(BotoesAVistaNaFaixa).ToHashSet();
        return new(indices.Where(aVista.Contains).Select(i => botoes[i]).ToList(),
            indices.Where(i => !aVista.Contains(i)).Select(i => botoes[i]).ToList());
    }
}
