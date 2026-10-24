// Dados FICTÍCIOS da prévia — nenhuma pessoa real, nenhum telefone real.
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using static PreviaBlazor.PessoasDoWhatsApp;

namespace PreviaBlazor.Sementes;

/// <summary>
/// O WHATSAPP DA RECEPÇÃO (out/2026, PR 3 do WhatsApp nativo — docs/whatsapp-nativo-implementacao.md):
/// a metade DA CLÍNICA das conversas do mockup. O CRM de mentira (<c>CrmDeMentira.cs</c>) guarda as
/// mensagens; aqui ficam as fichas, a agenda, as respostas à confirmação e os casos do
/// Acompanhamento que a tela cruza com elas — com os nomes e telefones de
/// <see cref="PessoasDoWhatsApp"/> (a mesma fonte que o CRM de mentira usa para achar as fichas) e as
/// horas do <see cref="RelogioDaPrevia"/> (a mesma conta das mensagens).
///
/// O que cada parte faz aparecer, pelas regras de verdade:
/// <list type="bullet">
/// <item><b>Avisos do sino</b> (<c>AvisosDeConfirmacao.Decidir</c>): Maria "não vem" (respondeu
/// "pediu para remarcar"), Carlos "sem resposta" (lembrete COM link há mais de 3 h) e João
/// "confirmou" (pelo WhatsApp, hoje). O lembrete do Pedro saiu sem link: não vira aviso — e a
/// conversa é da Terezinha, sem ficha conferida.</item>
/// <item><b>Duas fichas no mesmo telefone</b>: Terezinha e o filho Pedro.</item>
/// <item><b>A retenção</b> (<c>IAcompanhamentoPacienteService.ListarAsync</c>): Sebastião, Helena
/// (SEM autorização de mensagens), Marcos, Rita, Fernanda (aguardando: convidada ontem), Alzira
/// (respondeu ao convite), Felipe e Cláudia (novos no BSV, sem a 1ª sessão), Inês (voltou: marcou)
/// e Geraldo (não quer voltar), com sessões que dão os dias do mockup.</item>
/// <item>O endereço do CRM (fictício): sem ele a aba mostra "ainda não foi ligado".</item>
/// </list>
/// Pacientes PRÓPRIOS (telefones com DDD 22, que nenhuma outra semente usa), para não mexer no que
/// outras telas contam. Os dias de "recall" dependem só do calendário; as horas, do relógio da prévia.
/// </summary>
public sealed class SementeConversasDoWhatsApp : ISemente
{
    // Depois do Acompanhamento (70: usuários e motivos).
    public int Ordem => 88;

    public void Semear(ClinicaDbContext db)
    {
        var R = RelogioDaPrevia.Em;
        var inicio = RelogioDaPrevia.Inicio;
        var hoje = DateOnly.FromDateTime(inicio);
        var helena = db.Set<Profissional>().First(p => p.NomeCurto == "Dra. Helena");
        var marcos = db.Set<Profissional>().First(p => p.NomeCurto == "Dr. Marcos");
        var ana = db.Set<UsuarioSistema>().FirstOrDefault(u => u.Login == "ana.previa");
        var bruno = db.Set<UsuarioSistema>().FirstOrDefault(u => u.Login == "bruno.previa");

        if (db.Configuracoes.Find(ParametrosService.ChaveEnderecoDoCrm) is null)
            db.Configuracoes.Add(new ConfiguracaoGlobal { Chave = ParametrosService.ChaveEnderecoDoCrm, Valor = "https://crm.exemplo.invalid" });
        db.SaveChanges();

        Paciente P(Pessoa pessoa, DateOnly nascimento, bool autorizou, Convenio convenio = Convenio.UnimedPadrao,
            bool particular = false, string? documento = null, string? carteirinha = null, DateOnly? validade = null, string? observacoes = null)
        {
            var p = new Paciente
            {
                Nome = pessoa.Nome, Telefone = pessoa.Telefone, DataNascimento = nascimento, Convenio = convenio,
                ConvenioCodigo = particular ? ConvenioCadastro.CodigoParticular : null, Documento = documento,
                Carteirinha = particular ? null : carteirinha, ValidadeCarteirinha = particular ? null : validade, Observacoes = observacoes
            };
            db.Add(p);
            db.SaveChanges();
            if (autorizou)
                db.Add(new ConsentimentoLgpd { PacienteId = p.Id, Finalidade = FinalidadeConsentimento.ComunicacaoEMarketing, Concedido = true,
                    RegistradoEm = inicio.AddDays(-120), RegistradoPor = "previa", VersaoTermo = "Termo do BSV" });
            return p;
        }

        // As sessões realizadas, uma por semana, terminando há "ultimaHaDias" dias.
        void Sessoes(Paciente p, ModalidadeAtendimento m, int quantas, int ultimaHaDias)
        {
            for (var i = 0; i < quantas; i++)
            {
                var data = hoje.AddDays(-ultimaHaDias - i * 7);
                db.Add(new Atendimento { PacienteId = p.Id, Modalidade = m, Data = data, RealizadoEm = data.ToDateTime(new TimeOnly(10, 0)) });
            }
        }

        Agendamento A(Paciente p, DateTime quando, ModalidadeAtendimento m, Profissional prof,
            StatusAgendamento status = StatusAgendamento.Agendado, int duracao = 30)
        {
            var a = new Agendamento
            {
                PacienteId = p.Id, ProfissionalId = prof.Id, DataHora = quando, ModalidadePrevista = m, ModalidadeCodigo = m.ToString(),
                Status = status, DuracaoMinutos = duracao, CriadoPor = "recepcao", CriadoEm = inicio.AddDays(-10)
            };
            db.Add(a);
            db.SaveChanges();
            return a;
        }

        // O lembrete de confirmação do horário e a resposta (ou a falta dela).
        void Confirmacao(Agendamento a, StatusContato status, DateTime enviado, DateTime? respondido = null,
            string? comentario = null, string? token = null) => db.Add(new ContatoCampanha
        {
            PacienteId = a.PacienteId, AgendamentoId = a.Id, Tipo = TipoContato.ConfirmacaoSessao, Status = status,
            Canal = token is null ? CanalContato.WhatsApp : CanalContato.Sms,
            Origem = ContatoCampanha.OrigemDeAgendamento(a.Id), Referencia = DateOnly.FromDateTime(a.DataHora),
            CriadoEm = enviado.AddHours(-12), EnviadoEm = enviado, EnviadoPor = "crm", RespondidoEm = respondido,
            Comentario = comentario, TokenResposta = token
        });

        // ===================== as conversas
        var maria = P(Maria, new DateOnly(1968, 3, 12), true, documento: "12345678942", carteirinha: "0 064 1234 5678 9", validade: hoje.AddDays(37));
        Sessoes(maria, ModalidadeAtendimento.AcupunturaSimples, 5, 7);
        var hMaria = A(maria, R(0, 14, 0), ModalidadeAtendimento.AcupunturaSimples, helena);
        A(maria, R(13, 14, 0), ModalidadeAtendimento.AcupunturaSimples, helena);
        Confirmacao(hMaria, StatusContato.Respondido, R(0, 6, 0), R(0, 9, 58), RodadaDeConfirmacao.RespostaRemarcar);

        var terezinha = P(Terezinha, new DateOnly(1965, 8, 2), false, documento: "44455566631", carteirinha: "0 064 8811 2233 1", validade: hoje.AddDays(400));
        Sessoes(terezinha, ModalidadeAtendimento.BsvApenas, 1, 52);
        var pedro = P(Pedro, new DateOnly(2017, 5, 14), true, particular: true, documento: "11122233307",
            observacoes: "Responsável: Terezinha Souza (mãe), que usa o mesmo telefone.");
        Sessoes(pedro, ModalidadeAtendimento.AcupunturaSimples, 4, 7);
        var hPedro = A(pedro, R(1, 9, 0), ModalidadeAtendimento.AcupunturaSimples, helena);
        Confirmacao(hPedro, StatusContato.Enviado, R(0, 6, 0)); // pelo WhatsApp, sem link: não vira "sem resposta"

        var carlos = P(Carlos, new DateOnly(1980, 7, 30), true, Convenio.Amil, documento: "33344455515", carteirinha: "7788 0001 2", validade: new DateOnly(hoje.Year + 1, 12, 31));
        Sessoes(carlos, ModalidadeAtendimento.BsvComAcupuntura, 6, 7);
        var hCarlos = A(carlos, R(0, 16, 0), ModalidadeAtendimento.BsvComAcupuntura, marcos, duracao: 60);
        Confirmacao(hCarlos, StatusContato.Enviado, R(0, 6, 0), token: "pv-carlos-hoje");

        var joao = P(Joao, new DateOnly(1958, 11, 2), true, documento: "66677788888", carteirinha: "0 064 9988 7766 5", validade: new DateOnly(hoje.Year + 1, 6, 30));
        Sessoes(joao, ModalidadeAtendimento.AcupunturaSimples, 8, 7);
        var hJoao = A(joao, R(0, 15, 0), ModalidadeAtendimento.AcupunturaSimples, helena);
        Confirmacao(hJoao, StatusContato.Respondido, R(0, 6, 0), R(0, 9, 40), RodadaDeConfirmacao.RespostaConfirmouPeloWhatsApp);

        var roberto = P(Roberto, new DateOnly(1974, 1, 19), true, particular: true, documento: "99988877760");
        Sessoes(roberto, ModalidadeAtendimento.AcupunturaSimples, 3, 8);
        A(roberto, R(6, 9, 30), ModalidadeAtendimento.AcupunturaSimples, helena);
        var pacote = new PacotePaciente { PacienteId = roberto.Id, Nome = "Pacote de 10 sessões", DataCompra = hoje.AddDays(-30),
            ValidoAte = hoje.AddDays(150), SessoesContratadas = 10, Valor = 1500m };
        db.Add(pacote);
        db.SaveChanges();
        for (var i = 0; i < 3; i++) db.Add(new ConsumoPacote { PacotePacienteId = pacote.Id, Data = hoje.AddDays(-8 - i * 7) });

        var antonio = P(Antonio, new DateOnly(1952, 9, 21), true, documento: "22233344404", carteirinha: "0 064 1122 3344 5", validade: hoje.AddDays(70));
        Sessoes(antonio, ModalidadeAtendimento.BsvApenas, 3, 14);
        A(antonio, R(3, 10, 0), ModalidadeAtendimento.BsvApenas, marcos, duracao: 60);

        // ===================== a retenção (o Acompanhamento: Recall e Novo BSV)
        AcompanhamentoPaciente Caso(Paciente p, TipoAcompanhamento tipo, ModalidadeAtendimento m, int referenciaHaDias,
            UsuarioSistema? dono, int proximoEmDias, EtapaAcompanhamento etapa = EtapaAcompanhamento.AContatar, Profissional? prof = null)
        {
            var referencia = inicio.Date.AddDays(-referenciaHaDias);
            var c = new AcompanhamentoPaciente
            {
                PacienteId = p.Id, Tipo = tipo, Modalidade = m, ReferenciaEm = referencia, CriadoEm = referencia.AddHours(8),
                CriadoPor = "previa", ResponsavelId = dono?.Id, ProximoContato = hoje.AddDays(proximoEmDias), Etapa = etapa,
                ProfissionalId = (prof ?? helena).Id
            };
            // A abertura do caso não tem canal: não conta como contato com o paciente.
            c.Contatos.Add(new ContatoAcompanhamento { Idempotencia = Guid.NewGuid(), Em = referencia.AddHours(8), Operador = "previa",
                Resultado = tipo == TipoAcompanhamento.NovoBsv ? "Indicação de novo paciente BSV" : "Novo ciclo de recall",
                ResponsavelId = dono?.Id, ProximoContato = hoje });
            db.Add(c);
            return c;
        }

        void Contato(AcompanhamentoPaciente c, UsuarioSistema? quem, DateTime em, CanalContato canal, string resultado, string observacao = "")
            => c.Contatos.Add(new ContatoAcompanhamento { Idempotencia = Guid.NewGuid(), Em = em, Operador = quem?.Login ?? "previa",
                Canal = canal, Resultado = resultado, Observacao = observacao, ResponsavelId = quem?.Id, ProximoContato = c.ProximoContato });

        // "Fechado esta semana" conta de segunda até hoje: o momento é recuado até N dias, sem sair da semana nem passar de agora.
        var diasDesdeSegunda = ((int)inicio.DayOfWeek + 6) % 7;
        DateTime NestaSemana(int diasAtras, int hora)
        {
            var dia = inicio.Date.AddDays(-Math.Min(diasAtras, diasDesdeSegunda));
            var momento = dia.AddHours(hora);
            return momento <= inicio ? momento : (inicio.AddMinutes(-5) >= dia ? inicio.AddMinutes(-5) : dia);
        }

        // Fernanda: convidada ontem pelo WhatsApp (o modelo da conversa do CRM); aguardando a resposta.
        var fernanda = P(Fernanda, new DateOnly(1987, 6, 8), true, documento: "55566677723", carteirinha: "0 064 5544 3322 1", validade: new DateOnly(hoje.Year + 1, 3, 31));
        Sessoes(fernanda, ModalidadeAtendimento.AcupunturaSimples, 4, 20);
        var cFernanda = Caso(fernanda, TipoAcompanhamento.Recall, ModalidadeAtendimento.AcupunturaSimples, 20, bruno, 3, EtapaAcompanhamento.SemResposta);
        Contato(cFernanda, bruno, R(-1, 15, 10), CanalContato.WhatsApp, "Convite para voltar enviado pelo WhatsApp");

        // Alzira: o convite foi anteontem; ela respondeu ontem (o "Respondeu" vem da conversa do CRM).
        var alzira = P(Alzira, new DateOnly(1955, 4, 3), true, documento: "77788899977", carteirinha: "0 064 2211 3344 0", validade: new DateOnly(hoje.Year + 1, 9, 30));
        Sessoes(alzira, ModalidadeAtendimento.BsvApenas, 5, 16);
        var cAlzira = Caso(alzira, TipoAcompanhamento.Recall, ModalidadeAtendimento.BsvApenas, 16, bruno, 2, EtapaAcompanhamento.SemResposta, marcos);
        Contato(cAlzira, bruno, R(-2, 15, 12), CanalContato.WhatsApp, "Convite para voltar enviado pelo WhatsApp");

        var sebastiao = P(Sebastiao, new DateOnly(1963, 2, 11), true, documento: "10120230340", carteirinha: "0 064 3300 1122 9", validade: new DateOnly(hoje.Year + 1, 1, 31));
        Sessoes(sebastiao, ModalidadeAtendimento.BsvApenas, 6, 32);
        Caso(sebastiao, TipoAcompanhamento.Recall, ModalidadeAtendimento.BsvApenas, 32, null, 0, prof: marcos);
        var pacoteSeb = new PacotePaciente { PacienteId = sebastiao.Id, Nome = "Pacote de 10 sessões de BSV", DataCompra = hoje.AddDays(-80),
            ValidoAte = hoje.AddDays(100), SessoesContratadas = 10, Valor = 3200m };
        db.Add(pacoteSeb);
        db.SaveChanges();
        for (var i = 0; i < 6; i++) db.Add(new ConsumoPacote { PacotePacienteId = pacoteSeb.Id, Data = hoje.AddDays(-32 - i * 7) });

        // Helena: NÃO autorizou mensagens — a única ação é ligar.
        var helenaM = P(Helena, new DateOnly(1977, 8, 27), false, Convenio.Amil, documento: "20230340450", carteirinha: "0101 9988 7", validade: new DateOnly(hoje.Year + 1, 5, 31));
        Sessoes(helenaM, ModalidadeAtendimento.AcupunturaSimples, 3, 30);
        Caso(helenaM, TipoAcompanhamento.Recall, ModalidadeAtendimento.AcupunturaSimples, 30, null, 0);

        var marcosT = P(Marcos, new DateOnly(1981, 12, 9), true, particular: true, documento: "30340450560");
        Sessoes(marcosT, ModalidadeAtendimento.BsvComAcupuntura, 7, 21);
        Caso(marcosT, TipoAcompanhamento.Recall, ModalidadeAtendimento.BsvComAcupuntura, 21, ana, 0, prof: marcos);
        A(marcosT, R(-7, 14, 0), ModalidadeAtendimento.BsvComAcupuntura, marcos, StatusAgendamento.Faltou, 60);

        var rita = P(Rita, new DateOnly(1969, 6, 14), true, documento: "40450560670", carteirinha: "0 064 7766 5544 3", validade: new DateOnly(hoje.Year + 1, 2, 28));
        Sessoes(rita, ModalidadeAtendimento.AcupunturaSimples, 8, 15);
        Caso(rita, TipoAcompanhamento.Recall, ModalidadeAtendimento.AcupunturaSimples, 15, null, 0);

        // Novos no BSV: indicados pelo Dr. Marcos numa consulta, ainda sem a 1ª sessão.
        var felipe = P(Felipe, new DateOnly(1988, 5, 5), true, particular: true, documento: "50560670780");
        Sessoes(felipe, ModalidadeAtendimento.Consulta, 1, 3);
        Caso(felipe, TipoAcompanhamento.NovoBsv, ModalidadeAtendimento.BsvApenas, 3, null, 0, prof: marcos);
        var claudia = P(Claudia, new DateOnly(1973, 10, 30), true, documento: "60670780890", carteirinha: "0 064 9090 1212 3", validade: new DateOnly(hoje.Year + 1, 8, 31));
        Sessoes(claudia, ModalidadeAtendimento.Consulta, 1, 2);
        Caso(claudia, TipoAcompanhamento.NovoBsv, ModalidadeAtendimento.BsvApenas, 2, null, 0, prof: marcos);

        // Fechados esta semana: Inês voltou (marcou); Geraldo não quer voltar (mudou de cidade).
        var ines = P(Ines, new DateOnly(1979, 4, 18), true, documento: "70780890900", carteirinha: "0 064 7000 1234 6", validade: new DateOnly(hoje.Year + 1, 4, 30));
        Sessoes(ines, ModalidadeAtendimento.AcupunturaSimples, 5, 23);
        var cInes = Caso(ines, TipoAcompanhamento.Recall, ModalidadeAtendimento.AcupunturaSimples, 23, ana, 0, EtapaAcompanhamento.ProntoParaAgendar);
        Contato(cInes, ana, NestaSemana(2, 10), CanalContato.WhatsApp, "Convite para voltar enviado pelo WhatsApp");
        Contato(cInes, ana, NestaSemana(1, 11), CanalContato.WhatsApp, "Pronto para agendar", "Quer marcar: horário marcado com a Dra. Helena.");
        A(ines, R(6, 11, 0), ModalidadeAtendimento.AcupunturaSimples, helena);

        var geraldo = P(Geraldo, new DateOnly(1960, 1, 25), true, documento: "80890900011", carteirinha: "0 064 9100 9900 2", validade: new DateOnly(hoje.Year + 1, 7, 31));
        Sessoes(geraldo, ModalidadeAtendimento.BsvApenas, 2, 39);
        var cGeraldo = Caso(geraldo, TipoAcompanhamento.Recall, ModalidadeAtendimento.BsvApenas, 39, bruno, 0, prof: marcos);
        var encerrouEm = NestaSemana(3, 15);
        Contato(cGeraldo, bruno, encerrouEm, CanalContato.Telefone, "Não conformidade — encerrado", "Não quer voltar: mudou para Campos.");
        var mudouDeCidade = db.MotivosAcompanhamento.FirstOrDefault(m => m.Nome == "Mudou de cidade");
        if (mudouDeCidade is null)
        {
            mudouDeCidade = new MotivoAcompanhamento { Nome = "Mudou de cidade" };
            db.Add(mudouDeCidade);
        }
        db.SaveChanges();
        cGeraldo.EncerradoEm = encerrouEm;
        cGeraldo.EncerradoPor = bruno?.Login ?? "previa";
        cGeraldo.MotivoEncerramento = "Não quer voltar: mudou para Campos.";
        cGeraldo.MotivoId = mudouDeCidade.Id;
        db.SaveChanges();
    }
}
