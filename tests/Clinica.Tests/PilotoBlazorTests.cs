using Clinica.Ui.Blazor.Hospedagem;
using Microsoft.Extensions.FileProviders;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O piloto de interface em Blazor (out/2026, docs/piloto-blazor.md).
///
/// O BlazorWebView do WPF serve a página e o estilo de DENTRO da DLL (manifesto de
/// recursos). Arquivo que deixasse de ser embutido não quebraria build nenhum: a aba
/// abriria em branco, ou sem estilo, só na clínica — por isso o manifesto é conferido aqui.
/// </summary>
public class PilotoBlazorTests
{
    private static readonly IFileProvider Ativos =
        new ManifestEmbeddedFileProvider(typeof(IHostDaTela).Assembly, "Ativos");

    [Theory]
    [InlineData("index.html")]
    [InlineData("css/clinica.css")]
    [InlineData("tokens/colors.css")]
    [InlineData("tokens/typography.css")]
    [InlineData("tokens/layout.css")]
    [InlineData("tokens/semantic.css")]
    [InlineData("tokens/web-extras.css")]
    public void A_pagina_e_o_estilo_vao_embutidos_na_dll(string caminho)
    {
        var arquivo = Ativos.GetFileInfo(caminho);
        Assert.True(arquivo.Exists, $"{caminho} não está embutido no Clinica.Ui.Blazor.dll");
        Assert.True(arquivo.Length > 0);
    }

    [Fact]
    public void A_pagina_hospedeira_pinta_o_fundo_antes_de_tudo_e_carrega_o_blazor_do_webview()
    {
        // O fundo na primeira linha é metade do que impede a tela preta (TelaBlazor.cs).
        using var leitor = new StreamReader(Ativos.GetFileInfo("index.html").CreateReadStream());
        var html = leitor.ReadToEnd();
        Assert.Contains("background:#F8FAFC", html);
        Assert.Contains("_framework/blazor.webview.js", html);
        Assert.Contains("id=\"app\"", html);
    }

    [Fact]
    public void Todo_import_do_css_aponta_para_um_arquivo_embutido()
    {
        using var leitor = new StreamReader(Ativos.GetFileInfo("css/clinica.css").CreateReadStream());
        var css = leitor.ReadToEnd();
        var imports = System.Text.RegularExpressions.Regex.Matches(css, @"@import url\(""\.\./([^""]+)""\)");
        Assert.NotEmpty(imports);
        foreach (System.Text.RegularExpressions.Match m in imports)
            Assert.True(Ativos.GetFileInfo(m.Groups[1].Value).Exists, $"@import de {m.Groups[1].Value} não resolve");
    }

    [Fact]
    public void Dinheiro_sai_em_pt_BR_mesmo_com_a_cultura_invariante()
    {
        var anterior = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
            Assert.Equal("R$ 1.234,50", Formato.Dinheiro(1234.5m).Replace(' ', ' '));
            Assert.Equal("R$ 12,5 mil", Formato.DinheiroCurto(12_500m));
            Assert.Equal("−R$ 2 mil", Formato.DinheiroCurto(-2_000m));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = anterior;
        }
    }
}
