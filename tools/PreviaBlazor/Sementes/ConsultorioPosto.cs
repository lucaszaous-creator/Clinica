// Dados FICTÍCIOS da prévia — nenhuma pessoa real.
using Clinica.Application.Modelos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;

namespace PreviaBlazor.Sementes;

/// <summary>
/// O POSTO DO CONSULTÓRIO (out/2026 — Meu dia, Minha semana, Sessões sem evolução e Meus números
/// viraram Blazor). O dia de HOJE já vem cheio da semente da agenda do balcão (todas as situações
/// da fila); esta acrescenta o que é do lado de QUEM ATENDE:
/// <list type="bullet">
/// <item>sessões de hoje já ESCRITAS (coluna Prontuário "Escrito") e uma realizada sem evolução;</item>
/// <item>a DÍVIDA de prontuário: sessões dos últimos dias, atendidas e sem evolução (desde o corte
/// de <see cref="PoliticaRegistroPendente.DataInicio"/>), de dois profissionais — o combo do modo
/// sem vínculo tem o que recortar;</item>
/// <item>a SEMANA com horários em outros dias (inclusive uma sessão de uma hora, que cobre a faixa
/// seguinte, e um cancelado) e um fechamento da clínica num trecho de um dia (reunião de equipe),
/// que a grade escreve no vão;</item>
/// <item>os NÚMEROS do mês e do mês anterior vêm da semente dos indicadores da direção.</item>
/// </list>
/// Pacientes PRÓPRIOS, para não mexer no que outras telas contam. Sem Id fixo.
/// </summary>
public sealed class SementeConsultorioPosto : ISemente
{
    public int Ordem => 98;

    public void Semear(ClinicaDbContext db)
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var helena = db.Set<Profissional>().First(p => p.NomeCurto == "Dra. Helena");
        var marcos = db.Set<Profissional>().First(p => p.NomeCurto == "Dr. Marcos");

        Paciente P(string nome, string telefone)
        {
            var p = new Paciente { Nome = nome, Telefone = telefone, DataNascimento = new DateOnly(1971, 3, 9) };
            db.Add(p);
            return p;
        }

        var rosana = P("Rosana Figueiredo Lemos", "(21) 98222-0101");
        var tiago = P("Tiago Moreira Barros", "(21) 98222-0102");
        var irene = P("Irene Cavalcanti Paes", "(21) 98222-0103");
        var wagner = P("Wagner Siqueira Dantas", "(21) 98222-0104");
        var celia = P("Célia Rangel Monteiro", "(21) 98222-0105");
        var bruno = P("Bruno Esteves Farias", "(21) 98222-0106");
        db.SaveChanges();

        Agendamento A(Paciente p, DateOnly dia, int hora, int minuto, ModalidadeAtendimento m, Profissional prof,
            StatusAgendamento status = StatusAgendamento.Agendado, int duracao = 30)
        {
            var a = new Agendamento
            {
                PacienteId = p.Id, ProfissionalId = prof.Id, DataHora = dia.ToDateTime(new TimeOnly(hora, minuto)),
                ModalidadePrevista = m, ModalidadeCodigo = m.ToString(), Status = status, DuracaoMinutos = duracao,
                CriadoPor = "recepcao", CriadoEm = dia.ToDateTime(TimeOnly.MinValue).AddDays(-4)
            };
            db.Add(a);
            return a;
        }

        // Uma sessão REALIZADA: o atendimento existe, como na conclusão de verdade.
        Agendamento Realizada(Paciente p, DateOnly dia, int hora, ModalidadeAtendimento m, Profissional prof, int duracao = 30)
        {
            var at = new Atendimento { PacienteId = p.Id, Data = dia, Modalidade = m, RealizadoEm = dia.ToDateTime(new TimeOnly(hora, 25)) };
            db.Add(at);
            db.SaveChanges();
            var a = A(p, dia, hora, 0, m, prof, StatusAgendamento.Realizado, duracao);
            a.AtendimentoId = at.Id;
            a.ChegadaEm = dia.ToDateTime(new TimeOnly(hora, 0)).AddMinutes(-8);
            a.InicioAtendimentoEm = dia.ToDateTime(new TimeOnly(hora, 2));
            a.FimAtendimentoEm = dia.ToDateTime(new TimeOnly(hora, 27));
            return a;
        }

        void Evolucao(Agendamento a, Profissional prof, int evaAntes, int evaDepois) => db.Add(new Evolucao
        {
            PacienteId = a.PacienteId, ProfissionalId = prof.Id, AgendamentoId = a.Id, AtendimentoId = a.AtendimentoId,
            Data = DateOnly.FromDateTime(a.DataHora), QueixaPrincipal = "Dor lombar crônica",
            Conduta = "Acupuntura sistêmica com eletroestimulação", EvaAntes = evaAntes, EvaDepois = evaDepois,
            CriadoPor = "previa"
        });

        // ===== HOJE: duas já escritas e uma atendida sem evolução (o "Pendente" da coluna) =====
        var escrita1 = Realizada(rosana, hoje, 7, ModalidadeAtendimento.AcupunturaComEletro, helena);
        var escrita2 = Realizada(tiago, hoje, 11, ModalidadeAtendimento.AcupunturaSimples, marcos);
        Realizada(irene, hoje, 12, ModalidadeAtendimento.AcupunturaSimples, marcos);
        db.SaveChanges();
        Evolucao(escrita1, helena, 7, 3);
        Evolucao(escrita2, marcos, 6, 4);

        // ===== A DÍVIDA: sessões dos últimos dias, atendidas e sem evolução =====
        foreach (var (p, diasAtras, prof) in new[] { (wagner, 1, helena), (celia, 2, marcos), (bruno, 4, helena), (irene, 6, marcos) })
        {
            var dia = hoje.AddDays(-diasAtras);
            if (dia < PoliticaRegistroPendente.DataInicio) continue; // antes do corte não é cobrança
            Realizada(p, dia, 9 + diasAtras % 3, ModalidadeAtendimento.AcupunturaSimples, prof);
        }

        // ===== A SEMANA: horários em outros dias, uma sessão longa e um cancelado =====
        var segunda = hoje.AddDays(-(((int)hoje.DayOfWeek + 6) % 7));
        foreach (var d in Enumerable.Range(0, 7).Select(segunda.AddDays).Where(d => d != hoje))
        {
            if (d > hoje)
            {
                A(rosana, d, 8, 30, ModalidadeAtendimento.AcupunturaComEletro, helena);
                A(wagner, d, 15, 0, ModalidadeAtendimento.BsvComAcupuntura, marcos, duracao: 60);
            }
            else if (d.DayOfWeek == DayOfWeek.Wednesday)
            {
                A(celia, d, 16, 0, ModalidadeAtendimento.AcupunturaSimples, helena, StatusAgendamento.Cancelado);
            }
        }

        // A clínica fechada num trecho de um dia desta semana (que não é hoje): a grade escreve o
        // motivo no vão. No passado da semana quando dá, para não fechar horário a marcar.
        var diaDaReuniao = segunda == hoje ? segunda.AddDays(1) : segunda;
        db.Add(new BloqueioAgenda
        {
            Inicio = diaDaReuniao.ToDateTime(new TimeOnly(12, 0)), Fim = diaDaReuniao.ToDateTime(new TimeOnly(13, 0)),
            Motivo = "Reunião de equipe", CriadoEm = DateTime.Today.AddDays(-10), CriadoPor = "previa"
        });
        db.SaveChanges();
    }
}
