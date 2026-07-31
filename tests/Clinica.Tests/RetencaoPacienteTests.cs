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
/// Quem parou de vir (parcela 32).
///
/// O recall dispara mensagens por regra de tempo; esta é a LISTA, para a direção olhar
/// caso a caso. O que os testes protegem:
///
/// 1. **Quem tem sessão FUTURA marcada não sumiu** — por mais tempo que faça desde a
///    última, ele já voltou, só ainda não veio. É o erro que faria a clínica ligar para
///    quem tem horário na quinta.
/// 2. **A base é o ATENDIMENTO, não o agendamento**: agendamento cancelado não é visita,
///    e contar por ele diria que o paciente veio no dia em que desmarcou.
/// 3. **Paciente de tratamento aparece primeiro** — é o que mais dói perder e o que mais
///    responde a um telefonema.
/// 4. **Pacote em aberto é destaque**: ele pagou sessões que não usou.
/// </summary>
public class RetencaoPacienteTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly AtendimentoService _atendimentos;
    private readonly RetencaoPacienteService _retencao;

    private static readonly DateOnly Hoje = new(2026, 8, 10);

    public RetencaoPacienteTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _atendimentos = new AtendimentoService(_repo);
        _retencao = new RetencaoPacienteService(_repo, new PacoteService(_repo));
    }

    private async Task<int> CriarPacienteAsync(string nome)
    {
        var p = new Paciente
        {
            Nome = nome,
            Convenio = Convenio.UnimedIntercambio,
            Sexo = Sexo.Feminino,
            Telefone = "11988887777"
        };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    private Task AtenderAsync(int pacienteId, DateOnly dia)
        => _atendimentos.LancarAsync(pacienteId, dia, ModalidadeAtendimento.AcupunturaComEletro);

    private async Task MarcarFuturoAsync(int pacienteId, DateTime quando)
    {
        _db.Agendamentos.Add(new Agendamento
        {
            PacienteId = pacienteId,
            DataHora = quando,
            ModalidadePrevista = ModalidadeAtendimento.AcupunturaComEletro,
            Status = StatusAgendamento.Agendado
        });
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task Paciente_sem_vir_ha_meses_entra_na_lista()
    {
        var pacienteId = await CriarPacienteAsync("Maria");
        await AtenderAsync(pacienteId, Hoje.AddDays(-120));

        var lista = await _retencao.SumidosAsync(Hoje);

        lista.Should().ContainSingle();
        lista[0].DiasSemVir.Should().Be(120);
        lista[0].Faixa.Should().Be("3 a 6 meses");
    }

    [Fact]
    public async Task Paciente_que_veio_semana_passada_nao_entra()
    {
        var pacienteId = await CriarPacienteAsync("Recente");
        await AtenderAsync(pacienteId, Hoje.AddDays(-7));

        // Ligar para quem virá na semana que vem gasta o crédito da ligação.
        (await _retencao.SumidosAsync(Hoje)).Should().BeEmpty();
    }

    [Fact]
    public async Task Quem_tem_horario_marcado_a_frente_nao_sumiu()
    {
        var pacienteId = await CriarPacienteAsync("Volta na quinta");
        await AtenderAsync(pacienteId, Hoje.AddDays(-200));
        await MarcarFuturoAsync(pacienteId, Hoje.AddDays(4).ToDateTime(new TimeOnly(14, 0)));

        // Ele já voltou — só ainda não veio.
        (await _retencao.SumidosAsync(Hoje)).Should().BeEmpty();
    }

    [Fact]
    public async Task Horario_passado_nao_conta_como_volta()
    {
        var pacienteId = await CriarPacienteAsync("Maria");
        await AtenderAsync(pacienteId, Hoje.AddDays(-200));
        await MarcarFuturoAsync(pacienteId, Hoje.AddDays(-30).ToDateTime(new TimeOnly(14, 0)));

        (await _retencao.SumidosAsync(Hoje)).Should().ContainSingle();
    }

    [Fact]
    public async Task Paciente_de_tratamento_aparece_primeiro()
    {
        var visita = await CriarPacienteAsync("Veio uma vez");
        await AtenderAsync(visita, Hoje.AddDays(-100));

        var tratamento = await CriarPacienteAsync("Tratamento longo");
        for (var i = 0; i < 8; i++)
            await AtenderAsync(tratamento, Hoje.AddDays(-150 + i));

        var lista = await _retencao.SumidosAsync(Hoje);

        lista.Should().HaveCount(2);
        lista[0].Nome.Should().Be("Tratamento longo");
        lista[0].EraFrequente.Should().BeTrue();
        lista[1].EraFrequente.Should().BeFalse();
    }

    [Fact]
    public async Task Janela_maior_recorta_quem_sumiu_ha_mais_tempo()
    {
        var recente = await CriarPacienteAsync("Sumiu faz 70 dias");
        await AtenderAsync(recente, Hoje.AddDays(-70));

        var antigo = await CriarPacienteAsync("Sumiu faz um ano");
        await AtenderAsync(antigo, Hoje.AddDays(-365));

        (await _retencao.SumidosAsync(Hoje, diasMinimos: 60)).Should().HaveCount(2);

        var soAntigos = await _retencao.SumidosAsync(Hoje, diasMinimos: 180);
        soAntigos.Should().ContainSingle();
        soAntigos[0].Nome.Should().Be("Sumiu faz um ano");
    }

    [Fact]
    public async Task Paciente_sem_nenhum_atendimento_nao_entra()
    {
        // Cadastrado e nunca atendido não "parou de vir" — nunca começou.
        await CriarPacienteAsync("Só cadastrado");

        (await _retencao.SumidosAsync(Hoje, diasMinimos: 1)).Should().BeEmpty();
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }
}
