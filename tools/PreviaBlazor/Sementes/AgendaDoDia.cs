// Dados FICTÍCIOS da prévia — nenhuma pessoa real.
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;

namespace PreviaBlazor.Sementes;

/// <summary>
/// A AGENDA DO DIA do balcão (Recepção, out/2026): um dia de HOJE com todas as situações que a
/// lista sabe desenhar — quem ainda vem (confirmou, não confirmou, não vem, atrasado), quem está
/// no local, em atendimento, encerrado pelo profissional, concluído com pacote por debitar,
/// cancelado, falta, encaixe, consulta com especialidade, observação do balcão e um horário sem
/// profissional. Pacientes PRÓPRIOS desta semente, para não mexer no que outras telas contam.
/// </summary>
public sealed class SementeAgendaDoDia : ISemente
{
    public int Ordem => 45;

    public void Semear(ClinicaDbContext db)
    {
        var hoje = DateTime.Today;
        var agora = DateTime.Now;
        var helena = db.Set<Profissional>().First(p => p.NomeCurto == "Dra. Helena");
        var marcos = db.Set<Profissional>().First(p => p.NomeCurto == "Dr. Marcos");

        Paciente P(string nome, string telefone, string? convenio = null)
        {
            var p = new Paciente { Nome = nome, Telefone = telefone, DataNascimento = new DateOnly(1968, 5, 12), ConvenioCodigo = convenio };
            db.Add(p);
            return p;
        }

        var lucia = P("Lúcia Helena Carvalho", "(21) 98111-0001");
        var andre = P("André Luiz Teixeira", "(21) 98111-0002");
        var simone = P("Simone Batista Rocha", "(21) 98111-0003");
        var felipe = P("Felipe Augusto Nogueira", "(21) 98111-0004");
        var clara = P("Clara Menezes Duarte", "(21) 98111-0005");
        var heitor = P("Heitor Campos Leal", "(21) 98111-0006", ConvenioCadastro.CodigoParticular);
        var marta = P("Marta Rezende Pires", "(21) 98111-0007");
        var ivo = P("Ivo Sampaio Bastos", "(21) 98111-0008");
        var nadia = P("Nádia Fontes Ramos", "(21) 98111-0009");
        var otto = P("Otto Gusmão Prado", "(21) 98111-0010");
        db.SaveChanges();

        Agendamento A(Paciente p, int hora, int minuto, ModalidadeAtendimento m, Profissional? prof,
            StatusAgendamento status = StatusAgendamento.Agendado, int duracao = 30)
        {
            var a = new Agendamento
            {
                PacienteId = p.Id, ProfissionalId = prof?.Id, DataHora = hoje.AddHours(hora).AddMinutes(minuto),
                ModalidadePrevista = m, ModalidadeCodigo = m.ToString(), Status = status, DuracaoMinutos = duracao,
                CriadoPor = "recepcao", CriadoEm = hoje.AddDays(-3).AddHours(10)
            };
            db.Add(a);
            return a;
        }

        // Já aconteceu: concluída pelo consultório, com pacote por debitar.
        var concluido = new Atendimento { PacienteId = lucia.Id, Data = DateOnly.FromDateTime(hoje), Modalidade = ModalidadeAtendimento.AcupunturaComEletro, RealizadoEm = hoje.AddHours(8).AddMinutes(40) };
        db.Add(concluido);
        db.SaveChanges();
        var aLucia = A(lucia, 8, 0, ModalidadeAtendimento.AcupunturaComEletro, helena, StatusAgendamento.Realizado);
        aLucia.AtendimentoId = concluido.Id;
        aLucia.ChegadaEm = hoje.AddHours(7).AddMinutes(52);
        aLucia.InicioAtendimentoEm = hoje.AddHours(8).AddMinutes(5);
        aLucia.FimAtendimentoEm = hoje.AddHours(8).AddMinutes(38);
        var pacote = new PacotePaciente { PacienteId = lucia.Id, Nome = "Pacote de 10 sessões", DataCompra = DateOnly.FromDateTime(hoje).AddDays(-60), ValidoAte = DateOnly.FromDateTime(hoje).AddDays(60), SessoesContratadas = 10, Valor = 1500m };
        db.Add(pacote);
        db.SaveChanges();
        for (var i = 0; i < 9; i++) db.Add(new ConsumoPacote { PacotePacienteId = pacote.Id, Data = DateOnly.FromDateTime(hoje).AddDays(-7 * (i + 1)) });

        // Cancelou e faltou: ficam na lista, apagados.
        A(andre, 8, 30, ModalidadeAtendimento.AcupunturaSimples, marcos, StatusAgendamento.Cancelado);
        A(simone, 9, 0, ModalidadeAtendimento.BsvApenas, helena, StatusAgendamento.Faltou, 60);

        // Em atendimento — e um que o profissional já encerrou (vem fechar no balcão).
        var aFelipe = A(felipe, 9, 30, ModalidadeAtendimento.BsvComAcupuntura, helena, duracao: 60);
        aFelipe.ChegadaEm = hoje.AddHours(9).AddMinutes(20);
        aFelipe.InicioAtendimentoEm = hoje.AddHours(9).AddMinutes(35);
        var aClara = A(clara, 10, 0, ModalidadeAtendimento.AcupunturaSimples, marcos);
        aClara.ChegadaEm = hoje.AddHours(9).AddMinutes(50);
        aClara.InicioAtendimentoEm = hoje.AddHours(10).AddMinutes(2);
        aClara.FimAtendimentoEm = hoje.AddHours(10).AddMinutes(31);

        // No local, esperando — e um encaixe de particular.
        var aHeitor = A(heitor, 10, 30, ModalidadeAtendimento.Consulta, marcos);
        aHeitor.EspecialidadeConsultaCodigo = nameof(Especialidade.ClinicaDaDor);
        aHeitor.ChegadaEm = agora.AddMinutes(-12);
        aHeitor.Encaixe = true;
        aHeitor.Observacoes = "Trazer exames de imagem da última consulta.";

        // Ainda vêm: confirmou, não confirmou, não vem, e um sem profissional.
        var aMarta = A(marta, 14, 0, ModalidadeAtendimento.AcupunturaComEletro, helena);
        var aIvo = A(ivo, 14, 30, ModalidadeAtendimento.AcupunturaSimples, marcos);
        var aNadia = A(nadia, 15, 0, ModalidadeAtendimento.BsvApenas, helena, duracao: 60);
        A(otto, 16, 30, ModalidadeAtendimento.AcupunturaSimples, null);
        db.SaveChanges();

        void Confirmacao(Agendamento a, StatusContato status, string? comentario = null) => db.Add(new ContatoCampanha
        {
            PacienteId = a.PacienteId, AgendamentoId = a.Id, Tipo = TipoContato.ConfirmacaoSessao, Status = status,
            Origem = $"AGD:{a.Id}", Referencia = DateOnly.FromDateTime(hoje), EnviadoEm = hoje.AddDays(-1).AddHours(17),
            EnviadoPor = "recepcao", Comentario = comentario
        });
        Confirmacao(aMarta, StatusContato.Respondido);
        Confirmacao(aIvo, StatusContato.Enviado);
        Confirmacao(aNadia, StatusContato.Respondido, Clinica.Application.Modelos.RodadaDeConfirmacao.RespostaRemarcar);
        db.SaveChanges();
    }
}
