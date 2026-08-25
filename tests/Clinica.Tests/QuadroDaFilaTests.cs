using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// Os dados novos do kanban da fila (redesenho de ago/2026): o atraso do horário, o selo
/// da rodada de confirmação e a espera média com definição ÚNICA. A tela do quadro é WPF
/// e não compila aqui — o que se fixa é a regra, que por isso mora no domínio e na
/// aplicação, não no ViewModel.
/// </summary>
public class QuadroDaFilaTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;

    public QuadroDaFilaTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
    }

    private static readonly DateTime Meiodia = new(2026, 8, 25, 12, 0, 0);

    private static Agendamento Horario(DateTime quando) => new()
    {
        PacienteId = 1,
        DataHora = quando,
        ModalidadePrevista = ModalidadeAtendimento.AcupunturaComEletro
    };

    // ---- Atraso: a pergunta que o quadro nunca respondia ----

    [Fact]
    public void Hora_estourada_sem_check_in_e_atraso()
    {
        var ag = Horario(Meiodia.AddMinutes(-25));

        ag.AtrasoMinutos(Meiodia).Should().Be(25);
    }

    [Fact]
    public void Horario_futuro_nao_tem_atraso()
    {
        Horario(Meiodia.AddMinutes(30)).AtrasoMinutos(Meiodia).Should().BeNull();
        // Na hora exata também não: atraso zero não é atraso.
        Horario(Meiodia).AtrasoMinutos(Meiodia).Should().BeNull();
    }

    [Fact]
    public void Quem_ja_chegou_nao_esta_atrasado_esta_esperando()
    {
        var ag = Horario(Meiodia.AddMinutes(-25));
        ag.ChegadaEm = Meiodia.AddMinutes(-10);

        ag.AtrasoMinutos(Meiodia).Should().BeNull(
            "check-in feito é espera, não atraso — os dois selos juntos se contradiriam");
    }

    [Fact]
    public void Cancelado_falta_e_realizado_nao_tem_atraso_a_cobrar()
    {
        var cancelado = Horario(Meiodia.AddMinutes(-60));
        cancelado.Status = StatusAgendamento.Cancelado;
        cancelado.AtrasoMinutos(Meiodia).Should().BeNull();

        var faltou = Horario(Meiodia.AddMinutes(-60));
        faltou.Status = StatusAgendamento.Faltou;
        faltou.AtrasoMinutos(Meiodia).Should().BeNull();

        var realizado = Horario(Meiodia.AddMinutes(-60));
        realizado.Status = StatusAgendamento.Realizado;
        realizado.AtrasoMinutos(Meiodia).Should().BeNull();
    }

    // ---- O selo da rodada de confirmação, em lote ----

    [Fact]
    public async Task Confirmacoes_em_lote_devolvem_o_status_por_agendamento()
    {
        var p = new Paciente { Nome = "Maria", Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();

        var respondido = Horario(Meiodia.AddDays(1));
        var avisado = Horario(Meiodia.AddDays(1).AddHours(1));
        var semRodada = Horario(Meiodia.AddDays(1).AddHours(2));
        respondido.PacienteId = avisado.PacienteId = semRodada.PacienteId = p.Id;
        _db.Agendamentos.AddRange(respondido, avisado, semRodada);
        await _db.SaveChangesAsync();

        _db.Contatos.AddRange(
            new ContatoCampanha
            {
                PacienteId = p.Id, Tipo = TipoContato.ConfirmacaoSessao,
                AgendamentoId = respondido.Id, Origem = $"AGD:{respondido.Id}",
                Status = StatusContato.Respondido,
                Referencia = DateOnly.FromDateTime(Meiodia)
            },
            new ContatoCampanha
            {
                PacienteId = p.Id, Tipo = TipoContato.ConfirmacaoSessao,
                AgendamentoId = avisado.Id, Origem = $"AGD:{avisado.Id}",
                Status = StatusContato.Enviado,
                Referencia = DateOnly.FromDateTime(Meiodia)
            },
            // Recall do MESMO paciente: tipo errado não pode virar selo de confirmação.
            new ContatoCampanha
            {
                PacienteId = p.Id, Tipo = TipoContato.Recall,
                AgendamentoId = semRodada.Id, Origem = $"REC:{p.Id}:2026-08",
                Status = StatusContato.Respondido,
                Referencia = DateOnly.FromDateTime(Meiodia)
            });
        await _db.SaveChangesAsync();

        var mapa = await _repo.ConfirmacoesDosAgendamentosAsync(
            [respondido.Id, avisado.Id, semRodada.Id]);

        mapa[respondido.Id].Should().Be(StatusContato.Respondido);
        mapa[avisado.Id].Should().Be(StatusContato.Enviado);
        mapa.Should().NotContainKey(semRodada.Id,
            "sem rodada de CONFIRMAÇÃO não há o que afirmar — o cartão fica sem selo, "
            + "nunca acusando de 'não confirmou' quem nunca foi avisado");
    }

    [Fact]
    public async Task Confirmacoes_sem_ids_nao_vao_ao_banco()
    {
        (await _repo.ConfirmacoesDosAgendamentosAsync([])).Should().BeEmpty();
    }

    // ---- A espera média tem UMA definição (painel e fila leem a mesma) ----

    [Fact]
    public void Espera_media_ignora_falta_e_cancelado_e_sem_base_e_nula()
    {
        var esperando = Horario(Meiodia.AddHours(-1));
        esperando.ChegadaEm = Meiodia.AddMinutes(-10);

        var faltou = Horario(Meiodia.AddHours(-3));
        faltou.ChegadaEm = Meiodia.AddHours(-3);
        faltou.Status = StatusAgendamento.Faltou;

        PainelRecepcaoService.EsperaMediaMinutos([esperando, faltou], Meiodia)
            .Should().Be(10,
                "a falta com check-in correria até agora e explodiria a média por uma "
                + "sessão que oficialmente não aconteceu (item 3 da fila da parcela 69)");

        PainelRecepcaoService.EsperaMediaMinutos([faltou], Meiodia).Should().BeNull(
            "'não medido' e '0 min' são respostas diferentes — quem escreve é a tela");
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
