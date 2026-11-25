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
        RotasA1Tablet.Mapear(app, sessao);
        RotasSafeIdTablet.Mapear(app, sessao);
        var grupo = app.MapGroup("/api/clinico");
        grupo.MapPost("/atendimentos/{id:int}/novo-bsv", async(HttpContext c, PortalTabletService portal,
            AtendimentoTabletService acesso, IAcompanhamentoPacienteService acompanhamento, int id) =>
        {
            var usuario = await acesso.AutorizarAsync(await sessao(c, portal), c.RequestAborted);
            await acompanhamento.IndicarBsvAsync(usuario.Id, id, c.RequestAborted);
            return Results.Ok(await acompanhamento.EstadoBsvAsync(usuario.Id, id, c.RequestAborted));
        });
        grupo.MapPost("/atividade", async(HttpContext c, PortalTabletService portal, AtendimentoTabletService svc) =>
        {
            await svc.AutorizarAsync(await sessao(c,portal),c.RequestAborted,Permissao.VerProntuario,renovarAtividade:true);
            return Results.NoContent();
        });
        grupo.MapGet("/acesso",async(HttpContext c,PortalTabletService portal,AtendimentoTabletService svc,ClinicaDbContext db,IClinicaRepositorio repo)=>
        {var u=await svc.AutorizarAsync(await sessao(c,portal),c.RequestAborted,Permissao.VerProntuario);
            var catalogo=await db.Especialidades.AsNoTracking()
                .Select(e=>new {codigo=e.Codigo,nome=e.Nome,e.Ativo}).ToListAsync(c.RequestAborted);
            var especialidades=catalogo.Where(e=>e.Ativo).Select(e=>new {e.codigo,e.nome})
                .Concat(Enum.GetValues<Especialidade>()
                    .Where(e=>!catalogo.Any(cadastrada=>string.Equals(cadastrada.codigo,e.ToString(),StringComparison.OrdinalIgnoreCase)))
                    .Select(e=>new {codigo=e.ToString(),nome=EspecialidadeInfo.NomeExibicao(e)}))
                .GroupBy(e=>e.codigo,StringComparer.OrdinalIgnoreCase).Select(g=>g.First())
                .Where(e=>u.Profissional?.Atende(nameof(ModalidadeAtendimento.Consulta),e.codigo)==true).ToArray();
            return Results.Ok(new {continuidadeSemAssinatura=await ContinuidadeSemAssinatura.HabilitadaAsync(repo,c.RequestAborted),nome=u.Nome,sessoesEnfermagem=u.Perfil==PerfilAcesso.Enfermagem && u.Pode(Permissao.RegistrarEvolucaoEnfermagem | Permissao.VerAgenda),atender=PoliticaAtendimentoTablet.PodeAtender(u),enfermagem=u.Pode(Permissao.ChecarPrescricao),prescrever=u.Pode(Permissao.Prescrever),consultaCodigo=(int)ModalidadeAtendimento.Consulta,especialidades,modalidades=Enum.GetValues<ModalidadeAtendimento>().Where(m=>(u.Perfil!=PerfilAcesso.Psicologia||m==ModalidadeAtendimento.Consulta)&&(m==ModalidadeAtendimento.Consulta?especialidades.Length>0:u.Profissional?.Atende(m.ToString())==true)).Select(m=>new {codigo=(int)m,nome=RotulosEnum.De(m)})});});
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
                var folha=tipo=="execucao" ? await infusao.FolhaAsync(documento,FolhaPrescricao.RegistroExecucao,c.RequestAborted) : await infusao.DocumentoInfusaoAsync(documento,c.RequestAborted);
                return Results.File(folha.Pdf,"application/pdf",$"infusao-{documento}.pdf");
            }
            return Results.File(await pdf.GerarAsync(documento,await parametros.ObterPrestadorAsync(c.RequestAborted),c.RequestAborted),"application/pdf",$"documento-{documento}.pdf");
        });
        grupo.MapGet("/atendimentos/{id:int}/documento/{documento:int}/assinatura/endereco",
            async(HttpContext c, PortalTabletService portal, EnderecoPrescricaoTabletService svc, int id, int documento)
                => Results.Ok(await svc.ConferirAsync(await sessao(c,portal),id,documento,c.RequestAborted)));
        grupo.MapPost("/atendimentos/{id:int}/documento/{documento:int}/assinatura/endereco",
            async(HttpContext c, PortalTabletService portal, EnderecoPrescricaoTabletService svc, int id, int documento, EnderecoPrescricaoTablet pedido) =>
            { await svc.CompletarAsync(await sessao(c,portal),id,documento,pedido.Endereco,c.RequestAborted); return Results.NoContent(); });
        // "Enviar ao paciente pelo WhatsApp" (nov/2026): o documento ASSINADO entra na MESMA fila do CRM que os apps
        // usam (EnvioAoPacienteService). Fora da janela de 24 h o CRM manda o modelo aprovado semdor_documento com o
        // PDF no cabeçalho; a trilha (quem, canal, para onde) grava no mesmo SaveChanges. Repetir não duplica.
        grupo.MapPost("/documentos/{documento:int}/enviar-whatsapp",
            async(HttpContext c, PortalTabletService portal, AtendimentoTabletService svc, ClinicaDbContext db, EnvioAoPacienteService envio, int documento) =>
            {
                var (u,_)=await svc.ExigirDocumentoAsync(await sessao(c,portal),0,"documento",documento,false,c.RequestAborted);
                if(!u.Pode(Permissao.Prescrever)) throw new UnauthorizedAccessException();
                var assinado=await db.DocumentosClinicos.AnyAsync(d=>d.Id==documento&&d.AssinaturaTipo!=null&&d.CanceladoEm==null,c.RequestAborted);
                if(!assinado) throw ErroFormularioTablet.Criar("Assine o documento antes de enviar ao paciente.");
                var r=await envio.EnfileirarWhatsAppAsync(documento,u.Login,c.RequestAborted);
                if(!r.Ok) throw ErroFormularioTablet.Criar(r.Frase);
                return Results.Ok(new {frase=r.Frase});
            });
        // Os dados do paciente que o papel exige e faltam na ficha (o comprador no receituário, o endereço na receita).
        // "/paciente" é o nome de todos os papéis (nov/2026); "/comprador" fica para a página que ainda esteja aberta.
        foreach (var rota in new[] { "comprador", "paciente" })
            grupo.MapPost("/atendimentos/{id:int}/documento/{documento:int}/assinatura/" + rota,
                async(HttpContext c, PortalTabletService portal, EnderecoPrescricaoTabletService svc, int id, int documento, CompradorPrescricaoTablet pedido) =>
                {
                    await svc.CompletarCompradorAsync(await sessao(c,portal),id,documento,
                        new(pedido.Rg,pedido.OrgaoEmissor,pedido.Endereco,pedido.Cidade,pedido.Uf,pedido.Telefone),c.RequestAborted);
                    return Results.NoContent();
                });
    }
}
public sealed record EnderecoPrescricaoTablet(string? Endereco);
public sealed record CompradorPrescricaoTablet(string? Rg, string? OrgaoEmissor, string? Endereco, string? Cidade, string? Uf, string? Telefone);
