using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;
using Xunit;

namespace Clinica.Tests;

public class AlergiasSemDuplicacaoTests : IDisposable
{
    private readonly SqliteConnection _conn = new("DataSource=:memory:");
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly ProblemaPacienteService _problemas;
    private static DateOnly Hoje => DateOnly.FromDateTime(DateTime.Today);
    private static readonly IdentificacaoExecutante Tecnica = new(null, "Joana", "COREN 123");

    public AlergiasSemDuplicacaoTests()
    {
        _conn.Open();
        _db = new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _problemas = new ProblemaPacienteService(_repo);
    }

    private async Task<int> PacienteAsync()
    {
        var paciente = new Paciente { Nome = "Paciente teste", Convenio = Convenio.UnimedIntercambio };
        _db.Pacientes.Add(paciente);
        await _db.SaveChangesAsync();
        return paciente.Id;
    }

    private Task<ProblemaPaciente> RegistrarAsync(int pacienteId, string descricao, string? observacoes = null)
        => _problemas.SalvarAsync(new ProblemaPaciente
        {
            PacienteId = pacienteId, Natureza = NaturezaProblema.Alergia,
            Descricao = descricao, Observacoes = observacoes
        }, "Dra. Ana");

    [Theory]
    [InlineData(SituacaoProblema.Ativo)]
    [InlineData(SituacaoProblema.Resolvido)]
    public async Task Relatos_da_enfermagem_reutilizam_alergia_e_preservam_origem_e_historico(SituacaoProblema situacao)
    {
        var paciente = await PacienteAsync();
        await SessaoBsvTeste.CriarAsync(_db, paciente, Hoje);
        var original = await RegistrarAsync(paciente, "Plasil e bromoprida", "Reação relatada na primeira consulta.");
        if (situacao == SituacaoProblema.Resolvido)
            await _problemas.ResolverAsync(original.Id, Hoje.AddDays(-1));
        // Compara o valor persistido: PostgreSQL guarda microssegundos, não ticks do .NET.
        await _db.Entry(original).ReloadAsync();
        var fim = original.Fim;
        var criado = original.CriadoEm;
        var evolucoes = new EvolucaoEnfermagemService(_repo, () => DateTime.Today.AddHours(12));

        var primeira = await evolucoes.RegistrarAsync(paciente, Hoje, new TimeOnly(9, 0),
            "Paciente relata alergia conhecida.", Tecnica, alergiaObservada: " PLASIL  e\tBROMOPRIDA ");
        await evolucoes.RegistrarAsync(paciente, Hoje, new TimeOnly(10, 0),
            "Relato conferido novamente.", Tecnica, alergiaObservada: "Plasil e bromoprida");
        await evolucoes.RetificarAsync(primeira.Id, Hoje, new TimeOnly(9, 5),
            "Paciente relata alergia conhecida na admissão.", Tecnica, "Correção da hora.");

        var unica = (await _repo.ProblemasDoPacienteAsync(paciente)).Should().ContainSingle().Subject;
        unica.Id.Should().Be(original.Id);
        unica.CriadoEm.Should().Be(criado);
        unica.CriadoPor.Should().Be("Dra. Ana");
        unica.Situacao.Should().Be(situacao);
        unica.Fim.Should().Be(fim);
        unica.Observacoes.Should().Be("Reação relatada na primeira consulta.");
        (await evolucoes.DoPacienteAsync(paciente)).Should().HaveCount(3);
        (await _repo.EventosAuditoriaAsync()).Count(e => e.Acao == "AlergiaRelatadaNovamente").Should().Be(2);
    }

    [Fact]
    public async Task Cadastro_manual_reutiliza_id_e_acrescenta_observacao_sem_apagar_a_anterior()
    {
        var paciente = await PacienteAsync();
        var original = await RegistrarAsync(paciente, "Dipirona", "Prurido");
        var repetida = await RegistrarAsync(paciente, " dipirona ", "Urticária");
        await RegistrarAsync(paciente, "DIPIRONA", "Urticária");

        repetida.Id.Should().Be(original.Id);
        var unica = (await _repo.ProblemasDoPacienteAsync(paciente)).Should().ContainSingle().Subject;
        unica.Observacoes.Should().Be("Prurido\nUrticária");
    }

    [Fact]
    public async Task Edicao_nao_cria_segunda_alergia_com_a_mesma_descricao()
    {
        var paciente = await PacienteAsync();
        var dipirona = await RegistrarAsync(paciente, "Dipirona");
        var latex = await RegistrarAsync(paciente, "Látex");
        Func<Task> acao = () => _problemas.SalvarAsync(new ProblemaPaciente
        {
            Id = latex.Id, PacienteId = paciente,
            Natureza = NaturezaProblema.Alergia, Descricao = " DIPIRONA "
        });
        await acao.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Já existe uma alergia*");
        (await _problemas.ObterAsync(latex.Id))!.Descricao.Should().Be("Látex");
        await _problemas.SalvarAsync(new ProblemaPaciente
        {
            Id = dipirona.Id, PacienteId = paciente, Natureza = NaturezaProblema.Alergia,
            Descricao = "Dipirona", Observacoes = "Prurido confirmado."
        });
        (await _problemas.DoPacienteAsync(paciente)).Should().HaveCount(2);
    }

    [Fact]
    public async Task Alergia_descartada_permanece_descartada_e_novo_relato_cria_alerta()
    {
        var paciente = await PacienteAsync();
        var anterior = await RegistrarAsync(paciente, "Dipirona");
        await _problemas.DescartarAsync(anterior.Id, "Paciente havia confundido o nome.");
        var nova = await RegistrarAsync(paciente, "Dipirona");

        nova.Id.Should().NotBe(anterior.Id);
        (await _problemas.ObterAsync(anterior.Id))!.MotivoDescarte.Should().Be("Paciente havia confundido o nome.");
        (await _problemas.DoPacienteAsync(paciente)).Should().HaveCount(2);
        (await _problemas.AlertasAsync(paciente)).Should().ContainSingle().Which.Id.Should().Be(nova.Id);
    }

    [Fact]
    public async Task Nao_confunde_pacientes_naturezas_ou_descricao_parcial()
    {
        var primeiro = await PacienteAsync();
        var segundo = await PacienteAsync();
        await _problemas.SalvarAsync(new ProblemaPaciente
        {
            PacienteId = primeiro, Natureza = NaturezaProblema.MedicacaoContinua, Descricao = "Dipirona"
        });
        await RegistrarAsync(primeiro, "Dipirona");
        await RegistrarAsync(primeiro, "Dipirona e látex");
        await RegistrarAsync(segundo, "Dipirona");

        (await _problemas.DoPacienteAsync(primeiro)).Should().HaveCount(3);
        (await _problemas.DoPacienteAsync(segundo)).Should().ContainSingle();
    }

    [Theory]
    [InlineData(SituacaoProblema.Ativo)]
    [InlineData(SituacaoProblema.Resolvido)]
    public async Task Reabrir_descartada_apos_novo_relato_recusa_sem_apagar_o_historico(SituacaoProblema situacao)
    {
        var paciente = await PacienteAsync();
        var descartada = await RegistrarAsync(paciente, "Dipirona", "Relato original.");
        await _problemas.DescartarAsync(descartada.Id, "Nome informado incorretamente.");
        var vigente = await RegistrarAsync(paciente, " DIPIRONA ", "Novo relato confirmado.");
        if (situacao == SituacaoProblema.Resolvido)
            await _problemas.ResolverAsync(vigente.Id);
        var auditoriasAntes = (await _repo.EventosAuditoriaAsync()).Count;

        Func<Task> reabrir = () => _problemas.ReabrirAsync(descartada.Id);
        await reabrir.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Use a linha existente*");

        var historico = (await _problemas.ObterAsync(descartada.Id))!;
        historico.Situacao.Should().Be(SituacaoProblema.Descartado);
        historico.MotivoDescarte.Should().Be("Nome informado incorretamente.");
        historico.Observacoes.Should().Be("Relato original.");
        (await _problemas.ObterAsync(vigente.Id))!.Situacao.Should().Be(situacao);
        (await _problemas.DoPacienteAsync(paciente)).Should().HaveCount(2);
        (await _problemas.AlertasAsync(paciente)).Should().ContainSingle();
        (await _repo.EventosAuditoriaAsync()).Should().HaveCount(auditoriasAntes);
    }

    [Fact]
    public async Task Reabrir_diagnostico_preserva_semantica_de_episodios_independentes()
    {
        var paciente = await PacienteAsync();
        var primeiro = await _problemas.SalvarAsync(new ProblemaPaciente
        { PacienteId = paciente, Descricao = "Lombalgia", Natureza = NaturezaProblema.Diagnostico });
        await _problemas.DescartarAsync(primeiro.Id, "Registro incorreto.");
        await _problemas.SalvarAsync(new ProblemaPaciente
        { PacienteId = paciente, Descricao = "Lombalgia", Natureza = NaturezaProblema.Diagnostico });

        await _problemas.ReabrirAsync(primeiro.Id);

        (await _problemas.DoPacienteAsync(paciente)).Should().HaveCount(2)
            .And.OnlyContain(p => p.Situacao == SituacaoProblema.Ativo);
    }

    [Fact]
    public async Task Renomeacao_recusa_paciente_diferente_do_dono_da_trava()
    {
        var paciente = await PacienteAsync();
        var outro = await PacienteAsync();
        var alergia = await RegistrarAsync(paciente, "Dipirona");

        Func<Task> editar = () => _problemas.SalvarAsync(new ProblemaPaciente
        { Id = alergia.Id, PacienteId = outro, Natureza = NaturezaProblema.Alergia, Descricao = "Látex" });
        await editar.Should().ThrowAsync<InvalidOperationException>().WithMessage("*outro paciente*");
        (await _problemas.ObterAsync(alergia.Id))!.Descricao.Should().Be("Dipirona");
    }

    [Fact]
    public async Task Alertas_legados_repetidos_aparecem_uma_vez_sem_ocultar_observacoes_distintas()
    {
        var paciente = await PacienteAsync();
        foreach (var (descricao, observacao) in new[]
        {
            ("Plasil e bromoprida", "Reação observada."),
            (" PLASIL  E BROMOPRIDA ", "Reação observada."),
            ("Plasil e bromoprida", "Broncoespasmo.")
        })
            _db.ProblemasPaciente.Add(new ProblemaPaciente
            {
                PacienteId = paciente, Natureza = NaturezaProblema.Alergia,
                Descricao = descricao, Observacoes = observacao
            });
        await _db.SaveChangesAsync();

        var alertas = await _problemas.AlertasAsync(paciente);
        alertas.Should().HaveCount(2);
        alertas.Should().Contain(a => a.Observacoes == "Broncoespasmo.");
        (await _problemas.DoPacienteAsync(paciente)).Should().HaveCount(3, "o histórico clínico não é apagado");
        var cabecalho = await new ConsultorioService(_repo).CabecalhoAsync(paciente);
        cabecalho!.Alergias.Should().ContainSingle();
        var conferencia = await new PrescricaoService(_repo).ConferirAsync(paciente, ["Plasil"]);
        conferencia.ExigeConfirmacao.Should().BeTrue();
        conferencia.Alertas.Should().ContainSingle();
    }

    [Fact]
    public async Task Duas_checagens_reutilizam_alergia_e_preservam_os_dois_episodios()
    {
        var paciente = await PacienteAsync();
        var profissional = new Profissional { Nome = "Dra. Ana", RegistroConselho = "CRM 1" };
        _db.Profissionais.Add(profissional);
        await _db.SaveChangesAsync();
        var prescricao = new PrescricaoInterna
        {
            PacienteId = paciente, ProfissionalId = profissional.Id,
            Data = Hoje, Situacao = SituacaoPrescricao.Assinada,
            Numero = "PRE TESTE", CodigoVerificacao = "ALERG001",
            Itens = [new() { Ordem = 1, Descricao = "Dipirona" }, new() { Ordem = 2, Descricao = "Dipirona" }]
        };
        _db.PrescricoesInternas.Add(prescricao);
        await _db.SaveChangesAsync();
        var servico = new ChecagemPrescricaoService(_repo, () => DateTime.Today.AddHours(12));
        foreach (var item in prescricao.Itens)
            await servico.ChecarAsync(item.Id, SituacaoChecagem.NaoRealizado,
                new TimeOnly(9, 0), Tecnica, "Alergia conhecida.", alergiaObservada: "Dipirona");

        (await _problemas.DoPacienteAsync(paciente)).Should().ContainSingle();
        prescricao.Itens.Should().OnlyContain(i => i.ChecagemVigente != null);
        (await _repo.EventosAuditoriaAsync()).Should().Contain(e => e.Acao == "AlergiaRelatadaNovamente"
            && e.Detalhe!.Contains($"item #{prescricao.Itens[1].Id}"));
    }

    [Fact]
    public async Task Falha_apos_salvar_reverte_toda_a_transacao_da_alergia()
    {
        var paciente = await PacienteAsync();
        Func<Task> acao = async () => await _repo.ExecutarRegistroAlergiaAtomicoAsync<int>(paciente, async () =>
        {
            await RegistrarAsync(paciente, "Dipirona");
            throw new InvalidOperationException("Falha ao concluir a sessão.");
        });

        await acao.Should().ThrowAsync<InvalidOperationException>();
        (await _problemas.DoPacienteAsync(paciente)).Should().BeEmpty();
        (await _repo.EventosAuditoriaAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task No_Postgres_dois_contextos_reutilizam_o_mesmo_registro_concorrente()
    {
        if (!BancoDosTestes.NoPostgres) return;
        var paciente = await PacienteAsync();
        var esperaTrava = new DetectarTrava();
        await using var outroDb = new ClinicaDbContext(
            new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn)
                .AddInterceptors(esperaTrava).Options);
        var outro = new ProblemaPacienteService(new ClinicaRepositorio(outroDb));
        var primeiraGravada = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var liberarPrimeira = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var primeira = _repo.ExecutarRegistroAlergiaAtomicoAsync(paciente, async () =>
        {
            var registro = await RegistrarAsync(paciente, "Dipirona");
            primeiraGravada.SetResult();
            await liberarPrimeira.Task.WaitAsync(TimeSpan.FromSeconds(10));
            return registro;
        });
        await primeiraGravada.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var segunda = outro.SalvarAsync(new ProblemaPaciente
        {
            PacienteId = paciente, Natureza = NaturezaProblema.Alergia, Descricao = " DIPIRONA "
        });
        await esperaTrava.Tentativa.Task.WaitAsync(TimeSpan.FromSeconds(10));
        liberarPrimeira.SetResult();
        var resultados = await Task.WhenAll(primeira, segunda);
        resultados[0].Id.Should().Be(resultados[1].Id);
        (await _problemas.DoPacienteAsync(paciente)).Should().ContainSingle();
    }

    [Fact]
    public async Task No_Postgres_renomeacao_aguarda_relato_concorrente_e_recusa_colisao()
    {
        if (!BancoDosTestes.NoPostgres) return;
        var paciente = await PacienteAsync();
        var original = await RegistrarAsync(paciente, "Látex");
        var esperaTrava = new DetectarTrava();
        await using var outroDb = new ClinicaDbContext(
            new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn)
                .AddInterceptors(esperaTrava).Options);
        var outro = new ProblemaPacienteService(new ClinicaRepositorio(outroDb));
        var primeiraGravada = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var liberarPrimeira = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var primeira = _repo.ExecutarRegistroAlergiaAtomicoAsync(paciente, async () =>
        {
            var nova = await RegistrarAsync(paciente, "Dipirona");
            primeiraGravada.SetResult();
            await liberarPrimeira.Task.WaitAsync(TimeSpan.FromSeconds(10));
            return nova;
        });
        await primeiraGravada.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var renomeacao = outro.SalvarAsync(new ProblemaPaciente
        { Id = original.Id, PacienteId = paciente, Natureza = NaturezaProblema.Alergia, Descricao = " DIPIRONA " });
        try
        {
            await esperaTrava.Tentativa.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally { liberarPrimeira.TrySetResult(); }
        await primeira;
        Func<Task> concluir = async () => await renomeacao;
        await concluir.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Já existe uma alergia*");
        var problemas = await _problemas.DoPacienteAsync(paciente);
        problemas.Should().HaveCount(2);
        problemas.Should().Contain(p => p.Id == original.Id && p.Descricao == "Látex");
        problemas.Count(p => p.Descricao == "Dipirona").Should().Be(1);
    }

    private sealed class DetectarTrava : DbCommandInterceptor
    {
        public TaskCompletionSource Tentativa { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("pg_advisory_xact_lock")) Tentativa.TrySetResult();
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
