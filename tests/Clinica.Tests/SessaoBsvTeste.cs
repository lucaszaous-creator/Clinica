using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;

namespace Clinica.Tests;

internal static class SessaoBsvTeste
{
    public static async Task CriarAsync(ClinicaDbContext db, int paciente, params DateOnly[] dias)
    {
        foreach (var dia in dias.Distinct())
            db.Agendamentos.Add(new Agendamento { PacienteId = paciente, DataHora = dia.ToDateTime(new TimeOnly(8, 0)),
                ModalidadePrevista = ModalidadeAtendimento.BsvApenas, ModalidadeCodigo = nameof(ModalidadeAtendimento.BsvApenas) });
        await db.SaveChangesAsync();
    }
}
