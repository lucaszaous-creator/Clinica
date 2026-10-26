using Clinica.Domain;
using FluentAssertions;

namespace Clinica.Tests;

public class TelefoneTests
{
    [Theory]
    [InlineData("22996105104", "(22) 99610-5104")]
    [InlineData("2226651234", "(22) 2665-1234")]
    [InlineData("996105104", "99610-5104")]
    [InlineData("26651234", "2665-1234")]
    [InlineData("(22) 99610-5104", "(22) 99610-5104")]
    public void Formatar_PadraoBrasileiro(string entrada, string esperado)
        => Telefone.Formatar(entrada).Should().Be(esperado);

    [Fact]
    public void Formatar_ForaDoPadrao_DevolveComoDigitado()
        => Telefone.Formatar("123").Should().Be("123");

    [Fact]
    public void Normalizar_SoDigitos()
        => Telefone.Normalizar("(22) 99610-5104").Should().Be("22996105104");

    // Out/2026, bug visto na clínica: a ficha com "022998999334" ia para a Meta como está e a mensagem
    // não era entregue; o WhatsApp do celular entende o mesmo número como +55 22 99899-9334.
    [Theory]
    [InlineData("022998999334", "22998999334")]   // 0 do interurbano + celular
    [InlineData("02225000002", "2225000002")]     // 0 do interurbano + fixo
    [InlineData("01522998999334", "22998999334")] // 0 + operadora + celular
    [InlineData("0152225000002", "2225000002")]   // 0 + operadora + fixo
    [InlineData("22998999334", "22998999334")]    // sem prefixo: fica
    [InlineData("5522998999334", "5522998999334")] // já com o 55: fica
    [InlineData("0015551234567", "0015551234567")] // 00 é internacional: fica
    [InlineData("0999", "0999")]                  // curto demais: fica
    public void SemPrefixoDeDiscagem(string entrada, string esperado)
        => Telefone.SemPrefixoDeDiscagem(entrada).Should().Be(esperado);

    [Theory]
    [InlineData("022998999334")]
    [InlineData("(022) 99899-9334")]
    [InlineData("015 22 99899-9334")]
    public void Ponte_manda_o_numero_com_o_55_sem_o_zero(string ficha)
        => Clinica.Crm.Bridge.ClinicAgendaBridge.Phone(ficha).Should().Be("5522998999334");

    [Fact]
    public void A_conversa_acha_a_ficha_que_guarda_o_zero()
    {
        Clinica.Application.Servicos.ConversasNaTela.MesmoTelefone("5522998999334", "022998999334").Should().BeTrue();
        Clinica.Application.Servicos.WhatsAppDaRecepcaoService.Variantes("5522998999334").Should().Contain("022998999334");
        Clinica.Application.Servicos.ConversaNoCrm.Telefone("022998999334").Should().Be("22998999334");
        Clinica.Application.Servicos.ConversasNaTela.TelefoneNaTela("022998999334").Should().Be("(22) 99899-9334");
        // Os outros que montam o número: a fila de documentos pelo WhatsApp oficial e os links wa.me.
        Clinica.Application.Servicos.FilaEnviosCrm.Telefone("022998999334").Should().Be("5522998999334");
        Clinica.Application.Servicos.QuemMeDeveTela.TelefoneParaWhatsApp("022998999334").Should().Be("5522998999334");
        Clinica.Application.Modelos.ColetaDeTermoNaTela.EnderecoDoWhatsapp("022998999334", "oi").Should().StartWith("https://wa.me/5522998999334?");
    }
}
