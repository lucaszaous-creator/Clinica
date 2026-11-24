using Clinica.Ui.Blazor.Hospedagem;
using Microsoft.Extensions.FileProviders;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// Os atalhos que a tela pede (24/11/2026, F1b): Ctrl+F, Ctrl+Alt+N e Esc chegam à tela à vista que os declara em
/// <c>data-atalhos</c>. O script (js/janela.js, embutido na DLL) manda o NOME do atalho ao <c>PalcoDeTelas.Atalho</c>,
/// que o lê com <c>Enum.TryParse</c> — um nome que divergisse do enum seria ignorado em silêncio, sem build nem teste
/// que acusasse. Este teste casa os dois lados.
/// </summary>
public sealed class AtalhosDaTelaTests
{
    private static string Script()
    {
        var ativos = new ManifestEmbeddedFileProvider(typeof(IHostDaTela).Assembly, "Ativos");
        using var leitor = new StreamReader(ativos.GetFileInfo("js/janela.js").CreateReadStream());
        return leitor.ReadToEnd();
    }

    [Theory]
    [InlineData("buscar", "Buscar", AtalhoDeTela.Buscar)]
    [InlineData("nova", "Nova", AtalhoDeTela.Nova)]
    [InlineData("fechar", "Fechar", AtalhoDeTela.Fechar)]
    public void O_script_manda_o_nome_que_o_enum_conhece_e_so_para_a_tela_que_declara(string declarado, string nome, AtalhoDeTela atalho)
    {
        var js = Script();
        Assert.Contains($"pedido = '{declarado}'; nome = '{nome}'", js);
        Assert.Equal(atalho, Enum.Parse<AtalhoDeTela>(nome));
        Assert.Contains("data-atalhos", js);
        Assert.Contains("telaPede(pedido)", js);
    }

    [Fact]
    public void Os_atalhos_de_sempre_continuam()
    {
        var js = Script();
        foreach (var nome in new[] { "Salvar", "Imprimir", "Atualizar" })
        {
            Assert.Contains($"nome = '{nome}'", js);
            Assert.True(Enum.IsDefined(Enum.Parse<AtalhoDeTela>(nome)));
        }
    }
}
