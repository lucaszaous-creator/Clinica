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

// Fase 2: o "Escrever no WhatsApp" leva o pedido no fragmento da moldura (ConversaNoCrm).
public sealed class ConversaNoCrmTests
{
    [Theory]
    [InlineData("(22) 99942-2253", "22999422253")]
    [InlineData("+55 22 99942-2253", "5522999422253")]
    [InlineData("2225", null)]
    [InlineData(null, null)]
    public void Telefone_do_cadastro_vira_digitos_ou_nada(string? cadastro, string? esperado)
        => Assert.Equal(esperado, ConversaNoCrm.Telefone(cadastro));

    [Fact] public void O_pedido_vai_no_fragmento_com_o_bilhete_e_a_vez()
    {
        var url = ConversaNoCrm.Endereco("https://crm.exemplo", "b1", new PedidoDeConversa("(22) 99942-2253", "Maria Fictícia", 7, "Olá, Maria!"), 3);
        Assert.StartsWith("https://crm.exemplo/?modo=recepcao#entrada=b1&abrir=", url);
        Assert.EndsWith("&vez=3", url);
        var json = Uri.UnescapeDataString(url.Split("&abrir=")[1].Split("&vez=")[0]);
        var p = System.Text.Json.JsonDocument.Parse(json).RootElement;
        Assert.Equal(("22999422253", "Maria Fictícia", 7, "Olá, Maria!"),
            (p.GetProperty("telefone").GetString(), p.GetProperty("nome").GetString(), p.GetProperty("paciente").GetInt32(), p.GetProperty("texto").GetString()));
    }

    [Fact] public void Sem_pedido_ou_sem_telefone_so_entra()
    {
        Assert.Equal("https://crm.exemplo/?modo=recepcao#entrada=b1&vez=1", ConversaNoCrm.Endereco("https://crm.exemplo", "b1", null, 1));
        Assert.DoesNotContain("abrir=", ConversaNoCrm.Endereco("https://crm.exemplo", "b1", new PedidoDeConversa("123", "X", null, null), 2));
    }
}
