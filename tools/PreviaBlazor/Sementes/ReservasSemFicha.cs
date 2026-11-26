// Dados FICTÍCIOS da prévia — nenhuma pessoa real.
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;

namespace PreviaBlazor.Sementes;

/// <summary>
/// A RESERVA SEM FICHA (nov/2026): duas reservas de HOJE, no fim da tarde (sem colidir com a Agenda do dia), para a
/// Agenda › Dia e a Agenda › Visão geral mostrarem a marca da reserva e o "Virar paciente" — uma particular, uma de convênio.
/// </summary>
public sealed class SementeReservasSemFicha : ISemente
{
    public int Ordem => 46;

    public void Semear(ClinicaDbContext db)
    {
        var hoje = DateTime.Today;
        var helena = db.Set<Profissional>().First(p => p.NomeCurto == "Dra. Helena");
        var marcos = db.Set<Profissional>().First(p => p.NomeCurto == "Dr. Marcos");
        db.AddRange(
            new ReservaDeHorario
            {
                Nome = "Fernanda Lopes", Telefone = "5522998123344", ProfissionalId = helena.Id, DataHora = hoje.AddHours(19),
                DuracaoMinutos = 30, ModalidadeCodigo = nameof(ModalidadeAtendimento.AcupunturaSimples), Particular = true,
                CriadaEm = hoje.AddHours(10).AddMinutes(48), CriadaPor = "Bruna Reis"
            },
            new ReservaDeHorario
            {
                Nome = "Rafaela Moura", Telefone = "5522997654321", ProfissionalId = marcos.Id, DataHora = hoje.AddHours(19).AddMinutes(30),
                DuracaoMinutos = 40, ModalidadeCodigo = nameof(ModalidadeAtendimento.AcupunturaSimples), Particular = false, ConvenioNome = "Unimed",
                CriadaEm = hoje.AddHours(11).AddMinutes(5), CriadaPor = "Ana Lima"
            });
        db.SaveChanges();
    }
}
