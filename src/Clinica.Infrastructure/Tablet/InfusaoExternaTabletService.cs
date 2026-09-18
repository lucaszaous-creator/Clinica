using Clinica.Application.Servicos;
using Clinica.Application.Tablet;
using Clinica.Domain.Entities;

namespace Clinica.Infrastructure.Tablet;

public sealed partial class PostoTabletService
{
    public async Task<object> ContextoEnfermagemAsync(SessaoTablet s, int paciente, DateOnly data, CancellationToken ct)
    {
        var u = await Autorizar(s,ct,Permissao.RegistrarEvolucaoEnfermagem);
        await Paciente(paciente,ct);
        var sessoes = await repo.AgendamentosDoPacienteNoDiaAsync(paciente,data,ct);
        var medicos = await new PrescricaoInternaService(repo,conferencia).MedicosParaValidacaoAsync(ct);
        await Auditar(u,paciente,"TabletContextoEnfermagemConsultado",ct);await db.SaveChangesAsync(ct);
        return new { Sessoes = sessoes.Where(a=>a.Status is StatusAgendamento.Agendado or StatusAgendamento.Realizado)
                .Select(a=>new {a.Id,a.DataHora,a.ProfissionalId,Profissional=a.Profissional?.Nome,a.FimAtendimentoEm,
                    PermiteEvolucaoEnfermagem=EvolucaoEnfermagemService.PermiteEvolucao(a.ModalidadePrevista)}),
            Medicos = medicos.Where(p=>p.Id!=u.ProfissionalId).Select(p=>new {p.Id,p.Nome}) };
    }

    public Task<ResultadoFichaTablet> RegistrarInfusaoExternaAsync(SessaoTablet s,int paciente,InfusaoExternaTablet p,CancellationToken ct)
        => Escrever(s,paciente,p.Idempotencia,p,"TabletInfusaoExterna",
            Permissao.ChecarPrescricao|Permissao.RegistrarEvolucaoEnfermagem,async u=> {
                if(p.Dados.PacienteId!=paciente)throw new RecursoClinicoIndisponivel();
                var registro=await new PrescricaoInternaService(repo,conferencia).RegistrarExecucaoExternaAsync(p.Dados,u.Id,ct);
                return new ResultadoFichaTablet(registro.Id);
            },ct);
}
