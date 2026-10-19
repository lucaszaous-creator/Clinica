// Dados FICTÍCIOS da prévia — nenhuma pessoa real.
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;

namespace PreviaBlazor.Sementes;

/// <summary>
/// A GRADE da agenda (Recepção, out/2026). O dia de HOJE já vem da semente da Agenda do dia (todas
/// as situações); esta acrescenta só o que a grade tem e a lista não: a agenda FECHADA no fim da
/// tarde de um profissional (o vão hachurado com o motivo), dois horários SOBREPOSTOS (as raias),
/// uma sessão marcada em SÉRIE (o "cancelar o resto da série") e a LISTA DE ESPERA — um pedido
/// prioritário de qualquer profissional e outro só à tarde, com a Dra. Helena (o "quem chamar").
/// Pacientes PRÓPRIOS, para não mexer no que as outras telas contam.
/// </summary>
public sealed class SementeGradeDaAgenda : ISemente
{
    public int Ordem => 96;

    public void Semear(ClinicaDbContext db)
    {
        var hoje = DateTime.Today;
        var helena = db.Set<Profissional>().FirstOrDefault(p => p.NomeCurto == "Dra. Helena");
        var marcos = db.Set<Profissional>().FirstOrDefault(p => p.NomeCurto == "Dr. Marcos");
        if (helena is null || marcos is null) return;

        Paciente P(string nome, string telefone)
        {
            var p = new Paciente { Nome = nome, Telefone = telefone, DataNascimento = new DateOnly(1975, 3, 21) };
            db.Add(p);
            return p;
        }

        var rosana = P("Rosana Vilela Quintas", "(21) 98222-0001");
        var bento = P("Bento Arruda Moraes", "(21) 98222-0002");
        var iara = P("Iara Lacerda Fontes", "(21) 98222-0003");
        var caio = P("Caio Rangel Siqueira", "(21) 98222-0004");
        db.SaveChanges();

        // Fechado no fim da tarde: o vão hachurado com o motivo escrito.
        db.Add(new BloqueioAgenda
        {
            ProfissionalId = marcos.Id, Inicio = hoje.AddHours(17), Fim = hoje.AddHours(19),
            Motivo = "Congresso de acupuntura", CriadoPor = "recepcao"
        });

        // Dois horários sobrepostos com a Dra. Helena (um encaixe): as raias da coluna.
        db.Add(new Agendamento
        {
            PacienteId = rosana.Id, ProfissionalId = helena.Id, DataHora = hoje.AddHours(11),
            ModalidadePrevista = ModalidadeAtendimento.AcupunturaSimples, ModalidadeCodigo = nameof(ModalidadeAtendimento.AcupunturaSimples),
            DuracaoMinutos = 60, CriadoPor = "recepcao", CriadoEm = hoje.AddDays(-2).AddHours(9)
        });
        db.Add(new Agendamento
        {
            PacienteId = bento.Id, ProfissionalId = helena.Id, DataHora = hoje.AddHours(11).AddMinutes(30),
            ModalidadePrevista = ModalidadeAtendimento.Consulta, ModalidadeCodigo = nameof(ModalidadeAtendimento.Consulta),
            EspecialidadeConsultaCodigo = nameof(Especialidade.ClinicaDaDor), Encaixe = true,
            CriadoPor = "recepcao", CriadoEm = hoje.AddDays(-1).AddHours(15)
        });

        // Uma série de três sessões semanais (hoje e as duas próximas semanas).
        var serie = "SERIE-PREVIA-GRADE";
        for (var i = 0; i < 3; i++)
            db.Add(new Agendamento
            {
                PacienteId = iara.Id, ProfissionalId = marcos.Id, DataHora = hoje.AddDays(7 * i).AddHours(12),
                ModalidadePrevista = ModalidadeAtendimento.AcupunturaComEletro, ModalidadeCodigo = nameof(ModalidadeAtendimento.AcupunturaComEletro),
                SerieId = serie, CriadoPor = "recepcao", CriadoEm = hoje.AddDays(-10).AddHours(10)
            });

        // A lista de espera: um prioritário (qualquer profissional) e um só à tarde com a Helena.
        db.Add(new ListaEspera
        {
            PacienteId = caio.Id, Prioritario = true, Periodo = PeriodoPreferido.Qualquer,
            Observacoes = "Dor aguda — chamar no primeiro horário que vagar.", CriadoEm = hoje.AddDays(-4)
        });
        db.Add(new ListaEspera
        {
            PacienteId = rosana.Id, ProfissionalId = helena.Id, Periodo = PeriodoPreferido.Tarde,
            DisponivelAte = DateOnly.FromDateTime(hoje).AddDays(20), CriadoEm = hoje.AddDays(-9)
        });
        db.SaveChanges();
    }
}
