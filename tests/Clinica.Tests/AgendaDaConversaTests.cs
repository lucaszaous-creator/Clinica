using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// A AGENDA DENTRO DA CONVERSA (nov/2026, aprovada pelo dono): o que a aba afirma — o "o quê" de todas as especialidades,
/// quem atende, as vagas juntas, a mensagem pronta e a ficha que liga sozinha pelo telefone (1, 2+ e 0 fichas). Dados fictícios.
/// </summary>
public sealed class AgendaDaConversaTests
{
    private static readonly DateOnly Hoje = new(2026, 11, 26);   // quinta

    [Theory]
    [InlineData("(22) 99876-5432", "5522998765432")]
    [InlineData("22 9876-5432", "+55 22 99876-5432")]          // ficha antiga sem o 9º dígito
    [InlineData("022 99876-5432", "5522998765432")]
    public void O_mesmo_celular_com_ou_sem_55_e_9o_digito(string ficha, string conversa)
        => ConversasNaTela.MesmoTelefone(ficha, conversa).Should().BeTrue();

    [Fact]
    public void Fixo_nao_ganha_o_9_e_numeros_diferentes_nao_batem()
    {
        ConversasNaTela.MesmoTelefone("22 2765-4321", "22 92765-4321").Should().BeFalse("fixo começa com 2 a 5");
        ConversasNaTela.MesmoTelefone("22 99876-5432", "22 99876-5433").Should().BeFalse();
        WhatsAppDaRecepcaoService.Variantes("22 9876-5432").Should().Contain(["2298765432", "22998765432", "5522998765432"]);
    }

    [Theory]
    [InlineData(null, 1, DecisaoDoVinculo.LigarSozinha)]
    [InlineData(null, 2, DecisaoDoVinculo.Escolher)]
    [InlineData(null, 0, DecisaoDoVinculo.Cadastrar)]
    [InlineData(42, 1, DecisaoDoVinculo.JaLigada)]
    public void A_ficha_liga_sozinha_so_com_uma(int? ficha, int candidatas, DecisaoDoVinculo esperado)
        => AgendaDaConversa.Vinculo(ficha, candidatas).Should().Be(esperado);

    [Fact]
    public void Todas_as_especialidades_da_equipe_aparecem_e_quem_atende_vem_da_habilitacao()
    {
        var helena = new Profissional { Id = 1, Nome = "Dra. Helena Duarte", Ativo = true, HabilitacoesAtendimentoJson = System.Text.Json.JsonSerializer.Serialize(new List<AtendimentoHabilitado> { new("AcupunturaSimples", null) }) };
        var clara = new Profissional { Id = 2, Nome = "Dra. Clara Nogueira", Ativo = true, HabilitacoesAtendimentoJson = System.Text.Json.JsonSerializer.Serialize(new List<AtendimentoHabilitado> { new("Consulta", "Psiquiatria") }) };
        var opcoes = AgendaDaConversa.Opcoes([helena, clara]);
        opcoes.Select(o => o.Rotulo).Should().Contain(["Psiquiatria"]);
        opcoes.Should().HaveCount(2);
        var psiq = opcoes.Single(o => o.EspecialidadeCodigo == "Psiquiatria");
        AgendaDaConversa.QuemAtende(psiq, [helena, clara]).Should().ContainSingle().Which.Id.Should().Be(2);

        var todos = new Profissional { Id = 3, Nome = "Dr. Marcos Vieira", Ativo = true };   // sem habilitação = atende tudo
        AgendaDaConversa.Opcoes([todos]).Should().HaveCount(Clinica.Domain.Regras.CatalogoModalidades.Ativas.Select(m => m.Nome).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            "quem não tem habilitação declarada atende tudo: entram as modalidades ativas do catálogo");
    }

    [Fact]
    public void As_vagas_de_varios_profissionais_viram_uma_lista_pela_hora_com_os_dias_marcados()
    {
        var helena = new Profissional { Id = 1, Nome = "Dra. Helena Duarte", NomeCurto = "Dra. Helena" };
        var tulio = new Profissional { Id = 2, Nome = "Dr. Túlio Lins", NomeCurto = "Dr. Túlio" };
        var d1 = new DateTime(2026, 11, 27, 9, 0, 0);
        var vagas = AgendaDaConversa.Juntar([(helena, [new Vaga(d1.AddHours(1), d1.AddHours(1.5))]), (tulio, [new Vaga(d1, d1.AddMinutes(30)), new Vaga(d1.AddDays(3), d1.AddDays(3).AddMinutes(30))])]);
        vagas.Select(v => v.Profissional).Should().Equal("Dr. Túlio", "Dra. Helena", "Dr. Túlio");
        AgendaDaConversa.DiasComVaga(vagas).Should().BeEquivalentTo([new DateOnly(2026, 11, 27), new DateOnly(2026, 11, 30)]);
        AgendaDaConversa.DoDia(vagas, new DateOnly(2026, 11, 27)).Should().HaveCount(2);
    }

    [Fact]
    public void Enviar_estes_horarios_cola_tres_opcoes_com_quem_atende()
    {
        var v = new[] { new VagaNaConversa(new DateTime(2026, 11, 27, 8, 30, 0), 1, "Dra. Helena"),
                        new VagaNaConversa(new DateTime(2026, 11, 27, 10, 0, 0), 1, "Dra. Helena"),
                        new VagaNaConversa(new DateTime(2026, 11, 30, 14, 0, 0), 1, "Dra. Helena"),
                        new VagaNaConversa(new DateTime(2026, 12, 1, 9, 0, 0), 1, "Dra. Helena") };
        var fixo = AgendaDaConversa.HorariosLivres("Maria das Graças", "Acupuntura", v, Hoje, profissionalFixo: true)!;
        fixo.Should().StartWith("Maria, tenho estes horários de acupuntura com a Dra. Helena: ").And.EndWith(" Qual fica melhor?");
        fixo.Should().Contain(" ou ").And.NotContain("01/12", "só as 3 primeiras");
        AgendaDaConversa.HorariosLivres("Maria", "Acupuntura", v, Hoje, profissionalFixo: false)!.Should().Contain("(Dra. Helena)");
        AgendaDaConversa.HorariosLivres("Maria", "Acupuntura", [], Hoje, true).Should().BeNull();

        // Com "qualquer um", o mesmo horário de três profissionais é UMA opção; e o "(apenas)" do catálogo não vai à paciente.
        var mesmaHora = new[] { new VagaNaConversa(new DateTime(2026, 11, 27, 13, 30, 0), 1, "Bianca"), new VagaNaConversa(new DateTime(2026, 11, 27, 13, 30, 0), 2, "Joana"),
                                new VagaNaConversa(new DateTime(2026, 11, 27, 14, 0, 0), 2, "Joana") };
        AgendaDaConversa.Proximas(mesmaHora).Should().HaveCount(2);
        AgendaDaConversa.HorariosLivres("Maria", "Acupuntura (apenas)", mesmaHora, Hoje, false)!.Should().StartWith("Maria, tenho estes horários de acupuntura: ").And.Contain("(Bianca)").And.NotContain("(Joana) ou");
    }

    [Fact]
    public void As_mensagens_prontas_sao_as_do_prototipo()
    {
        var novo = new HorarioDoPaciente(9, new DateTime(2026, 11, 27, 10, 0, 0), "sex 27/11 10:00", "", "Marcado", "neutro", false, false, false, 1,
            ModalidadeAtendimento.AcupunturaSimples, "Dra. Helena");
        RascunhosDaConversa.Marcacao("Maria das Graças", novo, Hoje).Should().StartWith("Pronto, Maria! Marquei a sua ")
            .And.Contain("com a Dra. Helena").And.EndWith("Qualquer coisa é só chamar.");
        RascunhosDaConversa.Remarcacao("Maria", new DateTime(2026, 11, 26, 14, 0, 0), novo, Hoje).Should().Contain(" passou de ")
            .And.Contain("com a Dra. Helena").And.EndWith("Qualquer coisa é só chamar.");
        RascunhosDaConversa.Cancelamento("Maria", novo, Hoje).Should().StartWith("Maria, desmarquei o seu horário de ")
            .And.EndWith("Quando quiser remarcar, é só me chamar por aqui.");
        RascunhosDaConversa.ComQuem("Dr. Marcos").Should().Be(" com o Dr. Marcos");
        RascunhosDaConversa.ComQuem("Helena").Should().Be(" com Helena");
    }

    // ------------------------------------------------ o calendário do mês (pedido do dono: "todos os dias dos meses")

    [Theory]
    [InlineData(2026, 11)] // começa no domingo, 30 dias
    [InlineData(2026, 8)]  // começa no sábado, 31 dias: as seis semanas inteiras
    [InlineData(2026, 2)]  // fevereiro de 28 dias começando no domingo: cabe em 4, e continua com 6 linhas
    [InlineData(2027, 1)]  // começa na sexta, 31 dias
    public void A_grade_do_mes_tem_sempre_seis_semanas_de_domingo_a_sabado_com_todos_os_dias_do_mes(int ano, int mes)
    {
        var m = new DateOnly(ano, mes, 1);
        var grade = AgendaDaConversa.GradeDoMes(m.AddDays(9)); // qualquer dia do mês dá a mesma grade

        grade.Should().HaveCount(42);
        grade[0].DayOfWeek.Should().Be(DayOfWeek.Sunday);
        grade[^1].DayOfWeek.Should().Be(DayOfWeek.Saturday);
        grade.Should().OnlyHaveUniqueItems().And.BeInAscendingOrder();
        grade.Where(d => d.Month == mes).Should().HaveCount(DateTime.DaysInMonth(ano, mes))
            .And.Contain(m).And.Contain(new DateOnly(ano, mes, DateTime.DaysInMonth(ano, mes)));
        (grade[1].DayNumber - grade[0].DayNumber).Should().Be(1);
    }

    [Fact]
    public void Os_meses_do_seletor_sao_o_atual_e_os_seis_seguintes_virando_o_ano()
    {
        var meses = AgendaDaConversa.MesesParaEscolher(new DateOnly(2026, 11, 26));
        meses.Should().HaveCount(7);
        meses[0].Should().Be(new DateOnly(2026, 11, 1));
        meses[^1].Should().Be(new DateOnly(2027, 5, 1));
    }

    [Fact]
    public void A_leitura_do_mes_parte_de_agora_no_mes_atual_e_do_dia_1_num_mes_futuro()
    {
        var agora = new DateTime(2026, 11, 26, 10, 12, 0);
        AgendaDaConversa.InicioDaLeitura(new DateOnly(2026, 11, 1), agora).Should().Be(agora);
        AgendaDaConversa.InicioDaLeitura(new DateOnly(2027, 1, 15), agora).Should().Be(new DateTime(2027, 1, 1));
    }

    [Fact]
    public void So_as_vagas_do_mes_entram_no_calendario()
    {
        var vagas = new[]
        {
            new VagaNaConversa(new DateTime(2026, 11, 30, 17, 0, 0), 1, "Dra. Helena"),
            new VagaNaConversa(new DateTime(2026, 12, 1, 8, 0, 0), 1, "Dra. Helena"),
        };
        AgendaDaConversa.DoMes(vagas, new DateOnly(2026, 12, 1)).Should().ContainSingle().Which.Inicio.Day.Should().Be(1);
    }

    [Fact]
    public void Com_qualquer_um_o_dia_tem_um_botao_por_horario_e_com_profissional_todos()
    {
        var dia = new DateOnly(2026, 11, 27);
        var vagas = new[]
        {
            new VagaNaConversa(new DateTime(2026, 11, 27, 14, 0, 0), 1, "Bianca"),
            new VagaNaConversa(new DateTime(2026, 11, 27, 14, 0, 0), 2, "Dr. Túlio Lins"),
            new VagaNaConversa(new DateTime(2026, 11, 27, 14, 30, 0), 2, "Dr. Túlio Lins"),
            new VagaNaConversa(new DateTime(2026, 11, 28, 8, 0, 0), 1, "Bianca"),
        };
        AgendaDaConversa.DoDia(vagas, dia, umPorHorario: true).Select(v => v.Inicio.Hour * 100 + v.Inicio.Minute).Should().Equal(1400, 1430);
        AgendaDaConversa.DoDia(vagas, dia).Should().HaveCount(3);
    }

    [Theory]
    [InlineData("Dra. Helena Duarte", "Dra. Helena")]
    [InlineData("Dr. Túlio Lins", "Dr. Túlio")]
    [InlineData("Enf. Abigail", "Enf. Abigail")]
    [InlineData("Bianca Souza", "Bianca")]
    [InlineData("Joana", "Joana")]
    [InlineData("  ", "")]
    public void O_botao_do_horario_leva_o_titulo_e_o_primeiro_nome(string nome, string curto)
        => AgendaDaConversa.NomeCurto(nome).Should().Be(curto);

    // ------------------------------------------------ a reserva sem ficha (textos do protótipo aprovado pelo dono)

    [Fact]
    public void A_reserva_diz_o_que_fez_e_pede_o_cadastro_no_balcao_sem_documento_pelo_whatsapp()
    {
        var inicio = new DateTime(2026, 11, 27, 14, 0, 0);
        var particular = RascunhosDaConversa.Reserva("Fernanda Lopes", "Acupuntura (apenas)", inicio, "Dra. Helena", null, Hoje);
        particular.Should().StartWith("Pronto, Fernanda! Reservei acupuntura para ")
            .And.Contain("14:00 com a Dra. Helena. No dia, chegue 10 minutos antes para fazermos o seu cadastro. Qualquer coisa é só chamar.")
            .And.NotContain("CPF").And.NotContain("carteirinha");
        RascunhosDaConversa.Reserva("Fernanda", "Acupuntura", inicio, "Dra. Helena", "Unimed", Hoje)
            .Should().EndWith("para fazermos o seu cadastro, e traga a carteirinha da Unimed. Qualquer coisa é só chamar.");
        RascunhosDaConversa.Reserva(null, "Acupuntura", inicio, null, null, Hoje).Should().StartWith("Pronto! Reservei acupuntura para ");
    }

    [Fact]
    public void Remarcar_e_desmarcar_a_reserva_dizem_o_horario()
    {
        var antes = new DateTime(2026, 11, 27, 14, 0, 0);
        var novo = new DateTime(2026, 12, 1, 9, 30, 0);
        RascunhosDaConversa.RemarcacaoDaReserva("Fernanda", "Acupuntura", antes, novo, "Dra. Helena", Hoje)
            .Should().StartWith("Pronto, Fernanda! A sua reserva de acupuntura passou de ").And.Contain("09:30 com a Dra. Helena. Qualquer coisa é só chamar.");
        RascunhosDaConversa.CancelamentoDaReserva("Fernanda", antes, Hoje)
            .Should().StartWith("Fernanda, cancelei a sua reserva de ").And.EndWith("14:00. Quando quiser outro horário, é só me chamar por aqui.");
        RascunhosDaConversa.CancelamentoDaReserva(null, antes, Hoje).Should().StartWith("Olá! Cancelei a sua reserva de ");
    }
}
