using Clinica.Application.Modelos;
using Clinica.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O que a TELA DO PACIENTE afirma (out/2026, P0 da migração para Blazor): o rail, quem vê
/// qual seção, a seção que abre, a origem, o "voltar" e a situação da sessão. As regras
/// saíram da ViewModel WPF para a Application — o WPF e a tela Blazor leem as mesmas, e
/// aqui o <c>dotnet test</c> as alcança.
/// </summary>
public class TelaDoPacienteNaTelaTests
{
    private static int Indice(string secao) => TelaDoPacienteNaTela.IndiceDe(secao);

    private static bool Oculta(IReadOnlyList<TelaDoPacienteNaTela.ItemDoRail> rail, string secao)
        => rail[Indice(secao)].Oculta;

    [Fact]
    public void O_rail_tem_as_sete_secoes_na_ordem_com_os_grupos_contiguos()
    {
        var rail = TelaDoPacienteNaTela.Rail();

        rail.Select(s => s.Rotulo).Should().Equal(
            "Atendimento", "Atendimento de enfermagem", "Ficha do paciente", "Histórico",
            "Exames e anexos", "Prescrições e documentos", "Acompanhamento");
        rail.Select(s => s.Grupo).Should().Equal(
            "Sessão", "Sessão", "Paciente", "Paciente", "Paciente", "Paciente", "Paciente");
    }

    [Fact]
    public void O_indice_sai_do_nome_e_nome_desconhecido_cai_no_atendimento()
    {
        Indice(TelaDoPacienteNaTela.Historico).Should().Be(3);
        Indice(TelaDoPacienteNaTela.Acompanhamento).Should().Be(6);
        Indice("Seção que não existe").Should().Be(0);
    }

    [Fact]
    public void Quem_consulta_nao_ve_a_secao_de_escrita_da_enfermagem()
    {
        var rail = TelaDoPacienteNaTela.RailPara(
            PerfisAcesso.Padrao(PerfilAcesso.Profissional), PerfilAcesso.Profissional);

        Oculta(rail, TelaDoPacienteNaTela.Atendimento).Should().BeFalse();
        Oculta(rail, TelaDoPacienteNaTela.AtendimentoDeEnfermagem).Should().BeTrue();
        Oculta(rail, TelaDoPacienteNaTela.Historico).Should().BeFalse();
    }

    [Fact]
    public void A_enfermagem_nao_ve_o_SOAP_e_ve_a_passagem()
    {
        var rail = TelaDoPacienteNaTela.RailPara(
            PerfisAcesso.Padrao(PerfilAcesso.Enfermagem), PerfilAcesso.Enfermagem);

        Oculta(rail, TelaDoPacienteNaTela.Atendimento).Should().BeTrue();
        Oculta(rail, TelaDoPacienteNaTela.AtendimentoDeEnfermagem).Should().BeFalse();
    }

    [Fact]
    public void Sem_VerProntuario_so_a_ficha_fica_a_vista()
    {
        var semProntuario = PerfisAcesso.Padrao(PerfilAcesso.Profissional) & ~Permissao.VerProntuario;

        var rail = TelaDoPacienteNaTela.RailPara(semProntuario, PerfilAcesso.Profissional);

        rail.Where(i => !i.Oculta).Select(i => i.Rotulo)
            .Should().Equal(TelaDoPacienteNaTela.FichaDoPaciente);
    }

    [Fact]
    public void Esconder_nao_tira_da_lista_o_indice_continua_sendo_a_regua()
    {
        var rail = TelaDoPacienteNaTela.RailPara(
            PerfisAcesso.Padrao(PerfilAcesso.Enfermagem), PerfilAcesso.Enfermagem);

        rail.Should().HaveCount(TelaDoPacienteNaTela.SecoesDoPaciente.Count);
    }

    [Fact]
    public void Ficha_e_prescricoes_nao_ocupam_linha_no_menu()
    {
        var rail = TelaDoPacienteNaTela.RailPara(PerfisAcesso.Todas, PerfilAcesso.Gerente);

        rail[Indice(TelaDoPacienteNaTela.FichaDoPaciente)].OcultaNoMenu.Should().BeTrue();
        rail[Indice(TelaDoPacienteNaTela.PrescricoesEDocumentos)].OcultaNoMenu.Should().BeTrue();
        rail[Indice(TelaDoPacienteNaTela.Atendimento)].RotuloMenu.Should().Be("Evolução da sessão");
        rail[Indice(TelaDoPacienteNaTela.Historico)].RotuloMenu.Should().Be("Histórico clínico");
    }

    [Fact]
    public void A_passagem_pedida_por_quem_nao_a_ve_abre_no_atendimento()
    {
        var rail = TelaDoPacienteNaTela.RailPara(
            PerfisAcesso.Padrao(PerfilAcesso.Profissional), PerfilAcesso.Profissional);

        TelaDoPacienteNaTela.SecaoInicial(Indice(TelaDoPacienteNaTela.AtendimentoDeEnfermagem), rail)
            .Should().Be(Indice(TelaDoPacienteNaTela.Atendimento));
    }

    [Fact]
    public void Secao_escondida_por_falta_de_VerProntuario_abre_na_ficha()
    {
        var semProntuario = PerfisAcesso.Padrao(PerfilAcesso.Profissional) & ~Permissao.VerProntuario;
        var rail = TelaDoPacienteNaTela.RailPara(semProntuario, PerfilAcesso.Profissional);

        TelaDoPacienteNaTela.SecaoInicial(Indice(TelaDoPacienteNaTela.Historico), rail)
            .Should().Be(Indice(TelaDoPacienteNaTela.FichaDoPaciente));
    }

    [Fact]
    public void Secao_a_vista_e_indice_fora_da_faixa()
    {
        var rail = TelaDoPacienteNaTela.RailPara(
            PerfisAcesso.Padrao(PerfilAcesso.Profissional), PerfilAcesso.Profissional);

        TelaDoPacienteNaTela.SecaoInicial(Indice(TelaDoPacienteNaTela.Historico), rail)
            .Should().Be(Indice(TelaDoPacienteNaTela.Historico));
        TelaDoPacienteNaTela.SecaoInicial(99, rail).Should().Be(0);
    }

    [Fact]
    public void A_origem_diz_a_data_quando_nao_e_hoje()
    {
        var hoje = new DateOnly(2026, 10, 19);

        TelaDoPacienteNaTela.DescreverOrigem(null, null, hoje)
            .Should().StartWith("Escolhido na busca");
        TelaDoPacienteNaTela.DescreverOrigem(7, hoje, hoje)
            .Should().StartWith("Chamado da agenda de hoje");
        TelaDoPacienteNaTela.DescreverOrigem(7, new DateOnly(2026, 10, 14), hoje)
            .Should().StartWith("Chamado da agenda de 14/10/2026");
    }

    [Fact]
    public void O_voltar_leva_ao_meu_dia_so_quando_ha_horario_e_a_pessoa_o_alcanca()
    {
        TelaDoPacienteNaTela.VoltaParaMeuDia(7, alcancaMeuDia: true).Should().BeTrue();
        TelaDoPacienteNaTela.VoltaParaMeuDia(7, alcancaMeuDia: false).Should().BeFalse();
        TelaDoPacienteNaTela.VoltaParaMeuDia(null, alcancaMeuDia: true).Should().BeFalse();
        TelaDoPacienteNaTela.RotuloVoltar(true).Should().Be("Meu dia");
        TelaDoPacienteNaTela.RotuloVoltar(false).Should().Be("Pacientes");
    }

    // ---- situação da sessão ----

    private static readonly DateTime Agora = new(2026, 10, 19, 14, 30, 0);

    private static Agendamento Horario(StatusAgendamento status = StatusAgendamento.Agendado,
        DateTime? inicio = null, DateTime? fim = null, int? atendimentoId = null)
        => new()
        {
            Id = 1, PacienteId = 1, DataHora = new DateTime(2026, 10, 19, 14, 0, 0),
            Status = status, InicioAtendimentoEm = inicio, FimAtendimentoEm = fim,
            AtendimentoId = atendimentoId
        };

    [Fact]
    public void Sem_horario_nao_ha_sessao_nem_cronometro()
    {
        var s = SessaoDoAtendimentoNaTela.De(null, Agora);

        s.TemSessao.Should().BeFalse();
        s.Cronometro.Should().BeNull();
    }

    [Fact]
    public void Horario_que_nao_comecou_pode_iniciar_e_diz_a_hora()
    {
        var s = SessaoDoAtendimentoNaTela.De(Horario(), Agora);

        s.PodeIniciar.Should().BeTrue();
        s.EmAtendimento.Should().BeFalse();
        s.Situacao.Should().Be("Horário de 14:00 — o paciente ainda não entrou na sala");
        s.Cronometro.Should().BeNull();
    }

    [Fact]
    public void Em_atendimento_a_pilula_nao_repete_o_tempo_e_o_cronometro_corre()
    {
        var s = SessaoDoAtendimentoNaTela.De(Horario(inicio: Agora.AddMinutes(-12)), Agora);

        s.EmAtendimento.Should().BeTrue();
        s.Situacao.Should().Be("Em atendimento");
        s.Cronometro.Should().Be("00:12:00");
    }

    [Fact]
    public void Encerrado_e_nao_concluido_pode_reabrir()
    {
        var s = SessaoDoAtendimentoNaTela.De(
            Horario(inicio: Agora.AddMinutes(-30), fim: Agora.AddMinutes(-6)), Agora);

        s.Encerrada.Should().BeTrue();
        s.Concluida.Should().BeFalse();
        s.PodeReabrir.Should().BeTrue();
        s.Situacao.Should().StartWith("Conclusão pendente — encerrado às 14:24");
    }

    [Fact]
    public void Concluido_nao_reabre_por_aqui()
    {
        var s = SessaoDoAtendimentoNaTela.De(
            Horario(StatusAgendamento.Realizado, Agora.AddMinutes(-30), Agora.AddMinutes(-6), 9), Agora);

        s.Concluida.Should().BeTrue();
        s.PodeReabrir.Should().BeFalse();
        s.Situacao.Should().StartWith("Sessão concluída às 14:24");
    }

    [Theory]
    [InlineData(StatusAgendamento.Faltou, "Horário marcado como falta")]
    [InlineData(StatusAgendamento.Cancelado, "Horário cancelado")]
    [InlineData(StatusAgendamento.Substituido, "Horário substituído por uma sessão lançada por fora")]
    public void Horario_que_nao_aconteceu_diz_o_desfecho(StatusAgendamento status, string frase)
    {
        var s = SessaoDoAtendimentoNaTela.De(Horario(status), Agora);

        s.Situacao.Should().Be(frase);
        s.PodeIniciar.Should().BeFalse();
    }

    [Fact]
    public void Finalizar_existe_so_no_horario_em_aberto_e_para_quem_pode()
    {
        SessaoDoAtendimentoNaTela.PodeFinalizar(Horario(), podeConcluirComAcesso: true).Should().BeTrue();
        SessaoDoAtendimentoNaTela.PodeFinalizar(Horario(), podeConcluirComAcesso: false).Should().BeFalse();
        SessaoDoAtendimentoNaTela.PodeFinalizar(Horario(StatusAgendamento.Cancelado), true).Should().BeFalse();
        SessaoDoAtendimentoNaTela.PodeFinalizar(Horario(StatusAgendamento.Realizado, atendimentoId: 9), true)
            .Should().BeTrue();
        SessaoDoAtendimentoNaTela.PodeFinalizar(null, true).Should().BeFalse();
    }

    [Fact]
    public void O_rotulo_de_finalizar_diz_a_conclusao_pendente()
    {
        SessaoDoAtendimentoNaTela.RotuloFinalizar(Horario()).Should().Be("Salvar e finalizar");
        SessaoDoAtendimentoNaTela.RotuloFinalizar(Horario(fim: Agora)).Should().Be("Concluir sessão pendente");
    }
}
