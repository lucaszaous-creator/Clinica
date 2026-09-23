using Clinica.Application.Servicos;
using Clinica.Application.Tablet;
using Clinica.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Infrastructure.Tablet;

public sealed partial class PostoTabletService
{
    private static string Hash(object? valor) => ContratoTablet.Hash(ContratoTablet.Serializar(valor));
    private static void ConferirVersao(string? enviada, string atual)
    {
        if (enviada != atual) throw new ConflitoClinicoTablet("O registro mudou. Atualize a ficha e confira antes de salvar.");
    }
    private static void Textos(int limite, params string?[] valores)
    {
        if (valores.Any(v => v?.Length > limite)) throw ErroFormularioTablet.Criar($"Use no máximo {limite} caracteres por campo.");
    }
    private static object? DadosAnamnese(AnamnesePaciente? a) => a is null ? null : new {
        a.Id,a.AntecedentesPessoais,a.AntecedentesFamiliares,a.HabitosDeVida,a.HistoriaObstetrica,
        a.RevisaoDeSistemas,a.Observacoes,CriadaEm=a.CriadaEm.ToString("yyyy-MM-ddTHH:mm:ss.fff"),AtualizadaEm=a.AtualizadaEm?.ToString("yyyy-MM-ddTHH:mm:ss.fff")};
    private static object DadosProblema(ProblemaPaciente p) => new {
        p.Id,p.Natureza,p.Descricao,p.Cid,p.Inicio,p.Fim,p.Situacao,p.Observacoes,AtualizadoEm=p.AtualizadoEm?.ToString("yyyy-MM-ddTHH:mm:ss.fff")};
    private static string VersaoMedida(MedidaClinica m) => Hash(new {m.Id,m.Data,m.TipoCodigo,
        Valor=m.Valor.ToString("G29",System.Globalization.CultureInfo.InvariantCulture),
        ValorSecundario=m.ValorSecundario?.ToString("G29",System.Globalization.CultureInfo.InvariantCulture),m.Observacoes});

    public Task<ResultadoFichaTablet> SalvarAnamneseAsync(SessaoTablet s,int paciente,AnamneseTablet p,CancellationToken ct)
        => Escrever(s,paciente,p.Idempotencia,p,"TabletAnamnese",Permissao.EditarProntuario,async u => {
            Textos(4000,p.AntecedentesPessoais,p.AntecedentesFamiliares,p.HabitosDeVida,p.HistoriaObstetrica,p.RevisaoDeSistemas,p.Observacoes);
            Textos(500,p.Motivo);
            var atual=await repo.AnamneseDoPacienteAsync(paciente,ct);
            ConferirVersao(p.Versao,Hash(DadosAnamnese(atual)));
            if(atual is not null && string.IsNullOrWhiteSpace(p.Motivo)) throw ErroFormularioTablet.Criar("Informe o motivo da revisão da anamnese.");
            var salvo=await new AnamneseService(repo).SalvarAsync(paciente,new() {
                AntecedentesPessoais=p.AntecedentesPessoais,AntecedentesFamiliares=p.AntecedentesFamiliares,
                HabitosDeVida=p.HabitosDeVida,HistoriaObstetrica=p.HistoriaObstetrica,
                RevisaoDeSistemas=p.RevisaoDeSistemas,Observacoes=p.Observacoes},u.Login,p.Motivo,ct);
            return new ResultadoFichaTablet(salvo.Id);
        },ct);

    public Task<ResultadoFichaTablet> RegistrarMedidaAsync(SessaoTablet s,int paciente,MedidaTablet p,CancellationToken ct)
        => Escrever(s,paciente,p.Idempotencia,p,"TabletMedida",Permissao.EditarProntuario,async u => {
            Textos(1000,p.Observacoes);Textos(80,p.TipoCodigo);
            var m=await new MedidaClinicaService(repo).RegistrarAsync(new() {PacienteId=paciente,
                ProfissionalId=u.ProfissionalId,Data=p.Data,TipoCodigo=p.TipoCodigo,Valor=p.Valor,
                ValorSecundario=p.ValorSecundario,Observacoes=p.Observacoes},u.Login,ct);
            return new ResultadoFichaTablet(m.Id);
        },ct);

    public Task<ResultadoFichaTablet> CancelarMedidaAsync(SessaoTablet s,int paciente,int id,CancelarRegistroTablet p,CancellationToken ct)
        => Escrever(s,paciente,p.Idempotencia,new{id,p},"TabletMedidaCancelada",Permissao.EditarProntuario,async u => {
            Textos(500,p.Motivo);
            var m=await db.MedidasClinicas.SingleOrDefaultAsync(m=>m.Id==id&&m.PacienteId==paciente&&m.CanceladaEm==null,ct)
                ??throw new RecursoClinicoIndisponivel();
            ConferirVersao(p.Versao,VersaoMedida(m));
            await new MedidaClinicaService(repo).CancelarAsync(id,p.Motivo,u.Login,ct);
            return new ResultadoFichaTablet(id);
        },ct);

    public Task<ResultadoFichaTablet> SalvarProblemaAsync(SessaoTablet s,int paciente,ProblemaTablet p,CancellationToken ct)
        => Escrever(s,paciente,p.Idempotencia,p,"TabletProblema",Permissao.EditarProntuario,async u => {
            Textos(300,p.Descricao);Textos(2000,p.Observacoes);Textos(15,p.Cid);
            if(!Enum.IsDefined(p.Natureza)) throw ErroFormularioTablet.Criar("Escolha a natureza do registro.");
            ProblemaPaciente? atual=null;
            if(p.Id!=0) {
                atual=await db.ProblemasPaciente.SingleOrDefaultAsync(x=>x.Id==p.Id&&x.PacienteId==paciente,ct)??throw new RecursoClinicoIndisponivel();
                ConferirVersao(p.Versao,Hash(DadosProblema(atual)));
                if(atual.Situacao==SituacaoProblema.Descartado)throw ErroFormularioTablet.Criar("Reabra o registro antes de editar.");
            }
            var salvo=await new ProblemaPacienteService(repo).SalvarAsync(new() {Id=p.Id,PacienteId=paciente,
                ProfissionalId=atual?.ProfissionalId??u.ProfissionalId,EvolucaoId=atual?.EvolucaoId,
                Natureza=p.Natureza,Descricao=p.Descricao,Cid=p.Cid,Inicio=p.Inicio,Fim=atual?.Fim,Observacoes=p.Observacoes},u.Login,ct);
            return new ResultadoFichaTablet(salvo.Id);
        },ct);

    public Task<ResultadoFichaTablet> SituacaoProblemaAsync(SessaoTablet s,int paciente,int id,SituacaoProblemaTablet p,CancellationToken ct)
        => Escrever(s,paciente,p.Idempotencia,new{id,p},"TabletProblemaSituacao",Permissao.EditarProntuario,async u => {
            var atual=await db.ProblemasPaciente.SingleOrDefaultAsync(x=>x.Id==id&&x.PacienteId==paciente,ct)??throw new RecursoClinicoIndisponivel();
            ConferirVersao(p.Versao,Hash(DadosProblema(atual)));Textos(500,p.Motivo);
            var svc=new ProblemaPacienteService(repo);
            if(p.Situacao==SituacaoProblema.Resolvido) await svc.ResolverAsync(id,p.Fim,u.Login,ct);
            else if(p.Situacao==SituacaoProblema.Descartado) await svc.DescartarAsync(id,p.Motivo!,u.Login,ct);
            else if(p.Situacao==SituacaoProblema.Ativo) await svc.ReabrirAsync(id,u.Login,ct);
            else throw ErroFormularioTablet.Criar("Escolha uma situação válida.");
            return new ResultadoFichaTablet(id);
        },ct);

    public async Task<object> VersoesAnamneseAsync(SessaoTablet s,int paciente,CancellationToken ct)
    {
        var u=await Autorizar(s,ct);await Paciente(paciente,ct);
        var versoes=await db.VersoesAnamnese.AsNoTracking().Where(v=>v.Anamnese!.PacienteId==paciente)
            .OrderByDescending(v=>v.Versao).Take(100).Select(v=>new {v.Versao,v.SubstituidaEm,v.SubstituidaPor,v.Motivo,
                v.AntecedentesPessoais,v.AntecedentesFamiliares,v.HabitosDeVida,v.HistoriaObstetrica,v.RevisaoDeSistemas,v.Observacoes}).ToListAsync(ct);
        await Auditar(u,paciente,"TabletVersoesAnamnese",ct);await db.SaveChangesAsync(ct);return versoes;
    }
}
