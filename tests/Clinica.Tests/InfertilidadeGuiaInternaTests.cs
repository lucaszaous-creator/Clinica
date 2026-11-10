using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O ATENDIMENTO DE INFERTILIDADE e a GUIA INTERNA em que ele é lançado (nov/2026). O pedido
/// da direção: "já no agendamento, sinalizar na guia de quem vai passar — geriatria ou
/// psiquiatria —, para a gente saber para onde está lançando e ficar rastreável".
///
/// O que estes testes prendem: a marca não existe sem a guia, ela sobrevive a quem remarca sem
/// saber dela, a troca fica na trilha — e, principalmente, que ela é SÓ classificação interna:
/// as guias que o sistema gera são as mesmas com ou sem a marca.
/// </summary>
public class InfertilidadeGuiaInternaTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly AgendaService _agenda;
    private readonly int _medica;

    private static readonly DateTime Segunda = new(2026, 11, 16, 14, 0, 0);

    public InfertilidadeGuiaInternaTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        _db = new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options);
        _db.Database.EnsureCreated();
        var medica = new Profissional { Nome = "Dra. Patrícia" };
        _db.Profissionais.Add(medica);
        _db.SaveChanges();
        _medica = medica.Id;
        var repo = new ClinicaRepositorio(_db);
        var parametros = new ParametrosService(repo);
        _agenda = new AgendaService(repo, new AtendimentoService(repo, parametros: parametros), parametros);
        CatalogoConvenios.Atualizar(
        [
            new EntradaConvenio(nameof(Convenio.Petrobras), "Petrobras", Convenio.Petrobras, true, GeraGuia: true)
        ]);
    }

    public void Dispose()
    {
        CatalogoConvenios.Atualizar([]);
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<Paciente> PacienteAsync(string nome = "Rui Carvalho")
    {
        var p = new Paciente { Nome = nome, Convenio = Convenio.Petrobras, ConvenioCodigo = nameof(Convenio.Petrobras), Sexo = Sexo.Masculino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p;
    }

    private Task<Agendamento> ConsultaAsync(int pacienteId, DateTime quando, string? guiaDe)
        => _agenda.AgendarAsync(pacienteId, quando, ModalidadeAtendimento.Consulta, null,
            especialidadeConsultaCodigo: nameof(Especialidade.Geriatria), profissionalId: _medica,
            operador: "recepcao", infertilidadeGuiaDe: guiaDe);

    // ==================== a regra ====================

    [Fact]
    public void As_guias_possiveis_sao_geriatria_e_psiquiatria()
        => InfertilidadeNaTela.Opcoes.Select(o => o.Codigo).Should().Equal("Geriatria", "Psiquiatria");

    [Fact]
    public void Sem_a_marca_nao_ha_guia_e_com_a_marca_a_guia_e_obrigatoria()
    {
        InfertilidadeNaTela.Normalizar(false, "Geriatria").Should().BeNull("sem a marca, a guia não se grava");
        InfertilidadeNaTela.Normalizar(true, " psiquiatria ").Should().Be("Psiquiatria", "normaliza caixa e espaço");
        FluentActions.Invoking(() => InfertilidadeNaTela.Normalizar(true, null))
            .Should().Throw<InvalidOperationException>().WithMessage("*escolha em qual guia*");
        FluentActions.Invoking(() => InfertilidadeNaTela.Normalizar(true, "Endocrinologia"))
            .Should().Throw<InvalidOperationException>("guia fora da lista não é aceita");
    }

    [Fact]
    public void O_selo_diz_a_guia_e_a_dica_diz_que_e_controle_interno()
    {
        InfertilidadeNaTela.Selo("Psiquiatria").Should().Be("Infertilidade · guia Psiquiatria");
        InfertilidadeNaTela.DicaDoSelo("Psiquiatria").Should().Contain("controle interno");
        InfertilidadeNaTela.Selo(null).Should().BeNull();
        InfertilidadeNaTela.Selo(" ").Should().BeNull();
    }

    // ==================== a gravação ====================

    [Fact]
    public async Task Marcar_grava_a_guia_interna_e_ela_aparece_na_agenda_do_dia_e_na_grade()
    {
        var p = await PacienteAsync();
        var ag = await ConsultaAsync(p.Id, Segunda, "Geriatria");
        _db.ChangeTracker.Clear();

        var lido = await _db.Agendamentos.Include(a => a.Paciente).AsNoTracking().SingleAsync(a => a.Id == ag.Id);
        lido.InfertilidadeGuiaDe.Should().Be("Geriatria");
        lido.EhInfertilidade.Should().BeTrue();

        var linha = AgendaDoDiaNaTela.Montar(
            DadosDaAgendaDoDia.Vazio(DateOnly.FromDateTime(Segunda)) with { Horarios = [lido] }, Segunda.AddHours(-1)).Single();
        linha.SeloDaInfertilidade.Should().Be("Infertilidade · guia Geriatria");
        linha.Detalhe.Should().Contain("controle interno");
        GradeDaAgendaNaTela.Cartao(lido, new Dictionary<int, RespostaDeConfirmacao>())
            .SeloDaInfertilidade.Should().Be("Infertilidade · guia Geriatria");
    }

    [Fact]
    public async Task Marcar_infertilidade_sem_guia_valida_e_recusado_e_nada_e_gravado()
    {
        var p = await PacienteAsync();

        await FluentActions.Awaiting(() => ConsultaAsync(p.Id, Segunda, "Ginecologia"))
            .Should().ThrowAsync<InvalidOperationException>();
        await FluentActions.Awaiting(() => ConsultaAsync(p.Id, Segunda, ""))
            .Should().ThrowAsync<InvalidOperationException>();
        (await _db.Agendamentos.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Quem_remarca_sem_saber_da_marca_a_preserva()
    {
        // O faturamento, o Empurrar e a grade (arrastar) remarcam sem o parâmetro.
        var p = await PacienteAsync();
        var ag = await ConsultaAsync(p.Id, Segunda, "Psiquiatria");
        _db.ChangeTracker.Clear();

        await _agenda.RemarcarAsync(ag.Id, Segunda.AddDays(1), null, operador: "recepcao");

        (await _db.Agendamentos.AsNoTracking().SingleAsync()).InfertilidadeGuiaDe.Should().Be("Psiquiatria");
    }

    [Fact]
    public async Task Trocar_e_limpar_a_guia_ficam_na_trilha()
    {
        var p = await PacienteAsync();
        var ag = await ConsultaAsync(p.Id, Segunda, "Geriatria");
        _db.ChangeTracker.Clear();

        await _agenda.RemarcarAsync(ag.Id, Segunda, null, operador: "ana", infertilidadeGuiaDe: "Psiquiatria");
        _db.ChangeTracker.Clear();
        (await _db.Agendamentos.AsNoTracking().SingleAsync()).InfertilidadeGuiaDe.Should().Be("Psiquiatria");

        await _agenda.RemarcarAsync(ag.Id, Segunda, null, operador: "ana", infertilidadeGuiaDe: "");
        _db.ChangeTracker.Clear();
        (await _db.Agendamentos.AsNoTracking().SingleAsync()).InfertilidadeGuiaDe.Should().BeNull();

        var trilha = await _db.Auditoria.AsNoTracking().Where(e => e.Acao == "InfertilidadeGuiaInterna").ToListAsync();
        trilha.Should().HaveCount(2);
        trilha.Should().OnlyContain(e => e.Operador == "ana");
        trilha[0].Detalhe.Should().Contain("guia Geriatria").And.Contain("guia Psiquiatria");
    }

    // ==================== a premissa ====================

    [Fact]
    public async Task A_marca_e_so_classificacao_interna_as_guias_geradas_sao_as_mesmas()
    {
        // A direção: "isso não vai para o convênio". O que o sistema gera para a operadora
        // (os códigos do atendimento) não pode depender desta marca — é este teste que quebra
        // se alguém um dia a ligar à regra de guia.
        var com = await PacienteAsync("Com a marca");
        var sem = await PacienteAsync("Sem a marca");
        var marcada = await ConsultaAsync(com.Id, Segunda, "Psiquiatria");
        var comum = await ConsultaAsync(sem.Id, Segunda.AddHours(1), null);

        var guiasDaMarcada = (await _agenda.ConfirmarPresencaAsync(marcada.Id, operador: "recepcao")).Atendimento;
        var guiasDaComum = (await _agenda.ConfirmarPresencaAsync(comum.Id, operador: "recepcao")).Atendimento;

        guiasDaMarcada.Codigos.Select(c => (c.Tipo, c.Status))
            .Should().BeEquivalentTo(guiasDaComum.Codigos.Select(c => (c.Tipo, c.Status)));
        guiasDaMarcada.EspecialidadeConsultaCodigo.Should().Be(guiasDaComum.EspecialidadeConsultaCodigo)
            .And.Be("Geriatria", "a especialidade do atendimento é a da consulta, não a da guia interna");
    }
}
