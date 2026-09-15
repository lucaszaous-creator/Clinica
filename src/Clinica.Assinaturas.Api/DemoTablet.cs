using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Assinaturas.Api;

internal static class DemoTablet
{
    internal static async Task PrepararAsync(IServiceProvider services)
    {
        using var scope=services.CreateScope();
        var db=scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();
        await db.Database.EnsureCreatedAsync();
        if(await db.Usuarios.AnyAsync()) return;
        await scope.ServiceProvider.GetRequiredService<AcessoService>().CriarAsync("Enfermagem — demonstração",
            "demo","TabletDemo#2026",PerfilAcesso.Gerente);
        var tcle=ModelosTermoBsv.Consentimento(); tcle.Id=1;
        var bsv=ModelosTermoBsv.TermoDaSessao(); bsv.Id=2;
        db.ModelosDocumento.AddRange(tcle,bsv);
        db.ExigenciasTermo.AddRange(new ExigenciaTermoProcedimento {ModeloDocumentoId=1,Modalidade=ModalidadeAtendimento.BsvApenas,Ativa=true},
            new ExigenciaTermoProcedimento {ModeloDocumentoId=2,Modalidade=ModalidadeAtendimento.BsvApenas,Ativa=true,SoValeNoDiaDoProcedimento=true});
        for(int i=1;i<=2;i++)
        {
            var p=new Paciente {Nome=$"Paciente fictício {i}",DataNascimento=new DateOnly(1980,1,15),Convenio=Convenio.UnimedPadrao};
            db.Pacientes.Add(p);
            db.Agendamentos.Add(new Agendamento {Paciente=p,DataHora=DateTime.Today.AddHours(8+i),ModalidadePrevista=ModalidadeAtendimento.BsvApenas});
        }
        await db.SaveChangesAsync();
    }
}
