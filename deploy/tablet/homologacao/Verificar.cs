using System.Text.Json;
using Clinica.Application.Servicos;
using Clinica.Application.Tablet;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Clinica.Infrastructure.Tablet;
using Microsoft.EntityFrameworkCore;
using Npgsql;

internal static class Verificar
{
    internal static async Task Rodar(string banco)
    {
        await using var db=new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>().UseNpgsql(
            new NpgsqlConnectionStringBuilder {Host="/var/run/postgresql",Port=45432,Database=banco,Username="clinica-posto-hml",IncludeErrorDetail=false}.ConnectionString).Options);
        var repo=new ClinicaRepositorio(db);var prontuario=new ProntuarioService(repo);var prescricoes=new PrescricaoService(repo);
        var documentos=new DocumentoClinicoService(repo,prontuario,new(repo));var agenda=new AgendaService(repo,new(repo),new(repo));
        var portal=new PortalTabletService(db,repo,documentos,new(repo),new(repo),new(repo),new([1,2],true),TimeProvider.System);
        var acesso=new AtendimentoTabletService(db,repo,prontuario,agenda,documentos,new(repo,prescricoes),prescricoes,TimeProvider.System);
        var posto=new PostoTabletService(db,repo,acesso,agenda,prescricoes,new(repo));
        var equipe=await db.Usuarios.Include(u=>u.Profissional).ToListAsync();
        var u=equipe.First(PoliticaAtendimentoTablet.PodeAtender);
        var horario=await db.Agendamentos.Include(a=>a.Paciente).FirstAsync(a=>a.ProfissionalId==u.ProfissionalId&&a.Status==StatusAgendamento.Agendado);
        if(!horario.Paciente!.Nome.StartsWith("HOMOLOGAÇÃO — paciente fictício"))throw new InvalidOperationException("A conferência só opera pacientes fictícios.");
        var s=(await portal.EntrarAsync(u,"verificacao-restrita",null,default)).Sessao;
        await posto.BuscarAsync(s,"HOMOLOGAÇÃO",default);await posto.FichaAsync(s,horario.PacienteId,0,default);
        var pedido=new EmitirDocumentoTablet(Guid.NewGuid(),"receita","DOCUMENTO FICTÍCIO — validação de homologação, sem valor assistencial.");
        var d=await posto.EmitirAsync(s,horario.PacienteId,pedido,default);
        if(d!=await posto.EmitirAsync(s,horario.PacienteId,pedido,default))throw new InvalidOperationException("Recibo divergente.");
        var detalhe=JsonSerializer.SerializeToElement(await acesso.AbrirAsync(s,horario.Id,default),ContratoTablet.Json);
        var e=detalhe.GetProperty("evolucao").Deserialize<EvolucaoClinicaTablet>(ContratoTablet.Json)!;
        var result=await acesso.SalvarAsync(s,horario.Id,new(Guid.NewGuid(),e with {TextoEvolucao="Evolução fictícia: ensaio do fechamento completo."},true),default);
        if(!result.Finalizado||result.Guias==0)throw new InvalidOperationException("Fechamento não confirmado.");
        s.Modo="revogada";await db.SaveChangesAsync();
        Console.WriteLine("HOMOLOGACAO_CONFERIDA_COM_PAPEL_RESTRITO: ficha, agenda, documento avulso, recibo, evolução, conclusão e guias");
    }
}
