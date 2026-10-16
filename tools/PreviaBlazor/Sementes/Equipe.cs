// Dados FICTÍCIOS da prévia — nenhuma pessoa, CPF ou registro real.
using System.Text.Json;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;

namespace PreviaBlazor.Sementes;

/// <summary>
/// A EQUIPE da tela de Profissionais e salas: uma enfermeira com jornada declarada e CPF (assina
/// com e-CPF), um psiquiatra com trava ligada, habilitação restrita, cor na agenda e férias COM
/// uma sessão marcada dentro (o "Empurrar sessões"), um profissional inativo, quatro salas (uma
/// de infusão para três ao mesmo tempo, uma inativa) e um feriado da clínica inteira.
///
/// Cuidado com o banco COMPARTILHADO dos testes (`TelasBlazorAbremTests` clica cada botão): a
/// primeira linha de cada lista — que é a que o "Excluir" alcança — é um registro EM USO (com
/// bloqueio), para a exclusão ser recusada pela regra e não apagar o que outra tela lê. E os
/// bloqueios só alcançam gente e salas DESTA semente, para "Empurrar sessões" nunca mover a
/// agenda de outra tela (o feriado é no fim de dezembro, longe da agenda das outras sementes).
/// </summary>
public sealed class SementeEquipe : ISemente
{
    public int Ordem => 80;

    public void Semear(ClinicaDbContext db)
    {
        var hoje = DateTime.Today;
        int Dias(params DayOfWeek[] d) => d.Aggregate(0, (a, x) => a | Profissional.BitDe(x));

        var abigail = db.Add(new Profissional
        {
            Nome = "Abigail Nunes", NomeCurto = "Enf. Abigail", RegistroConselho = "COREN-RJ 345.678",
            Cpf = "52998224725", Telefone = "(21) 98888-1001", Email = "abigail@clinica.exemplo",
            DiasDeAtendimento = Dias(DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday),
            AtendeDas = new TimeOnly(7, 0), AtendeAte = new TimeOnly(13, 0), Ordem = 0,
            Observacoes = "Responsável pela sala de infusão."
        }).Entity;
        var caio = db.Add(new Profissional
        {
            Nome = "Dr. Caio Rezende", NomeCurto = "Dr. Caio", RegistroConselho = "CRM-RJ 77.2468",
            Cpf = "11144477735", EspecialidadeCodigo = Especialidade.Psiquiatria.ToString(), Cor = "#0F766E",
            DuracaoPadraoMinutos = 50, AgendaProtegida = true,
            DiasDeAtendimento = Dias(DayOfWeek.Tuesday, DayOfWeek.Thursday),
            AtendeDas = new TimeOnly(14, 0), AtendeAte = new TimeOnly(19, 0), Ordem = 1,
            HabilitacoesAtendimentoJson = JsonSerializer.Serialize(new[]
            {
                new AtendimentoHabilitado(ModalidadeAtendimento.Consulta.ToString(), Especialidade.Psiquiatria.ToString())
            })
        }).Entity;
        db.Add(new Profissional
        {
            Nome = "Rafael Antunes", NomeCurto = "Rafael", RegistroConselho = "CREFITO-2 98765-F",
            Ativo = false, Ordem = 9, Observacoes = "Saiu da clínica em agosto."
        });

        var alecrim = db.Add(new Sala { Nome = "Consultório Alecrim", Capacidade = 1, Ordem = 0 }).Entity;
        db.Add(new Sala { Nome = "Consultório Bromélia", Capacidade = 1, Ordem = 1 });
        db.Add(new Sala { Nome = "Sala de infusão Cedro", Capacidade = 3, Ordem = 2, Observacoes = "Três poltronas de infusão." });
        db.Add(new Sala { Nome = "Sala antiga Damasco", Capacidade = 1, Ordem = 3, Ativa = false, Observacoes = "Virou depósito." });
        db.SaveChanges();

        BloqueioAgenda B(DateTime inicio, DateTime fim, string motivo, int? profissional = null, int? sala = null)
            => db.Add(new BloqueioAgenda
            {
                Inicio = inicio, Fim = fim, Motivo = motivo, ProfissionalId = profissional, SalaId = sala,
                CriadoEm = hoje.AddDays(-3), CriadoPor = "previa"
            }).Entity;

        // Dois bloqueios da Abigail: o "Reabrir" do teste tira um, e ela continua EM USO.
        B(hoje.AddDays(9), hoje.AddDays(9).AddHours(23).AddMinutes(59), "Folga compensatória", profissional: abigail.Id);
        B(hoje.AddDays(40), hoje.AddDays(54).AddHours(23).AddMinutes(59), "Férias", profissional: abigail.Id);

        // As férias do Dr. Caio, com uma sessão que já estava marcada dentro.
        var inicioFerias = hoje.AddDays(18);
        B(inicioFerias, inicioFerias.AddDays(6).AddHours(23).AddMinutes(59), "Congresso de psiquiatria", profissional: caio.Id);
        var paciente = db.Pacientes.OrderBy(p => p.Id).First();
        db.Add(new Agendamento
        {
            PacienteId = paciente.Id, ProfissionalId = caio.Id, DataHora = inicioFerias.AddDays(2).AddHours(15),
            ModalidadePrevista = ModalidadeAtendimento.Consulta, Status = StatusAgendamento.Agendado, DuracaoMinutos = 50
        });

        B(hoje.AddDays(12).AddHours(13), hoje.AddDays(12).AddHours(18), "Manutenção do ar-condicionado", sala: alecrim.Id);

        var natal = new DateTime(hoje.Month == 12 && hoje.Day > 20 ? hoje.Year + 1 : hoje.Year, 12, 25);
        B(natal, natal.AddHours(23).AddMinutes(59), "Feriado — Natal");
        db.SaveChanges();
    }
}
