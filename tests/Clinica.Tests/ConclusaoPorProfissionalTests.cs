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

    private async Task<(Profissional Medica, Agendamento Horario)> CenarioAsync(
        int? horas = 10, bool chegou = true, StatusAgendamento status = StatusAgendamento.Agendado)
    {
        var medica = new Profissional { Nome = "Dra. Fictícia da Silva", NomeCurto = "Dra. Fictícia", Ativo = true, ConcluirAutomaticamenteHoras = horas };
        _db.Profissionais.Add(medica);
        await _db.SaveChangesAsync();
        var horario = new Agendamento
        {
            Paciente = new Paciente { Nome = "Paciente Exemplo", Convenio = Convenio.UnimedPadrao },
            ProfissionalId = medica.Id, DataHora = Horario, Status = status,
            ModalidadePrevista = ModalidadeAtendimento.AcupunturaComEletro,
            ChegadaEm = chegou ? Horario.AddMinutes(-10) : null
        };
        _db.Agendamentos.Add(horario);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        return (medica, horario);
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

        await equipe.SalvarProfissionalAsync(new Profissional { Id = medica.Id, Nome = lida.Nome, Ativo = true, ConcluirAutomaticamenteHoras = 0 });
        Assert.Null((await _db.Profissionais.AsNoTracking().SingleAsync(p => p.Id == medica.Id)).ConcluirAutomaticamenteHoras);

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
