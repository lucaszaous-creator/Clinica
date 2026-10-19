using Clinica.Application.Modelos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O que o MEU DIA afirma (out/2026 — a tela virou Blazor e a regra do <c>MeuDiaViewModel</c>
/// WPF desceu para <see cref="MeuDiaNaTela"/>). Cada teste fixa um comportamento que o WPF tinha
/// e que a migração não pode perder: a ordem da lista, o filtro da enfermagem, a linha apagada do
/// cancelado, a coluna PRONTUÁRIO, o rótulo do botão, a dívida em palavras e quando o Atender
/// carimba a entrada na sala.
/// </summary>
public class MeuDiaNaTelaTests
{
    private static readonly DateOnly Hoje = new(2026, 10, 15);

    private static SessaoDoDia S(int id, int hora, StatusAgendamento status = StatusAgendamento.Agendado,
        EtapaFila etapa = EtapaFila.Aguardando, int? evolucao = null, ModalidadeAtendimento familia = ModalidadeAtendimento.AcupunturaSimples,
        DateOnly? dia = null, string? sala = null, string? obs = null, int minuto = 0)
        => new(id, (dia ?? Hoje).ToDateTime(new TimeOnly(hora, minuto)), 100 + id, $"Paciente {id}", "Acupuntura", sala,
            status, etapa, false, null, evolucao)
        {
            ModalidadeFamilia = familia,
            Observacoes = obs
        };

    private static DiaDoProfissional Dia(params SessaoDoDia[] s) => new(Hoje, 1, "Dra. Helena", s);

    [Fact]
    public void A_lista_sai_na_ordem_da_hora_e_o_id_desempata()
    {
        var linhas = MeuDiaNaTela.Montar(Dia(S(3, 10), S(1, 8), S(2, 8)), perfilDeEnfermagem: false, Hoje);
        Assert.Equal([1, 2, 3], linhas.Select(l => l.AgendamentoId));
        Assert.Equal("08:00–08:30", linhas[0].Hora);
    }

    [Fact]
    public void A_enfermagem_ve_so_as_sessoes_que_admitem_passagem_de_enfermagem()
    {
        var dia = Dia(S(1, 8, familia: ModalidadeAtendimento.BsvApenas), S(2, 9), S(3, 10, familia: ModalidadeAtendimento.BsvComAcupuntura));
        Assert.Equal([1, 3], MeuDiaNaTela.Montar(dia, perfilDeEnfermagem: true, Hoje).Select(l => l.AgendamentoId));
        Assert.Equal(3, MeuDiaNaTela.Montar(dia, perfilDeEnfermagem: false, Hoje).Count);
    }

    [Fact]
    public void Cancelado_fica_na_lista_marcado_sem_o_atender_e_sem_pendencia()
    {
        var l = MeuDiaNaTela.Linha(S(1, 8, StatusAgendamento.Cancelado), Hoje);
        Assert.True(l.ForaDaFila);
        Assert.False(l.PodeAtender);
        Assert.Equal("Cancelado", l.Status);
        Assert.Equal("—", l.Prontuario);
        Assert.False(l.ProntuarioPendente);
    }

    [Fact]
    public void A_coluna_prontuario_diz_pendente_ate_ter_evolucao_e_o_botao_muda_de_nome()
    {
        var semRegistro = MeuDiaNaTela.Linha(S(1, 8), Hoje);
        var escrita = MeuDiaNaTela.Linha(S(2, 9, StatusAgendamento.Realizado, EtapaFila.Finalizado, evolucao: 7), Hoje);

        Assert.Equal("Pendente", semRegistro.Prontuario);
        Assert.Equal("Atender", semRegistro.RotuloRegistro);
        Assert.Equal("Escrito", escrita.Prontuario);
        Assert.Equal("Ver registro", escrita.RotuloRegistro);
    }

    [Fact]
    public void Sem_sala_nao_escreve_sala_e_o_recado_do_balcao_viaja_na_linha()
    {
        Assert.Equal("Acupuntura", MeuDiaNaTela.Linha(S(1, 8), Hoje).Contexto);
        Assert.Equal("Acupuntura · sala 2", MeuDiaNaTela.Linha(S(2, 8, sala: "2"), Hoje).Contexto);
        Assert.Equal("Trazer exames", MeuDiaNaTela.Linha(S(3, 8, obs: "Trazer exames"), Hoje).Observacoes);
        Assert.Equal(string.Empty, MeuDiaNaTela.Linha(S(4, 8), Hoje).Observacoes);
    }

    [Theory]
    [InlineData(0, "Prontuário em dia")]
    [InlineData(1, "1 sessão sem evolução")]
    [InlineData(12, "12 sessões sem evolução")]
    public void A_divida_em_palavras(int n, string esperado) => Assert.Equal(esperado, MeuDiaNaTela.RotuloPendentes(n));

    [Fact]
    public void Agenda_fechada_so_quando_ha_bloqueio()
    {
        Assert.Null(MeuDiaNaTela.AgendaFechada(null));
        Assert.Equal("Agenda fechada neste dia: Férias", MeuDiaNaTela.AgendaFechada("Férias"));
    }

    [Fact]
    public void O_atender_carimba_a_entrada_so_hoje_em_aberto_e_nunca_pela_enfermagem()
    {
        var aguardando = MeuDiaNaTela.Linha(S(1, 8), Hoje);
        var chegou = MeuDiaNaTela.Linha(S(2, 8, etapa: EtapaFila.Chegou), Hoje);
        var naSala = MeuDiaNaTela.Linha(S(3, 8, etapa: EtapaFila.EmAtendimento), Hoje);
        var deOntem = MeuDiaNaTela.Linha(S(4, 8, dia: Hoje.AddDays(-1)), Hoje);

        Assert.True(MeuDiaNaTela.CarimbaEntradaAoAtender(false, aguardando));
        Assert.True(MeuDiaNaTela.CarimbaEntradaAoAtender(false, chegou));
        Assert.False(MeuDiaNaTela.CarimbaEntradaAoAtender(false, naSala));
        Assert.False(MeuDiaNaTela.CarimbaEntradaAoAtender(false, deOntem));
        Assert.False(MeuDiaNaTela.CarimbaEntradaAoAtender(true, aguardando));
    }

    [Fact]
    public void O_status_usa_o_vocabulario_da_lista_do_balcao()
    {
        var l = MeuDiaNaTela.Linha(S(1, 8, etapa: EtapaFila.Chegou), Hoje);
        Assert.Equal(StatusDaFila.Palavra(StatusAgendamento.Agendado, EtapaFila.Chegou), l.Status);
        Assert.Equal("no-local", l.GrupoVisual);
    }
}
