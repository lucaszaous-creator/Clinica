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
/// A grade da agenda: a visão por SALA e o vão FECHADO (parcela 63).
///
/// As duas metades da feature 02 que faltavam, e as duas pelo mesmo motivo — o dado
/// existia e a grade não o usava:
///
/// 1. **`Agendamento.SalaId`** era gravado, respeitado no choque com a capacidade da sala
///    e bloqueável por período desde a parcela 26, e **nenhum modo de grade o usava como
///    coluna**. A planilha marcava ✅ porque o CAMPO existe; campo não é tela.
/// 2. **O bloqueio** era recusado pelo `AgendaService` no Salvar, e o vão fechado era
///    visualmente idêntico ao livre. O vão livre é clicável desde a parcela 58, então a
///    recepcionista escolhia o paciente, preenchia o formulário e levava a recusa com ele
///    na frente dela.
///
/// O que se testa aqui é a REGRA que a grade consulta, não o XAML: o agrupamento por sala
/// e o alcance do fechamento por recurso. O desenho fica para o olho; o que não pode
/// mudar sem alguém notar é qual horário cai em qual coluna e qual vão está fechado.
/// </summary>
public class AgendaGradeTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly AgendaService _agenda;
    private readonly BloqueioAgendaService _bloqueios;
    private readonly EquipeService _equipe;

    private static readonly DateTime Manha = new(2026, 8, 10, 9, 0, 0);


    /// <summary>
    /// Todo horário MARCADO precisa de dono desde a parcela 95 — o fixture cria um para os
    /// cenários que não se importam com QUEM atende.
    /// </summary>
    private readonly int _profPadrao;

    public AgendaGradeTests()
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
        _equipe = new EquipeService(_repo);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }

    // ==================== A visão por SALA ====================

    /// <summary>
    /// O horário cai na coluna da sala dele. É o mínimo que a visão precisa acertar, e é
    /// o que nenhuma tela fazia: a leitura do dia já trazia `SalaId` e a grade agrupava
    /// tudo por profissional.
    /// </summary>
    [Fact]
    public async Task Horario_com_sala_cai_na_coluna_daquela_sala()
    {
        var pacienteId = await CriarPacienteAsync();
        var infusao = await CriarSalaAsync("Infusão");
        var agulha = await CriarSalaAsync("Acupuntura 1");

        await _agenda.AgendarAsync(
            pacienteId, Manha, ModalidadeAtendimento.AcupunturaComEletro, null, salaId: infusao, profissionalId: _profPadrao);

        var doDia = await _agenda.DoDiaAsync(DateOnly.FromDateTime(Manha));

        doDia.Where(a => a.SalaId == infusao).Should().ContainSingle();
        doDia.Where(a => a.SalaId == agulha).Should().BeEmpty();
    }

    /// <summary>
    /// **"Sem sala" é o caso NORMAL, não resíduo.** Metade dos horários da clínica não
    /// informa sala — o faturamento nunca informou, e o balcão só informa quando a sala
    /// importa. Se a coluna "Sem sala" não existisse, a visão por sala mostraria um dia
    /// cheio como se estivesse vazio, que é a pior resposta possível para "cabe às 14h?".
    /// </summary>
    [Fact]
    public async Task Horario_sem_sala_nao_desaparece_da_visao_por_sala()
    {
        var pacienteId = await CriarPacienteAsync();
        await CriarSalaAsync("Infusão");

        await _agenda.AgendarAsync(
            pacienteId, Manha, ModalidadeAtendimento.AcupunturaComEletro, null, profissionalId: _profPadrao);

        var doDia = await _agenda.DoDiaAsync(DateOnly.FromDateTime(Manha));

        doDia.Should().ContainSingle();
        doDia.Where(a => a.SalaId is null).Should()
            .ContainSingle("é o que alimenta a coluna \"Sem sala\" — sem ela o dia pareceria vazio");
    }

    /// <summary>
    /// As salas INATIVAS não viram coluna: a clínica desativa a sala que deixou de
    /// existir, e uma coluna permanentemente vazia na grade é ruído que não se resolve.
    /// </summary>
    [Fact]
    public async Task Sala_inativa_nao_vira_coluna()
    {
        await CriarSalaAsync("Infusão");
        var desativada = await CriarSalaAsync("Sala antiga");

        var sala = await _db.Salas.FindAsync(desativada);
        sala!.Ativa = false;
        await _db.SaveChangesAsync();

        var ativas = await _equipe.SalasAtivasAsync();

        ativas.Should().ContainSingle().Which.Nome.Should().Be("Infusão");
    }

    // ==================== O vão FECHADO ====================

    /// <summary>
    /// O fechamento do período chega à grade numa leitura só.
    ///
    /// É por PERÍODO e não por vão de propósito: um dia com seis colunas e 26 faixas são
    /// ~150 células, e perguntar ao banco por célula seriam 150 idas a um banco remoto
    /// para desenhar uma tela. Havia um `BloqueioDoHorarioAsync` que respondia por um vão
    /// só e nunca teve chamador — foi o desenho errado que ficou pronto.
    /// </summary>
    [Fact]
    public async Task Bloqueio_do_dia_chega_a_grade_numa_leitura_so()
    {
        var profissionalId = await CriarProfissionalAsync();

        await _bloqueios.CriarAsync(
            Manha.Date.AddHours(8), Manha.Date.AddHours(12), "Férias da Ana",
            profissionalId: profissionalId);

        var doDia = await _bloqueios.NoPeriodoAsync(Manha.Date, Manha.Date.AddDays(1));

        doDia.Should().ContainSingle().Which.Motivo.Should().Be("Férias da Ana");
    }

    /// <summary>
    /// **O bloqueio da SALA não fecha o vão do profissional, e vice-versa.** É a regra que
    /// decide qual célula fica cinza em cada visão: a sala 2 em manutenção não impede a
    /// Ana de atender — ela usa outra sala. Pintar as duas colunas faria a grade recusar
    /// horário que a clínica pode marcar, que é pior do que não avisar nada (a lição do
    /// formato do número da guia: regra apertada demais trava o dia e o balcão não tem
    /// como contornar).
    /// </summary>
    [Fact]
    public async Task Fechamento_alcanca_o_recurso_dele_e_nao_o_vizinho()
    {
        var ana = await CriarProfissionalAsync("Dra. Ana");
        var bruno = await CriarProfissionalAsync("Dr. Bruno");
        var sala2 = await CriarSalaAsync("Sala 2");

        await _bloqueios.CriarAsync(
            Manha.Date, Manha.Date.AddDays(1), "Férias da Ana", profissionalId: ana);
        await _bloqueios.CriarAsync(
            Manha.Date, Manha.Date.AddDays(1), "Manutenção", salaId: sala2);

        var doDia = await _bloqueios.NoPeriodoAsync(Manha.Date, Manha.Date.AddDays(1));

        // Na grade por PROFISSIONAL: a coluna da Ana fecha, a do Bruno não.
        doDia.Any(b => b.AlcancaRecurso(ana, null)).Should().BeTrue();
        doDia.Any(b => b.AlcancaRecurso(bruno, null)).Should().BeFalse(
            "a manutenção da sala 2 não impede o Bruno de atender noutra sala");

        // Na grade por SALA: a sala 2 fecha; as férias da Ana não fecham sala nenhuma.
        doDia.Any(b => b.AlcancaRecurso(null, sala2)).Should().BeTrue();
        doDia.Any(b => b.AlcancaRecurso(null, salaId: 999)).Should().BeFalse();
    }

    /// <summary>
    /// O bloqueio da CLÍNICA fecha todas as colunas das três visões — é o feriado, e não
    /// há recurso que escape dele.
    /// </summary>
    [Fact]
    public async Task Fechamento_da_clinica_fecha_toda_coluna()
    {
        var ana = await CriarProfissionalAsync();
        var sala = await CriarSalaAsync("Infusão");

        await _bloqueios.CriarAsync(Manha.Date, Manha.Date.AddDays(1), "Feriado");

        var feriado = (await _bloqueios.NoPeriodoAsync(Manha.Date, Manha.Date.AddDays(1))).Single();

        feriado.DaClinica.Should().BeTrue();
        feriado.AlcancaRecurso(ana, null).Should().BeTrue();
        feriado.AlcancaRecurso(null, sala).Should().BeTrue();
        feriado.AlcancaRecurso(null, null).Should().BeTrue(
            "a coluna \"Sem profissional\" também está dentro do feriado");
    }

    /// <summary>
    /// **Fim exclusivo dos dois lados.** O vão das 12h não está dentro de um bloqueio que
    /// termina às 12h — senão a grade pintaria de cinza a primeira faixa livre depois das
    /// férias, e a recepção acharia que a tarde toda continua fechada.
    /// </summary>
    [Fact]
    public async Task O_vao_que_comeca_quando_o_fechamento_acaba_esta_aberto()
    {
        var ana = await CriarProfissionalAsync();

        await _bloqueios.CriarAsync(
            Manha.Date.AddHours(8), Manha.Date.AddHours(12), "Folga", profissionalId: ana);

        var bloqueio = (await _bloqueios.NoPeriodoAsync(Manha.Date, Manha.Date.AddDays(1))).Single();

        // A faixa de meia hora que COMEÇA às 11h30 ainda é dentro.
        bloqueio.ColideCom(Manha.Date.AddHours(11.5), Manha.Date.AddHours(12)).Should().BeTrue();

        // A que começa às 12h, não.
        bloqueio.ColideCom(Manha.Date.AddHours(12), Manha.Date.AddHours(12.5)).Should().BeFalse();
    }

    // ==================== Cenário ====================

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

    private async Task<int> CriarSalaAsync(string nome)
    {
        var s = new Sala { Nome = nome, Capacidade = 1 };
        _db.Salas.Add(s);
        await _db.SaveChangesAsync();
        return s.Id;
    }
}
