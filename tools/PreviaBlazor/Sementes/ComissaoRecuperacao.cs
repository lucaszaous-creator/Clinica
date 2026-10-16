// Dados FICTÍCIOS da prévia — nenhuma pessoa real.
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;

namespace PreviaBlazor.Sementes;

/// <summary>
/// O que a tela "Comissão de recuperação" (Gerente) precisa além da base: duas atendentes,
/// pacientes que sumiram, foram ASSUMIDOS no Acompanhamento e voltaram neste mês — um com guia,
/// um particular, um sem contato registrado (só assumiu), um BSV —, uma recuperação já validada,
/// uma recusada, e os valores (geral e um específico de BSV). Pacientes PRÓPRIOS, para não
/// disputar a chave única (paciente + fila + modalidade) com a semente do Acompanhamento.
/// </summary>
public sealed class SementeComissaoRecuperacao : ISemente
{
    public int Ordem => 70;

    public void Semear(ClinicaDbContext db)
    {
        var profissional = db.Set<Profissional>().OrderBy(p => p.Id).FirstOrDefault();
        if (profissional is null) return;
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var inicioMes = new DateOnly(hoje.Year, hoje.Month, 1);
        var agora = DateTime.Now;

        var ana = new UsuarioSistema { Nome = "Ana Paula (recepção)", Login = "ana.recepcao", Perfil = PerfilAcesso.Recepcao };
        var bruno = new UsuarioSistema { Nome = "Bruno Lima (faturamento)", Login = "bruno.faturamento", Perfil = PerfilAcesso.Faturista };
        db.AddRange(ana, bruno);
        db.Add(new RegraComissaoRecuperacao { Valor = 30m, VigenteDe = inicioMes.AddMonths(-6), CriadaEm = agora, CriadaPor = "direcao",
            Observacao = "Valor combinado em reunião de equipe." });
        db.Add(new RegraComissaoRecuperacao { Valor = 50m, Tipo = TipoAcompanhamento.NovoBsv, VigenteDe = inicioMes.AddMonths(-6), CriadaEm = agora, CriadaPor = "direcao" });
        db.Add(new RegraComissaoRecuperacao { Valor = 25m, VigenteDe = inicioMes.AddMonths(-12), VigenteAte = inicioMes.AddMonths(-6).AddDays(-1),
            Ativa = false, CriadaEm = agora, CriadaPor = "direcao" });
        db.SaveChanges();

        // (nome, fila, modalidade, quem assumiu, contatos registrados, gera guia, dias desde o começo do mês)
        var casos = new (string Nome, TipoAcompanhamento Tipo, ModalidadeAtendimento Modalidade, UsuarioSistema Dono, int Contatos, bool Guia, int Dia)[]
        {
            ("Rosana Teixeira (fictícia)", TipoAcompanhamento.Recall, ModalidadeAtendimento.AcupunturaComEletro, ana, 2, true, 1),
            ("Carlos Eduardo Ramos (fictício)", TipoAcompanhamento.Recall, ModalidadeAtendimento.AcupunturaComEletro, ana, 0, true, 2),
            ("Lúcia Prado (fictícia)", TipoAcompanhamento.Recall, ModalidadeAtendimento.Consulta, bruno, 1, false, 3),
            ("Paulo Nunes (fictício)", TipoAcompanhamento.NovoBsv, ModalidadeAtendimento.BsvApenas, bruno, 3, true, 4),
            ("Helena Martins (fictícia)", TipoAcompanhamento.Recall, ModalidadeAtendimento.AcupunturaComEletro, bruno, 1, true, 5),
        };
        var dia = 0;
        var sessoes = new List<Atendimento>();
        foreach (var c in casos)
        {
            var paciente = new Paciente { Nome = c.Nome, Telefone = "2199" + (1000000 + dia * 7919).ToString(), Convenio = Convenio.UnimedPadrao, Sexo = Sexo.Feminino };
            db.Add(paciente);
            db.SaveChanges();
            var retorno = inicioMes.AddDays(Math.Min(c.Dia, Math.Max(0, hoje.DayNumber - inicioMes.DayNumber)));
            var abertura = retorno.AddDays(-12).ToDateTime(new TimeOnly(9, 0));
            var caso = new AcompanhamentoPaciente { PacienteId = paciente.Id, Tipo = c.Tipo, Modalidade = c.Modalidade,
                ReferenciaEm = retorno.AddDays(-80).ToDateTime(TimeOnly.MinValue), CriadoEm = abertura, CriadoPor = "direcao",
                ProfissionalId = profissional.Id, ResponsavelId = c.Dono.Id, ProximoContato = retorno };
            caso.Contatos.Add(new() { Idempotencia = Guid.NewGuid(), Em = abertura, Operador = "direcao",
                Resultado = c.Tipo == TipoAcompanhamento.NovoBsv ? "Indicação de novo paciente BSV" : "Novo ciclo de recall" });
            caso.Contatos.Add(new() { Idempotencia = Guid.NewGuid(), Em = abertura.AddHours(1), Operador = c.Dono.Login,
                Resultado = "Assumiu o acompanhamento", ResponsavelId = c.Dono.Id });
            for (var i = 0; i < c.Contatos; i++)
                caso.Contatos.Add(new() { Idempotencia = Guid.NewGuid(), Em = abertura.AddDays(2 + i * 2), Operador = c.Dono.Login,
                    Canal = CanalContato.WhatsApp, Resultado = i == c.Contatos - 1 ? "Pronto para agendar" : "Sem resposta", ResponsavelId = c.Dono.Id });
            db.Add(caso);
            var sessao = new Atendimento { PacienteId = paciente.Id, Modalidade = c.Modalidade, Data = retorno,
                RealizadoEm = retorno.ToDateTime(new TimeOnly(10, 0)), LancadoPor = c.Dono.Login, LancadoEm = retorno.ToDateTime(new TimeOnly(9, 50)) };
            db.Add(sessao);
            db.SaveChanges();
            db.Add(new Agendamento { PacienteId = paciente.Id, ProfissionalId = profissional.Id, DataHora = retorno.ToDateTime(new TimeOnly(10, 0)),
                ModalidadePrevista = c.Modalidade, Status = StatusAgendamento.Realizado, AtendimentoId = sessao.Id });
            db.Add(new CodigoFaturamento { AtendimentoId = sessao.Id, Tipo = TipoCodigo.Consulta, DataPrevistaFaturamento = retorno,
                Status = c.Guia ? StatusCodigo.Aberto : StatusCodigo.NaoAplicavel, Descricao = c.Guia ? "Guia da sessão" : "Particular — sem guia" });
            sessoes.Add(sessao);
            dia++;
        }
        db.SaveChanges();

        // Uma já validada e uma recusada — para a tela mostrar os quatro estados.
        var casosGravados = db.Set<AcompanhamentoPaciente>().Where(a => a.CriadoPor == "direcao").OrderBy(a => a.Id).ToList();
        AcompanhamentoPaciente CasoDe(Atendimento s) => casosGravados.First(a => a.PacienteId == s.PacienteId);
        db.Add(new ComissaoRecuperacao { AcompanhamentoPacienteId = CasoDe(sessoes[3]).Id, AtendimentoId = sessoes[3].Id, PacienteId = sessoes[3].PacienteId,
            UsuarioId = bruno.Id, AssumidoEm = CasoDe(sessoes[3]).CriadoEm.AddHours(1), RetornoEm = sessoes[3].Data, Decisao = DecisaoComissaoRecuperacao.Validada,
            Valor = 50m, RegraDescricao = "R$ 50,00 por paciente — novos BSV, todas as modalidades", DecididaEm = agora, DecididaPor = "direcao" });
        db.Add(new ComissaoRecuperacao { AcompanhamentoPacienteId = CasoDe(sessoes[4]).Id, AtendimentoId = sessoes[4].Id, PacienteId = sessoes[4].PacienteId,
            UsuarioId = bruno.Id, AssumidoEm = CasoDe(sessoes[4]).CriadoEm.AddHours(1), RetornoEm = sessoes[4].Data, Decisao = DecisaoComissaoRecuperacao.Recusada,
            Valor = 0m, Observacao = "Voltou por indicação do médico, antes de qualquer contato.", DecididaEm = agora, DecididaPor = "direcao" });
        db.SaveChanges();
    }
}
