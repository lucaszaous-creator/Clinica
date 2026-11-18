using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// A conclusão automática POR PROFISSIONAL (nov/2026): quem não usa o sistema tem as sessões com
/// chegada concluídas N horas depois do horário, pelo mesmo núcleo do "Concluir". Dados fictícios.
/// </summary>
public sealed class ConclusaoPorProfissionalTests : IDisposable
{
    private static readonly DateTime Horario = new(2026, 11, 10, 9, 0, 0);
    private static readonly DateTime DezHorasDepois = Horario.AddHours(10);

    private readonly SqliteConnection _conexao = new("Data Source=:memory:");
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;

    public ConclusaoPorProfissionalTests()
    {
        _conexao.Open();
        _db = new(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conexao).Options);
        _db.Database.EnsureCreated();
        _repo = new(_db);
    }

    public void Dispose() { _db.Dispose(); _conexao.Dispose(); }

    private ConclusaoAutomaticaService Servico => new(_db, _repo, new(_repo, new(_repo)), new(_repo));

    private static readonly DateTime APartirDe = new(2026, 11, 1);

    private async Task<(Profissional Medica, Agendamento Horario)> CenarioAsync(
        int? horas = 10, bool chegou = true, StatusAgendamento status = StatusAgendamento.Agendado,
        DateTime? horario = null, bool semData = false)
    {
        var medica = new Profissional { Nome = "Dra. Fictícia da Silva", NomeCurto = "Dra. Fictícia", Ativo = true,
            ConcluirAutomaticamenteHoras = horas, ConcluirAutomaticamenteDesde = semData ? null : APartirDe };
        _db.Profissionais.Add(medica);
        await _db.SaveChangesAsync();
        var quando = horario ?? Horario;
        var h = await NovoHorarioAsync(medica.Id, quando, chegou, status);
        _db.ChangeTracker.Clear();
        return (medica, h);
    }

    private async Task<Agendamento> NovoHorarioAsync(int profissionalId, DateTime quando, bool chegou = true,
        StatusAgendamento status = StatusAgendamento.Agendado)
    {
        var horario = new Agendamento
        {
            Paciente = new Paciente { Nome = "Paciente Exemplo", Convenio = Convenio.UnimedPadrao },
            ProfissionalId = profissionalId, DataHora = quando, Status = status,
            ModalidadePrevista = ModalidadeAtendimento.AcupunturaComEletro,
            ChegadaEm = chegou ? quando.AddMinutes(-10) : null
        };
        _db.Agendamentos.Add(horario);
        await _db.SaveChangesAsync();
        return horario;
    }

    private Task<Agendamento> RelerAsync(int id) => _db.Agendamentos.AsNoTracking().SingleAsync(a => a.Id == id);

    [Fact]
    public async Task Com_chegada_conclui_as_19h_e_gera_os_codigos_pelo_mesmo_nucleo()
    {
        var (_, h) = await CenarioAsync();

        Assert.Empty(await Servico.CandidatosPorProfissionalAsync(DezHorasDepois.AddMinutes(-1)));
        Assert.False(await Servico.ConcluirPorProfissionalAsync(h.Id, DezHorasDepois.AddMinutes(-1)));
        Assert.Equal([h.Id], await Servico.CandidatosPorProfissionalAsync(DezHorasDepois));
        Assert.True(await Servico.ConcluirPorProfissionalAsync(h.Id, DezHorasDepois));

        var concluido = await RelerAsync(h.Id);
        Assert.Equal(StatusAgendamento.Realizado, concluido.Status);
        Assert.Equal(DezHorasDepois, concluido.ConcluidoAutomaticamenteEm);
        Assert.NotNull(concluido.FimAtendimentoEm);
        Assert.NotNull(concluido.AtendimentoId);
        Assert.NotEmpty(await _db.Codigos.AsNoTracking().Where(c => c.AtendimentoId == concluido.AtendimentoId).ToListAsync());

        // Sem prontuário: nenhuma evolução nasce da conclusão automática.
        Assert.Empty(await _db.Evolucoes.AsNoTracking().ToListAsync());

        // Quem assina é o sistema, com a regra e a hora — nunca um usuário humano.
        var trilha = await _db.Auditoria.AsNoTracking().SingleAsync(e => e.Acao == "ConclusaoAutomaticaPorProfissional");
        Assert.Equal("Conclusão automática (Dra. Fictícia, 10h)", trilha.Operador);
        Assert.Contains("concluída às 10/11/2026 19:00", trilha.Detalhe);
        Assert.Contains(await _db.Auditoria.AsNoTracking().Where(e => e.Acao == "PresencaConfirmada").Select(e => e.Operador).ToListAsync(),
            o => o == "Conclusão automática (Dra. Fictícia, 10h)");
    }

    [Fact]
    public async Task Rodar_duas_vezes_conclui_uma_vez_so_e_nao_duplica_codigo()
    {
        var (_, h) = await CenarioAsync();
        Assert.True(await Servico.ConcluirPorProfissionalAsync(h.Id, DezHorasDepois));
        var codigos = await _db.Codigos.AsNoTracking().OrderBy(c => c.Id).Select(c => c.Id).ToArrayAsync();

        Assert.False(await Servico.ConcluirPorProfissionalAsync(h.Id, DezHorasDepois.AddHours(1)));
        Assert.Empty(await Servico.CandidatosPorProfissionalAsync(DezHorasDepois.AddHours(1)));

        Assert.Equal(codigos, await _db.Codigos.AsNoTracking().OrderBy(c => c.Id).Select(c => c.Id).ToArrayAsync());
        Assert.Single(await _db.Atendimentos.AsNoTracking().ToListAsync());
        Assert.Single(await _db.Auditoria.AsNoTracking().Where(e => e.Acao == "ConclusaoAutomaticaPorProfissional").ToListAsync());
    }

    [Fact]
    public async Task Sem_chegada_nao_conclui_e_vira_aviso_para_a_recepcao()
    {
        var (medica, h) = await CenarioAsync(chegou: false);

        Assert.Empty(await Servico.CandidatosPorProfissionalAsync(DezHorasDepois));
        Assert.False(await Servico.ConcluirPorProfissionalAsync(h.Id, DezHorasDepois));
        Assert.Equal(StatusAgendamento.Agendado, (await RelerAsync(h.Id)).Status);
        Assert.Empty(await _db.Codigos.AsNoTracking().ToListAsync());

        Assert.Empty(await Servico.SemChegadaAsync(DezHorasDepois.AddMinutes(-1)));
        var aviso = Assert.Single(await Servico.SemChegadaAsync(DezHorasDepois));
        Assert.Equal(medica.Id, aviso.ProfissionalId);
        Assert.Equal(1, aviso.Quantos);
        Assert.Equal("1 horário sem chegada na agenda de Dra. Fictícia: confirme falta ou presença.", aviso.Frase);
    }

    [Theory]
    [InlineData(StatusAgendamento.Cancelado)]
    [InlineData(StatusAgendamento.Faltou)]
    public async Task Cancelado_ou_falta_nao_conclui_nem_avisa(StatusAgendamento status)
    {
        var (_, h) = await CenarioAsync(status: status);
        Assert.Empty(await Servico.CandidatosPorProfissionalAsync(DezHorasDepois));
        Assert.False(await Servico.ConcluirPorProfissionalAsync(h.Id, DezHorasDepois));
        Assert.Empty(await Servico.SemChegadaAsync(DezHorasDepois));
        Assert.Equal(status, (await RelerAsync(h.Id)).Status);
        Assert.Empty(await _db.Codigos.AsNoTracking().ToListAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public async Task Profissional_sem_a_regra_nada_acontece(int? horas)
    {
        var (_, h) = await CenarioAsync(horas: horas);
        Assert.Empty(await Servico.CandidatosPorProfissionalAsync(DezHorasDepois.AddDays(2)));
        Assert.False(await Servico.ConcluirPorProfissionalAsync(h.Id, DezHorasDepois.AddDays(2)));
        Assert.Empty(await Servico.SemChegadaAsync(DezHorasDepois.AddDays(2)));
        Assert.Equal(StatusAgendamento.Agendado, (await RelerAsync(h.Id)).Status);
    }

    [Fact]
    public async Task Antes_da_data_nao_conclui_nem_avisa_e_depois_dela_conclui_na_primeira_volta()
    {
        // A gerente grava 10 h a partir de 01/11 em 18/11: 30/10 fica intocado; 05/11 conclui na primeira volta.
        var gravadaEm = new DateTime(2026, 11, 18, 10, 0, 0);
        var (medica, antes) = await CenarioAsync(horario: new DateTime(2026, 10, 30, 9, 0, 0));
        var semChegadaAntes = await NovoHorarioAsync(medica.Id, new DateTime(2026, 10, 31, 9, 0, 0), chegou: false);
        var depois = await NovoHorarioAsync(medica.Id, new DateTime(2026, 11, 5, 9, 0, 0));
        _db.ChangeTracker.Clear();

        Assert.Equal([depois.Id], await Servico.CandidatosPorProfissionalAsync(gravadaEm));
        Assert.False(await Servico.ConcluirPorProfissionalAsync(antes.Id, gravadaEm));
        Assert.True(await Servico.ConcluirPorProfissionalAsync(depois.Id, gravadaEm));

        Assert.Equal(StatusAgendamento.Agendado, (await RelerAsync(antes.Id)).Status);
        Assert.Null((await RelerAsync(antes.Id)).ConcluidoAutomaticamenteEm);
        Assert.Equal(StatusAgendamento.Realizado, (await RelerAsync(depois.Id)).Status);
        Assert.Empty(await Servico.SemChegadaAsync(gravadaEm)); // 31/10 sem chegada: antes da data, sem aviso
        Assert.Equal(StatusAgendamento.Agendado, (await RelerAsync(semChegadaAntes.Id)).Status);
    }

    [Fact]
    public async Task Data_em_outubro_gravada_em_novembro_conclui_desde_a_data_e_nada_antes()
    {
        // Mais velho que os 30 dias do sino: a conclusão vai até a data "a partir de" (teto de 120 dias).
        var hoje = new DateTime(2026, 11, 18, 10, 0, 0);
        var medica = new Profissional { Nome = "Dra. Fictícia da Silva", NomeCurto = "Dra. Fictícia", Ativo = true,
            ConcluirAutomaticamenteHoras = 10, ConcluirAutomaticamenteDesde = new DateTime(2026, 10, 1) };
        _db.Profissionais.Add(medica);
        await _db.SaveChangesAsync();
        var antes = await NovoHorarioAsync(medica.Id, new DateTime(2026, 9, 30, 9, 0, 0));
        var depois = await NovoHorarioAsync(medica.Id, new DateTime(2026, 10, 2, 9, 0, 0));
        _db.ChangeTracker.Clear();

        Assert.Equal([depois.Id], await Servico.CandidatosPorProfissionalAsync(hoje));
        Assert.True(await Servico.ConcluirPorProfissionalAsync(depois.Id, hoje));
        Assert.False(await Servico.ConcluirPorProfissionalAsync(antes.Id, hoje));
        Assert.Equal(StatusAgendamento.Realizado, (await RelerAsync(depois.Id)).Status);
        Assert.Equal(StatusAgendamento.Agendado, (await RelerAsync(antes.Id)).Status);
    }

    [Fact]
    public async Task Teto_de_120_dias_vale_mesmo_com_data_mais_antiga()
    {
        var hoje = new DateTime(2026, 11, 18, 10, 0, 0);
        var medica = new Profissional { Nome = "Dra. Fictícia da Silva", Ativo = true,
            ConcluirAutomaticamenteHoras = 10, ConcluirAutomaticamenteDesde = new DateTime(2026, 1, 1) };
        _db.Profissionais.Add(medica);
        await _db.SaveChangesAsync();
        var velho = await NovoHorarioAsync(medica.Id, hoje.Date.AddDays(-ConclusaoPorProfissional.DiasMaximoRetroativo - 1).AddHours(9));
        var dentro = await NovoHorarioAsync(medica.Id, hoje.Date.AddDays(-ConclusaoPorProfissional.DiasMaximoRetroativo).AddHours(9));
        _db.ChangeTracker.Clear();

        Assert.Equal([dentro.Id], await Servico.CandidatosPorProfissionalAsync(hoje));
        Assert.False(await Servico.ConcluirPorProfissionalAsync(velho.Id, hoje));
        Assert.Equal(StatusAgendamento.Agendado, (await RelerAsync(velho.Id)).Status);
    }

    [Fact]
    public async Task Primeiro_de_novembro_as_9h_com_chegada_conclui_as_19h()
    {
        var (_, h) = await CenarioAsync(horario: new DateTime(2026, 11, 1, 9, 0, 0));
        Assert.False(await Servico.ConcluirPorProfissionalAsync(h.Id, new DateTime(2026, 11, 1, 18, 59, 0)));
        Assert.True(await Servico.ConcluirPorProfissionalAsync(h.Id, new DateTime(2026, 11, 1, 19, 0, 0)));
    }

    [Fact]
    public async Task Sem_data_gravada_a_regra_nao_anda_para_tras()
    {
        // Horas preenchidas sem data (gravado por fora do serviço): nada, nem aviso.
        var (_, h) = await CenarioAsync(semData: true);
        Assert.Empty(await Servico.CandidatosPorProfissionalAsync(DezHorasDepois));
        Assert.False(await Servico.ConcluirPorProfissionalAsync(h.Id, DezHorasDepois));
        Assert.Empty(await Servico.SemChegadaAsync(DezHorasDepois));
    }

    [Fact]
    public async Task Cadastro_sem_data_grava_o_dia_da_gravacao_e_nao_conclui_o_que_veio_antes()
    {
        var medica = new Profissional { Nome = "Dra. Fictícia da Silva", NomeCurto = "Dra. Fictícia", Ativo = true };
        _db.Profissionais.Add(medica);
        await _db.SaveChangesAsync();
        var ontem = DateTime.Today.AddDays(-1).AddHours(9);
        var h = await NovoHorarioAsync(medica.Id, ontem);
        _db.ChangeTracker.Clear();

        await new EquipeService(_repo).SalvarProfissionalAsync(
            new Profissional { Id = medica.Id, Nome = medica.Nome, Ativo = true, ConcluirAutomaticamenteHoras = 10 });
        var gravado = await _db.Profissionais.AsNoTracking().SingleAsync(p => p.Id == medica.Id);
        Assert.Equal(DateTime.Today, gravado.ConcluirAutomaticamenteDesde);

        _db.ChangeTracker.Clear();
        Assert.Empty(await Servico.CandidatosPorProfissionalAsync(DateTime.Now.AddHours(1)));
        Assert.False(await Servico.ConcluirPorProfissionalAsync(h.Id, DateTime.Now.AddHours(1)));
        Assert.Equal(StatusAgendamento.Agendado, (await RelerAsync(h.Id)).Status);
    }

    [Fact]
    public void Sessao_concluida_pelo_sistema_nao_e_pendencia_de_evolucao_da_medica()
    {
        var dia = PoliticaRegistroPendente.DataInicio.AddDays(30).ToDateTime(new TimeOnly(9, 0));
        var sessao = new SessaoDoDia(1, dia, 1, "Paciente Exemplo", "Consulta", null, StatusAgendamento.Realizado,
            EtapaFila.Finalizado, false, 10, null);
        Assert.True(sessao.RegistroPendente);
        Assert.False((sessao with { ConcluidoAutomaticamenteEm = dia.AddHours(10) }).RegistroPendente);
    }

    [Fact]
    public async Task Cadastro_zero_desliga_nulo_preserva_e_fora_da_faixa_recusa()
    {
        var (medica, _) = await CenarioAsync();
        var equipe = new EquipeService(_repo);
        var lida = (await equipe.ObterProfissionalAsync(medica.Id))!;

        // A tela de Acessos regrava o profissional sem saber deste campo: nulo preserva.
        await equipe.SalvarProfissionalAsync(new Profissional { Id = medica.Id, Nome = lida.Nome, Ativo = true });
        Assert.Equal(10, (await _db.Profissionais.AsNoTracking().SingleAsync(p => p.Id == medica.Id)).ConcluirAutomaticamenteHoras);

        Assert.Equal(APartirDe, (await _db.Profissionais.AsNoTracking().SingleAsync(p => p.Id == medica.Id)).ConcluirAutomaticamenteDesde);

        // A tela manda horas sem data: a data que já estava fica (não avança a cada salvar).
        await equipe.SalvarProfissionalAsync(new Profissional { Id = medica.Id, Nome = lida.Nome, Ativo = true, ConcluirAutomaticamenteHoras = 12 });
        Assert.Equal(APartirDe, (await _db.Profissionais.AsNoTracking().SingleAsync(p => p.Id == medica.Id)).ConcluirAutomaticamenteDesde);

        await equipe.SalvarProfissionalAsync(new Profissional { Id = medica.Id, Nome = lida.Nome, Ativo = true, ConcluirAutomaticamenteHoras = 0 });
        var desligado = await _db.Profissionais.AsNoTracking().SingleAsync(p => p.Id == medica.Id);
        Assert.Null(desligado.ConcluirAutomaticamenteHoras);
        Assert.Null(desligado.ConcluirAutomaticamenteDesde);

        await Assert.ThrowsAsync<InvalidOperationException>(() => equipe.SalvarProfissionalAsync(
            new Profissional { Id = medica.Id, Nome = lida.Nome, Ativo = true, ConcluirAutomaticamenteHoras = Profissional.ConcluirAutomaticamenteHorasMaximo + 1 }));
    }

    [Fact]
    public void Operador_e_frases_cabem_e_dizem_quem_agiu()
    {
        var longo = new Profissional { Nome = new string('A', 200) };
        Assert.True(ConclusaoPorProfissional.Operador(longo, 72).Length <= 80);
        Assert.Equal("Conclusão automática (Dra. Ana, 10h)", ConclusaoPorProfissional.Operador(new() { Nome = "Ana Exemplo", NomeCurto = "Dra. Ana" }, 10));
        Assert.Equal("3 horários sem chegada na agenda de Dra. Ana: confirme falta ou presença.", ConclusaoPorProfissional.FraseSemChegada("Dra. Ana", 3));
        Assert.Null(ConclusaoPorProfissional.Horas(new() { ConcluirAutomaticamenteHoras = Profissional.ConcluirAutomaticamenteHorasMaximo + 1 }));
        Assert.True(ConclusaoPorProfissional.Venceu(Horario, 10, DezHorasDepois));
        Assert.False(ConclusaoPorProfissional.Venceu(Horario, 10, DezHorasDepois.AddSeconds(-1)));
    }
}
