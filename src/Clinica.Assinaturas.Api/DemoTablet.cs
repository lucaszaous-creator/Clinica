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
        var profissional = new Profissional {Nome="Dra. Ana Martins — demonstração",NomeCurto="Dra. Ana Martins",
            Cpf="52998224725",RegistroConselho="CRM-RJ 000000",Ativo=true};
        var outro = new Profissional {Nome="Dr. Bruno — demonstração",Ativo=true};
        db.Profissionais.AddRange(profissional,outro); await db.SaveChangesAsync();
        var medica = await scope.ServiceProvider.GetRequiredService<AcessoService>().CriarAsync("Dra. Ana Martins",
            "medica.demo","TabletDemo#2026",PerfilAcesso.Profissional);
        medica.ProfissionalId=profissional.Id; medica.DeveTrocarSenha=false;
        var hoje=DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow,"America/Sao_Paulo").DateTime);
        var nomes=new[]{"Marina Oliveira — fictícia","Carlos Santos — fictício","Paciente de outro profissional — fictício"};
        for(var i=0;i<nomes.Length;i++)
        {
            var paciente=new Paciente {Nome=nomes[i],DataNascimento=new DateOnly(1980+i,3,12),Convenio=Convenio.UnimedPadrao};
            db.Pacientes.Add(paciente);
            db.Agendamentos.Add(new Agendamento {Paciente=paciente,Profissional=i==2?outro:profissional,
                DataHora=hoje.ToDateTime(new TimeOnly(9+i,0)),ModalidadePrevista=ModalidadeAtendimento.AcupunturaSimples,
                ChegadaEm=i==0?hoje.ToDateTime(new TimeOnly(8,50)):null});
            if(i==0) db.Evolucoes.Add(new Evolucao {Paciente=paciente,Profissional=profissional,Data=hoje.AddDays(-7),
                CriadoPor="demonstração",QueixaPrincipal="Dor lombar — exemplo fictício",TextoEvolucao="Paciente relata melhora desde a última sessão. Registro de demonstração, sem valor assistencial.",
                Conduta="Conduta a revisar pelo profissional.",PlanoTerapeutico="Reavaliar na próxima sessão.",EvaAntes=5,EvaDepois=3});
        }
        db.ModelosEvolucao.Add(new ModeloEvolucao {Nome="Retorno • roteiro",ProfissionalId=profissional.Id,
            TextoEvolucao="Evolução desde a última sessão:\n\nResposta ao tratamento:\n\nConduta de hoje:",Ativo=true});
        await db.SaveChangesAsync();
    }
}
