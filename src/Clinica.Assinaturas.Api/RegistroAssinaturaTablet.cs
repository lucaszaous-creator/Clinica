using Clinica.Application.Tablet;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Clinica.Infrastructure.Tablet;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Assinaturas.Api;

/// <summary>Impede nova tentativa cega apÃ³s um ato remoto de resultado incerto.</summary>
public sealed class RegistroAssinaturaTablet(ClinicaDbContext db, TimeProvider tempo)
{
    private long Agora => tempo.GetUtcNow().ToUnixTimeMilliseconds();
    public async Task<OperacaoAssinaturaTablet?> AtivaAsync(string tipo,int documento,CancellationToken ct)
    {
        var op=await db.OperacoesAssinaturaTablet.AsNoTracking().SingleOrDefaultAsync(x=>x.ChaveAtiva==tipo+":"+documento,ct);
        if(op?.Situacao=="aguardando" && op.ExpiraEm<=Agora)return null;
        if(op?.Situacao=="assinando" && Agora-op.AtualizadaEm>180_000)op.Situacao="verificar";
        return op;
    }
    public async Task CriarAsync(AutorizacoesSafeIdTablet.Autorizacao a, CancellationToken ct)
    {
        // SÃ³ a autorizaÃ§Ã£o ainda nÃ£o consumida pode expirar liberando outra tentativa.
        await db.OperacoesAssinaturaTablet.Where(x => x.Tipo == a.Tipo && x.Documento == a.Documento
            && x.Situacao == "aguardando" && x.ExpiraEm <= Agora)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Situacao, "expirado").SetProperty(x => x.ChaveAtiva, (string?)null), ct);
        var ativa = await db.OperacoesAssinaturaTablet.AsNoTracking()
            .SingleOrDefaultAsync(x => x.ChaveAtiva == a.Tipo + ":" + a.Documento, ct);
        if (ativa is not null)
            throw new ConflitoClinicoTablet("HÃ¡ uma assinatura em andamento ou com resultado a conferir. Consulte o documento antes de autorizar novamente.");
        db.OperacoesAssinaturaTablet.Add(new OperacaoAssinaturaTablet { Id=a.Id, SessaoId=a.Sessao,
            Tipo=a.Tipo, Documento=a.Documento, Agendamento=a.Agendamento, ChaveAtiva=a.Tipo+":"+a.Documento,
            ExpiraEm=a.ExpiraEm, AtualizadaEm=Agora });
        await db.SaveChangesAsync(ct);
    }
    public async Task IniciarAsync(Guid id, CancellationToken ct)
    {
        var alteradas=await db.OperacoesAssinaturaTablet.Where(x=>x.Id==id && x.Situacao=="aguardando")
            .ExecuteUpdateAsync(s=>s.SetProperty(x=>x.Situacao,"assinando").SetProperty(x=>x.AtualizadaEm,Agora),ct);
        if(alteradas!=1)throw new ConflitoClinicoTablet("Esta tentativa jÃ¡ foi processada. Confira o documento no histÃ³rico.");
    }
    // Chamado DENTRO da mesma transaÃ§Ã£o que grava PDF e assinatura.
    public async Task ConfirmarAsync(Guid id,CancellationToken ct)
    {
        var alteradas=await db.OperacoesAssinaturaTablet.Where(x=>x.Id==id && x.Situacao=="assinando")
            .ExecuteUpdateAsync(s=>s.SetProperty(x=>x.Situacao,"concluido").SetProperty(x=>x.ChaveAtiva,(string?)null).SetProperty(x=>x.AtualizadaEm,Agora),ct);
        if(alteradas!=1)throw new ConflitoClinicoTablet("Não foi possível confirmar o recibo da assinatura. Confira o documento antes de repetir.");
    }
    public Task FalharAsync(Guid id,bool resultadoIncerto,CancellationToken ct) => db.OperacoesAssinaturaTablet
        .Where(x=>x.Id==id && (x.Situacao=="aguardando" || x.Situacao=="assinando"))
        .ExecuteUpdateAsync(s=>s.SetProperty(x=>x.Situacao,resultadoIncerto?"verificar":"falha")
            .SetProperty(x=>x.ChaveAtiva,x=>resultadoIncerto?x.ChaveAtiva:null).SetProperty(x=>x.AtualizadaEm,Agora),ct);
    public async Task<OperacaoAssinaturaTablet?> ObterAsync(Guid id,string sessao,CancellationToken ct)
    {
        var op=await db.OperacoesAssinaturaTablet.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id && x.SessaoId==sessao,ct);
        if(op?.Situacao=="aguardando" && op.ExpiraEm<=Agora)op.Situacao="expirado";
        if(op?.Situacao=="assinando" && Agora-op.AtualizadaEm>180_000)op.Situacao="verificar";
        return op;
    }
}
