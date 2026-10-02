using Clinica.Application.Servicos;
using Clinica.Application.Tablet;
using Clinica.Domain.Entities;
using Clinica.Infrastructure.Tablet;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;
public sealed partial class AtendimentoTabletTests
{
    [Fact] public async Task Portal_preserva_grupos_na_leitura_correcao_e_execucao()
    {
        await HabilitarSemA1(); var p=await Folha(SituacaoPrescricao.Rascunho);
        var itens=new ItemInfusaoTablet[] {
            new("Item fictício A",null,"SF 0,9%","250 mL",ViaAdministracao.Endovenosa,null,null,false,null,GrupoInfusao:1),
            new("Item fictício B",null,"SF 0,9%","250 mL",ViaAdministracao.Endovenosa,null,null,false,null,GrupoInfusao:1),
            new("Item fictício C",null,"Diluente fictício","100 mL",ViaAdministracao.Endovenosa,null,null,false,null,GrupoInfusao:2)
        };
        var req=new RascunhoTablet(Guid.NewGuid(),PostoTabletService.Versao(p),"Conferência de grupos",null,null,null,null,false,itens,DiluicaoUnica:false);
        var r=await Posto.CorrigirRascunhoAsync(sessao,p.PacienteId,"infusao",p.Id,req,default);
        var nova=(await repo.ObterPrescricaoInternaAsync(r.Id))!;
        var detalhe=Json(await Posto.RascunhoAsync(sessao,nova.PacienteId,"infusao",nova.Id,default));
        Assert.Equal(new[]{1,1,2},detalhe.GetProperty("itens").EnumerateArray().Select(i=>i.GetProperty("grupoInfusao").GetInt32()));
        var telaAntiga=req with {Idempotencia=Guid.NewGuid(),Versao=PostoTabletService.Versao(nova),Itens=itens.Select(i=>i with {GrupoInfusao=null}).ToArray()};
        await Assert.ThrowsAsync<InvalidOperationException>(()=>Posto.CorrigirRascunhoAsync(sessao,nova.PacienteId,"infusao",nova.Id,telaAntiga,default));
        await db.Entry(sessao).ReloadAsync();
        await Posto.LiberarSemAssinaturaAsync(sessao,nova.Id,Liberacao(nova),default);
        await EntrarEnfermagemSemA1();
        var folha=Json(await Posto.InfusaoAsync(sessao,nova.Id,default));
        Assert.Equal(new[]{1,1,2},folha.GetProperty("itens").EnumerateArray().Select(i=>i.GetProperty("grupoInfusao").GetInt32()));
        foreach(var item in nova.Itens.OrderBy(i=>i.Ordem))
            await Posto.ChecarAsync(sessao,nova.Id,new(Guid.NewGuid(),item.Id,PostoTabletService.Versao(nova),SituacaoChecagem.Realizado,new(10,0),Data:svc.Hoje.AddDays(-1)),default);
        await Posto.EncerrarAsync(sessao,nova.Id,new(Guid.NewGuid(),PostoTabletService.Versao(nova)),default);
        Assert.Equal(SituacaoPrescricao.Encerrada,nova.Situacao);Assert.Empty(nova.Assinaturas);
        Assert.Equal(new int?[]{1,1,2},nova.Itens.OrderBy(i=>i.Ordem).Select(i=>i.GrupoInfusao));
    }
}
