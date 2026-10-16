// Dados FICTÍCIOS da prévia — nenhuma pessoa real.
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;

namespace PreviaBlazor.Sementes;

/// <summary>
/// O ACOMPANHAMENTO DE PACIENTES (Relacionamento → Acompanhamento, out/2026): uma fila com
/// um caso em cada faixa do funil e em cada desvio que a tela precisa desenhar — a assumir,
/// para contatar hoje, atrasado, sem resposta (com tentativas), quer marcar, aguardando o
/// plano, sem telefone, sem autorização de mensagem, cancelou, agendado, voltou, encerrado
/// com motivo e as indicações de BSV.
///
/// Os casos são da "Ana Lima" (a recepcionista da prévia) e do "Bruno Castro"; quem entra na
/// prévia é a Ana, como Gerente, para o Configurar aparecer. ⚠️ No `dotnet test` quem entra é
/// a direção do teste (criada depois das sementes): a sessão só é trocada aqui quando o
/// processo é a PRÉVIA — senão uma semente mudaria a sessão global de outra suíte.
/// </summary>
public sealed class SementeAcompanhamento : ISemente
{
    public int Ordem => 70;

    public void Semear(ClinicaDbContext db)
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var agora = DateTime.Now;
        DateTime Dia(int dias) => DateTime.Today.AddDays(dias);

        var ana = new UsuarioSistema { Nome = "Ana Lima", Login = "ana.previa", Perfil = PerfilAcesso.Gerente, SenhaHash = "-", SenhaSalt = "-", Ativo = true };
        var bruno = new UsuarioSistema { Nome = "Bruno Castro", Login = "bruno.previa", Perfil = PerfilAcesso.Recepcao, SenhaHash = "-", SenhaSalt = "-", Ativo = true };
        db.AddRange(ana, bruno);
        // As migrations já semeiam alguns motivos (no Postgres da suíte elas rodam; no SQLite não):
        // reaproveita o que existe pelo nome — o índice é único — e só cria o que falta.
        var existentes = db.MotivosAcompanhamento.ToList();
        var motivos = new[] { "Plano negou autorização", "Pediu adiamento", "Mudou de cidade" }
            .Select(n => existentes.FirstOrDefault(m => m.Nome == n) ?? new MotivoAcompanhamento { Nome = n }).ToArray();
        db.AddRange(motivos.Where(m => m.Id == 0));
        db.SaveChanges();

        var helena = db.Profissionais.First(p => p.Nome == "Dra. Helena Duarte");
        var marcos = db.Profissionais.First(p => p.Nome == "Dr. Marcos Vieira");
        foreach (var (chave, valor) in new[] { (AcompanhamentoPacienteService.ChaveProfissional, marcos.Id), (AcompanhamentoPacienteService.ChaveResponsavel, 0) })
            if (db.Configuracoes.Find(chave) is null) db.Configuracoes.Add(new ConfiguracaoGlobal { Chave = chave, Valor = valor.ToString() });

        // (nome, telefone, autoriza mensagem, convênio)
        Paciente P(string nome, string? telefone, bool autoriza, Convenio convenio = Convenio.UnimedPadrao)
        {
            var p = new Paciente { Nome = nome, Telefone = telefone, Convenio = convenio, DataNascimento = new DateOnly(1975, 6, 12) };
            db.Add(p);
            db.SaveChanges();
            if (autoriza)
                db.Add(new ConsentimentoLgpd { PacienteId = p.Id, Finalidade = FinalidadeConsentimento.ComunicacaoEMarketing, Concedido = true, RegistradoEm = agora.AddDays(-200), RegistradoPor = "previa" });
            return p;
        }

        // A última sessão realizada da modalidade — é ela que dá a data de "sumiu" e as sessões do tratamento.
        void Sessoes(Paciente p, ModalidadeAtendimento m, int quantas, int ultimaHaDias)
        {
            for (var i = 0; i < quantas; i++)
            {
                var data = hoje.AddDays(-ultimaHaDias - i * 7);
                db.Add(new Atendimento { PacienteId = p.Id, Modalidade = m, Data = data, RealizadoEm = data.ToDateTime(new TimeOnly(10, 0)) });
            }
        }

        AcompanhamentoPaciente Caso(Paciente p, TipoAcompanhamento tipo, ModalidadeAtendimento m, int referenciaHaDias,
            UsuarioSistema? dono, int proximoEmDias, EtapaAcompanhamento etapa = EtapaAcompanhamento.AContatar, int? profissional = null)
        {
            var c = new AcompanhamentoPaciente
            {
                PacienteId = p.Id, Tipo = tipo, Modalidade = m, ReferenciaEm = Dia(-referenciaHaDias), CriadoEm = Dia(-referenciaHaDias + 60),
                CriadoPor = "previa", ResponsavelId = dono?.Id, ProximoContato = hoje.AddDays(proximoEmDias), Etapa = etapa,
                ProfissionalId = profissional ?? helena.Id
            };
            c.Contatos.Add(new ContatoAcompanhamento { Idempotencia = Guid.NewGuid(), Em = Dia(-referenciaHaDias + 60).AddHours(9), Operador = "previa",
                Resultado = tipo == TipoAcompanhamento.NovoBsv ? "Indicação de novo paciente BSV" : "Novo ciclo de recall", ResponsavelId = dono?.Id, ProximoContato = hoje });
            if (dono is not null)
                c.Contatos.Add(new ContatoAcompanhamento { Idempotencia = Guid.NewGuid(), Em = Dia(-3).AddHours(10), Operador = dono.Login,
                    Resultado = "Assumiu o acompanhamento", ResponsavelId = dono.Id, ProximoContato = c.ProximoContato });
            db.Add(c);
            return c;
        }

        void Tentativa(AcompanhamentoPaciente c, UsuarioSistema quem, int haDias, string resultado, string observacao = "")
            => c.Contatos.Add(new ContatoAcompanhamento { Idempotencia = Guid.NewGuid(), Em = Dia(-haDias).AddHours(15), Operador = quem.Login,
                Canal = CanalContato.WhatsApp, Resultado = resultado, Observacao = observacao, ResponsavelId = quem.Id, ProximoContato = c.ProximoContato });

        // ===== Recall =====
        var carlos = P("Carlos Eduardo Ramos", "(21) 98765-1100", true, Convenio.Petrobras);
        Sessoes(carlos, ModalidadeAtendimento.Consulta, 2, 65);
        Caso(carlos, TipoAcompanhamento.Recall, ModalidadeAtendimento.Consulta, 65, null, 0);

        var paulo = P("Paulo Sérgio Nunes", "(21) 98765-1101", true);
        Sessoes(paulo, ModalidadeAtendimento.AcupunturaComEletro, 3, 71);
        Caso(paulo, TipoAcompanhamento.Recall, ModalidadeAtendimento.AcupunturaComEletro, 71, null, 0);

        var maria = P("Maria Aparecida Souza", "(21) 99876-5432", true);
        Sessoes(maria, ModalidadeAtendimento.AcupunturaComEletro, 32, 74);
        var cMaria = Caso(maria, TipoAcompanhamento.Recall, ModalidadeAtendimento.AcupunturaComEletro, 74, ana, -2);
        Tentativa(cMaria, ana, 6, "Sem resposta");
        db.Add(new PacotePaciente { PacienteId = maria.Id, Nome = "Pacote de 10 sessões", DataCompra = hoje.AddDays(-120), ValidoAte = hoje.AddDays(120), SessoesContratadas = 10, Valor = 1500m });

        var lucia = P("Lúcia Prado", "(21) 98765-1103", true);
        Sessoes(lucia, ModalidadeAtendimento.AcupunturaComEletro, 6, 62);
        Caso(lucia, TipoAcompanhamento.Recall, ModalidadeAtendimento.AcupunturaComEletro, 62, ana, 0);

        var joao = P("João Pedro Lima", "(21) 98765-1104", true, Convenio.Amil);
        Sessoes(joao, ModalidadeAtendimento.AcupunturaComEletro, 4, 61);
        var cJoao = Caso(joao, TipoAcompanhamento.Recall, ModalidadeAtendimento.AcupunturaComEletro, 61, ana, 2, EtapaAcompanhamento.SemResposta);
        Tentativa(cJoao, ana, 4, "Sem resposta");
        Tentativa(cJoao, ana, 1, "Sem resposta", "Chamou e não visualizou.");

        var rosana = P("Rosana Teixeira", "(21) 98765-1105", true, Convenio.Amil);
        Sessoes(rosana, ModalidadeAtendimento.AcupunturaComEletro, 8, 88);
        var cRosana = Caso(rosana, TipoAcompanhamento.Recall, ModalidadeAtendimento.AcupunturaComEletro, 88, ana, 0, EtapaAcompanhamento.ProntoParaAgendar);
        Tentativa(cRosana, ana, 1, "Pronto para agendar", "Prefere de manhã.");

        var helenaM = P("Helena Martins", null, true);
        Sessoes(helenaM, ModalidadeAtendimento.AcupunturaComEletro, 2, 92);
        Caso(helenaM, TipoAcompanhamento.Recall, ModalidadeAtendimento.AcupunturaComEletro, 92, ana, 0);

        var sergio = P("Sérgio Antunes", "(21) 98765-1107", false);
        Sessoes(sergio, ModalidadeAtendimento.Consulta, 1, 80);
        Caso(sergio, TipoAcompanhamento.Recall, ModalidadeAtendimento.Consulta, 80, bruno, 0);

        var beatriz = P("Beatriz Carvalho", "(21) 98765-1108", true, Convenio.Petrobras);
        Sessoes(beatriz, ModalidadeAtendimento.Consulta, 2, 70);
        var cBeatriz = Caso(beatriz, TipoAcompanhamento.Recall, ModalidadeAtendimento.Consulta, 70, bruno, 1, EtapaAcompanhamento.AguardandoPlano);
        Tentativa(cBeatriz, bruno, 2, "Aguardando plano", "Pedido de autorização enviado ao plano.");

        var rafael = P("Rafael Monteiro", "(21) 98765-1109", true);
        Sessoes(rafael, ModalidadeAtendimento.AcupunturaComEletro, 3, 66);
        var cRafael = Caso(rafael, TipoAcompanhamento.Recall, ModalidadeAtendimento.AcupunturaComEletro, 66, ana, 5, EtapaAcompanhamento.RetornarNaData);
        Tentativa(cRafael, ana, 2, "Retornar na data combinada", "Pediu para ligar depois do dia 20.");

        // Cancelou o horário que tinha marcado depois do contato.
        var tereza = P("Tereza Gonçalves", "(21) 98765-1110", true);
        Sessoes(tereza, ModalidadeAtendimento.AcupunturaComEletro, 5, 68);
        var cTereza = Caso(tereza, TipoAcompanhamento.Recall, ModalidadeAtendimento.AcupunturaComEletro, 68, bruno, 0);
        Tentativa(cTereza, bruno, 10, "Pronto para agendar");
        db.Add(new Agendamento { PacienteId = tereza.Id, ProfissionalId = helena.Id, DataHora = Dia(-3).AddHours(14), ModalidadePrevista = ModalidadeAtendimento.AcupunturaComEletro, Status = StatusAgendamento.Cancelado, DuracaoMinutos = 30 });

        // Agendado: a sessão marcada pela conversa.
        var otavio = P("Otávio Ferraz", "(21) 98765-1111", true);
        Sessoes(otavio, ModalidadeAtendimento.AcupunturaComEletro, 4, 75);
        var cOtavio = Caso(otavio, TipoAcompanhamento.Recall, ModalidadeAtendimento.AcupunturaComEletro, 75, ana, 0, EtapaAcompanhamento.ProntoParaAgendar);
        Tentativa(cOtavio, ana, 3, "Pronto para agendar");
        db.Add(new Agendamento { PacienteId = otavio.Id, ProfissionalId = helena.Id, DataHora = Dia(4).AddHours(9), ModalidadePrevista = ModalidadeAtendimento.AcupunturaComEletro, Status = StatusAgendamento.Agendado, DuracaoMinutos = 30 });

        // Voltou: a sessão depois do contato, dentro dos 30 dias do funil.
        var vera = P("Vera Lúcia Campos", "(21) 98765-1112", true);
        Sessoes(vera, ModalidadeAtendimento.AcupunturaComEletro, 6, 90);
        var cVera = Caso(vera, TipoAcompanhamento.Recall, ModalidadeAtendimento.AcupunturaComEletro, 90, ana, 0, EtapaAcompanhamento.ProntoParaAgendar);
        Tentativa(cVera, ana, 12, "Pronto para agendar");
        db.Add(new Atendimento { PacienteId = vera.Id, Modalidade = ModalidadeAtendimento.AcupunturaComEletro, Data = hoje.AddDays(-5), RealizadoEm = Dia(-5).AddHours(10) });

        // Encerrado com motivo.
        var gilberto = P("Gilberto Assis", "(21) 98765-1113", true);
        Sessoes(gilberto, ModalidadeAtendimento.Consulta, 1, 95);
        var cGilberto = Caso(gilberto, TipoAcompanhamento.Recall, ModalidadeAtendimento.Consulta, 95, bruno, 0);
        Tentativa(cGilberto, bruno, 8, "Não conformidade — encerrado", "Mudou-se para outro estado.");
        cGilberto.EncerradoEm = Dia(-8); cGilberto.EncerradoPor = bruno.Login; cGilberto.MotivoEncerramento = "Mudou-se para outro estado.";
        db.SaveChanges();
        cGilberto.MotivoId = motivos[2].Id;

        // ===== Novos BSV =====
        var nadia = P("Nádia Rocha", "(21) 98765-1120", true);
        Caso(nadia, TipoAcompanhamento.NovoBsv, ModalidadeAtendimento.BsvApenas, 3, null, 0, profissional: marcos.Id);
        var igor = P("Igor Batista", "(21) 98765-1121", true, Convenio.Amil);
        var cIgor = Caso(igor, TipoAcompanhamento.NovoBsv, ModalidadeAtendimento.BsvApenas, 6, ana, 1, EtapaAcompanhamento.SemResposta, marcos.Id);
        Tentativa(cIgor, ana, 1, "Sem resposta");
        var cecilia = P("Cecília Andrade", null, false);
        Caso(cecilia, TipoAcompanhamento.NovoBsv, ModalidadeAtendimento.BsvApenas, 2, ana, 0, profissional: marcos.Id);

        db.SaveChanges();

        // Só na PRÉVIA a Ana entra; no teste quem entra é a direção do teste (ver o resumo).
        if (System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name == "PreviaBlazor")
            new SessaoUsuario().Entrar(ana);
    }
}
