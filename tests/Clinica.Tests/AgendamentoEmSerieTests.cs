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
/// Agendamento em série: o pacote de dez sessões marcado de uma vez.
///
/// O que estes testes protegem:
///
/// 1. **A série sai da PRIMEIRA data mais N períodos**, nunca da ocorrência anterior
///    mais um — encadear faria uma sessão adiada empurrar todas as seguintes, e o
///    paciente perderia o horário fixo, que é o motivo de marcar em série.
/// 2. **Conflito PULA a data e diz qual**, em vez de abortar a série. Recusar as dez
///    porque a sexta caiu em feriado devolveria a recepção ao trabalho manual.
/// 3. **Todas compartilham a chave da série**, que é o que permite cancelar o resto do
///    bloco quando o paciente desiste no meio.
/// 4. **Cancelar a série não toca no que já foi atendido**: sessão realizada é fato.
/// </summary>
public class AgendamentoEmSerieTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly AgendaService _agenda;
    private readonly BloqueioAgendaService _bloqueios;

    private static readonly DateTime Primeira = new(2026, 9, 1, 14, 0, 0);


    /// <summary>
    /// Todo horário MARCADO precisa de dono desde a parcela 95 — o fixture cria um para os
    /// cenários que não se importam com QUEM atende.
    /// </summary>
    private readonly int _profPadrao;

    public AgendamentoEmSerieTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        var profPadrao = new Profissional { Nome = "Dra. Padrão" };
        _db.Profissionais.Add(profPadrao);
        _db.SaveChanges();
        _profPadrao = profPadrao.Id;
        _repo = new ClinicaRepositorio(_db);
        _agenda = new AgendaService(_repo, new AtendimentoService(_repo));
        _bloqueios = new BloqueioAgendaService(_repo, _agenda);
    }

    private async Task<int> CriarPacienteAsync(string nome = "Maria")
    {
        var p = new Paciente { Nome = nome, Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    private async Task<int> CriarProfissionalAsync(string nome = "Dra. Ana")
    {
        var p = new Profissional { Nome = nome, RegistroConselho = "CRM-SP 1234" };
        _db.Profissionais.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    [Fact]
    public async Task Serie_marca_todas_as_sessoes_no_mesmo_horario()
    {
        var pacienteId = await CriarPacienteAsync();
        var profissionalId = await CriarProfissionalAsync();

        var serie = await _agenda.AgendarSerieAsync(
            pacienteId, Primeira, ModalidadeAtendimento.AcupunturaComEletro, quantidade: 10,
            intervaloDias: 7, profissionalId: profissionalId);

        serie.Marcados.Should().HaveCount(10);
        serie.TudoMarcado.Should().BeTrue();

        // Mesma hora, toda semana: é o que a recepção combinaria à mão.
        serie.Marcados.Select(a => a.DataHora.TimeOfDay).Distinct().Should().ContainSingle();
        serie.Marcados[1].DataHora.Should().Be(Primeira.AddDays(7));
        serie.Marcados[9].DataHora.Should().Be(Primeira.AddDays(63));
    }

    [Fact]
    public async Task Serie_sai_da_primeira_data_e_nao_encadeia()
    {
        var pacienteId = await CriarPacienteAsync();
        var profissionalId = await CriarProfissionalAsync();

        var serie = await _agenda.AgendarSerieAsync(
            pacienteId, Primeira, ModalidadeAtendimento.AcupunturaComEletro, quantidade: 4,
            intervaloDias: 7, profissionalId: profissionalId);

        // Cada data é a PRIMEIRA mais N períodos. Encadear (anterior + 7) daria o mesmo
        // resultado aqui, mas divergiria assim que uma data fosse pulada — e é por isso
        // que a conta parte sempre da origem.
        for (var i = 0; i < serie.Marcados.Count; i++)
            serie.Marcados[i].DataHora.Should().Be(Primeira.AddDays(7 * i));
    }

    [Fact]
    public async Task Data_bloqueada_e_pulada_e_o_resto_da_serie_entra()
    {
        var pacienteId = await CriarPacienteAsync();
        var profissionalId = await CriarProfissionalAsync();

        // A terceira sessão cai num feriado.
        var feriado = Primeira.AddDays(14).Date;
        await _bloqueios.CriarAsync(feriado, feriado.AddDays(1), "Feriado");

        var serie = await _agenda.AgendarSerieAsync(
            pacienteId, Primeira, ModalidadeAtendimento.AcupunturaComEletro, quantidade: 5,
            intervaloDias: 7, profissionalId: profissionalId);

        serie.Marcados.Should().HaveCount(4);
        serie.TudoMarcado.Should().BeFalse();
        serie.Recusados.Should().ContainSingle();
        serie.Recusados[0].Quando.Date.Should().Be(feriado);
        serie.Recusados[0].Motivo.Should().Contain("Feriado");
    }

    [Fact]
    public async Task Sessoes_da_serie_compartilham_a_chave()
    {
        var pacienteId = await CriarPacienteAsync();
        var profissionalId = await CriarProfissionalAsync();

        var serie = await _agenda.AgendarSerieAsync(
            pacienteId, Primeira, ModalidadeAtendimento.AcupunturaComEletro, quantidade: 3,
            profissionalId: profissionalId);

        serie.Marcados.Select(a => a.SerieId).Should().AllBe(serie.SerieId);
        (await _agenda.DaSerieAsync(serie.SerieId)).Should().HaveCount(3);
    }

    [Fact]
    public async Task Horario_avulso_continua_sem_serie()
    {
        var pacienteId = await CriarPacienteAsync();

        var avulso = await _agenda.AgendarAsync(
            pacienteId, Primeira, ModalidadeAtendimento.AcupunturaComEletro, null, profissionalId: _profPadrao);

        avulso.SerieId.Should().BeNull();
    }

    [Fact]
    public async Task Cancelar_a_serie_cancela_so_o_que_ainda_esta_marcado()
    {
        var pacienteId = await CriarPacienteAsync();
        var profissionalId = await CriarProfissionalAsync();

        var serie = await _agenda.AgendarSerieAsync(
            pacienteId, Primeira, ModalidadeAtendimento.AcupunturaComEletro, quantidade: 4,
            profissionalId: profissionalId);

        // A primeira já aconteceu: ela é fato e não pode sumir da agenda.
        var primeira = serie.Marcados[0];
        primeira.Status = StatusAgendamento.Realizado;
        await _db.SaveChangesAsync();

        var cancelados = await _agenda.CancelarSerieAsync(serie.SerieId, "ana");

        cancelados.Should().Be(3);

        var daSerie = await _agenda.DaSerieAsync(serie.SerieId);
        daSerie.Count(a => a.Status == StatusAgendamento.Cancelado).Should().Be(3);
        daSerie.Should().Contain(a => a.Status == StatusAgendamento.Realizado);

        (await _repo.EventosAuditoriaAsync())
            .Should().Contain(e => e.Acao == "SerieCancelada" && e.Operador == "ana");
    }

    [Fact]
    public async Task Serie_de_uma_sessao_so_e_recusada()
    {
        var pacienteId = await CriarPacienteAsync();

        var marcar = () => _agenda.AgendarSerieAsync(
            pacienteId, Primeira, ModalidadeAtendimento.AcupunturaComEletro, quantidade: 1);

        await marcar.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*duas sessões*");
    }

    [Fact]
    public async Task Serie_gigante_e_recusada()
    {
        var pacienteId = await CriarPacienteAsync();

        var marcar = () => _agenda.AgendarSerieAsync(
            pacienteId, Primeira, ModalidadeAtendimento.AcupunturaComEletro,
            quantidade: AgendaService.MaximoSessoesPorSerie + 1);

        await marcar.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No máximo*");
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }
}
