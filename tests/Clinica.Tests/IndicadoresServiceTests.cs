using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Tests;

/// <summary>
/// Indicadores gerenciais (parcela 5 — feature 12): ocupação, no-show e produtividade.
///
/// O que estes testes protegem é a HONESTIDADE dos números:
/// - cancelamento avisado não conta como falta (quem desmarcou deu chance de reocupar);
/// - ocupação sem base de cálculo devolve null, nunca 0% (que pareceria agenda vazia);
/// - a queda média da dor só considera as sessões com o PAR de medidas da EVA.
/// </summary>
public class IndicadoresServiceTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly IndicadoresService _indicadores;
    private readonly ParametrosService _parametros;

    private static readonly DateOnly Inicio = new(2026, 8, 1);
    private static readonly DateOnly Fim = new(2026, 8, 31);

    public IndicadoresServiceTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _parametros = new ParametrosService(_repo);
        _indicadores = new IndicadoresService(_repo, _parametros);
    }

    private async Task<Paciente> CriarPacienteAsync(string nome = "Maria Souza")
    {
        var p = new Paciente { Nome = nome, Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p;
    }

    private async Task<Profissional> CriarProfissionalAsync(string nome = "Dra. Ana")
    {
        var p = new Profissional { Nome = nome };
        _db.Profissionais.Add(p);
        await _db.SaveChangesAsync();
        return p;
    }

    private async Task<Agendamento> MarcarAsync(
        int pacienteId, DateOnly dia, StatusAgendamento status,
        int? profissionalId = null, int duracao = 30, bool encaixe = false, int hora = 14)
    {
        var ag = new Agendamento
        {
            PacienteId = pacienteId,
            ProfissionalId = profissionalId,
            DataHora = dia.ToDateTime(new TimeOnly(hora, 0)),
            ModalidadePrevista = ModalidadeAtendimento.AcupunturaSimples,
            DuracaoMinutos = duracao,
            Encaixe = encaixe,
            Status = status
        };
        _db.Agendamentos.Add(ag);
        await _db.SaveChangesAsync();
        return ag;
    }

    // ===== No-show =====

    [Fact]
    public async Task NoShow_ContaSobreOsHorariosQueChegaramAoFim()
    {
        var paciente = await CriarPacienteAsync();
        await MarcarAsync(paciente.Id, Inicio, StatusAgendamento.Realizado, hora: 8);
        await MarcarAsync(paciente.Id, Inicio, StatusAgendamento.Realizado, hora: 9);
        await MarcarAsync(paciente.Id, Inicio, StatusAgendamento.Realizado, hora: 10);
        await MarcarAsync(paciente.Id, Inicio, StatusAgendamento.Faltou, hora: 11);

        var painel = await _indicadores.GerarAsync(Inicio, Fim);

        painel.Agenda.Fechados.Should().Be(4);
        painel.Agenda.TaxaNoShow.Should().Be(25);
    }

    [Fact]
    public async Task NoShow_CancelamentoAvisadoNaoContaComoFalta()
    {
        var paciente = await CriarPacienteAsync();
        await MarcarAsync(paciente.Id, Inicio, StatusAgendamento.Realizado, hora: 8);
        await MarcarAsync(paciente.Id, Inicio, StatusAgendamento.Cancelado, hora: 9);

        var painel = await _indicadores.GerarAsync(Inicio, Fim);

        // Quem desmarcou deu à clínica a chance de reocupar o horário: misturar os dois
        // esconderia justamente o problema que o indicador existe para mostrar.
        painel.Agenda.TaxaNoShow.Should().Be(0);
        painel.Agenda.Cancelados.Should().Be(1);
    }

    [Fact]
    public async Task NoShow_SemHorarioFechado_NaoInventaTaxa()
    {
        var paciente = await CriarPacienteAsync();
        await MarcarAsync(paciente.Id, Inicio, StatusAgendamento.Agendado);

        var painel = await _indicadores.GerarAsync(Inicio, Fim);

        painel.Agenda.Fechados.Should().Be(0);
        painel.Agenda.TaxaNoShow.Should().Be(0);
    }

    // ===== Ocupação =====

    [Fact]
    public async Task Ocupacao_MedeSobreAJornadaDosDiasComAgenda()
    {
        var paciente = await CriarPacienteAsync();
        var profissional = await CriarProfissionalAsync();

        // 4 sessões de 60 min num único dia = 240 min de 480 = 50%.
        for (var hora = 8; hora < 12; hora++)
            await MarcarAsync(paciente.Id, Inicio, StatusAgendamento.Realizado,
                profissional.Id, duracao: 60, hora: hora);

        var painel = await _indicadores.GerarAsync(Inicio, Fim);

        painel.JornadaDiariaMinutos.Should().Be(ParametrosService.JornadaDiariaPadraoMinutos);
        painel.Agenda.MinutosOcupados.Should().Be(240);
        painel.Agenda.OcupacaoPercentual.Should().Be(50);
    }

    [Fact]
    public async Task Ocupacao_SemAgendaNoPeriodo_DevolveNaoMedido()
    {
        var painel = await _indicadores.GerarAsync(Inicio, Fim);

        // Null e não 0%: "não deu para medir" e "agenda vazia" são coisas diferentes.
        painel.Agenda.OcupacaoPercentual.Should().BeNull();
    }

    [Fact]
    public async Task Ocupacao_RespeitaAJornadaConfigurada()
    {
        var paciente = await CriarPacienteAsync();
        var profissional = await CriarProfissionalAsync();
        await _parametros.SalvarJornadaDiariaAsync(240); // clínica de meio período

        for (var hora = 8; hora < 10; hora++)
            await MarcarAsync(paciente.Id, Inicio, StatusAgendamento.Realizado,
                profissional.Id, duracao: 60, hora: hora);

        var painel = await _indicadores.GerarAsync(Inicio, Fim);

        // 120 de 240 = 50%. Com a jornada padrão o mesmo dia mostraria 25% e mentiria.
        painel.Agenda.OcupacaoPercentual.Should().Be(50);
    }

    [Fact]
    public async Task Ocupacao_DoisProfissionaisNoMesmoDia_DobramABaseDeCalculo()
    {
        var paciente = await CriarPacienteAsync();
        var ana = await CriarProfissionalAsync("Dra. Ana");
        var bruno = await CriarProfissionalAsync("Dr. Bruno");

        await MarcarAsync(paciente.Id, Inicio, StatusAgendamento.Realizado, ana.Id, duracao: 480, hora: 8);
        await MarcarAsync(paciente.Id, Inicio, StatusAgendamento.Realizado, bruno.Id, duracao: 240, hora: 8);

        var painel = await _indicadores.GerarAsync(Inicio, Fim);

        // 720 min ocupados sobre 2 agendas de 480 = 75%. Sem contar a segunda agenda,
        // a taxa passaria de 100% e o indicador viraria piada.
        painel.Agenda.OcupacaoPercentual.Should().Be(75);
    }

    // ===== Produtividade =====

    [Fact]
    public async Task Produtividade_SeparaPorProfissional()
    {
        var paciente = await CriarPacienteAsync();
        var ana = await CriarProfissionalAsync("Dra. Ana");
        var bruno = await CriarProfissionalAsync("Dr. Bruno");

        await MarcarAsync(paciente.Id, Inicio, StatusAgendamento.Realizado, ana.Id, hora: 8);
        await MarcarAsync(paciente.Id, Inicio, StatusAgendamento.Realizado, ana.Id, hora: 9);
        await MarcarAsync(paciente.Id, Inicio, StatusAgendamento.Faltou, bruno.Id, hora: 8);

        var painel = await _indicadores.GerarAsync(Inicio, Fim);

        var linhaAna = painel.Produtividade.Single(p => p.ProfissionalId == ana.Id);
        linhaAna.Atendidos.Should().Be(2);
        linhaAna.Nome.Should().Be("Dra. Ana");

        var linhaBruno = painel.Produtividade.Single(p => p.ProfissionalId == bruno.Id);
        linhaBruno.Atendidos.Should().Be(0);
        linhaBruno.Faltas.Should().Be(1);
        linhaBruno.TaxaNoShow.Should().Be(100);
    }

    [Fact]
    public async Task Produtividade_HorarioSemProfissional_ApareceNaPropriaLinha()
    {
        // É o caminho do faturamento, que marca sem dizer quem atende. Sumir com ele
        // faria os totais da tela não fecharem com a agenda.
        var paciente = await CriarPacienteAsync();
        await MarcarAsync(paciente.Id, Inicio, StatusAgendamento.Realizado);

        var painel = await _indicadores.GerarAsync(Inicio, Fim);

        var linha = painel.Produtividade.Single(p => p.ProfissionalId is null);
        linha.Nome.Should().Contain("Sem profissional");
        linha.Atendidos.Should().Be(1);
    }

    [Fact]
    public async Task Produtividade_ContaEvolucoesEQuedaMediaDaDor()
    {
        var paciente = await CriarPacienteAsync();
        var ana = await CriarProfissionalAsync();
        await MarcarAsync(paciente.Id, Inicio, StatusAgendamento.Realizado, ana.Id);

        var prontuario = new ProntuarioService(_repo);
        await prontuario.SalvarAsync(new Evolucao
        {
            PacienteId = paciente.Id, ProfissionalId = ana.Id, Data = Inicio,
            EvaAntes = 8, EvaDepois = 3
        });
        await prontuario.SalvarAsync(new Evolucao
        {
            PacienteId = paciente.Id, ProfissionalId = ana.Id, Data = Inicio.AddDays(1),
            EvaAntes = 7, EvaDepois = 4
        });

        var painel = await _indicadores.GerarAsync(Inicio, Fim);

        var linha = painel.Produtividade.Single(p => p.ProfissionalId == ana.Id);
        linha.Evolucoes.Should().Be(2);
        linha.MelhoraMediaEva.Should().Be(4); // (5 + 3) / 2
    }

    [Fact]
    public async Task Produtividade_EvaSemPar_NaoEntraNaMedia()
    {
        var paciente = await CriarPacienteAsync();
        var ana = await CriarProfissionalAsync();
        await MarcarAsync(paciente.Id, Inicio, StatusAgendamento.Realizado, ana.Id);

        var prontuario = new ProntuarioService(_repo);
        await prontuario.SalvarAsync(new Evolucao
        {
            PacienteId = paciente.Id, ProfissionalId = ana.Id, Data = Inicio,
            EvaAntes = 8, EvaDepois = 3
        });
        await prontuario.SalvarAsync(new Evolucao
        {
            PacienteId = paciente.Id, ProfissionalId = ana.Id, Data = Inicio.AddDays(1),
            EvaAntes = 9 // só antes: meia medida oscila por falta de dado, não por dor
        });

        var painel = await _indicadores.GerarAsync(Inicio, Fim);

        var linha = painel.Produtividade.Single(p => p.ProfissionalId == ana.Id);
        linha.Evolucoes.Should().Be(2);
        linha.MelhoraMediaEva.Should().Be(5, "só a sessão com o par de medidas entra");
    }

    [Fact]
    public async Task Produtividade_SemEvolucaoComPar_NaoInventaMedia()
    {
        var paciente = await CriarPacienteAsync();
        var ana = await CriarProfissionalAsync();
        await MarcarAsync(paciente.Id, Inicio, StatusAgendamento.Realizado, ana.Id);

        var painel = await _indicadores.GerarAsync(Inicio, Fim);

        painel.Produtividade.Single(p => p.ProfissionalId == ana.Id)
            .MelhoraMediaEva.Should().BeNull();
    }

    [Fact]
    public async Task Encaixe_EhContadoSeparadamente()
    {
        var paciente = await CriarPacienteAsync();
        var ana = await CriarProfissionalAsync();
        await MarcarAsync(paciente.Id, Inicio, StatusAgendamento.Realizado, ana.Id, hora: 8);
        await MarcarAsync(paciente.Id, Inicio, StatusAgendamento.Realizado, ana.Id, encaixe: true, hora: 9);

        var painel = await _indicadores.GerarAsync(Inicio, Fim);

        painel.Agenda.Encaixes.Should().Be(1);
    }

    // ===== Série mensal =====

    [Fact]
    public async Task Serie_QuebraPorMes()
    {
        var paciente = await CriarPacienteAsync();
        await MarcarAsync(paciente.Id, new DateOnly(2026, 7, 15), StatusAgendamento.Realizado);
        await MarcarAsync(paciente.Id, new DateOnly(2026, 8, 15), StatusAgendamento.Realizado);
        await MarcarAsync(paciente.Id, new DateOnly(2026, 8, 16), StatusAgendamento.Faltou);

        var painel = await _indicadores.GerarAsync(new DateOnly(2026, 7, 1), new DateOnly(2026, 8, 31));

        painel.Serie.Should().HaveCount(2);
        painel.Serie[0].Mes.Should().Be(7);
        painel.Serie[1].Mes.Should().Be(8);
        painel.Serie[1].Faltas.Should().Be(1);
        painel.Serie[1].TaxaNoShow.Should().Be(50);
    }

    [Fact]
    public async Task Periodo_ForaDoIntervalo_FicaDeFora()
    {
        var paciente = await CriarPacienteAsync();
        await MarcarAsync(paciente.Id, Inicio.AddDays(-1), StatusAgendamento.Realizado);
        await MarcarAsync(paciente.Id, Fim.AddDays(1), StatusAgendamento.Realizado);

        var painel = await _indicadores.GerarAsync(Inicio, Fim);

        painel.Agenda.Agendados.Should().Be(0);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }
}
