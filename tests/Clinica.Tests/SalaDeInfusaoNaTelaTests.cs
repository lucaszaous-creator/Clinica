using Clinica.Application.Modelos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Xunit;

namespace Clinica.Tests;

public class SalaDeInfusaoNaTelaTests
{
    private static readonly DateOnly Hoje = new(2026, 10, 19);

    private static PrescricaoInterna Folha(int id, bool origemEnfermagem = false, int? registradaPor = null, DateTime? assinadaEm = null)
        => new() { Id = id, Data = Hoje, OrigemEnfermagem = origemEnfermagem, RegistradaPorUsuarioId = registradaPor, AssinadaEm = assinadaEm };

    [Fact]
    public void Folha_que_aguarda_assinatura_nao_aparece_duas_vezes()
    {
        var a = Folha(1);
        var linhas = SalaDeInfusaoNaTela.Montar([a, Folha(2)], [a], podeChecar: true, usuarioId: 9, Hoje);
        Assert.Equal(new[] { 1, 2 }, linhas.Select(l => l.PrescricaoId));
    }

    [Fact]
    public void Folha_da_enfermagem_so_aparece_a_quem_pode_checar_e_antes_de_validada_so_a_quem_registrou()
    {
        var daEnfermagem = Folha(1, origemEnfermagem: true, registradaPor: 5);
        Assert.Empty(SalaDeInfusaoNaTela.Montar([daEnfermagem], [], podeChecar: false, usuarioId: 5, Hoje));
        Assert.Empty(SalaDeInfusaoNaTela.Montar([daEnfermagem], [], podeChecar: true, usuarioId: 6, Hoje));
        Assert.Single(SalaDeInfusaoNaTela.Montar([daEnfermagem], [], podeChecar: true, usuarioId: 5, Hoje));

        var validada = Folha(2, origemEnfermagem: true, registradaPor: 5, assinadaEm: new DateTime(2026, 10, 19, 10, 0, 0));
        Assert.Single(SalaDeInfusaoNaTela.Montar([validada], [], podeChecar: true, usuarioId: 6, Hoje));
    }

    [Fact]
    public void Resumo_separa_o_que_se_resolve_administrando_do_que_se_resolve_assinando()
    {
        Assert.Equal("Nenhuma prescrição de infusão para hoje.", SalaDeInfusaoNaTela.Resumo([]));
        var encerrada = new PrescricaoInterna { Id = 3, Data = Hoje, Situacao = SituacaoPrescricao.Encerrada, ExigeAssinaturaEletronicaDaExecucao = true };
        var resumo = SalaDeInfusaoNaTela.Resumo([LinhaSalaInfusao.De(encerrada, Hoje)]);
        Assert.Contains("1 folha", resumo);
        Assert.Contains("1 aguardando a assinatura da enfermagem", resumo);
    }

    [Fact]
    public void Resumo_das_validacoes_concorda_no_singular_e_no_plural()
    {
        Assert.Equal("1 infusão aguardando sua avaliação e assinatura", SalaDeInfusaoNaTela.ResumoValidacoes(1));
        Assert.Equal("3 infusões aguardando sua avaliação e assinatura", SalaDeInfusaoNaTela.ResumoValidacoes(3));
    }
}
