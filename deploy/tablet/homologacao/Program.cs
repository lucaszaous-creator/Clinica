using System.Text.RegularExpressions;
using Clinica.Application.Servicos;
using Clinica.Application.Tablet;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;

// Ferramenta administrativa de uso único. Nunca é executada pelo serviço web.
// Não lê pacientes, documentos, sessões ou certificados da base de origem.
if(args.Length!=2 || !Regex.IsMatch(args[1],"^clinica_posto_hml_[0-9]{8}$") || args[0]==args[1])
    throw new InvalidOperationException("Informe origem e banco novo de homologação.");
if(args[0]=="verificar") {await Verificar.Rodar(args[1]);return;}
ClinicaDbContext Banco(string nome,bool leitura=false)=>new(new DbContextOptionsBuilder<ClinicaDbContext>().UseNpgsql(
    new NpgsqlConnectionStringBuilder {Host="/var/run/postgresql",Port=45432,Database=nome,Username="postgres",
        Options=leitura?"-c default_transaction_read_only=on":null,IncludeErrorDetail=false}.ConnectionString).Options);
await using var origem=Banco(args[0],true);
await using var destino=Banco(args[1]);
await destino.Database.MigrateAsync();
if(await destino.Usuarios.AnyAsync()||await destino.Pacientes.AnyAsync())throw new InvalidOperationException("Homologação já contém dados; não sobrescrever.");
var equipe=(await origem.Usuarios.AsNoTracking().Include(u=>u.Profissional).Where(u=>u.Ativo&&u.ProfissionalId!=null).ToListAsync())
    .Where(u=>u.Profissional!.Ativo&&PoliticaAtendimentoTablet.PodeUsarPosto(u)).ToArray();
if(equipe.Length==0)throw new InvalidOperationException("Nenhum profissional clínico ativo disponível para homologação.");
await using var tx=await destino.Database.BeginTransactionAsync();
foreach(var p in equipe.Select(u=>u.Profissional!).DistinctBy(p=>p.Id))
    destino.Profissionais.Add(new Profissional {Id=p.Id,Nome=p.Nome,NomeCurto=p.NomeCurto,Cpf=p.Cpf,RegistroConselho=p.RegistroConselho,Ativo=p.Ativo});
foreach(var u in equipe)
    destino.Usuarios.Add(new UsuarioSistema {Id=u.Id,Nome=u.Nome,Login=u.Login,SenhaHash=u.SenhaHash,SenhaSalt=u.SenhaSalt,
        Perfil=u.Perfil,PermissoesExtras=u.PermissoesExtras,PermissoesNegadas=u.PermissoesNegadas,ProfissionalId=u.ProfissionalId,
        Ativo=u.Ativo,DeveTrocarSenha=u.DeveTrocarSenha,CriadoEm=u.CriadoEm,TentativasFalhas=u.TentativasFalhas,BloqueadoAte=u.BloqueadoAte});
var dadosPrestador=await origem.Configuracoes.AsNoTracking().SingleOrDefaultAsync(c=>c.Chave==ParametrosService.ChavePrestador);
if(dadosPrestador!=null)destino.Configuracoes.Add(dadosPrestador);
var tcle=ModelosTermoBsv.Consentimento();tcle.Id=1;var bsv=ModelosTermoBsv.TermoDaSessao();bsv.Id=2;
destino.ModelosDocumento.AddRange(tcle,bsv);
var hoje=DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow,"America/Sao_Paulo").DateTime);
foreach(var u in equipe.Where(PoliticaAtendimentoTablet.PodeAtender).DistinctBy(u=>u.ProfissionalId))
{
    var paciente=new Paciente {Nome=$"HOMOLOGAÇÃO — paciente fictício {u.ProfissionalId}",DataNascimento=new(1980,1,15),
        Documento="00000000000",Endereco="Endereço fictício para homologação",Convenio=Convenio.UnimedPadrao,
        Observacoes="DADOS FICTÍCIOS. NÃO USAR PARA ASSISTÊNCIA."};
    destino.Pacientes.Add(paciente);
    destino.Agendamentos.Add(new Agendamento {Paciente=paciente,ProfissionalId=u.ProfissionalId,
        DataHora=hoje.ToDateTime(new(9,0)),ModalidadePrevista=ModalidadeAtendimento.AcupunturaSimples});
    destino.Evolucoes.Add(new Evolucao {Paciente=paciente,ProfissionalId=u.ProfissionalId,Data=hoje.AddDays(-7),
        TextoEvolucao="Registro fictício para testar a cópia e revisão pelo profissional.",CriadoPor="homologação"});
}
await destino.SaveChangesAsync();
await destino.Database.ExecuteSqlRawAsync("SELECT setval(pg_get_serial_sequence('\"Usuarios\"','Id'), COALESCE((SELECT MAX(\"Id\") FROM \"Usuarios\"),1),true)");
await destino.Database.ExecuteSqlRawAsync("SELECT setval(pg_get_serial_sequence('\"Profissionais\"','Id'), COALESCE((SELECT MAX(\"Id\") FROM \"Profissionais\"),1),true)");
await destino.Database.ExecuteSqlRawAsync("SELECT setval(pg_get_serial_sequence('\"ModelosDocumento\"','Id'), COALESCE((SELECT MAX(\"Id\") FROM \"ModelosDocumento\"),1),true)");
await tx.CommitAsync();
Console.WriteLine($"HOMOLOGACAO_PREPARADA; profissionais={equipe.Length}; pacientes_reais_copiados=0");
