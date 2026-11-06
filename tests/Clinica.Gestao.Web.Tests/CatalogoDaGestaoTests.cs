using Clinica.Domain.Entities;
using Clinica.Gestao.Web.Hospedagem;
using Xunit;

namespace Clinica.Gestao.Web.Tests;

/// <summary>O catálogo gerado e o filtro de acesso da moldura da gestão (nov/2026).</summary>
public sealed class CatalogoDaGestaoTests
{
    private static readonly CatalogoDaGestao Catalogo = CatalogoDaGestao.Embutido();

    private static SessaoUsuario Sessao(PerfilAcesso perfil)
        => SessaoUsuario.Fotografar(new UsuarioSistema { Id = 7, Nome = "Teste", Login = "teste", Perfil = perfil });

    [Fact]
    public void As_quatro_areas_na_ordem_pedida_e_toda_chave_tem_tela()
    {
        Assert.Equal(["Gestão", "Financeiro", "Recepção", "Clínico"], Catalogo.Areas.Select(a => a.Rotulo));
        foreach (var i in Catalogo.Todos)
            Assert.True(i.Tela is not null || i.Abas.Length > 0, $"A chave {i.Chave} não tem tela nem abas.");
        foreach (var a in Catalogo.Todos.SelectMany(i => i.Abas))
            Assert.NotNull(Catalogo.ItemDe(a.Chave)?.Tela);
    }

    [Fact]
    public void O_faturamento_mora_no_financeiro()
    {
        var financeiro = Catalogo.Areas.Single(a => a.Rotulo == "Financeiro");
        Assert.Contains("faturamento-gerencial", financeiro.Itens);
        Assert.Contains("faturamento-pendencias", Catalogo.ItemDe("faturamento-gerencial")!.Abas.Select(a => a.Chave));
    }

    [Fact]
    public void Gerente_ve_as_quatro_areas_e_abre_a_mesa_da_direcao()
    {
        var m = new MolduraDaGestao(Catalogo, Sessao(PerfilAcesso.Gerente), () => { });
        Assert.Equal(4, m.Barra.Count);
        Assert.Equal("painel-direcao", m.ChaveAtual);
        Assert.Null(m.FalhaDaTela);
        Assert.False(m.TemTreinamento);
        Assert.Equal("Sair", m.Saida.Botao);
    }

    [Fact]
    public void Quem_nao_tem_o_bit_nao_ve_o_item_nem_navega_por_chave()
    {
        // A recepção não tem VerIndicadores: nada da Gestão, e a chave da Mesa não abre.
        var m = new MolduraDaGestao(Catalogo, Sessao(PerfilAcesso.Recepcao), () => { });
        Assert.DoesNotContain(m.Barra, g => g.Rotulo == "Gestão" && g.Itens.Any(i => i.Chave == "painel-direcao"));
        Assert.False(m.Conhece("painel-direcao"));
        m.Navegar("painel-direcao");
        Assert.NotEqual("painel-direcao", m.ChaveAtual);
        Assert.NotNull(m.FalhaDaTela);
    }

    [Fact]
    public void A_busca_acha_aba_pelo_nome_e_abre_no_pai()
    {
        var m = new MolduraDaGestao(Catalogo, Sessao(PerfilAcesso.Gerente), () => { });
        m.TextoPesquisa = "glosas";
        var r = m.ResultadosPesquisa.First();
        m.AbrirResultado(r);
        Assert.Equal("faturamento-glosas", m.ChaveAtual);
        Assert.Contains(m.Abas, a => a.Ativa && a.Rotulo.Contains("Glosas"));
    }
}
