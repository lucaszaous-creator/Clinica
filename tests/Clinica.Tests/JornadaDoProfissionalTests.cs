using Clinica.Application.Modelos;
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
/// A jornada do profissional (set/2026): dias e horário de atendimento no cadastro, que a
/// grade pinta, a marcação respeita (salvo encaixe) e a ocupação usa como base.
///
/// A regra que vale em toda parte: sem jornada declarada, NADA muda — é o que toda linha
/// já gravada vale, e é o que protege quem não preencher.
/// </summary>
public class JornadaDoProfissionalTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly EquipeService _equipe;
    private readonly AgendaService _agenda;

    // Uma segunda-feira.
    private static readonly DateTime Segunda9h = new(2026, 9, 14, 9, 0, 0);

    public JornadaDoProfissionalTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _equipe = new EquipeService(_repo);
        _agenda = new AgendaService(_repo, new AtendimentoService(_repo));
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }

    private static readonly TimeOnly Oito = new(8, 0);
    private static readonly TimeOnly Doze = new(12, 0);

    private static int SegQuaSex =>
        Profissional.BitDe(DayOfWeek.Monday) | Profissional.BitDe(DayOfWeek.Wednesday) | Profissional.BitDe(DayOfWeek.Friday);

    // ==================== A entidade ====================

    [Fact]
    public void Sem_jornada_declarada_tudo_cabe()
    {
        var p = new Profissional { Nome = "Ana" };

        p.JornadaDeclarada.Should().BeFalse();
        p.AtendeEm(DayOfWeek.Sunday).Should().BeTrue();
        p.DentroDoExpediente(Segunda9h, Segunda9h.AddMinutes(30)).Should().BeTrue();
        p.MinutosDeJornada.Should().BeNull();
        p.DescricaoJornada.Should().BeNull();
    }

    [Fact]
    public void Dias_e_horario_decidem_o_que_cabe()
    {
        var p = new Profissional { Nome = "Ana", DiasDeAtendimento = SegQuaSex, AtendeDas = Oito, AtendeAte = Doze };

        p.DentroDoExpediente(Segunda9h, Segunda9h.AddMinutes(30)).Should().BeTrue();
        // Termina exatamente no fim do expediente: cabe.
        p.DentroDoExpediente(Segunda9h.AddHours(2).AddMinutes(30), Segunda9h.AddHours(3)).Should().BeTrue();
        // Começa dentro e termina depois do fim: não cabe.
        p.DentroDoExpediente(Segunda9h.AddHours(2).AddMinutes(45), Segunda9h.AddHours(3).AddMinutes(15)).Should().BeFalse();
        // Antes do início: não cabe.
        p.DentroDoExpediente(Segunda9h.AddHours(-2), Segunda9h.AddHours(-1).AddMinutes(30)).Should().BeFalse();
        // Terça não é dia dele, mesmo na hora certa.
        p.DentroDoExpediente(Segunda9h.AddDays(1), Segunda9h.AddDays(1).AddMinutes(30)).Should().BeFalse();
        p.MinutosDeJornada.Should().Be(240);
        p.DescricaoJornada.Should().Be("seg · qua · sex, das 08:00 às 12:00");
    }

    [Fact]
    public void Descricao_diz_todos_os_dias_e_so_o_que_foi_declarado()
    {
        new Profissional { Nome = "A", AtendeDas = Oito, AtendeAte = Doze }
            .DescricaoJornada.Should().Be("das 08:00 às 12:00");
        new Profissional { Nome = "A", DiasDeAtendimento = Profissional.TodosOsDias }
            .DescricaoJornada.Should().Be("todos os dias");
        new Profissional { Nome = "A", DiasDeAtendimento = Profissional.BitDe(DayOfWeek.Saturday) }
            .DescricaoJornada.Should().Be("sáb");
    }

    [Fact]
    public void Sessao_que_atravessa_a_meia_noite_nunca_cabe()
    {
        var p = new Profissional { Nome = "A", AtendeDas = new TimeOnly(0, 0), AtendeAte = new TimeOnly(23, 59) };
        var inicio = new DateTime(2026, 9, 14, 23, 30, 0);

        p.DentroDoExpediente(inicio, inicio.AddHours(1)).Should().BeFalse();
    }

    // ==================== O cadastro ====================

    [Fact]
    public async Task Jornada_e_gravada_e_lida_de_volta()
    {
        var salvo = await _equipe.SalvarProfissionalAsync(new Profissional
        {
            Nome = "Dra. Ana", DiasDeAtendimento = SegQuaSex, AtendeDas = Oito, AtendeAte = Doze
        });

        var lido = await _equipe.ObterProfissionalAsync(salvo.Id);
        lido!.DiasDeAtendimento.Should().Be(SegQuaSex);
        lido.AtendeDas.Should().Be(Oito);
        lido.AtendeAte.Should().Be(Doze);
    }

    /// <summary>
    /// O lugar 3 da lista de conferência: a cópia campo a campo do serviço. Editar o nome
    /// não pode apagar a jornada — e "nenhum dia" vira "não declarado", não zero.
    /// </summary>
    [Fact]
    public async Task Editar_outro_campo_preserva_a_jornada_e_zero_dias_vira_nulo()
    {
        var salvo = await _equipe.SalvarProfissionalAsync(new Profissional
        {
            Nome = "Dra. Ana", DiasDeAtendimento = SegQuaSex, AtendeDas = Oito, AtendeAte = Doze
        });

        await _equipe.SalvarProfissionalAsync(new Profissional
        {
            Id = salvo.Id, Nome = "Dra. Ana Lima", DiasDeAtendimento = SegQuaSex, AtendeDas = Oito, AtendeAte = Doze
        });
        (await _equipe.ObterProfissionalAsync(salvo.Id))!.DiasDeAtendimento.Should().Be(SegQuaSex);

        await _equipe.SalvarProfissionalAsync(new Profissional { Id = salvo.Id, Nome = "Dra. Ana Lima", DiasDeAtendimento = 0 });
        var lido = await _equipe.ObterProfissionalAsync(salvo.Id);
        lido!.DiasDeAtendimento.Should().BeNull();
        lido.JornadaDeclarada.Should().BeFalse();
    }

    [Fact]
    public async Task Horario_pela_metade_ou_invertido_e_recusado()
    {
        await _equipe.Invoking(e => e.SalvarProfissionalAsync(new Profissional { Nome = "A", AtendeDas = Oito }))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*início E o fim*");
        await _equipe.Invoking(e => e.SalvarProfissionalAsync(new Profissional { Nome = "A", AtendeDas = Doze, AtendeAte = Oito }))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*depois do início*");
    }

    // ==================== A marcação ====================

    private async Task<int> PacienteAsync()
    {
        var p = new Paciente { Nome = "Maria" };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    [Fact]
    public async Task Marcar_fora_do_expediente_e_recusado_dizendo_quando_ele_atende()
    {
        var ana = await _equipe.SalvarProfissionalAsync(new Profissional
        {
            Nome = "Ana", NomeCurto = "Dra. Ana", DiasDeAtendimento = SegQuaSex, AtendeDas = Oito, AtendeAte = Doze
        });
        var paciente = await PacienteAsync();

        // Segunda às 14h: dia certo, hora errada.
        var acao = () => _agenda.AgendarAsync(
            paciente, Segunda9h.AddHours(5), ModalidadeAtendimento.Consulta, null, profissionalId: ana.Id);

        var erro = await acao.Should().ThrowAsync<InvalidOperationException>();
        erro.Which.Message.Should().Contain("Dra. Ana").And.Contain("seg · qua · sex, das 08:00 às 12:00");

        // Terça às 9h: hora certa, dia errado.
        await _agenda.Invoking(a => a.AgendarAsync(
                paciente, Segunda9h.AddDays(1), ModalidadeAtendimento.Consulta, null, profissionalId: ana.Id))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Dentro_do_expediente_marca_e_o_encaixe_fura_a_jornada()
    {
        var ana = await _equipe.SalvarProfissionalAsync(new Profissional
        {
            Nome = "Ana", DiasDeAtendimento = SegQuaSex, AtendeDas = Oito, AtendeAte = Doze
        });
        var paciente = await PacienteAsync();

        var dentro = await _agenda.AgendarAsync(
            paciente, Segunda9h, ModalidadeAtendimento.Consulta, null, profissionalId: ana.Id);
        dentro.Id.Should().BePositive();

        var outro = await PacienteAsync();
        var encaixe = await _agenda.AgendarAsync(
            outro, Segunda9h.AddHours(5), ModalidadeAtendimento.Consulta, null,
            profissionalId: ana.Id, encaixe: true);
        encaixe.Encaixe.Should().BeTrue();
    }

    [Fact]
    public async Task Sem_jornada_declarada_a_marcacao_continua_como_sempre()
    {
        var ana = await _equipe.SalvarProfissionalAsync(new Profissional { Nome = "Ana" });
        var paciente = await PacienteAsync();

        var domingoNoite = new DateTime(2026, 9, 13, 21, 0, 0);
        var ag = await _agenda.AgendarAsync(
            paciente, domingoNoite, ModalidadeAtendimento.Consulta, null, profissionalId: ana.Id);

        ag.Id.Should().BePositive();
    }

    /// <summary>A crítica que a tela mostra ANTES do clique nomeia o recurso novo.</summary>
    [Fact]
    public async Task A_critica_de_choque_lista_o_expediente_como_conflito()
    {
        var ana = await _equipe.SalvarProfissionalAsync(new Profissional
        {
            Nome = "Ana", DiasDeAtendimento = SegQuaSex, AtendeDas = Oito, AtendeAte = Doze
        });

        var conflitos = await _agenda.ConflitosAsync(Segunda9h.AddHours(5), profissionalId: ana.Id);

        conflitos.Should().ContainSingle(c => c.Recurso == RecursoAgenda.Expediente);
    }

    // ==================== A ocupação ====================

    /// <summary>
    /// Quem atende meio período era medido contra a jornada global de 480 min e saía com a
    /// ocupação pela metade do que trabalhou. Com a jornada declarada, a base é a dele.
    /// </summary>
    [Fact]
    public async Task Ocupacao_usa_a_jornada_declarada_e_a_global_para_quem_nao_declarou()
    {
        var meioPeriodo = await _equipe.SalvarProfissionalAsync(new Profissional
        {
            Nome = "Meio", AtendeDas = Oito, AtendeAte = Doze
        });
        var integral = await _equipe.SalvarProfissionalAsync(new Profissional { Nome = "Integral" });
        var paciente = await PacienteAsync();
        var outro = await PacienteAsync();

        await _agenda.AgendarAsync(paciente, Segunda9h, ModalidadeAtendimento.Consulta, null, profissionalId: meioPeriodo.Id);
        await _agenda.AgendarAsync(outro, Segunda9h, ModalidadeAtendimento.Consulta, null, profissionalId: integral.Id);

        var parametros = new ParametrosService(_repo);
        var painel = await new IndicadoresService(_repo, parametros).GerarAsync(
            DateOnly.FromDateTime(Segunda9h), DateOnly.FromDateTime(Segunda9h));

        var jornadaGlobal = await parametros.ObterJornadaDiariaAsync();
        painel.Agenda.MinutosDisponiveis.Should().Be(240 + jornadaGlobal);
        painel.Produtividade.Single(p => p.ProfissionalId == meioPeriodo.Id).MinutosDisponiveis.Should().Be(240);
        painel.Produtividade.Single(p => p.ProfissionalId == integral.Id).MinutosDisponiveis.Should().Be(jornadaGlobal);
    }
}
