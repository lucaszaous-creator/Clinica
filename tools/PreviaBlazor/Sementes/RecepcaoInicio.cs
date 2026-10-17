// Dados FICTÍCIOS da prévia — nenhuma pessoa real.
using Clinica.Application.Abstracoes;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Clinica.Infrastructure.Planejamento;

namespace PreviaBlazor.Sementes;

/// <summary>
/// O INÍCIO da Recepção e os PEDIDOS DA ENFERMAGEM (out/2026, migração para Blazor).
///
/// O dia de hoje com um horário em cada estado que a tela desenha: atendido, falta,
/// cancelado, na sala, já no local, confirmou, avisado sem resposta, avisou que NÃO VEM,
/// marcado sem contato (sem pílula) e um encaixe de quem tem guia do 2º código pendente (a
/// semente do Faturamento lança a sessão de dias atrás). Uma aniversariante que vem hoje, a
/// rodada de confirmação de amanhã pela metade, e a caixa da enfermagem com um aviso em cada
/// situação (novo para hoje e amanhã, visto, encerrado e retirado).
/// </summary>
public sealed class SementeRecepcaoInicio : ISemente
{
    public int Ordem => 75;

    public void Semear(ClinicaDbContext db)
    {
        var hoje = DateTime.Today;
        var agora = DateTime.Now;
        var helena = db.Profissionais.First(p => p.Nome == "Dra. Helena Duarte");
        var marcos = db.Profissionais.First(p => p.Nome == "Dr. Marcos Vieira");
        var bianca = db.Profissionais.First(p => p.Nome == "Bianca Moraes");
        Paciente Paciente(string nome) => db.Pacientes.First(p => p.Nome == nome);

        var aniversariante = new Paciente
        {
            Nome = "Laura Bittencourt Reis", Telefone = "(21) 98111-2233", Convenio = Convenio.UnimedPadrao,
            DataNascimento = new DateOnly(1981, hoje.Month, Math.Min(hoje.Day, 28))
        };
        var aniversarianteSemana = new Paciente
        {
            Nome = "Otília Ramos", Telefone = "(21) 98222-3344",
            DataNascimento = DateOnly.FromDateTime(hoje.AddDays(3)).AddYears(-67)
        };
        db.AddRange(aniversariante, aniversarianteSemana);
        db.SaveChanges();

        Agendamento Horario(Paciente p, int hora, int minuto, ModalidadeAtendimento m, Profissional quem,
            StatusAgendamento status = StatusAgendamento.Agendado, bool encaixe = false)
        {
            var a = new Agendamento
            {
                PacienteId = p.Id, ProfissionalId = quem.Id, DataHora = hoje.AddHours(hora).AddMinutes(minuto),
                ModalidadePrevista = m, Status = status, DuracaoMinutos = 30, Encaixe = encaixe, CriadoPor = "previa", CriadoEm = hoje.AddDays(-3)
            };
            db.Add(a);
            return a;
        }

        var atendido = Horario(Paciente("Adriana Lopes Machado"), 7, 30, ModalidadeAtendimento.AcupunturaComEletro, helena, StatusAgendamento.Realizado);
        atendido.ChegadaEm = hoje.AddHours(7).AddMinutes(20);
        atendido.InicioAtendimentoEm = hoje.AddHours(7).AddMinutes(32);
        atendido.FimAtendimentoEm = hoje.AddHours(8);
        Horario(Paciente("Ana Beatriz Souza"), 8, 0, ModalidadeAtendimento.AcupunturaSimples, helena, StatusAgendamento.Faltou);
        Horario(Paciente("Carlos Eduardo Lima"), 8, 30, ModalidadeAtendimento.Consulta, marcos, StatusAgendamento.Cancelado);

        var naSala = Horario(Paciente("Fernanda Ribeiro"), 9, 0, ModalidadeAtendimento.BsvComAcupuntura, helena);
        naSala.ChegadaEm = agora.AddMinutes(-40);
        naSala.ChamadoEm = agora.AddMinutes(-25);
        naSala.InicioAtendimentoEm = agora.AddMinutes(-24);

        var noLocal = Horario(Paciente("João Pedro Alves"), 9, 30, ModalidadeAtendimento.AcupunturaComEletro, bianca);
        noLocal.ChegadaEm = agora.AddMinutes(-12);

        var confirmou = Horario(Paciente("Mariana Costa"), 10, 0, ModalidadeAtendimento.BsvApenas, marcos);
        var semResposta = Horario(Paciente("Paulo Henrique Martins"), 10, 30, ModalidadeAtendimento.Consulta, marcos);
        var naoVem = Horario(Paciente("Renata Oliveira"), 11, 0, ModalidadeAtendimento.AcupunturaSimples, bianca);
        Horario(Paciente("Bruno Siqueira Campos"), 11, 30, ModalidadeAtendimento.AcupunturaComEletro, helena, encaixe: true);
        Horario(aniversariante, 14, 0, ModalidadeAtendimento.AcupunturaSimples, bianca);
        Horario(Paciente("Tiago Fernandes"), 15, 0, ModalidadeAtendimento.Consulta, marcos);

        // A rodada de amanhã: um avisado sem resposta, um confirmado e dois ainda por avisar.
        var amanha = new[]
        {
            Horario(Paciente("Luciana Prado"), 24 + 8, 0, ModalidadeAtendimento.AcupunturaSimples, bianca),
            Horario(Paciente("Roberto Nunes"), 24 + 9, 0, ModalidadeAtendimento.BsvApenas, marcos),
            Horario(Paciente("Camila Fontes Barreto"), 24 + 10, 0, ModalidadeAtendimento.AcupunturaComEletro, helena),
            Horario(Paciente("Diego Albuquerque"), 24 + 11, 0, ModalidadeAtendimento.Consulta, marcos)
        };
        db.SaveChanges();

        void Contato(Agendamento a, StatusContato status, string? comentario = null)
            => db.Add(new ContatoCampanha
            {
                PacienteId = a.PacienteId, Tipo = TipoContato.ConfirmacaoSessao, Status = status,
                Origem = ContatoCampanha.OrigemDeAgendamento(a.Id), AgendamentoId = a.Id,
                Referencia = DateOnly.FromDateTime(a.DataHora), CriadoEm = hoje.AddDays(-1),
                EnviadoEm = status == StatusContato.Pendente ? null : hoje.AddDays(-1).AddHours(17),
                EnviadoPor = status == StatusContato.Pendente ? null : "previa",
                RespondidoEm = status == StatusContato.Respondido ? hoje.AddDays(-1).AddHours(18) : null,
                Comentario = comentario
            });
        Contato(confirmou, StatusContato.Respondido);
        Contato(semResposta, StatusContato.Enviado);
        Contato(naoVem, StatusContato.Respondido, Clinica.Application.Modelos.RodadaDeConfirmacao.RespostaNaoVaiPeloLink);
        Contato(amanha[0], StatusContato.Enviado);
        Contato(amanha[1], StatusContato.Respondido);
        Contato(amanha[2], StatusContato.Pendente);
        Contato(amanha[3], StatusContato.Pendente);
        db.SaveChanges();

        SemearPedidosDaEnfermagem(db, hoje, agora);
    }

    /// <summary>
    /// A caixa da enfermagem. A referência da agenda de cada período vem do PRÓPRIO serviço
    /// (<c>PeriodoAsync</c>): sem ela todo aviso apareceria com "a agenda mudou desde o envio".
    /// Um deles fica de propósito com a referência antiga, para a tela desenhar esse aviso.
    /// </summary>
    private static void SemearPedidosDaEnfermagem(ClinicaDbContext db, DateTime hojeLocal, DateTime agoraLocal)
    {
        // O serviço só aceita datas SEM fuso (Kind Unspecified), como o que o banco devolve.
        var hoje = DateTime.SpecifyKind(hojeLocal, DateTimeKind.Unspecified);
        var agora = DateTime.SpecifyKind(agoraLocal, DateTimeKind.Unspecified);
        var joana = new Profissional { Nome = "Joana Técnica de Enfermagem", NomeCurto = "Joana", RegistroConselho = "COREN-RJ 123.456" };
        db.Add(joana);
        db.SaveChanges();
        var enfermagem = new UsuarioSistema
        {
            Nome = "Joana Técnica", Login = "joana.previa", Perfil = PerfilAcesso.Enfermagem,
            SenhaHash = "-", SenhaSalt = "-", Ativo = true, ProfissionalId = joana.Id
        };
        db.Add(enfermagem);
        db.SaveChanges();

        var servico = new PlanejamentoEnfermagemService(db);
        string Referencia(DateTime inicio, DateTime fim)
            => servico.PeriodoAsync(enfermagem.Id, new PeriodoEnfermagem(inicio, fim)).GetAwaiter().GetResult().Referencia;

        SolicitacaoEnfermagem Pedido(DateTime inicio, int horas, ModalidadeAtendimento m, int quantidade, string observacao,
            string situacao = "Novo", bool referenciaAntiga = false)
        {
            var fim = inicio.AddHours(horas);
            var p = new SolicitacaoEnfermagem
            {
                AutoraId = enfermagem.Id, Idempotencia = Guid.NewGuid(), PedidoHash = "previa", Inicio = inicio, Fim = fim,
                Validade = inicio > agora ? inicio : agora.AddDays(-1), CriadaEm = agora.AddHours(-2), Modalidade = m,
                Quantidade = quantidade, Observacao = observacao, Situacao = situacao,
                AgendaReferencia = referenciaAntiga ? "previa-antiga" : Referencia(inicio, fim), TotalReferencia = 3
            };
            p.Eventos.Add(new EventoSolicitacaoEnfermagem
            {
                Idempotencia = p.Idempotencia, PedidoHash = "previa", UsuarioId = enfermagem.Id, Autor = enfermagem.Nome,
                Em = p.CriadaEm, Acao = "Pedido enviado", Observacao = observacao
            });
            db.Add(p);
            return p;
        }

        // Para hoje, à tarde — com a agenda já diferente do que estava no envio.
        Pedido(hoje.AddHours(23), 1, ModalidadeAtendimento.BsvApenas, 2,
            "Duas cadeiras livres na sala de infusão no fim do dia.", referenciaAntiga: true);
        Pedido(hoje.AddDays(1).AddHours(8), 4, ModalidadeAtendimento.BsvApenas, 4,
            "A sala de infusão está com folga pela manhã; podem encaixar até 4 BSV.");
        var visto = Pedido(hoje.AddDays(3).AddHours(13), 3, ModalidadeAtendimento.BsvComAcupuntura, 3, "", "Visto");
        visto.VistaEm = agora.AddHours(-1);
        visto.VistaPor = "Ana Lima";
        visto.Eventos.Add(new EventoSolicitacaoEnfermagem
        {
            Idempotencia = Guid.NewGuid(), PedidoHash = "previa", UsuarioId = enfermagem.Id, Autor = "Ana Lima",
            Em = agora.AddHours(-1), Acao = "Visto pela recepção"
        });
        var encerrado = Pedido(hoje.AddDays(-4).AddHours(9), 3, ModalidadeAtendimento.BsvApenas, 2,
            "Folga na sala depois do almoço.", "Encerrado");
        encerrado.Eventos.Add(new EventoSolicitacaoEnfermagem
        {
            Idempotencia = Guid.NewGuid(), PedidoHash = "previa", UsuarioId = enfermagem.Id, Autor = "Ana Lima",
            Em = hoje.AddDays(-5).AddHours(16), Acao = "Aviso encerrado", Observacao = "Marcamos 2 BSV às 13h."
        });
        Pedido(hoje.AddDays(-2).AddHours(14), 2, ModalidadeAtendimento.BsvApenas, 1, "Uma cadeira livre.", "Retirado");
        db.SaveChanges();
    }
}
