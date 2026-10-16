using System.Text.Json;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O que a tela de Profissionais e salas afirma (out/2026, migração do EquipeViewModel e das
/// janelas de profissional, sala, jornada e bloqueio para Blazor). As decisões que moravam nos
/// ViewModels do WPF subiram para <see cref="TelaDaEquipe"/> e são conferidas aqui.
/// </summary>
public class TelaDaEquipeTests
{
    private static readonly EntradaModalidade Acupuntura = new("AcupunturaComEletro", "Acupuntura com eletro", ModalidadeAtendimento.AcupunturaComEletro, true);
    private static readonly EntradaModalidade Consulta = new("Consulta", "Consulta", ModalidadeAtendimento.Consulta, true);
    private static readonly EntradaModalidade Inativa = new("Velha", "Modalidade velha", ModalidadeAtendimento.AcupunturaComEletro, false);
    private static readonly EntradaEspecialidade Psiquiatria = new("Psiquiatria", "Psiquiatria", true);
    private static readonly EntradaEspecialidade Psicologia = new("Psicologia", "Psicologia", true);

    private static IReadOnlyList<OpcaoDeAtendimento> Opcoes()
        => TelaDaEquipe.OpcoesDeAtendimento([Acupuntura, Consulta, Inativa], [Psiquiatria, Psicologia]);

    [Fact]
    public void Consulta_vira_uma_linha_por_especialidade_e_modalidade_inativa_fica_de_fora()
    {
        var opcoes = Opcoes();
        opcoes.Select(o => o.Rotulo).Should().Equal(
            "Acupuntura com eletro", "Consulta · Psicologia", "Consulta · Psiquiatria");
        opcoes[0].EspecialidadeCodigo.Should().BeNull();
        opcoes.Where(o => o.EhConsulta).Should().OnlyContain(o => o.EspecialidadeCodigo != null);
    }

    [Fact]
    public void Psicologia_como_principal_so_permite_consulta_de_psicologia()
    {
        var opcoes = Opcoes();
        opcoes.Where(o => TelaDaEquipe.Permitida(o, "Psicologia")).Select(o => o.Rotulo)
            .Should().Equal("Consulta · Psicologia");
        opcoes.Should().OnlyContain(o => TelaDaEquipe.Permitida(o, "Psiquiatria"));
    }

    [Fact]
    public void Sem_configuracao_tudo_o_que_e_permitido_abre_marcado()
    {
        var opcoes = Opcoes();
        opcoes.Should().OnlyContain(o => TelaDaEquipe.MarcadaNaAbertura(o, null, null));
        var atuais = new List<AtendimentoHabilitado> { new("Consulta", "Psiquiatria") };
        opcoes.Where(o => TelaDaEquipe.MarcadaNaAbertura(o, atuais, null)).Select(o => o.Rotulo)
            .Should().Equal("Consulta · Psiquiatria");
    }

    [Fact]
    public void Quem_nao_e_gerente_nunca_grava_habilitacoes()
        => TelaDaEquipe.HabilitacoesParaGravar(false, true, null, Opcoes()).Should().BeNull();

    [Fact]
    public void Abrir_e_salvar_sem_mexer_preserva_o_sem_restricao()
    {
        // O gerente corrige o telefone de quem nunca teve restrição: nulo PRESERVA, e a
        // modalidade cadastrada amanhã continua alcançando o profissional.
        TelaDaEquipe.HabilitacoesParaGravar(true, false, null, Opcoes()).Should().BeNull();
    }

    [Fact]
    public void Gerente_que_mexeu_grava_a_lista_marcada_e_lista_vazia_e_decisao()
    {
        var json = TelaDaEquipe.HabilitacoesParaGravar(true, true, null, Opcoes().Take(1));
        JsonSerializer.Deserialize<List<AtendimentoHabilitado>>(json!)
            .Should().Equal(new AtendimentoHabilitado("AcupunturaComEletro", null));

        var vazio = TelaDaEquipe.HabilitacoesParaGravar(true, true, [], []);
        vazio.Should().Be("[]");
    }

    [Fact]
    public void Configuracao_existente_e_regravada_mesmo_sem_mudanca()
    {
        var originais = new List<AtendimentoHabilitado> { new("Consulta", "Psiquiatria") };
        TelaDaEquipe.HabilitacoesParaGravar(true, false, originais, Opcoes().Where(o => o.Casa(originais[0])))
            .Should().NotBeNull();
    }

    [Fact]
    public void Habilitacoes_dizem_a_restricao_e_avisam_quem_nao_pode_ser_marcado()
    {
        TelaDaEquipe.Habilitacoes(new Profissional()).Should().Be("todas as modalidades (sem restrição)");
        var semNada = new Profissional { HabilitacoesAtendimentoJson = "[]" };
        TelaDaEquipe.Habilitacoes(semNada).Should().Be("nenhum atendimento — não pode ser marcado");
        TelaDaEquipe.SemNadaHabilitado(semNada).Should().BeTrue();
        TelaDaEquipe.Habilitacoes(new Profissional { EspecialidadeCodigo = "Psicologia" })
            .Should().Be("só consulta de Psicologia");
        TelaDaEquipe.Habilitacoes(new Profissional
        {
            HabilitacoesAtendimentoJson = JsonSerializer.Serialize(new[] { new AtendimentoHabilitado("Consulta", "Psiquiatria"), new AtendimentoHabilitado("AcupunturaComEletro", null) })
        }).Should().Be("2 atendimentos habilitados");
    }

    [Fact]
    public void Jornada_em_branco_e_qualquer_dia_e_declarada_usa_a_frase_do_dominio()
    {
        TelaDaEquipe.Jornada(new Profissional()).Should().Be("qualquer dia e horário");
        var p = new Profissional
        {
            DiasDeAtendimento = Profissional.BitDe(DayOfWeek.Monday) | Profissional.BitDe(DayOfWeek.Wednesday),
            AtendeDas = new TimeOnly(8, 0), AtendeAte = new TimeOnly(12, 0)
        };
        TelaDaEquipe.Jornada(p).Should().Be(p.DescricaoJornada).And.Be("seg · qua, das 08:00 às 12:00");
        TelaDaEquipe.DiasDeclarados(p).Should().Equal(DayOfWeek.Monday, DayOfWeek.Wednesday);
        TelaDaEquipe.DiasDeclarados(new Profissional()).Should().BeEmpty();
    }

    [Fact]
    public void Ler_jornada_aceita_os_dois_vazios_e_recusa_meio_horario_e_fim_antes_do_inicio()
    {
        TelaDaEquipe.LerJornada([], "", " ").Should().Be((null, null, null, null));

        var ok = TelaDaEquipe.LerJornada([DayOfWeek.Tuesday, DayOfWeek.Tuesday], "08:00", "18:00");
        ok.Erro.Should().BeNull();
        ok.Dias.Should().Be(Profissional.BitDe(DayOfWeek.Tuesday));
        ok.Das.Should().Be(new TimeOnly(8, 0));

        TelaDaEquipe.LerJornada([], "08:00", "").Erro.Should().Contain("início E fim");
        TelaDaEquipe.LerJornada([], "8h", "18:00").Erro.Should().Contain("início E fim");
        TelaDaEquipe.LerJornada([], "18:00", "08:00").Erro.Should().Contain("depois do início");
        // O campo de hora de alguns navegadores devolve segundos.
        TelaDaEquipe.LerJornada([], "08:00:00", "12:30:00").Erro.Should().BeNull();
    }

    [Fact]
    public void Duracao_vazia_diz_qual_e_o_padrao_da_clinica()
    {
        TelaDaEquipe.Duracao(new Profissional()).Should().Be($"padrão da clínica ({Agendamento.DuracaoPadraoMinutos} min)");
        TelaDaEquipe.Duracao(new Profissional { DuracaoPadraoMinutos = 50 }).Should().Be("50 min por sessão");
    }

    [Fact]
    public void Cor_da_agenda_so_reconhece_RRGGBB()
    {
        TelaDaEquipe.CorDaAgenda("#0f766e").Should().Be("#0F766E");
        TelaDaEquipe.CorDaAgenda("0F766E").Should().Be("#0F766E");
        TelaDaEquipe.CorDaAgenda("verde").Should().BeNull();
        TelaDaEquipe.CorDaAgenda(" ").Should().BeNull();
    }

    [Fact]
    public void Iniciais_pulam_o_tratamento()
    {
        TelaDaEquipe.Iniciais("Dra. Helena Duarte").Should().Be("HD");
        TelaDaEquipe.Iniciais("Bianca").Should().Be("B");
        TelaDaEquipe.Iniciais("  ").Should().Be("?");
    }

    [Fact]
    public void Busca_ignora_acento_e_caixa_e_alcanca_o_registro()
    {
        var p = new Profissional { Nome = "Dra. Helena Duarte", RegistroConselho = "CRM-RJ 52.1234" };
        TelaDaEquipe.CasaBusca(p, "helena").Should().BeTrue();
        TelaDaEquipe.CasaBusca(p, "crm-rj").Should().BeTrue();
        TelaDaEquipe.CasaBusca(new Profissional { Nome = "Mônica" }, "monica").Should().BeTrue();
        TelaDaEquipe.CasaBusca(p, "psiquiatria", "Psiquiatria").Should().BeTrue();
        TelaDaEquipe.CasaBusca(p, "marcos").Should().BeFalse();
    }

    [Fact]
    public void Desativar_explica_que_nada_some_da_agenda()
    {
        TelaDaEquipe.EfeitoDaSituacao(false, false).Should().Contain("continuam na agenda").And.Contain("preservado");
        TelaDaEquipe.EfeitoDaSituacao(false, true).Should().Contain("continuam na agenda");
        TelaDaEquipe.EfeitoDaSituacao(true, false).Should().StartWith("Ativo");
    }

    [Fact]
    public void Bloqueio_diz_de_quem_e_quando_e_o_que_ficou_marcado()
    {
        var feriado = new BloqueioAgenda { Inicio = new DateTime(2026, 11, 2), Fim = new DateTime(2026, 11, 2, 23, 59, 0), Motivo = "Finados" };
        TelaDaEquipe.AlvoDoBloqueio(feriado).Should().Be("Clínica inteira");
        TelaDaEquipe.PeriodoDoBloqueio(feriado).Should().Be("02/11/2026 · dia inteiro");

        var tarde = new BloqueioAgenda { ProfissionalId = 3, Profissional = new Profissional { Nome = "Helena Duarte", NomeCurto = "Dra. Helena" },
            Inicio = new DateTime(2026, 11, 3, 13, 0, 0), Fim = new DateTime(2026, 11, 3, 18, 0, 0) };
        TelaDaEquipe.AlvoDoBloqueio(tarde).Should().Be("Dra. Helena");
        TelaDaEquipe.PeriodoDoBloqueio(tarde).Should().Be("03/11/2026 · 13:00 às 18:00");

        var ferias = new BloqueioAgenda { SalaId = 2, Sala = new Sala { Nome = "Ipê" },
            Inicio = new DateTime(2026, 12, 20, 8, 0, 0), Fim = new DateTime(2027, 1, 5, 18, 0, 0) };
        TelaDaEquipe.AlvoDoBloqueio(ferias).Should().Be("Sala Ipê");
        TelaDaEquipe.PeriodoDoBloqueio(ferias).Should().Be("20/12/2026 08:00 a 05/01/2027 18:00");

        TelaDaEquipe.SituacaoDoBloqueio(0).Should().Be("nada marcado dentro");
        TelaDaEquipe.SituacaoDoBloqueio(3).Should().Be("3 sessões marcadas dentro — remarque");
    }

    [Fact]
    public void Deslocamento_aceita_negativo_e_recusa_zero_e_texto()
    {
        TelaDaEquipe.LerDeslocamento("7", out var d).Should().BeTrue();
        d.Should().Be(7);
        TelaDaEquipe.LerDeslocamento("-2", out d).Should().BeTrue();
        TelaDaEquipe.LerDeslocamento("0", out _).Should().BeFalse();
        TelaDaEquipe.LerDeslocamento("uma semana", out _).Should().BeFalse();
    }

    [Fact]
    public void Painel_conta_so_ativos_e_soma_as_vagas_das_salas_ativas()
    {
        var cpfValido = "52998224725";
        var profissionais = new List<Profissional>
        {
            new() { Ativo = true, Cpf = cpfValido, AtendeDas = new TimeOnly(8, 0), AtendeAte = new TimeOnly(12, 0) },
            new() { Ativo = true },
            new() { Ativo = false, Cpf = cpfValido }
        };
        var salas = new List<Sala> { new() { Ativa = true, Capacidade = 3 }, new() { Ativa = true, Capacidade = 1 }, new() { Ativa = false, Capacidade = 5 } };
        var p = TelaDaEquipe.Painel(profissionais, salas, [0, 2, 1]);
        p.Should().Be(new PainelDaEquipe(Ativos: 2, Inativos: 1, ComJornada: 1, ComCpf: 1,
            SalasAtivas: 2, SalasInativas: 1, VagasSimultaneas: 4, AgendaFechada: 3, SessoesARemarcar: 3));
    }

    [Fact]
    public void Especialidade_desativada_continua_no_seletor_para_nao_ser_apagada()
    {
        var ativas = new[] { Psiquiatria, new EntradaEspecialidade("Geriatria", "Geriatria", false) };
        TelaDaEquipe.EspecialidadesParaEscolha(ativas, "Psiquiatria").Select(e => e.Codigo).Should().Equal("Psiquiatria");
        TelaDaEquipe.EspecialidadesParaEscolha(ativas, "Geriatria", "Geriatria")
            .Should().ContainSingle(e => e.Codigo == "Geriatria" && e.Nome == "Geriatria (inativa no catálogo)");
        TelaDaEquipe.EspecialidadesParaEscolha(ativas, null).Should().HaveCount(1);
    }

    [Fact]
    public void Capacidade_vira_frase()
    {
        TelaDaEquipe.Capacidade(1).Should().Be("1 atendimento por vez");
        TelaDaEquipe.Capacidade(3).Should().Be("3 atendimentos ao mesmo tempo");
    }
}
