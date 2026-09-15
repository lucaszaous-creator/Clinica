using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

public sealed class RetomadaAtendimentoTests : IDisposable
{
    private readonly SqliteConnection _conn = new("DataSource=:memory:");
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly AgendaService _agenda;
    private readonly ProntuarioService _prontuario;
    private readonly int _paciente, _profissional, _substituto;
    private static readonly DateOnly Dia = new(2026, 9, 14);

    public RetomadaAtendimentoTests()
    {
        _conn.Open();
        _db = NovoContexto();
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _agenda = new AgendaService(_repo, new AtendimentoService(_repo));
        _prontuario = new ProntuarioService(_repo);
        var paciente = new Paciente { Nome = "Paciente de teste", Convenio = Convenio.UnimedIntercambio };
        var profissional = new Profissional { Nome = "Profissional da agenda" };
        var substituto = new Profissional { Nome = "Profissional substituto" };
        _db.AddRange(paciente, profissional, substituto);
        _db.SaveChanges();
        _paciente = paciente.Id; _profissional = profissional.Id; _substituto = substituto.Id;
    }

    private ClinicaDbContext NovoContexto() => new(new DbContextOptionsBuilder<ClinicaDbContext>()
        .UseSqlite(_conn).Options);
    private Task<Agendamento> Marcar(DateOnly? dia = null) => _agenda.AgendarAsync(_paciente,
        (dia ?? Dia).ToDateTime(new TimeOnly(9, 0)), ModalidadeAtendimento.AcupunturaComEletro,
        null, profissionalId: _profissional);
    private FechamentoSessaoService Fechamento(ClinicaRepositorio? repo = null)
    {
        repo ??= _repo;
        return new(repo, new AgendaService(repo, new AtendimentoService(repo)),
            new PacoteService(repo), new EstoqueService(repo), new FinanceiroService(repo));
    }
    private Task<Evolucao> Escrever(int? horario, int? autor = null) => _prontuario.SalvarAsync(new Evolucao
    {
        PacienteId = _paciente, Data = Dia, AgendamentoId = horario,
        ProfissionalId = autor ?? _profissional, TextoEvolucao = "Evolução preservada"
    }, "teste");

    [Fact]
    public async Task Finalizar_salva_fim_presenca_guias_e_vinculo_e_aceita_repeticao()
    {
        var ag = await Marcar();
        await _agenda.IniciarAtendimentoAsync(ag.Id, "teste");
        var evo = await Escrever(ag.Id);
        var primeiro = await Fechamento().RegistrarAtendimentoAsync(ag.Id, "teste", concluirClinico: true);
        var fim = ag.FimAtendimentoEm;
        var segundo = await Fechamento().RegistrarAtendimentoAsync(ag.Id, "teste", concluirClinico: true);
        ag.Status.Should().Be(StatusAgendamento.Realizado);
        fim.Should().NotBeNull();
        ag.FimAtendimentoEm.Should().Be(fim);
        segundo.Atendimento.Id.Should().Be(primeiro.Atendimento.Id);
        evo.AtendimentoId.Should().Be(primeiro.Atendimento.Id);
        (await _db.Atendimentos.CountAsync()).Should().Be(1);
        (await _db.Evolucoes.CountAsync()).Should().Be(1);
        primeiro.GuiasGeradas.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Evolucao_escrita_sem_cronometro_finaliza_no_mesmo_horario_com_guias()
    {
        var ag = await Marcar();
        var evo = await Escrever(ag.Id);
        var resultado = await Fechamento().RegistrarAtendimentoAsync(ag.Id, "profissional", concluirClinico: true);
        _db.ChangeTracker.Clear();
        var horario = (await _repo.ObterAgendamentoAsync(ag.Id))!;
        horario.Status.Should().Be(StatusAgendamento.Realizado);
        horario.InicioAtendimentoEm.Should().BeNull("finalizar não inventa o início do cronômetro");
        horario.FimAtendimentoEm.Should().NotBeNull();
        (await _repo.ObterEvolucaoAsync(evo.Id))!.AtendimentoId.Should().Be(resultado.Atendimento.Id);
        resultado.GuiasGeradas.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Guias_preexistentes_sao_preservadas_ao_finalizar_registro_clinico()
    {
        var ag = await Marcar();
        var lancamento = await _agenda.ConfirmarPresencaAsync(ag.Id);
        var ids = lancamento.Atendimento.Codigos.Select(c => c.Id).Order().ToArray();
        var evo = await Escrever(ag.Id);
        var resultado = await Fechamento().RegistrarAtendimentoAsync(ag.Id, "profissional", concluirClinico: true);
        var repetido = await Fechamento().RegistrarAtendimentoAsync(ag.Id, "profissional", concluirClinico: true);
        ag.FimAtendimentoEm.Should().NotBeNull();
        resultado.Atendimento.Id.Should().Be(lancamento.Atendimento.Id).And.Be(repetido.Atendimento.Id);
        resultado.Atendimento.Codigos.Select(c => c.Id).Order().Should().Equal(ids);
        evo.AtendimentoId.Should().Be(resultado.Atendimento.Id);
        (await _db.Atendimentos.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Falha_de_convenio_preserva_texto_sem_carimbar_fim_e_permite_retomada()
    {
        var ag = await Marcar();
        await _agenda.IniciarAtendimentoAsync(ag.Id, "teste");
        var evo = await Escrever(ag.Id);
        var paciente = (await _repo.ObterPacienteAsync(_paciente))!;
        paciente.ConvenioCodigo = ConvenioCadastro.CodigoADefinir;
        await _db.SaveChangesAsync();
        await FluentActions.Awaiting(() => _agenda.ConcluirAtendimentoClinicoAsync(ag.Id, "teste"))
            .Should().ThrowAsync<ConvenioNaoDefinidoException>();
        ag.FimAtendimentoEm.Should().BeNull();
        ag.Status.Should().Be(StatusAgendamento.Agendado);
        evo.TextoEvolucao.Should().Be("Evolução preservada");
        paciente.ConvenioCodigo = Convenio.UnimedIntercambio.ToString();
        await _db.SaveChangesAsync();
        await _agenda.ConcluirAtendimentoClinicoAsync(ag.Id, "teste");
        (await _db.Atendimentos.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Encerramento_parcial_antigo_pode_ser_concluido_sem_reabrir()
    {
        var ag = await Marcar();
        await _agenda.IniciarAtendimentoAsync(ag.Id, "teste");
        await _agenda.EncerrarAtendimentoAsync(ag.Id, "teste");
        var fim = ag.FimAtendimentoEm;
        await _agenda.ConcluirAtendimentoClinicoAsync(ag.Id, "teste");
        ag.FimAtendimentoEm.Should().Be(fim);
        ag.Status.Should().Be(StatusAgendamento.Realizado);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Guia_antecipada_nao_conclui_nem_duplica_ao_medico_finalizar(bool avulso)
    {
        var marcado = avulso ? null : await Marcar();
        var (ag, primeira) = avulso
            ? await _agenda.LancarAvulsoAsync(_paciente, Dia.ToDateTime(new TimeOnly(9, 0)),
                ModalidadeAtendimento.AcupunturaComEletro, null, profissionalId: _profissional, concluirSessao: false)
            : await _agenda.LancarNoHorarioAsync(marcado!.Id, null, concluirSessao: false);
        ag.Status.Should().Be(StatusAgendamento.Agendado);
        primeira.Atendimento.RealizadoEm.Should().BeNull();
        var quantidade = primeira.Atendimento.Codigos.Count;
        await _agenda.IniciarAtendimentoAsync(ag.Id, "teste");
        var segunda = await _agenda.ConcluirAtendimentoClinicoAsync(ag.Id, "teste");
        segunda.Atendimento.Id.Should().Be(primeira.Atendimento.Id);
        (await _db.Codigos.CountAsync()).Should().Be(quantidade);
    }

    [Fact]
    public async Task Evolucao_do_substituto_resolve_pendencia_sem_trazer_seu_texto_na_projecao()
    {
        var ag = await Marcar();
        await _agenda.ConfirmarPresencaAsync(ag.Id);
        await Escrever(ag.Id, _substituto);
        var consultorio = new ConsultorioService(_repo);
        (await consultorio.DoDiaAsync(Dia, _profissional)).RegistrosPendentes.Should().Be(0);
        (await consultorio.RegistrosPendentesAsync(Dia.AddDays(1), _profissional)).Should().BeEmpty();
        var vinculos = await _repo.VinculosEvolucoesNoPeriodoAsync(Dia, Dia);
        vinculos.Should().ContainSingle();
        vinculos[0].TextoEvolucao.Should().BeNull();
        vinculos[0].ProfissionalId.Should().Be(_substituto);
    }

    [Fact]
    public async Task Registros_ambiguos_exigem_vinculo_conferido_preservando_autor_e_texto()
    {
        var antigo = await Marcar();
        var real = await Marcar();
        await _agenda.ConfirmarPresencaAsync(real.Id);
        var evo = await Escrever(null, _substituto);
        ConsultorioService.EvolucaoDoHorario([evo], real.Id, _paciente, Dia, [antigo, real]).Should().BeNull();
        await _prontuario.VincularAoHorarioAsync(evo.Id, real.Id, "conferente");
        evo.TextoEvolucao.Should().Be("Evolução preservada");
        evo.ProfissionalId.Should().Be(_substituto);
        evo.AgendamentoId.Should().Be(real.Id);
        evo.AtendimentoId.Should().Be(real.AtendimentoId);
        evo.Versoes.Should().ContainSingle();
        (await new ConsultorioService(_repo).RegistrosPendentesAsync(Dia.AddDays(1), _profissional))
            .Should().BeEmpty();
        await FluentActions.Awaiting(() => _prontuario.VincularAoHorarioAsync(evo.Id, antigo.Id, "teste"))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Vinculo_recusa_outro_dia_e_sessao_com_registro_existente()
    {
        var outroDia = await Marcar(Dia.AddDays(-1));
        var evo = await Escrever(null);
        await FluentActions.Awaiting(() => _prontuario.VincularAoHorarioAsync(evo.Id, outroDia.Id, "teste"))
            .Should().ThrowAsync<InvalidOperationException>();
        var ag = await Marcar();
        await Escrever(ag.Id);
        await FluentActions.Awaiting(() => _prontuario.VincularAoHorarioAsync(evo.Id, ag.Id, "teste"))
            .Should().ThrowAsync<InvalidOperationException>();
        evo.AgendamentoId.Should().BeNull();
    }

    [Fact]
    public async Task Pendencia_de_conclusao_aparece_hoje_e_atravessa_a_virada_sem_carencia()
    {
        var encerrado = await Marcar();
        await _agenda.IniciarAtendimentoAsync(encerrado.Id, "teste");
        await _agenda.EncerrarAtendimentoAsync(encerrado.Id, "teste");
        var apenasIniciado = await Marcar();
        await _agenda.IniciarAtendimentoAsync(apenasIniciado.Id, "teste");
        var apenasMarcado = await Marcar();
        (await _repo.HorariosComConclusaoPendenteAsync(Dia)).Select(a => a.Id)
            .Should().Equal(encerrado.Id);
        (await _repo.HorariosComConclusaoPendenteAsync(Dia.AddDays(1))).Select(a => a.Id)
            .Should().BeEquivalentTo([encerrado.Id, apenasIniciado.Id]);
        await _agenda.ConcluirAtendimentoClinicoAsync(encerrado.Id, "teste");
        (await _repo.HorariosComConclusaoPendenteAsync(Dia)).Should().BeEmpty();
    }

    [Fact]
    public async Task Fechamento_repetido_reutiliza_caixa_e_insumos()
    {
        var ag = await Marcar();
        var estoque = new EstoqueService(_repo);
        var item = new ItemEstoque { Nome = "Agulha", Unidade = "un" };
        _db.ItensEstoque.Add(item); await _db.SaveChangesAsync();
        await estoque.EntrarAsync(item.Id, 10, data: Dia);
        var decisao = new DecisaoFechamento(ag.Id, DebitarPacote: false, GerarLancamento: true,
            Valor: 100, Insumos: [new(item.Id, 2)]);
        var primeiro = await Fechamento().ConcluirAsync(decisao);
        var segundo = await Fechamento().ConcluirAsync(decisao with { Valor = 100.00m, Insumos = [new(item.Id, 2.00m)] });
        segundo.Lancamento!.Id.Should().Be(primeiro.Lancamento!.Id);
        segundo.Movimentos.Single().Id.Should().Be(primeiro.Movimentos.Single().Id);
        (await _db.Lancamentos.CountAsync()).Should().Be(1);
        (await _db.MovimentosEstoque.CountAsync(m => m.AtendimentoId != null)).Should().Be(1);
        var alterado = await Fechamento().ConcluirAsync(decisao with { Valor = 150 });
        alterado.Avisos.Should().Contain(a => a.Contains("outros valores"));
        (await _db.Lancamentos.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Falha_entre_efeito_e_recibo_reverte_ambos_e_permite_repetir()
    {
        var ag = await Marcar();
        var atendimento = (await _agenda.ConfirmarPresencaAsync(ag.Id)).Atendimento;
        async Task<int> Falhar()
        {
            await new FinanceiroService(_repo).LancarAsync(Dia, TipoLancamento.Entrada, "Teste", 10,
                atendimentoId: atendimento.Id);
            throw new InvalidOperationException("queda simulada");
        }
        await FluentActions.Awaiting(() => _repo.ExecutarEtapaFechamentoAsync(atendimento.Id, "caixa", "teste", Falhar))
            .Should().ThrowAsync<InvalidOperationException>();
        (await _db.Lancamentos.CountAsync()).Should().Be(0);
        (await _db.EtapasFechamentoSessao.CountAsync()).Should().Be(0);
        var resultado = await Fechamento().ConcluirAsync(new(ag.Id, GerarLancamento: true, Valor: 10));
        resultado.Lancamento.Should().NotBeNull();
    }

    [Fact]
    public async Task Duas_estacoes_no_postgres_confirmam_a_mesma_cobranca_uma_unica_vez()
    {
        if (!BancoDosTestes.NoPostgres) return; // Exercitado no job testes-postgres, com conexões independentes.
        var ag = await Marcar();
        await _agenda.ConfirmarPresencaAsync(ag.Id);
        await using var dbA = NovoContexto();
        await using var dbB = NovoContexto();
        var decisao = new DecisaoFechamento(ag.Id, DebitarPacote: false, GerarLancamento: true, Valor: 80);
        var resultados = await Task.WhenAll(Fechamento(new(dbA)).ConcluirAsync(decisao),
            Fechamento(new(dbB)).ConcluirAsync(decisao));
        resultados.Should().OnlyContain(r => r.TudoCerto);
        resultados[0].Lancamento!.Id.Should().Be(resultados[1].Lancamento!.Id);
        (await _db.Lancamentos.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Falha_no_commit_nao_grava_encerramento_sem_guias()
    {
        var ag = await Marcar();
        await _agenda.IniciarAtendimentoAsync(ag.Id, "teste");
        await Escrever(ag.Id);
        var falha = new FalhaNoCommit();
        await using var db = new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>()
            .UseSqlite(_conn).AddInterceptors(falha).Options);
        var repo = new ClinicaRepositorio(db);
        await FluentActions.Awaiting(() => new AgendaService(repo, new AtendimentoService(repo))
            .ConcluirAtendimentoClinicoAsync(ag.Id, "teste")).Should().ThrowAsync<InvalidOperationException>();
        var persistido = await _db.Agendamentos.AsNoTracking().SingleAsync(a => a.Id == ag.Id);
        persistido.FimAtendimentoEm.Should().BeNull();
        persistido.AtendimentoId.Should().BeNull();
        (await _db.Evolucoes.CountAsync()).Should().Be(1);
        (await _db.Codigos.CountAsync()).Should().Be(0);
        await _agenda.ConcluirAtendimentoClinicoAsync(ag.Id, "teste");
        (await _db.Atendimentos.CountAsync()).Should().Be(1);
    }

    private sealed class FalhaNoCommit : Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesInterceptor
    {
        public override ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int>> SavingChangesAsync(
            Microsoft.EntityFrameworkCore.Diagnostics.DbContextEventData dados,
            Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int> resultado,
            CancellationToken ct = default)
        {
            if (dados.Context!.ChangeTracker.Entries<Atendimento>().Any(e => e.State == EntityState.Added))
                throw new InvalidOperationException("Falha simulada antes do commit");
            return ValueTask.FromResult(resultado);
        }
    }

    [Fact]
    public async Task Retomar_etapa_que_falhou_nao_repete_o_caixa_ja_registrado()
    {
        var ag = await Marcar();
        var item = new ItemEstoque { Nome = "Insumo", Unidade = "un" };
        _db.ItensEstoque.Add(item); await _db.SaveChangesAsync();
        var decisao = new DecisaoFechamento(ag.Id, DebitarPacote: false, GerarLancamento: true,
            Valor: 60, Insumos: [new(item.Id, 2)]);
        var primeiro = await Fechamento().ConcluirAsync(decisao);
        primeiro.Avisos.Should().ContainSingle();
        primeiro.Lancamento.Should().NotBeNull();
        await new EstoqueService(_repo).EntrarAsync(item.Id, 10, data: Dia);
        var segundo = await Fechamento().ConcluirAsync(decisao);
        segundo.TudoCerto.Should().BeTrue();
        segundo.Lancamento!.Id.Should().Be(primeiro.Lancamento!.Id);
        (await _db.Lancamentos.CountAsync()).Should().Be(1);
        segundo.Movimentos.Should().ContainSingle();
    }

    public void Dispose() { _db.Dispose(); _conn.Dispose(); }
}
