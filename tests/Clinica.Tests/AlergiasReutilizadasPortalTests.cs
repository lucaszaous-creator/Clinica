using System.Text.Json;
using Clinica.Application.Servicos;
using Clinica.Application.Tablet;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Prontuario;
using Clinica.Infrastructure;
using Clinica.Infrastructure.Tablet;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

public sealed class AlergiasReutilizadasPortalTests : IDisposable
{
    private readonly SqliteConnection conexao = new("DataSource=:memory:");
    private readonly ClinicaDbContext db;
    private readonly ClinicaRepositorio repo;
    private readonly PortalTabletService portal;
    private Paciente paciente = null!;

    public AlergiasReutilizadasPortalTests()
    {
        conexao.Open();
        db = new(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(conexao).Options);
        db.Database.EnsureCreated();
        repo = new(db);
        portal = new(db, repo, new(repo, new(repo), new(repo)), new(repo), new(repo),
            new(repo), new([1, 2]), TimeProvider.System);
    }

    public void Dispose() { db.Dispose(); conexao.Dispose(); }

    private async Task<SessaoTablet> PrepararAsync()
    {
        var usuario = await new AcessoService(repo).CriarAsync("Equipe sintética", "alergiasteste",
            "TabletTeste#2026", PerfilAcesso.Enfermagem);
        paciente = new() { Nome = "Paciente sintético", DataNascimento = new(1980, 1, 15), Convenio = Convenio.UnimedPadrao };
        db.Pacientes.Add(paciente);
        var consentimento = ModelosTermoBsv.Consentimento(); consentimento.Id = 1;
        var sessao = ModelosTermoBsv.TermoDaSessao(); sessao.Id = 2;
        db.ModelosDocumento.AddRange(consentimento, sessao);
        await db.SaveChangesAsync();
        var entrada = await portal.EntrarAsync(usuario, "tablet sintético", null, default);
        await portal.PrepararAsync(entrada.Sessao, new(paciente.Id, [1, 2]), default);
        return entrada.Sessao;
    }

    private static EnviarRubrica Relatar(ColetaTablet coleta, string relato)
    {
        var documento = JsonSerializer.Deserialize<DocumentoTablet>(coleta.ConteudoJson, ContratoTablet.Json)!;
        return new(Guid.NewGuid(), coleta.ConteudoHash,
            documento.Itens.ToDictionary(i => i.Ordem, _ => (string?)"Sim"), relato,
            Convert.ToBase64String(PortalTabletTests.Png()), true);
    }

    [Theory]
    [InlineData(SituacaoProblema.Ativo)]
    [InlineData(SituacaoProblema.Resolvido)]
    public async Task Termos_reutilizam_alergia_existente_com_espacos_caixa_e_unicode_diferentes(SituacaoProblema estado)
    {
        var sessao = await PrepararAsync();
        var original = new ProblemaPaciente
        {
            PacienteId = paciente.Id, Natureza = NaturezaProblema.Alergia,
            Descricao = "Látex sintético", Situacao = estado,
            Observacoes = "Observação clínica original", CriadoPor = "profissional original"
        };
        db.Add(original); await db.SaveChangesAsync();
        foreach (var coleta in await db.ColetasTablet.OrderBy(c => c.DocumentoId).ToListAsync())
            await portal.ReceberAsync(sessao, coleta.Id, Relatar(coleta, " LA\u0301TEX   SINTÉTICO "), default);

        db.ChangeTracker.Clear();
        var atual = (await db.ProblemasPaciente.ToListAsync()).Should().ContainSingle().Subject;
        atual.Id.Should().Be(original.Id);
        atual.Descricao.Should().Be(original.Descricao);
        atual.Situacao.Should().Be(estado);
        atual.Observacoes.Should().Be("Observação clínica original");
        atual.CriadoPor.Should().Be("profissional original");
        (await db.ColetasTablet.ToListAsync()).Should().OnlyContain(c => c.Estado == "recebido");
    }

    [Theory]
    [InlineData("LÁTEX   sintético; Agente fictício")]
    [InlineData("Agente fictício; LÁTEX   sintético")]
    public async Task Confirmacao_da_lista_preenchida_nao_cria_alergia_composta(string relato)
    {
        var sessao = await PrepararAsync();
        db.AddRange(new ProblemaPaciente { PacienteId = paciente.Id, Natureza = NaturezaProblema.Alergia, Descricao = "Látex sintético" },
            new ProblemaPaciente { PacienteId = paciente.Id, Natureza = NaturezaProblema.Alergia, Descricao = "Agente fictício" });
        await db.SaveChangesAsync();
        var coleta = await db.ColetasTablet.OrderBy(c => c.DocumentoId).FirstAsync();
        await portal.ReceberAsync(sessao, coleta.Id, Relatar(coleta, relato), default);
        (await db.ProblemasPaciente.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Novo_relato_preserva_descartada_e_nao_associa_paciente_homonimo()
    {
        var sessao = await PrepararAsync();
        var outro = new Paciente { Nome = paciente.Nome, Convenio = paciente.Convenio };
        db.Add(outro); await db.SaveChangesAsync();
        var descartada = new ProblemaPaciente { PacienteId = paciente.Id, Natureza = NaturezaProblema.Alergia,
            Descricao = "Agente fictício", Situacao = SituacaoProblema.Descartado, MotivoDescarte = "Revisão anterior" };
        db.AddRange(descartada, new ProblemaPaciente { PacienteId = outro.Id, Natureza = NaturezaProblema.Alergia, Descricao = "Agente fictício" });
        await db.SaveChangesAsync();
        var coletas = await db.ColetasTablet.OrderBy(c => c.DocumentoId).ToListAsync();
        foreach (var coleta in coletas)
            await portal.ReceberAsync(sessao, coleta.Id, Relatar(coleta, "AGENTE   fictício"), default);
        db.ChangeTracker.Clear();
        var doPaciente = await db.ProblemasPaciente.Where(p => p.PacienteId == paciente.Id).ToListAsync();
        doPaciente.Should().HaveCount(2);
        doPaciente.Single(p => p.Id == descartada.Id).MotivoDescarte.Should().Be("Revisão anterior");
        doPaciente.Single(p => p.Id == descartada.Id).Situacao.Should().Be(SituacaoProblema.Descartado);
        doPaciente.Should().ContainSingle(p => p.Situacao == SituacaoProblema.Ativo);
        (await db.ProblemasPaciente.CountAsync(p => p.PacienteId == outro.Id)).Should().Be(1);
    }

    [Fact]
    public async Task Relato_diferente_permanece_distinto_sem_aproximacao_de_nomes()
    {
        var sessao = await PrepararAsync();
        db.Add(new ProblemaPaciente { PacienteId = paciente.Id, Natureza = NaturezaProblema.Alergia,
            Descricao = "Agente fictício A" });
        await db.SaveChangesAsync();
        var coleta = await db.ColetasTablet.OrderBy(c => c.DocumentoId).FirstAsync();
        await portal.ReceberAsync(sessao, coleta.Id, Relatar(coleta, "Agente fictício B"), default);
        (await db.ProblemasPaciente.Select(p => p.Descricao).ToListAsync())
            .Should().BeEquivalentTo("Agente fictício A", "Agente fictício B");
    }

    [Fact]
    public async Task Segundo_termo_nao_desfaz_descarte_da_equipe_na_mesma_coleta()
    {
        var sessao = await PrepararAsync();
        var coletas = await db.ColetasTablet.OrderBy(c => c.DocumentoId).ToListAsync();
        await portal.ReceberAsync(sessao, coletas[0].Id, Relatar(coletas[0], "Agente fictício"), default);
        var alergia = await db.ProblemasPaciente.SingleAsync();
        await new ProblemaPacienteService(repo).DescartarAsync(alergia.Id, "Revisado com o paciente", "Equipe sintética");
        await portal.ReceberAsync(sessao, coletas[1].Id, Relatar(coletas[1], "AGENTE   fictício"), default);
        db.ChangeTracker.Clear();
        var mantida = (await db.ProblemasPaciente.ToListAsync()).Should().ContainSingle().Subject;
        mantida.Id.Should().Be(alergia.Id);
        mantida.Situacao.Should().Be(SituacaoProblema.Descartado);
        mantida.MotivoDescarte.Should().Be("Revisado com o paciente");
    }
}
