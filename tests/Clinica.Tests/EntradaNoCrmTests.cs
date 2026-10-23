using Clinica.Application.Servicos;
using Xunit;

namespace Clinica.Tests;

// A regra do bilhete da aba "WhatsApp" (docs/crm-na-recepcao.md): formato, validade com a folga
// do relógio e o endereço que a aba abre.
public sealed class EntradaNoCrmTests
{
    static readonly DateTime Agora = new(2026, 10, 23, 9, 0, 0);

    [Fact] public void Bilhete_novo_tem_formato_fixo_e_o_hash_nao_revela_o_bilhete()
    {
        var (bilhete, hash) = EntradaNoCrm.Novo();
        Assert.True(EntradaNoCrm.FormatoValido(bilhete));
        Assert.Equal(64, hash.Length);
        Assert.Equal(hash, EntradaNoCrm.Hash(bilhete));
        Assert.NotEqual(bilhete, EntradaNoCrm.Novo().Bilhete);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("curto")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa+")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void Formato_errado_nem_chega_ao_banco(string? bilhete) => Assert.False(EntradaNoCrm.FormatoValido(bilhete));

    [Fact] public void Vale_uma_vez_enquanto_nao_vence_com_a_folga_do_relogio()
    {
        var vence = Agora + EntradaNoCrm.Validade;
        Assert.True(EntradaNoCrm.Vale(Agora, vence, null, Agora));
        Assert.True(EntradaNoCrm.Vale(Agora, vence, null, vence + EntradaNoCrm.Folga));
        Assert.False(EntradaNoCrm.Vale(Agora, vence, null, vence + EntradaNoCrm.Folga + TimeSpan.FromSeconds(1)));
        Assert.False(EntradaNoCrm.Vale(Agora, vence, Agora, Agora));
        // emitido por um PC adiantado além da folga: não vale
        Assert.False(EntradaNoCrm.Vale(Agora + TimeSpan.FromMinutes(5), vence + TimeSpan.FromMinutes(5), null, Agora));
    }

    [Theory]
    [InlineData("https://crm.clinicasemdormacae.com.br", "https://crm.clinicasemdormacae.com.br")]
    [InlineData(" https://crm.clinicasemdormacae.com.br/ ", "https://crm.clinicasemdormacae.com.br")]
    [InlineData("https://crm.exemplo:8443", "https://crm.exemplo:8443")]
    [InlineData("http://crm.exemplo", null)]
    [InlineData("https://crm.exemplo/caminho", null)]
    [InlineData("https://crm.exemplo/?modo=x", null)]
    [InlineData("crm.exemplo", null)]
    [InlineData("", null)]
    public void Endereco_do_CRM_so_https_e_sem_caminho(string digitado, string? esperado)
        => Assert.Equal(esperado, EntradaNoCrm.EnderecoValido(digitado));

    [Fact] public void A_aba_abre_o_modo_Recepcao_com_o_bilhete_no_fragmento()
        => Assert.Equal("https://crm.exemplo/?modo=recepcao#entrada=ab-c_d",
            EntradaNoCrm.Endereco("https://crm.exemplo/", "ab-c_d"));
}
