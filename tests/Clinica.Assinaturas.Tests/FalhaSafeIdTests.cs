using Clinica.Assinaturas.Api;
using Xunit;

namespace Clinica.Assinaturas.Tests;

public class FalhaSafeIdTests
{
    [Theory]
    [InlineData("O SafeID recusou a chamada a 'token' (401 Unauthorized). Detalhe: segredo-cpf-token", "PSC-401")]
    [InlineData("O SafeID recusou a chamada a 'certificate-discovery?from_authorization=true' (403 Forbidden). segredo-cpf-token", "PSC-403")]
    [InlineData("A cadeia deste certificado não é reconhecida por esta máquina. segredo-cpf-token", "CADEIA-CERTIFICADO")]
    [InlineData("segredo-cpf-token", "INTERNO")]
    public void DiagnosticoNaoIncluiConteudoDaExcecao(string mensagem, string codigo)
    {
        var resultado = FalhaSafeIdTablet.Classificar(new InvalidOperationException(mensagem));
        Assert.Equal(codigo, resultado.Codigo);
        Assert.DoesNotContain("segredo-cpf-token", resultado.ToString());
    }

    [Fact]
    public void ReconheceFalhaDeRedeEncapsuladaSemExporEndereco()
    {
        var erro = new InvalidOperationException("CPF secreto", new HttpRequestException("https://exemplo/?token=secreto"));
        Assert.Equal(new FalhaSafeIdTablet("REDE", "indisponivel"), FalhaSafeIdTablet.Classificar(erro));
    }
}
