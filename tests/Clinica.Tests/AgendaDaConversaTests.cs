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
}
