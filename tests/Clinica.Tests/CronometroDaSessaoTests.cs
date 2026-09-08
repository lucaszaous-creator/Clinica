using Clinica.Application.Modelos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O CRONÔMETRO da tela de atendimento (set/2026): o visor que corre enquanto o
/// profissional está com o paciente, pedido pela direção e aprovado em mockup.
///
/// O que estes testes prendem é o que a tela AFIRMA — e é por isso que a decisão mora na
/// Application: as ViewModels do WPF não compilam aqui, e regra que o <c>dotnet test</c>
/// não alcança apodrece sem ninguém notar.
/// </summary>
public class CronometroDaSessaoTests
{
    private static readonly DateTime Entrada = new(2026, 9, 5, 14, 0, 0);

    private static Agendamento EmAtendimento(DateTime? fim = null,
        StatusAgendamento status = StatusAgendamento.Agendado)
        => new()
        {
            Id = 1,
            PacienteId = 1,
            DataHora = Entrada.AddMinutes(-5),
            Status = status,
            InicioAtendimentoEm = Entrada,
            FimAtendimentoEm = fim
        };

    // ==================== O que ele mostra ====================

    [Fact]
    public void Corre_com_horas_minutos_e_segundos()
        => CronometroDaSessao.De(EmAtendimento(), Entrada.AddMinutes(12).AddSeconds(35))
            .Should().Be("00:12:35");

    /// <summary>
    /// As horas aparecem SEMPRE, como no painel da referência — e é decisão: escondê-las
    /// até virar a hora faria o visor mudar de largura no meio do atendimento.
    /// </summary>
    [Fact]
    public void No_primeiro_segundo_o_visor_ja_existe_e_esta_zerado()
        => CronometroDaSessao.De(EmAtendimento(), Entrada)
            .Should().Be("00:00:00");

    [Fact]
    public void Passando_de_uma_hora_a_casa_das_horas_anda()
        => CronometroDaSessao.De(EmAtendimento(), Entrada.AddHours(1).AddMinutes(4).AddSeconds(22))
            .Should().Be("01:04:22");

    /// <summary>
    /// Atendimento de mais de um dia não existe — mas o visor não pode DAR A VOLTA e
    /// mostrar um número pequeno como se a consulta tivesse acabado de começar.
    /// </summary>
    [Fact]
    public void Passando_de_um_dia_o_visor_continua_crescendo()
        => CronometroDaSessao.De(EmAtendimento(), Entrada.AddHours(26).AddMinutes(3))
            .Should().Be("26:03:00");

    // ==================== Quando ele NÃO existe ====================

    /// <summary>
    /// A regra central do desenho: sem consulta em curso não há visor. Um cronômetro
    /// zerado convidaria a "iniciar" um atendimento que não existe na agenda de ninguém.
    /// </summary>
    [Fact]
    public void Sem_entrada_na_sala_nao_ha_visor()
    {
        var so_marcado = EmAtendimento();
        so_marcado.InicioAtendimentoEm = null;

        CronometroDaSessao.De(so_marcado, Entrada.AddMinutes(30)).Should().BeNull();
    }

    /// <summary>
    /// Depois do Finalizar quem fala é a pílula ("durou 24 min"). Manter o visor parado
    /// ao lado dela seria dois leitores dizendo a mesma coisa com pesos diferentes.
    /// </summary>
    [Fact]
    public void Encerrado_o_visor_sai_de_cena()
        => CronometroDaSessao.De(EmAtendimento(fim: Entrada.AddMinutes(24)), Entrada.AddMinutes(30))
            .Should().BeNull();

    [Theory]
    [InlineData(StatusAgendamento.Cancelado)]
    [InlineData(StatusAgendamento.Faltou)]
    [InlineData(StatusAgendamento.Substituido)]
    [InlineData(StatusAgendamento.Realizado)]
    public void Horario_que_saiu_da_fila_nao_tem_cronometro(StatusAgendamento status)
        => CronometroDaSessao.De(EmAtendimento(status: status), Entrada.AddMinutes(10))
            .Should().BeNull();

    [Fact]
    public void Sem_horario_de_origem_nao_ha_visor()
        => CronometroDaSessao.De(null, Entrada).Should().BeNull();

    // ==================== A guarda do relógio torto ====================

    /// <summary>
    /// Acerto de hora e fuso fazem o relógio andar para trás. Numa tela que o médico olha
    /// o tempo todo, isso não pode virar um visor negativo.
    /// </summary>
    [Fact]
    public void Relogio_que_anda_para_tras_nao_produz_visor_negativo()
        => CronometroDaSessao.De(EmAtendimento(), Entrada.AddMinutes(-8))
            .Should().Be("00:00:00");

    // ==================== A conta é UMA ====================

    /// <summary>
    /// O visor e a frase da pílula ("há 12 min") saem do MESMO cálculo: duas contas do
    /// mesmo tempo divergiriam na primeira correção, e o médico veria o número discordar
    /// da frase ao lado dele.
    /// </summary>
    [Theory]
    [InlineData(0, "00:00:00", 0)]
    [InlineData(59, "00:00:59", 1)]
    [InlineData(90, "00:01:30", 2)]
    [InlineData(3_725, "01:02:05", 62)]
    public void O_visor_e_a_duracao_em_minutos_saem_da_mesma_conta(
        int segundos, string visor, int minutos)
    {
        var ag = EmAtendimento();
        var agora = Entrada.AddSeconds(segundos);

        CronometroDaSessao.De(ag, agora).Should().Be(visor);
        ag.DuracaoDoAtendimento(agora).Should().Be(minutos);
    }

    [Fact]
    public void Sem_tempo_nao_ha_texto()
        => CronometroDaSessao.Formatar(null).Should().BeNull();

    // ==================== Achar o atendimento em curso ====================
    //
    // A tela da Enfermagem abre o paciente SEM vir da agenda — é a porta da passagem fora
    // de horário. Sem esta leitura ela nunca teria cronômetro, e eu teria prometido no
    // mockup um visor que ela não mostra.

    [Fact]
    public void Entre_os_horarios_do_dia_acha_o_que_esta_em_curso()
    {
        var encerrado = EmAtendimento(fim: Entrada.AddMinutes(-30));
        var emCurso = EmAtendimento();
        var soMarcado = EmAtendimento();
        soMarcado.InicioAtendimentoEm = null;

        CronometroDaSessao.EmCurso([encerrado, soMarcado, emCurso])
            .Should().BeSameAs(emCurso);
    }

    [Fact]
    public void Sem_ninguem_em_curso_devolve_nulo()
    {
        var soMarcado = EmAtendimento();
        soMarcado.InicioAtendimentoEm = null;

        CronometroDaSessao.EmCurso([soMarcado]).Should().BeNull();
        CronometroDaSessao.EmCurso([]).Should().BeNull();
        CronometroDaSessao.EmCurso(null).Should().BeNull();
    }

    /// <summary>
    /// Dois em curso não deveriam existir. Se existirem, vence o que começou por ÚLTIMO —
    /// é o paciente que está na sala agora.
    /// </summary>
    [Fact]
    public void Com_dois_em_curso_vence_o_mais_recente()
    {
        var antigo = EmAtendimento();
        var recente = EmAtendimento();
        recente.InicioAtendimentoEm = Entrada.AddMinutes(20);

        CronometroDaSessao.EmCurso([antigo, recente]).Should().BeSameAs(recente);
    }

    [Fact]
    public void Horario_cancelado_nao_conta_como_em_curso()
        => CronometroDaSessao.EmCurso([EmAtendimento(status: StatusAgendamento.Cancelado)])
            .Should().BeNull();
}
