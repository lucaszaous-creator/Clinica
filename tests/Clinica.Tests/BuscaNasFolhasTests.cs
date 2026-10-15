using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// A busca da aba "O que já saiu" (out/2026 — "laudos fáceis de localizar e imprimir"). O
/// que ela fixa é o que a tela promete no campo: paciente, número com ou sem a barra,
/// código de conferência, folha e quem assinou — e que "nulo" no tipo é "todas".
/// </summary>
public class BuscaNasFolhasTests
{
    private static FolhaEmitida Folha(
        string numero = "2026/0001", string codigo = "AB12-CD34",
        string rotulo = "Atestado", string? paciente = "José Antônio Pereira",
        string? profissional = "Dra. Helena Duarte")
        => new(1, NaturezaFolha.Clinico, numero, codigo, rotulo, paciente, profissional,
               new DateOnly(2026, 10, 9), new DateTime(2026, 10, 9, 10, 0, 0), "ana", false, null);

    [Theory]
    [InlineData("jose antonio")]   // sem acento e sem caixa
    [InlineData("2026/0001")]
    [InlineData("20260001")]        // o número sem a barra, como sai digitado
    [InlineData("ab12cd34")]        // o código sem o hífen
    [InlineData("AB12-CD34")]
    [InlineData("atestado")]
    [InlineData("helena")]
    public void O_termo_casa_nos_campos_que_o_campo_promete(string termo)
        => Assert.True(BuscaNasFolhas.Casa(Folha(), termo, null));

    [Fact]
    public void Termo_que_nao_esta_em_nenhum_campo_nao_casa()
        => Assert.False(BuscaNasFolhas.Casa(Folha(), "maria", null));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(BuscaNasFolhas.TodasAsFolhas)]
    public void Tipo_nulo_vazio_ou_todas_nao_filtra(string? tipo)
        => Assert.True(BuscaNasFolhas.Casa(Folha(), null, tipo));

    [Fact]
    public void O_tipo_recorta_pela_folha()
    {
        Assert.True(BuscaNasFolhas.Casa(Folha(rotulo: "Laudo médico"), null, "Laudo médico"));
        Assert.False(BuscaNasFolhas.Casa(Folha(rotulo: "Atestado"), null, "Laudo médico"));
    }

    [Fact]
    public void Os_tipos_tem_todas_primeiro_o_catalogo_e_o_que_so_a_lista_trouxe()
    {
        var tipos = BuscaNasFolhas.Tipos(
            ["Receituário", "Atestado"],
            [Folha(rotulo: "Atestado"), Folha(rotulo: "Laudo médico")]);

        Assert.Equal(
            [BuscaNasFolhas.TodasAsFolhas, "Receituário", "Atestado", "Laudo médico"], tipos);
    }

    [Fact]
    public void O_recorte_so_existe_quando_algo_filtra()
    {
        Assert.Equal(string.Empty, BuscaNasFolhas.Recorte(30, 30, null, BuscaNasFolhas.TodasAsFolhas));
        Assert.Equal("3 de 30 com “laudo”", BuscaNasFolhas.Recorte(3, 30, " laudo ", null));
        Assert.Equal("2 de 30 de Atestado", BuscaNasFolhas.Recorte(2, 30, null, "Atestado"));
    }
}
