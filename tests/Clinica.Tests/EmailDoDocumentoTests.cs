using Clinica.Application.Email;
using Clinica.Application.Modelos;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O e-mail que leva o documento ao paciente (out/2026): texto + HTML com a marca. O que ele
/// AFIRMA mora em <see cref="EmailDoDocumento"/>, e é isto que se fixa aqui.
/// </summary>
public class EmailDoDocumentoTests
{
    private static readonly byte[] Logo = [0x89, 0x50, 0x4E, 0x47];

    private static DadosPrestador Clinica(string? endereco = "Rua A, 10 — Macaé", string? telefone = "(22) 2500-0002",
        string? email = null)
        => new() { NomeFantasia = "Clínica SemDor", Endereco = endereco, Telefone = telefone, Email = email };

    [Fact]
    public void Leva_as_duas_versoes_e_a_logo_embutida()
    {
        var m = EmailDoDocumento.Montar("Maria da Silva", "Receita", "2026/0012", false, Clinica(), Logo);

        m.Assunto.Should().Be("Seu documento — Clínica SemDor");
        m.Texto.Should().Contain("Olá, Maria!").And.Contain("Clínica SemDor");
        m.Html.Should().Contain("Olá, Maria!").And.Contain("Receita").And.Contain("2026/0012");
        m.Html.Should().Contain($"cid:{EmailDoDocumento.CidLogo}");
        m.Imagens.Should().ContainSingle(i => i.ContentId == EmailDoDocumento.CidLogo && i.TipoMime == "image/png");
    }

    [Fact]
    public void Sem_a_logo_escreve_o_nome_e_nao_cita_imagem_que_nao_viaja()
    {
        var m = EmailDoDocumento.Montar("Maria", "Atestado", "2026/0003", false, Clinica(), null);

        m.Imagens.Should().BeEmpty();
        m.Html.Should().NotContain("cid:").And.Contain("Clínica SemDor");
    }

    [Fact]
    public void Texto_do_banco_passa_pelo_escape()
    {
        var m = EmailDoDocumento.Montar("<script>alert(1)</script> Ana", "Receita", "1",
            false, Clinica(endereco: "Rua <b>&</b>"), Logo);

        m.Html.Should().NotContain("<script>").And.Contain("&lt;script&gt;");
        m.Html.Should().NotContain("Rua <b>").And.Contain("Rua &lt;b&gt;&amp;&lt;/b&gt;");
    }

    [Fact]
    public void A_caixa_da_assinatura_so_aparece_no_documento_assinado()
    {
        var assinado = EmailDoDocumento.Montar("Maria", "Receita", "1", true, Clinica(), Logo);
        var simples = EmailDoDocumento.Montar("Maria", "Receita", "1", false, Clinica(), Logo);

        assinado.Html.Should().Contain("assinatura digital").And.Contain("assinaturadigital.iti.gov.br");
        assinado.Texto.Should().Contain("assinatura digital");
        simples.Html.Should().NotContain("assinatura digital");
        simples.Texto.Should().NotContain("assinatura digital");
    }

    [Fact]
    public void O_rodape_so_diz_o_contato_que_esta_cadastrado()
    {
        var m = EmailDoDocumento.Montar("Maria", "Receita", "1", false,
            Clinica(endereco: null, telefone: "(22) 2500-0002"), Logo);

        m.Html.Should().Contain("(22) 2500-0002").And.NotContain(" ·  · ");
        m.Texto.Should().EndWith("(22) 2500-0002");

        var semContato = EmailDoDocumento.Montar("Maria", "Receita", "1", false,
            Clinica(endereco: null, telefone: null), Logo);
        semContato.Texto.Should().NotEndWith("\n");
    }

    [Fact]
    public void Sem_nome_cadastrado_a_clinica_continua_sendo_dita()
    {
        var m = EmailDoDocumento.Montar("Maria", "Receita", "1", false, new DadosPrestador(), Logo);

        m.Assunto.Should().Be("Seu documento da clínica");
        m.Html.Should().Contain("a clínica preparou");
    }
}
