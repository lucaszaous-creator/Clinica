// Dados FICTÍCIOS da prévia — nenhuma pessoa real.
using Clinica.Application.Modelos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;

namespace PreviaBlazor.Sementes;

/// <summary>
/// A rodada de confirmação de AMANHÃ (a tela abre nela) com um caso de cada: confirmou pelo
/// link do SMS, avisou pelo SMS que não vai, SMS enviado sem resposta, WhatsApp sem resposta,
/// não avisado com e sem telefone, confirmou no balcão e dispensado com motivo. E a fila de
/// "Retornos a marcar": atrasado, esta semana, mais adiante e um já marcado.
///
/// Os casos de SMS são HISTÓRICO: o SMS da véspera saiu na fase 3 Recepção×CRM (out/2026), e
/// as sessões avisadas por ele antes disso continuam aparecendo assim.
/// </summary>
public sealed class SementeConfirmacoesERetornos : ISemente
{
    public int Ordem => 80;

    public void Semear(ClinicaDbContext db)
    {
        var helena = db.Profissionais.First(p => p.Nome == "Dra. Helena Duarte");
        var marcos = db.Profissionais.First(p => p.Nome == "Dr. Marcos Vieira");
        var bianca = db.Profissionais.First(p => p.Nome == "Bianca Moraes");
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var amanha = hoje.AddDays(1).ToDateTime(TimeOnly.MinValue);

        Paciente P(string nome, string? telefone, string? email = null)
        {
            var p = new Paciente { Nome = nome, Telefone = telefone, Email = email, Convenio = Convenio.UnimedPadrao, DataNascimento = new DateOnly(1980, 4, 9) };
            db.Add(p);
            db.SaveChanges();
            return p;
        }

        void Sessao(Paciente p, Profissional prof, int hora, int minuto, ModalidadeAtendimento m,
            StatusContato status, CanalContato canal, int enviadoHaHoras = -1, string? comentario = null, string? por = null)
        {
            var a = new Agendamento
            {
                PacienteId = p.Id, ProfissionalId = prof.Id, DataHora = amanha.AddHours(hora).AddMinutes(minuto),
                ModalidadePrevista = m, Status = StatusAgendamento.Agendado, DuracaoMinutos = 30,
                EspecialidadeConsultaCodigo = m == ModalidadeAtendimento.Consulta ? Especialidade.Psiquiatria.ToString() : null
            };
            db.Add(a);
            db.SaveChanges();
            var enviado = enviadoHaHoras >= 0 ? DateTime.Now.AddHours(-enviadoHaHoras) : (DateTime?)null;
            db.Add(new ContatoCampanha
            {
                PacienteId = p.Id, Tipo = TipoContato.ConfirmacaoSessao, Status = status, Canal = canal,
                Origem = $"AGD:{a.Id}", AgendamentoId = a.Id, Referencia = DateOnly.FromDateTime(amanha),
                EnviadoEm = enviado, EnviadoPor = enviado is null ? null : por ?? "sistema (SMS automático)",
                RespondidoEm = status is StatusContato.Respondido or StatusContato.Dispensado ? DateTime.Now.AddHours(-1) : null,
                Comentario = comentario
            });
        }

        Sessao(P("Beatriz Carvalho Mendes", "(21) 98123-4501", "beatriz@exemplo.com"), helena, 8, 0, ModalidadeAtendimento.AcupunturaComEletro,
            StatusContato.Respondido, CanalContato.Sms, 20, RodadaDeConfirmacao.RespostaConfirmouPeloLink);
        Sessao(P("Rodrigo Teixeira Lopes", "(21) 98123-4502"), helena, 8, 30, ModalidadeAtendimento.AcupunturaSimples,
            StatusContato.Respondido, CanalContato.Sms, 20, RodadaDeConfirmacao.RespostaNaoVaiPeloLink);
        Sessao(P("Cláudia Regina Farias", "(21) 98123-4503", "claudia@exemplo.com"), marcos, 9, 0, ModalidadeAtendimento.Consulta,
            StatusContato.Enviado, CanalContato.Sms, 19);
        Sessao(P("Eduardo Pinheiro Rocha", "(21) 98123-4504"), marcos, 10, 0, ModalidadeAtendimento.Consulta,
            StatusContato.Enviado, CanalContato.WhatsApp, 3, por: "Recepção (prévia)");
        Sessao(P("Sônia Albuquerque", "(21) 98123-4505"), bianca, 10, 30, ModalidadeAtendimento.AcupunturaComEletro,
            StatusContato.Pendente, CanalContato.WhatsApp);
        Sessao(P("Wagner Siqueira", null), bianca, 11, 0, ModalidadeAtendimento.BsvComAcupuntura,
            StatusContato.Pendente, CanalContato.WhatsApp);
        Sessao(P("Patrícia Moura Dias", "(21) 98123-4507"), helena, 14, 0, ModalidadeAtendimento.AcupunturaSimples,
            StatusContato.Respondido, CanalContato.WhatsApp, 5, "confirmou", "Recepção (prévia)");
        Sessao(P("Igor Nascimento", "(21) 98123-4508"), marcos, 15, 0, ModalidadeAtendimento.Consulta,
            StatusContato.Dispensado, CanalContato.Presencial, comentario: "confirmou pessoalmente no balcão hoje");
        db.SaveChanges();

        // ===== Retornos a marcar =====
        void Retorno(Paciente p, Profissional prof, int sessaoHaDias, int retornoEmDias, string? nota)
            => db.Add(new Evolucao
            {
                PacienteId = p.Id, ProfissionalId = prof.Id, Data = hoje.AddDays(-sessaoHaDias),
                QueixaPrincipal = "Dor lombar crônica", Conduta = "Acupuntura com eletro",
                RetornoSugeridoEm = hoje.AddDays(retornoEmDias), RetornoSugeridoNota = nota
            });

        Retorno(P("Helena Batista Cunha", "(21) 98123-4511"), helena, 30, -9, "Reavaliar a dor após o 5º ciclo");
        Retorno(P("Marcelo Antunes", "(21) 98123-4512"), marcos, 14, -2, null);
        Retorno(P("Joana D'Arc Freitas", null), helena, 10, 1, "Ver exames de imagem");
        Retorno(P("Fábio Quintela", "(21) 98123-4514"), bianca, 6, 4, "Sessão de manutenção");
        Retorno(P("Lúcia Vasconcelos", "(21) 98123-4515"), marcos, 3, 25, "Retorno da consulta psiquiátrica");
        var marcado = P("Otávio Ramalho", "(21) 98123-4516");
        Retorno(marcado, helena, 12, 2, "Controle");
        db.SaveChanges();
        db.Add(new Agendamento
        {
            PacienteId = marcado.Id, ProfissionalId = helena.Id, DataHora = hoje.AddDays(2).ToDateTime(new TimeOnly(16, 0)),
            ModalidadePrevista = ModalidadeAtendimento.AcupunturaSimples, Status = StatusAgendamento.Agendado, DuracaoMinutos = 30
        });
        db.SaveChanges();
    }
}
