using Clinica.Application.Tablet;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Infrastructure.Tablet;

public sealed partial class PostoTabletService
{
    public async Task<object> PendenciasAsync(SessaoTablet s,int pagina,CancellationToken ct)
    {
        var u=await Autorizar(s,ct);
        if(pagina is <0 or >10000)throw ErroFormularioTablet.Criar("Página inválida.");
        var inicio=acesso.Hoje.AddDays(-90).ToDateTime(TimeOnly.MinValue);
        var fim=acesso.Hoje.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var sessoes=await db.Agendamentos.AsNoTracking()
            .Where(a=>PoliticaAtendimentoTablet.PodeAtender(u)&&a.ProfissionalId==u.ProfissionalId&&a.DataHora>=inicio&&a.DataHora<fim
                &&(a.Status==StatusAgendamento.Agendado||a.Status==StatusAgendamento.Realizado&&a.FimAtendimentoEm==null))
            .OrderBy(a=>a.DataHora).ThenBy(a=>a.Id).Skip(pagina*50).Take(51)
            .Select(a=>new {a.Id,a.PacienteId,Paciente=a.Paciente!.Nome,a.DataHora,
                EvolucaoSalva=db.Evolucoes.Any(e=>e.AgendamentoId==a.Id&&e.CanceladaEm==null),a.InicioAtendimentoEm}).ToListAsync(ct);
        var podePrescrever=u.Pode(Permissao.Prescrever);
        var documentos=await db.DocumentosClinicos.AsNoTracking()
            .Where(d=>podePrescrever&&d.ProfissionalId==u.ProfissionalId&&d.CanceladoEm==null&&d.AssinadoEm==null&&d.PacienteAssinadoEm==null&&TiposEditaveis.Contains(d.Tipo))
            .OrderBy(d=>d.Id).Skip(pagina*50).Take(51)
            .Select(d=>new {d.Id,d.PacienteId,Paciente=d.Paciente!.Nome,d.Data,d.Numero,Tipo=d.Tipo.ToString()}).ToListAsync(ct);
        var recepcao=await db.Agendamentos.AsNoTracking()
            .Where(a=>PoliticaAtendimentoTablet.PodeAtender(u)&&a.ProfissionalId==u.ProfissionalId&&a.DataHora>=inicio&&a.DataHora<fim
                &&a.FimAtendimentoEm!=null&&a.Atendimento!=null&&a.Atendimento.EstornadoEm==null
                &&(a.Atendimento.Codigos.Any(c=>c.DataBaixa==null&&c.Status!=StatusCodigo.NaoAplicavel)
                    ||!a.Atendimento.Codigos.Any(c=>c.Status!=StatusCodigo.NaoAplicavel)
                    &&!db.ConsumosPacote.Any(c=>c.AtendimentoId==a.AtendimentoId&&c.CanceladoEm==null)
                    &&!db.Lancamentos.Any(l=>l.AtendimentoId==a.AtendimentoId&&l.Tipo==TipoLancamento.Entrada&&l.Status!=StatusLancamento.Cancelado)))
            .OrderBy(a=>a.DataHora).ThenBy(a=>a.Id).Skip(pagina*50).Take(51)
            .Select(a=>new {a.Id,a.PacienteId,Paciente=a.Paciente!.Nome,a.DataHora,Numero=a.Atendimento!.Numero,
                GuiasSemBaixa=a.Atendimento.Codigos.Count(c=>c.DataBaixa==null&&c.Status!=StatusCodigo.NaoAplicavel)}).ToListAsync(ct);
        return new {Pagina=pagina,Desde=DateOnly.FromDateTime(inicio),Ate=acesso.Hoje,
            Mais=sessoes.Count>50||documentos.Count>50||recepcao.Count>50,
            MaisDocumentos=documentos.Count>50,
            Sessoes=sessoes.Take(50),Documentos=documentos.Take(50),Recepcao=recepcao.Take(50)};
    }
}
