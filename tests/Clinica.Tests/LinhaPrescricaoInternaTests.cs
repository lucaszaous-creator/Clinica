using Clinica.Application.Modelos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Xunit;

namespace Clinica.Tests;

public class LinhaPrescricaoInternaTests
{
    [Fact]
    public void Rascunho_diz_que_nao_foi_assinado_e_so_quem_prescreve_edita_ou_cancela()
    {
        var p = new PrescricaoInterna { Id = 1, Situacao = SituacaoPrescricao.Rascunho, Data = new DateOnly(2026, 10, 19) };
        var comBit = LinhaPrescricaoInterna.De(p, podePrescrever: true);
        var semBit = LinhaPrescricaoInterna.De(p, podePrescrever: false);

        Assert.Equal("ainda não assinada", comBit.Execucao);
        Assert.Equal("0 itens", comBit.Resumo);
        Assert.True(comBit.PodeCancelar);
        Assert.False(semBit.PodeCancelar);
        Assert.False(semBit.PodeEditar);
    }

    [Fact]
    public void Encerrada_nao_se_cancela_nem_com_o_bit()
    {
        var p = new PrescricaoInterna { Id = 2, Situacao = SituacaoPrescricao.Encerrada };
        var linha = LinhaPrescricaoInterna.De(p, podePrescrever: true);
        Assert.False(linha.PodeCancelar);
        Assert.Contains("realizados", linha.Execucao);
    }
}
