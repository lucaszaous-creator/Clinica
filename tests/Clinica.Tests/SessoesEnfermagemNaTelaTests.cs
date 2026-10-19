using Clinica.Application.Tablet;
using Clinica.Domain;
using Xunit;

namespace Clinica.Tests;

public class SessoesEnfermagemNaTelaTests
{
    private static SessaoEnfermagemItem S(bool chegada, bool apos, bool legado = false)
        => new(1, 1, "Ana", new DateTime(2026, 10, 19, 9, 0, 0), "Dr. X", ModalidadeAtendimento.BsvApenas, null, chegada, apos, legado, false);

    [Fact]
    public void Tom_segue_a_situacao_da_passagem()
    {
        Assert.Equal("sucesso", SessoesEnfermagemNaTela.Tom(S(true, true)));
        Assert.Equal("aviso", SessoesEnfermagemNaTela.Tom(S(true, false)));
        Assert.Equal("info", SessoesEnfermagemNaTela.Tom(S(false, false, legado: true)));
    }

    [Fact]
    public void A_situacao_padrao_e_uma_das_opcoes_do_filtro()
        => Assert.Contains(SessoesEnfermagemNaTela.Situacoes, s => s.Codigo == SessoesEnfermagemNaTela.SituacaoPadrao);

    [Fact]
    public void Resumo_diz_quando_ha_proxima_pagina()
    {
        Assert.Equal("Página 1 · 12 sessão(ões)", SessoesEnfermagemNaTela.Resumo(0, 12, false));
        Assert.Contains("há mais", SessoesEnfermagemNaTela.Resumo(1, 50, true));
    }
}
