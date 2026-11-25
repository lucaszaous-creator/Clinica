namespace Clinica.Ui.Blazor.Hospedagem;

/// <summary>O tipo de um item do menu de contexto do navegador (o espelho de <c>CoreWebView2ContextMenuItemKind</c>).</summary>
public enum TipoDoItemDoNavegador { Comando, Separador, Submenu, Outro }

/// <summary>Um item do menu de contexto do navegador: o nome sem tradução (<c>CoreWebView2ContextMenuItem.Name</c>) e o tipo.</summary>
public sealed record ItemDoMenuDoNavegador(string Nome, TipoDoItemDoNavegador Tipo);

/// <summary>
/// O CORRETOR ORTOGRÁFICO no navegador dos apps (24/11/2026, F1b do "CRM com cara de WhatsApp"). O navegador da suíte
/// nascia sem menu de contexto (o do Edge, com "Inspecionar", "Salvar como", "Imprimir"…), e com ele ia a sugestão do
/// corretor: a palavra sublinhada em vermelho não tinha como ser trocada. A regra:
/// <list type="bullet">
/// <item><b>Fora de campo editável</b> continua SEM menu (<see cref="Manter"/> devolve nulo): a tela tem o menu dela
/// (o botão direito do WhatsApp, <c>MenuDeContexto</c>) e o do navegador não aparece por cima.</item>
/// <item><b>Em campo editável</b> fica só o que serve a quem escreve: as sugestões do corretor e "Adicionar ao
/// dicionário" (o WebView2 dá a todos o nome <c>spellCheck</c>), Recortar, Copiar e Colar — sem Emoji, Desfazer,
/// "Colar como texto sem formatação", "Selecionar tudo", direção da escrita nem as opções do corretor (submenu).</item>
/// </list>
/// Pura, com teste (<c>MenuDoCorretorTests</c>); quem aplica é o <c>NavegadorEmbutido</c> do shell, no
/// <c>ContextMenuRequested</c>. O idioma do navegador (<see cref="Idioma"/>) é o que escolhe o dicionário.
/// </summary>
public static class MenuDoCorretor
{
    /// <summary>O idioma do navegador (BCP 47): os menus, o <c>accept-language</c> e o dicionário do corretor.</summary>
    public const string Idioma = "pt-BR";

    /// <summary>O nome que o WebView2 dá às sugestões do corretor e ao "Adicionar ao dicionário".</summary>
    public const string Corretor = "spellCheck";

    /// <summary>Os comandos que ficam no menu de um campo editável, além do corretor.</summary>
    public static readonly IReadOnlySet<string> EdicaoQueFica = new HashSet<string>(StringComparer.Ordinal) { "cut", "copy", "paste" };

    /// <summary>
    /// Os índices de <paramref name="itens"/> que ficam, na ordem do navegador — ou nulo, quando não há menu (alvo não
    /// editável, ou nada sobrou). Separadores só ENTRE grupos que ficaram: nunca no começo, no fim nem dois seguidos.
    /// </summary>
    public static IReadOnlyList<int>? Manter(bool editavel, IReadOnlyList<ItemDoMenuDoNavegador> itens)
    {
        if (!editavel) return null;
        var ficam = new List<int>();
        var separadorPendente = false;
        for (var i = 0; i < itens.Count; i++)
        {
            var item = itens[i];
            if (item.Tipo == TipoDoItemDoNavegador.Separador)
            {
                separadorPendente = ficam.Count > 0;
                continue;
            }
            if (!Fica(item)) continue;
            if (separadorPendente && ficam.Count > 0 && itens[ficam[^1]].Tipo != TipoDoItemDoNavegador.Separador)
            {
                var separador = UltimoSeparadorAntes(itens, i);
                if (separador >= 0) ficam.Add(separador);
            }
            separadorPendente = false;
            ficam.Add(i);
        }
        return ficam.Count == 0 ? null : ficam;
    }

    private static bool Fica(ItemDoMenuDoNavegador item)
        => item.Tipo == TipoDoItemDoNavegador.Comando
           && (item.Nome == Corretor || EdicaoQueFica.Contains(item.Nome));

    private static int UltimoSeparadorAntes(IReadOnlyList<ItemDoMenuDoNavegador> itens, int indice)
    {
        for (var j = indice - 1; j >= 0; j--)
            if (itens[j].Tipo == TipoDoItemDoNavegador.Separador) return j;
        return -1;
    }
}
