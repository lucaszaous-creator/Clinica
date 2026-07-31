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
/// Aniversário e padrão de falta (parcela 28) — as duas perguntas do balcão que o sistema
/// gravava e nenhuma tela lia.
///
/// O que estes testes protegem:
///
/// 1. **Aniversário casa DIA E MÊS**, nunca a data inteira: o ano de nascimento não tem
///    nada a ver com a pergunta.
/// 2. **A janela cobre a semana**, porque a clínica não abre todo dia — sem ela, todo
///    aniversário de domingo se perderia.
/// 3. **Cancelamento avisado não conta como falta.** Quem desmarcou deu à clínica a
///    chance de reocupar o horário, e misturar os dois esconde o comportamento que o
///    número existe para mostrar.
/// 4. **Taxa sem base é nula, nunca zero** — e reincidência exige base mínima: uma falta
///    em duas sessões dá 50% e não diz nada sobre a pessoa.
/// </summary>
public class RelacionamentoTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly RelacionamentoService _relacionamento;

    private static readonly DateOnly Hoje = new(2026, 8, 10);

    public RelacionamentoTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _relacionamento = new RelacionamentoService(
            _repo, new AgendaService(_repo, new AtendimentoService(_repo)));
    }

    private async Task<int> CriarPacienteAsync(string nome, DateOnly? nascimento)
    {
        var p = new Paciente
        {
            Nome = nome,
            Convenio = Convenio.UnimedIntercambio,
            Sexo = Sexo.Feminino,
            Telefone = "11988887777",
            DataNascimento = nascimento
        };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    private async Task MarcarAsync(int pacienteId, DateTime quando, StatusAgendamento status)
    {
        _db.Agendamentos.Add(new Agendamento
        {
            PacienteId = pacienteId,
            DataHora = quando,
            ModalidadePrevista = ModalidadeAtendimento.AcupunturaComEletro,
            Status = status
        });
        await _db.SaveChangesAsync();
    }

    // ==================== Aniversário ====================

    [Fact]
    public async Task Aniversariante_do_dia_aparece_qualquer_que_seja_o_ano()
    {
        await CriarPacienteAsync("Maria", new DateOnly(1980, 8, 10));
        await CriarPacienteAsync("João", new DateOnly(1955, 8, 10));
        await CriarPacienteAsync("Ana", new DateOnly(1990, 9, 10));

        var lista = await _relacionamento.AniversariantesAsync(Hoje);

        lista.Should().HaveCount(2);
        lista.Select(a => a.Nome).Should().Contain(["Maria", "João"]);
    }

    [Fact]
    public async Task Janela_cobre_a_semana_inteira()
    {
        // A clínica não abre todo dia: sem a janela, o aniversário de domingo se perde.
        await CriarPacienteAsync("Domingo", new DateOnly(1980, 8, 16));

        (await _relacionamento.AniversariantesAsync(Hoje)).Should().BeEmpty();
        (await _relacionamento.AniversariantesAsync(Hoje, janelaDias: 6))
            .Should().ContainSingle(a => a.Nome == "Domingo");
    }

    [Fact]
    public async Task Janela_atravessa_a_virada_do_mes()
    {
        await CriarPacienteAsync("Setembro", new DateOnly(1980, 9, 2));

        var lista = await _relacionamento.AniversariantesAsync(
            new DateOnly(2026, 8, 30), janelaDias: 6);

        lista.Should().ContainSingle(a => a.Nome == "Setembro");
    }

    [Fact]
    public async Task Quem_vem_hoje_aparece_primeiro()
    {
        var vem = await CriarPacienteAsync("Vem hoje", new DateOnly(1980, 8, 10));
        await CriarPacienteAsync("Não vem", new DateOnly(1980, 8, 10));
        await MarcarAsync(vem, Hoje.ToDateTime(new TimeOnly(14, 0)), StatusAgendamento.Agendado);

        var lista = await _relacionamento.AniversariantesAsync(Hoje);

        // Com o paciente no balcão, o parabéns é dado na hora e não custa nem a ligação.
        lista[0].Nome.Should().Be("Vem hoje");
        lista[0].VemHoje.Should().BeTrue();
        lista[1].VemHoje.Should().BeFalse();
    }

    [Fact]
    public async Task Idade_implausivel_nao_e_inventada()
    {
        // Cadastro antigo às vezes traz 01/01/0001; "2025 anos" na tela é pior que vazio.
        await CriarPacienteAsync("Sem data real", new DateOnly(1, 8, 10));

        var lista = await _relacionamento.AniversariantesAsync(Hoje);

        lista.Should().ContainSingle();
        lista[0].Idade.Should().BeNull();
    }

    [Fact]
    public async Task Paciente_sem_nascimento_nao_entra()
    {
        await CriarPacienteAsync("Sem data", null);

        (await _relacionamento.AniversariantesAsync(Hoje, janelaDias: 30)).Should().BeEmpty();
    }

    // ==================== Padrão de falta ====================

    [Fact]
    public async Task Cancelamento_avisado_nao_conta_como_falta()
    {
        var pacienteId = await CriarPacienteAsync("Maria", new DateOnly(1980, 1, 1));

        await MarcarAsync(pacienteId, new DateTime(2026, 7, 1, 9, 0, 0), StatusAgendamento.Realizado);
        await MarcarAsync(pacienteId, new DateTime(2026, 7, 8, 9, 0, 0), StatusAgendamento.Faltou);
        await MarcarAsync(pacienteId, new DateTime(2026, 7, 15, 9, 0, 0), StatusAgendamento.Cancelado);

        var h = await _relacionamento.FaltasDoPacienteAsync(pacienteId);

        h.Faltas.Should().Be(1);
        h.Cancelamentos.Should().Be(1);
        // Fechados = atendidos + faltas. O cancelamento fica de fora do denominador.
        h.Fechados.Should().Be(2);
        h.TaxaPercentual.Should().Be(50.0);
    }

    [Fact]
    public async Task Paciente_novo_nao_tem_taxa()
    {
        var pacienteId = await CriarPacienteAsync("Novo", new DateOnly(1980, 1, 1));

        var h = await _relacionamento.FaltasDoPacienteAsync(pacienteId);

        // Nulo, e não zero: 0% diria que ele já provou que comparece.
        h.TaxaPercentual.Should().BeNull();
        h.Reincidente.Should().BeFalse();
    }

    [Fact]
    public async Task Uma_falta_em_duas_sessoes_nao_e_padrao()
    {
        var pacienteId = await CriarPacienteAsync("Maria", new DateOnly(1980, 1, 1));
        await MarcarAsync(pacienteId, new DateTime(2026, 7, 1, 9, 0, 0), StatusAgendamento.Realizado);
        await MarcarAsync(pacienteId, new DateTime(2026, 7, 8, 9, 0, 0), StatusAgendamento.Faltou);

        var h = await _relacionamento.FaltasDoPacienteAsync(pacienteId);

        // 50% com base de duas sessões não diz nada sobre a pessoa.
        h.TaxaPercentual.Should().Be(50.0);
        h.Reincidente.Should().BeFalse();
    }

    [Fact]
    public async Task Padrao_de_falta_com_base_suficiente_vira_aviso()
    {
        var pacienteId = await CriarPacienteAsync("Maria", new DateOnly(1980, 1, 1));

        for (var i = 0; i < 4; i++)
            await MarcarAsync(pacienteId, new DateTime(2026, 6, 1 + i, 9, 0, 0),
                StatusAgendamento.Realizado);

        await MarcarAsync(pacienteId, new DateTime(2026, 7, 6, 9, 0, 0), StatusAgendamento.Faltou);
        await MarcarAsync(pacienteId, new DateTime(2026, 7, 20, 9, 0, 0), StatusAgendamento.Faltou);

        var h = await _relacionamento.FaltasDoPacienteAsync(pacienteId);

        h.Faltas.Should().Be(2);
        h.Fechados.Should().Be(6);
        h.Reincidente.Should().BeTrue();
        h.UltimaFalta.Should().Be(new DateOnly(2026, 7, 20));
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }
}
