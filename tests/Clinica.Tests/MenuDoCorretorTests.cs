using Clinica.Ui.Blazor.Hospedagem;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O corretor ortográfico no navegador da suíte (24/11/2026, F1b): o menu de contexto só existe em campo editável, e só
/// com as sugestões do corretor, "Adicionar ao dicionário", recortar, copiar e colar. Os nomes são os do WebView2
/// (<c>CoreWebView2ContextMenuItem.Name</c>: o rótulo em inglês em lowerCamelCase; o corretor é sempre "spellCheck").
/// </summary>
public sealed class MenuDoCorretorTests
{
    private static ItemDoMenuDoNavegador C(string nome) => new(nome, TipoDoItemDoNavegador.Comando);
    private static readonly ItemDoMenuDoNavegador S = new("separator", TipoDoItemDoNavegador.Separador);

    // O menu do Edge num campo com uma palavra errada: duas sugestões, adicionar ao dicionário, emoji, desfazer…
    private static readonly ItemDoMenuDoNavegador[] NoCampo =
    [
        C("spellCheck"), C("spellCheck"), C("spellCheck"), S,
        C("emoji"), S,
        C("undo"), C("redo"), S,
        C("cut"), C("copy"), C("paste"), C("pasteAndMatchStyle"), C("selectAll"), S,
        new("spellCheck", TipoDoItemDoNavegador.Submenu), new("writingDirection", TipoDoItemDoNavegador.Submenu), S,
        C("inspectElement")
    ];

    [Fact]
    public void Fora_de_campo_editavel_nao_ha_menu()
    {
        Assert.Null(MenuDoCorretor.Manter(false, NoCampo));
        Assert.Null(MenuDoCorretor.Manter(false, [C("back"), C("reload"), C("saveAs"), C("print"), C("inspectElement")]));
    }

    [Fact]
    public void No_campo_ficam_o_corretor_e_recortar_copiar_colar_com_um_separador_entre_os_grupos()
    {
        var ficam = MenuDoCorretor.Manter(true, NoCampo)!;
        Assert.Equal(["spellCheck", "spellCheck", "spellCheck", "separator", "cut", "copy", "paste"], ficam.Select(i => NoCampo[i].Nome));
        Assert.Equal(ficam.OrderBy(i => i), ficam); // na ordem do navegador
    }

    [Fact]
    public void Sem_palavra_errada_o_menu_do_campo_e_so_recortar_copiar_colar()
    {
        ItemDoMenuDoNavegador[] itens = [C("emoji"), S, C("undo"), C("redo"), S, C("cut"), C("copy"), C("paste"), C("selectAll"), S, C("inspectElement")];
        Assert.Equal(["cut", "copy", "paste"], MenuDoCorretor.Manter(true, itens)!.Select(i => itens[i].Nome));
    }

    [Fact]
    public void Campo_sem_nada_que_fica_nao_mostra_menu_vazio()
        => Assert.Null(MenuDoCorretor.Manter(true, [C("emoji"), S, C("selectAll"), S, C("inspectElement")]));

    [Fact]
    public void Submenu_das_opcoes_do_corretor_nao_entra()
    {
        ItemDoMenuDoNavegador[] itens = [new("spellCheck", TipoDoItemDoNavegador.Submenu), C("copy")];
        Assert.Equal(["copy"], MenuDoCorretor.Manter(true, itens)!.Select(i => itens[i].Nome));
    }

    [Fact]
    public void O_idioma_do_navegador_e_o_pt_BR()
        => Assert.Equal("pt-BR", MenuDoCorretor.Idioma);
}
