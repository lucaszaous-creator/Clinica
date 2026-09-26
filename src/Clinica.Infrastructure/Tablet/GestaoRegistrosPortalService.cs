using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
using Clinica.Application.Tablet;
using Clinica.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Infrastructure.Tablet;

/// <summary>Consulta e reversão auditada de registros pelo gerente no portal.</summary>
public sealed class GestaoRegistrosPortalService(
    ClinicaDbContext db, IClinicaRepositorio repo, EstornoAtendimentoService estornos,
    PrescricaoInternaService prescricoes)
{
    public static bool PodeGerenciar(UsuarioSistema? usuario) => usuario is {Ativo:true,DeveTrocarSenha:false,Perfil:PerfilAcesso.Gerente}
        && usuario.Pode(Permissao.GerenciarUsuarios);

    private static UsuarioSistema Autorizar(SessaoTablet sessao)
    {
        if (sessao.Modo != "equipe" || !PodeGerenciar(sessao.Usuario)) throw new UnauthorizedAccessException();
        return sessao.Usuario!;
    }

    public async Task<object> BuscarPacientesAsync(SessaoTablet sessao,string? termo,CancellationToken ct)
    {
        Autorizar(sessao);
        var busca=termo?.Trim();
        if (busca?.Length is not (>=3 and <=80)) throw ErroFormularioTablet.Criar("Digite ao menos três caracteres do nome do paciente.");
        var normalizada=busca.ToLower();
        return await db.Pacientes.AsNoTracking().Where(p=>p.Nome.ToLower().Contains(normalizada))
            .OrderBy(p=>p.Nome).Take(20).Select(p=>new {p.Id,p.Nome,Nascimento=p.DataNascimento}).ToListAsync(ct);
    }

    public async Task<object> RegistrosAsync(SessaoTablet sessao,int pacienteId,CancellationToken ct)
    {
        var gerente=Autorizar(sessao);
        var paciente=await db.Pacientes.AsNoTracking().Where(p=>p.Id==pacienteId)
            .Select(p=>new {p.Id,p.Nome,Nascimento=p.DataNascimento}).SingleOrDefaultAsync(ct)
            ?? throw new RecursoClinicoIndisponivel();
        var atendimentos=await db.Atendimentos.AsNoTracking().Where(a=>a.PacienteId==pacienteId)
            .OrderByDescending(a=>a.Data).ThenByDescending(a=>a.Id).Take(100)
            .Select(a=>new {a.Id,a.Numero,a.Data,Modalidade=a.Modalidade.ToString(),a.EstornadoEm,
                AgendamentoId=db.Agendamentos.Where(h=>h.AtendimentoId==a.Id).Select(h=>(int?)h.Id).FirstOrDefault()}).ToListAsync(ct);
        var infusoes=await db.PrescricoesInternas.AsNoTracking().Where(p=>p.PacienteId==pacienteId)
            .OrderByDescending(p=>p.Data).ThenByDescending(p=>p.Id).Take(100)
            .Select(p=>new {p.Id,p.Numero,p.Data,p.Hora,Situacao=p.Situacao.ToString(),
                p.OrigemEnfermagem,p.CanceladaEm,p.DevolvidaEm}).ToListAsync(ct);
        db.Auditoria.Add(new EventoAuditoria {Operador=gerente.Login,PacienteId=pacienteId,
            Acao="PortalGestaoRegistrosConsultada",Detalhe="Consulta administrativa dos atendimentos e infusões"});
        await db.SaveChangesAsync(ct);
        return new {Paciente=paciente,Atendimentos=atendimentos,Infusoes=infusoes};
    }

    public async Task<object> PreviaAtendimentoAsync(SessaoTablet sessao,int id,CancellationToken ct)
    {
        Autorizar(sessao);
        var previa=await estornos.PreverAsync(id,ct);
        return previa;
    }

    public async Task<ResultadoDoEstorno> EstornarAtendimentoAsync(SessaoTablet sessao,int id,
        EstornarAtendimentoPortal pedido,CancellationToken ct)
    {
        var gerente=Autorizar(sessao);
        var previa=await estornos.PreverAsync(id,ct);
        if (!previa.Pode) throw ErroFormularioTablet.Criar(previa.Impedimento!);
        if (pedido.NumeroConfirmado?.Trim()!=previa.Numero) throw ErroFormularioTablet.Criar("O número confirmado não corresponde ao atendimento.");
        if (pedido.Motivo?.Trim().Length is not (>=5 and <=500)) throw ErroFormularioTablet.Criar("Descreva o motivo em 5 a 500 caracteres.");
        return await estornos.EstornarAsync(id,new DecisaoDeEstorno(pedido.Motivo.Trim(),
            pedido.DesfazerCaixa,pedido.DevolverSessaoDoPacote,pedido.DevolverInsumoAoEstoque),gerente.Login,ct);
    }

    public async Task<object> PreviaInfusaoAsync(SessaoTablet sessao,int id,CancellationToken ct)
    {
        Autorizar(sessao);
        var p=await repo.ObterPrescricaoInternaAsync(id,ct)??throw new RecursoClinicoIndisponivel();
        return new {p.Id,p.Numero,p.PacienteId,Paciente=p.Paciente?.Nome,p.Data,p.Hora,
            Situacao=p.Situacao.ToString(),p.OrigemEnfermagem,p.CanceladaEm,p.DevolvidaEm,
            Itens=p.Itens.Count,Checagens=p.Itens.Sum(i=>i.Checagens.Count),Assinaturas=p.Assinaturas.Count,
            Versao=PostoTabletService.Versao(p),
            Impedimento=p.CanceladaEm!=null?"Esta infusão já está cancelada."
                :p.DevolvidaEm!=null?"A folha devolvida permanece no histórico; revise a nova versão."
                :p.Situacao==SituacaoPrescricao.Encerrada&&!p.OrigemEnfermagem?"A execução encerrada não pode ser cancelada."
                :!p.OrigemEnfermagem&&p.Itens.Any(i=>i.ChecagemVigente!=null)?"Há itens executados nesta folha; ela não pode ser cancelada."
                :null};
    }

    public async Task<object> CancelarInfusaoAsync(SessaoTablet sessao,int id,CancelarInfusaoPortal pedido,CancellationToken ct)
    {
        var gerente=Autorizar(sessao);
        var p=await repo.ObterPrescricaoInternaAsync(id,ct)??throw new RecursoClinicoIndisponivel();
        if (pedido.NumeroConfirmado?.Trim()!=p.Numero) throw ErroFormularioTablet.Criar("O número confirmado não corresponde à infusão.");
        if (pedido.Motivo?.Trim().Length is not (>=5 and <=500)) throw ErroFormularioTablet.Criar("Descreva o motivo em 5 a 500 caracteres.");
        if (PostoTabletService.Versao(p)!=pedido.Versao) throw new ConflitoClinicoTablet("A infusão mudou. Atualize a prévia antes de cancelar.");
        await prescricoes.CancelarAsync(id,pedido.Motivo.Trim(),gerente.Login,ct);
        return new {Id=id,Situacao="Cancelada"};
    }
}

public sealed record EstornarAtendimentoPortal(string? NumeroConfirmado,string? Motivo,
    bool DesfazerCaixa=false,bool DevolverSessaoDoPacote=false,bool DevolverInsumoAoEstoque=false);
public sealed record CancelarInfusaoPortal(string? NumeroConfirmado,string? Versao,string? Motivo);
