using Clinica.Application.Abstracoes;
using Clinica.Domain;
using Clinica.Application.Assinatura;
using Clinica.Application.Servicos;
using Clinica.Application.Tablet;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Clinica.Infrastructure.Tablet;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Assinaturas.Api;

internal static class RotasAtendimentoTablet
{
    internal static void Mapear(WebApplication app, Func<HttpContext, PortalTabletService, Task<SessaoTablet>> sessao)
    {
        var grupo = app.MapGroup("/api/clinico");
        grupo.MapPost("/atividade", async(HttpContext c, PortalTabletService portal, AtendimentoTabletService svc) =>
        {
            await svc.AutorizarAsync(await sessao(c,portal),c.RequestAborted,Permissao.VerProntuario,renovarAtividade:true);
            return Results.NoContent();
        });
        grupo.MapGet("/acesso",async(HttpContext c,PortalTabletService portal,AtendimentoTabletService svc,ClinicaDbContext db)=>
        {var u=await svc.AutorizarAsync(await sessao(c,portal),c.RequestAborted,Permissao.VerProntuario);
            var catalogo=await db.Especialidades.AsNoTracking()
                .Select(e=>new {codigo=e.Codigo,nome=e.Nome,e.Ativo}).ToListAsync(c.RequestAborted);
            var especialidades=catalogo.Where(e=>e.Ativo).Select(e=>new {e.codigo,e.nome})
                .Concat(Enum.GetValues<Especialidade>()
                    .Where(e=>!catalogo.Any(cadastrada=>string.Equals(cadastrada.codigo,e.ToString(),StringComparison.OrdinalIgnoreCase)))
                    .Select(e=>new {codigo=e.ToString(),nome=EspecialidadeInfo.NomeExibicao(e)}))
                .GroupBy(e=>e.codigo,StringComparer.OrdinalIgnoreCase).Select(g=>g.First())
                .Where(e=>u.Profissional?.Atende(nameof(ModalidadeAtendimento.Consulta),e.codigo)==true).ToArray();
            return Results.Ok(new {nome=u.Nome,sessoesEnfermagem=u.Perfil==PerfilAcesso.Enfermagem && u.Pode(Permissao.RegistrarEvolucaoEnfermagem | Permissao.VerAgenda),atender=PoliticaAtendimentoTablet.PodeAtender(u),enfermagem=u.Pode(Permissao.ChecarPrescricao),prescrever=u.Pode(Permissao.Prescrever),consultaCodigo=(int)ModalidadeAtendimento.Consulta,especialidades,modalidades=Enum.GetValues<ModalidadeAtendimento>().Where(m=>(u.Perfil!=PerfilAcesso.Psicologia||m==ModalidadeAtendimento.Consulta)&&(m==ModalidadeAtendimento.Consulta?especialidades.Length>0:u.Profissional?.Atende(m.ToString())==true)).Select(m=>new {codigo=(int)m,nome=RotulosEnum.De(m)})});});
        grupo.MapGet("/dia", async(HttpContext c, PortalTabletService portal, AtendimentoTabletService svc, DateOnly? data)
            => Results.Ok(await svc.DiaAsync(await sessao(c,portal),data,c.RequestAborted)));
        grupo.MapGet("/atendimentos/{id:int}", async(HttpContext c, PortalTabletService portal, AtendimentoTabletService svc, int id)
            => Results.Ok(await svc.AbrirAsync(await sessao(c,portal),id,c.RequestAborted)));
        grupo.MapGet("/atendimentos/{id:int}/materiais", async(HttpContext c, PortalTabletService portal, AtendimentoTabletService svc, int id)
            => Results.Ok(await svc.MateriaisAsync(await sessao(c,portal),id,c.RequestAborted)));
        grupo.MapPost("/atendimentos/{id:int}/materiais", async(HttpContext c, PortalTabletService portal, AtendimentoTabletService svc, int id, RegistrarMateriaisTablet pedido)
            => Results.Ok(await svc.RegistrarMateriaisAsync(await sessao(c,portal),id,pedido,c.RequestAborted)));
        grupo.MapGet("/atendimentos/{id:int}/mapas/{evolucao:int}", async(HttpContext c, PortalTabletService portal, AtendimentoTabletService svc, int id,int evolucao)
            => Results.Ok(await svc.CopiarMapaAsync(await sessao(c,portal),id,evolucao,c.RequestAborted)));
        grupo.MapPost("/atendimentos/{id:int}/salvar", async(HttpContext c, PortalTabletService portal, AtendimentoTabletService svc, int id, SalvarAtendimentoTablet pedido)
            => Results.Ok(await svc.SalvarAsync(await sessao(c,portal),id,pedido,c.RequestAborted)));
        grupo.MapPost("/atendimentos/{id:int}/documentos", async(HttpContext c, PortalTabletService portal, AtendimentoTabletService svc, int id, EmitirDocumentoTablet pedido)
            => Results.Ok(await svc.EmitirAsync(await sessao(c,portal),id,pedido,c.RequestAborted)));
        grupo.MapPost("/atendimentos/{id:int}/modelos-mapa", async(HttpContext c, PortalTabletService portal, AtendimentoTabletService svc, int id, SalvarModeloMapaTablet pedido)
            => Results.Ok(await svc.SalvarModeloMapaAsync(await sessao(c,portal),id,pedido,c.RequestAborted)));
        grupo.MapGet("/atendimentos/{id:int}/{tipo}/{documento:int}/pdf", async(HttpContext c, PortalTabletService portal,
            AtendimentoTabletService svc, ClinicaDbContext db, DocumentosClinicosPdfService pdf, ParametrosService parametros,
            AssinaturaDePrescricaoService infusao,int id,string tipo,int documento) =>
        {
            var (u,a)=await svc.ExigirDocumentoAsync(await sessao(c,portal),id,tipo,documento,false,c.RequestAborted);
            db.Auditoria.Add(new EventoAuditoria {Operador=u.Login,PacienteId=a,
                Acao="TabletClinicoPdf",Detalhe="Consulta de via clínica"});
            await db.SaveChangesAsync(c.RequestAborted);
            if(tipo is "infusao" or "execucao")
            {
                var folha=await infusao.FolhaAsync(documento,tipo=="execucao"?FolhaPrescricao.RegistroExecucao:FolhaPrescricao.Prescricao,c.RequestAborted);
                return Results.File(folha.Pdf,"application/pdf",$"infusao-{documento}.pdf");
            }
            return Results.File(await pdf.GerarAsync(documento,await parametros.ObterPrestadorAsync(c.RequestAborted),c.RequestAborted),"application/pdf",$"documento-{documento}.pdf");
        });
        grupo.MapGet("/safeid",async(HttpContext c,PortalTabletService portal,AtendimentoTabletService acesso,SafeIdTabletService svc)=>
        {await acesso.AutorizarAsync(await sessao(c,portal),c.RequestAborted,Permissao.VerProntuario);return Results.Ok(new {habilitado=svc.Habilitado});});
        grupo.MapGet("/atendimentos/{id:int}/documento/{documento:int}/safeid/endereco",
            async(HttpContext c, PortalTabletService portal, EnderecoPrescricaoTabletService svc, int id, int documento)
                => Results.Ok(await svc.ConferirAsync(await sessao(c,portal),id,documento,c.RequestAborted)));
        grupo.MapPost("/atendimentos/{id:int}/documento/{documento:int}/safeid/endereco",
            async(HttpContext c, PortalTabletService portal, EnderecoPrescricaoTabletService svc, int id, int documento, EnderecoPrescricaoTablet pedido) =>
            { await svc.CompletarAsync(await sessao(c,portal),id,documento,pedido.Endereco,c.RequestAborted); return Results.NoContent(); });
        grupo.MapPost("/atendimentos/{id:int}/{tipo}/{documento:int}/safeid", async(HttpContext c,PortalTabletService portal,
            SafeIdTabletService svc,int id,string tipo,int documento,PedidoSafeIdTablet pedido)
            => Results.Ok(await svc.IniciarAsync(await sessao(c,portal),id,tipo,documento,pedido.ConfirmouAlergia,c.RequestAborted)));
        // O retorno não assina: state/PKCE são de uso único; assinatura exige a sessão original e CSRF em POST.
        app.MapGet("/safeid/retorno", (HttpContext c, AutorizacoesSafeIdTablet autorizacoes, string? state,string? code,string? error) =>
        {
            if(state is null) throw new RecursoClinicoIndisponivel();
            var id=autorizacoes.Receber(state,code,error);
            return Results.Redirect("/profissional/?assinatura="+id);
        });
        grupo.MapGet("/safeid/{id:guid}", async(HttpContext c,PortalTabletService portal,AtendimentoTabletService svc,
            AutorizacoesSafeIdTablet autorizacoes,Guid id) =>
        {
            var s=await sessao(c,portal); await svc.AutorizarAsync(s,c.RequestAborted,Permissao.VerProntuario);
            var a=autorizacoes.Obter(id,s.Id);
            var (_,paciente)=await svc.ExigirDocumentoAsync(s,a.Agendamento,a.Tipo,a.Documento,true,c.RequestAborted);
            return Results.Ok(new {a.Id,a.Agendamento,a.Documento,a.Tipo,a.Situacao,PacienteId=paciente});
        });
        grupo.MapPost("/safeid/{id:guid}/concluir",async(HttpContext c,PortalTabletService portal,SafeIdTabletService svc,Guid id)
            => Results.Ok(await svc.ConcluirAsync(await sessao(c,portal),id,c.RequestAborted)));
    }
}
public sealed record PedidoSafeIdTablet(bool ConfirmouAlergia);
public sealed record EnderecoPrescricaoTablet(string? Endereco);
