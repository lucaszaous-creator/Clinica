using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Xunit;

namespace Clinica.Tests;
public sealed class EtapasInfusaoTests
{
    [Fact] public void Prescricao_medica_so_conclui_depois_da_execucao_e_dos_arquivos_assinados()
    {
        var p = new PrescricaoInterna { ExigeAssinaturaEletronicaDaExecucao = true };
        Assert.Equal("medico", Assert.Single(EtapasInfusao.Da(p).Where(e => e.Atual)).Codigo);
        p.AssinadaEm = DateTime.Now; p.Situacao = SituacaoPrescricao.Assinada;
        p.Assinaturas.Add(new() { Papel = PapelAssinatura.Prescritor, ArquivoId = 1 });
        Assert.Equal("execucao", Assert.Single(EtapasInfusao.Da(p).Where(e => e.Atual)).Codigo);
        p.Situacao = SituacaoPrescricao.Encerrada;
        Assert.Equal("enfermagem", Assert.Single(EtapasInfusao.Da(p).Where(e => e.Atual)).Codigo);
        p.Assinaturas.Add(new() { Papel = PapelAssinatura.Executante, ArquivoId = 2 });
        Assert.All(EtapasInfusao.Da(p), e => Assert.True(e.Concluida));
        Assert.DoesNotContain(EtapasInfusao.Da(p), e => e.Atual);
    }
    [Fact] public void Origem_enfermagem_mostra_validacao_medica_depois_da_primeira_assinatura()
    {
        var p = new PrescricaoInterna { OrigemEnfermagem = true, Situacao = SituacaoPrescricao.Encerrada };
        Assert.Equal(new[] { "execucao", "enfermagem", "medico", "conclusao" }, EtapasInfusao.Da(p).Select(e => e.Codigo));
        Assert.Equal("enfermagem", Assert.Single(EtapasInfusao.Da(p).Where(e => e.Atual)).Codigo);
        p.Assinaturas.Add(new() { Papel = PapelAssinatura.Executante, ArquivoId = 1, ArquivoRegistroId = 1 });
        Assert.Equal("medico", Assert.Single(EtapasInfusao.Da(p).Where(e => e.Atual)).Codigo);
        p.DevolvidaEm = DateTime.Now;
        Assert.Equal("revisao", Assert.Single(EtapasInfusao.Da(p).Where(e => e.Atual)).Codigo);
        Assert.True(EtapasInfusao.Da(p).Single(e => e.Codigo == "enfermagem").Concluida);
        Assert.False(EtapasInfusao.Da(p).Last().Concluida);
    }
    [Fact] public void Via_impressa_e_cancelamento_nao_viram_assinatura_eletronica_concluida()
    {
        var p = new PrescricaoInterna { Situacao = SituacaoPrescricao.Encerrada, AssinadaEm = DateTime.Now, ExigeAssinaturaEletronicaDaExecucao = false };
        var atual = Assert.Single(EtapasInfusao.Da(p).Where(e => e.Atual));
        Assert.Equal("Conferir assinatura na via impressa", atual.Titulo); Assert.True(atual.Atencao);
        p.Situacao = SituacaoPrescricao.Cancelada; p.CanceladaEm = DateTime.Now;
        Assert.DoesNotContain(EtapasInfusao.Da(p), e => e.Atual);
        Assert.False(EtapasInfusao.Da(p).Last().Concluida);
    }
}
