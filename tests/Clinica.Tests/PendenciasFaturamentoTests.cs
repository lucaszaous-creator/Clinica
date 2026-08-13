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

/// <summary>Pendências novas do módulo de faturamento: prazo de recurso de glosa e carteirinhas a vencer.</summary>
public class PendenciasFaturamentoTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly PendenciaService _pendencias;

    public PendenciasFaturamentoTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _pendencias = new PendenciaService(_repo);
    }

    private async Task<CodigoFaturamento> CriarGlosadaAsync(
        DateOnly dataGlosa, int prazoDias, string nome = "P", string? telefone = null)
    {
        var p = new Paciente
        {
            Nome = nome, Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino,
            Telefone = telefone
        };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        var r = await new AtendimentoService(_repo).LancarAsync(p.Id, dataGlosa.AddDays(-5), ModalidadeAtendimento.AcupunturaSimples);
        var codigo = r.Atendimento.Codigos.First();
        await new FaturamentoService(_repo).DarBaixaAsync(codigo.Id, dataGlosa.AddDays(-4), "9001", "sec", null);
        codigo.RegistrarGlosa(dataGlosa, "motivo", "1201", prazoDias);
        await _db.SaveChangesAsync();
        return codigo;
    }

    /// <summary>
    /// A pendência carrega MODALIDADE e ESPECIALIDADE (parcela 61) — a resposta a
    /// "12 pendências de quê?" dos filtros do painel. A especialidade segue o caminho da
    /// consulta de guias (parcela 45): a do CÓDIGO quando ele a tem, senão a do
    /// atendimento — sem o caminho de baixo, a guia de um atendimento com especialidade
    /// declarada ficaria fora do filtro da própria especialidade.
    /// </summary>
    [Fact]
    public async Task Pendencia_carrega_modalidade_e_especialidade_com_fallback()
    {
        var p = new Paciente { Nome = "Filtrada", Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();

        var hoje = new DateOnly(2026, 8, 12);
        var atendimentos = new AtendimentoService(_repo);
        await atendimentos.LancarAsync(p.Id, hoje, ModalidadeAtendimento.Consulta,
            especialidadeConsulta: Especialidade.Psiquiatria);

        var pendencias = await _pendencias.CodigosPendentesAsync(hoje);

        pendencias.Should().NotBeEmpty();
        pendencias.Should().OnlyContain(x => x.Modalidade == ModalidadeAtendimento.Consulta);
        // O código da consulta avulsa carrega a especialidade; quando não carregar, o
        // fallback do atendimento responde — as duas fontes dão a MESMA resposta aqui.
        pendencias.Should().OnlyContain(x => x.Especialidade == Especialidade.Psiquiatria);
    }

    [Fact]
    public async Task GlosasARecorrer_SemaforoPorDiasRestantes()
    {
        var hoje = new DateOnly(2026, 7, 19);
        await CriarGlosadaAsync(hoje.AddDays(-28), 30); // restam 2 dias  → vermelho
        await CriarGlosadaAsync(hoje.AddDays(-25), 30); // restam 5 dias  → amarelo
        await CriarGlosadaAsync(hoje.AddDays(-5), 30);  // restam 25 dias → verde

        var lista = await _pendencias.GlosasARecorrerAsync(hoje);

        lista.Should().HaveCount(3);
        lista[0].Urgencia.Should().Be(NivelUrgencia.Vermelho);
        lista[0].DiasParaFimPrazo.Should().Be(2);
        lista[1].Urgencia.Should().Be(NivelUrgencia.Amarelo);
        lista[2].Urgencia.Should().Be(NivelUrgencia.Verde);
        lista[0].MotivoResumo.Should().Contain("Carteirinha"); // descrição do 1201 da tabela ANS

        // Badge: só as com prazo apertado (amarelo/vermelho) contam.
        (await _pendencias.TotalPendenciasAsync(hoje)).Should().Be(2);
    }

    /// <summary>
    /// A linha da glosa leva o CONTATO do paciente (parcela 57).
    ///
    /// Era o único dos quatro modelos de pendência sem telefone, e a falta não aparecia
    /// como erro: a célula saía em branco, que na tela é indistinguível de "este paciente
    /// não tem telefone cadastrado". Quem recorre da glosa precisa falar com o paciente
    /// para obter o documento que sustenta o recurso, e o prazo está correndo.
    /// </summary>
    [Fact]
    public async Task GlosasARecorrer_LevamNomeCompletoETelefoneDoPaciente()
    {
        var hoje = new DateOnly(2026, 7, 19);
        await CriarGlosadaAsync(hoje.AddDays(-28), 30,
            nome: "Maria Aparecida da Silva Nascimento", telefone: "(22) 99999-1234");

        var lista = await _pendencias.GlosasARecorrerAsync(hoje);

        lista.Should().ContainSingle();
        lista[0].PacienteNome.Should().Be("Maria Aparecida da Silva Nascimento");
        lista[0].PacienteTelefone.Should().Be("(22) 99999-1234");
    }

    /// <summary>
    /// Sem telefone no cadastro o campo vem NULO, e não vazio: é o que faz a tela dizer
    /// "sem telefone no cadastro" em vez de mostrar uma linha em branco — as duas coisas
    /// se parecem, e só uma diz o que fazer.
    /// </summary>
    [Fact]
    public async Task GlosasARecorrer_SemTelefoneNoCadastro_VemNulo()
    {
        var hoje = new DateOnly(2026, 7, 19);
        await CriarGlosadaAsync(hoje.AddDays(-28), 30);

        var lista = await _pendencias.GlosasARecorrerAsync(hoje);

        lista.Should().ContainSingle();
        lista[0].PacienteTelefone.Should().BeNull();
    }

    [Fact]
    public async Task GlosaRecuperada_SaiDasPendenciasDeRecurso()
    {
        var hoje = new DateOnly(2026, 7, 19);
        var codigo = await CriarGlosadaAsync(hoje.AddDays(-28), 30);
        codigo.MarcarGlosaRecuperada();
        await _db.SaveChangesAsync();

        (await _pendencias.GlosasARecorrerAsync(hoje)).Should().BeEmpty();
    }

    [Fact]
    public async Task CarteirinhasAVencer_VencidaVermelha_VencendoAmarela_ForaDaJanelaNaoAparece()
    {
        var hoje = new DateOnly(2026, 7, 19);
        _db.Pacientes.AddRange(
            new Paciente { Nome = "Vencida", Convenio = Convenio.Amil, ValidadeCarteirinha = hoje.AddDays(-2) },
            new Paciente { Nome = "Vencendo", Convenio = Convenio.Amil, ValidadeCarteirinha = hoje.AddDays(10) },
            new Paciente { Nome = "Longe", Convenio = Convenio.Amil, ValidadeCarteirinha = hoje.AddDays(90) },
            new Paciente { Nome = "SemValidade", Convenio = Convenio.Amil });
        await _db.SaveChangesAsync();

        var lista = await _pendencias.CarteirinhasAVencerAsync(hoje);

        lista.Should().HaveCount(2);
        lista[0].PacienteNome.Should().Be("Vencida");
        lista[0].Urgencia.Should().Be(NivelUrgencia.Vermelho);
        lista[0].DiasParaVencer.Should().Be(-2);
        lista[1].PacienteNome.Should().Be("Vencendo");
        lista[1].Urgencia.Should().Be(NivelUrgencia.Amarelo);
    }

    [Fact]
    public async Task PendenciasDoPaciente_FiltraSomenteOPacienteInformado()
    {
        var hoje = new DateOnly(2026, 7, 19);
        var ana = new Paciente { Nome = "Ana", Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino };
        var bruno = new Paciente { Nome = "Bruno", Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Masculino };
        _db.Pacientes.AddRange(ana, bruno);
        await _db.SaveChangesAsync();

        var svc = new AtendimentoService(_repo);
        // Atendimentos no passado → a guia já é faturável (pendente) na data de referência.
        await svc.LancarAsync(ana.Id, hoje.AddDays(-2), ModalidadeAtendimento.AcupunturaSimples);
        await svc.LancarAsync(bruno.Id, hoje.AddDays(-2), ModalidadeAtendimento.AcupunturaSimples);

        var daAna = await _pendencias.PendenciasDoPacienteAsync(ana.Id, hoje);

        daAna.Should().NotBeEmpty();
        daAna.Should().OnlyContain(p => p.PacienteId == ana.Id);
        (await _pendencias.PendenciasDoPacienteAsync(-1, hoje)).Should().BeEmpty();
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
