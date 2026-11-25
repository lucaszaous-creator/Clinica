using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// Nov/2026: "Para assinar" (Clínico → Prescrições) e o "Documentos para assinar" do Consultório leem a MESMA regra.
/// Entram os documentos PRÓPRIOS, sem assinatura, não cancelados, dos tipos que o profissional assina — com ou sem
/// atendimento (a receita 0480 emitida pela gestão, sem horário, é o caso real). Dados fictícios.
/// </summary>
public sealed class DocumentosParaAssinarTests : IDisposable
{
    private readonly SqliteConnection _conn = new("DataSource=:memory:");
    private readonly ClinicaDbContext _db;

    public DocumentosParaAssinarTests()
    {
        _conn.Open();
        _db = new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options);
        _db.Database.EnsureCreated();
    }

    public void Dispose() { _db.Dispose(); _conn.Dispose(); }

    [Fact]
    public async Task So_os_proprios_sem_assinatura_e_dos_tipos_que_se_assinam()
    {
        var eu = new Profissional { Nome = "Dra. Fictícia" }; var outra = new Profissional { Nome = "Dra. Outra" };
        var p = new Paciente { Nome = "Paciente Fictícia", Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino };
        _db.AddRange(eu, outra, p); _db.SaveChanges();
        var n = 0;
        DocumentoClinico Doc(TipoDocumentoClinico tipo, Profissional dono, Action<DocumentoClinico>? ajuste = null)
        {
            var d = new DocumentoClinico { Numero = $"2026/{++n:0000}", CodigoVerificacao = $"TST{n:000}", Tipo = tipo, PacienteId = p.Id, ProfissionalId = dono.Id, Data = new DateOnly(2026, 11, 24), Corpo = "Texto fictício." };
            ajuste?.Invoke(d); _db.Add(d); return d;
        }
        var semAtendimento = Doc(TipoDocumentoClinico.Receita, eu);                      // 0480: emitida pela gestão, sem horário
        var atestado = Doc(TipoDocumentoClinico.Atestado, eu);
        Doc(TipoDocumentoClinico.Receita, eu, d => d.AssinadoEm = DateTime.Now);           // já assinada
        Doc(TipoDocumentoClinico.Receita, eu, d => d.CanceladoEm = DateTime.Now);          // cancelada
        Doc(TipoDocumentoClinico.Receita, outra);                                          // de outra profissional
        Doc(TipoDocumentoClinico.TermoProcedimento, eu);                                   // termo: o paciente assina
        _db.SaveChanges(); _db.ChangeTracker.Clear();

        var lidos = await new ClinicaRepositorio(_db).DocumentosParaAssinarAsync(eu.Id);
        lidos.Select(d => d.Id).Should().Equal(semAtendimento.Id, atestado.Id);
        lidos.Should().OnlyContain(d => d.Paciente != null && d.Paciente.Nome == "Paciente Fictícia");
        DocumentosParaAssinar.Tipos.Should().Contain(TipoDocumentoClinico.Receita).And.NotContain(TipoDocumentoClinico.TermoProcedimento);
    }
}
