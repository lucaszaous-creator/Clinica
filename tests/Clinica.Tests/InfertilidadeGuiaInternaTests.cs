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

    [Fact]
    public void A_guia_e_pedida_pelo_TIPO_de_atendimento_infertilidade_e_so_por_ele()
    {
        InfertilidadeNaTela.EhInfertilidade(ehConsulta: true, "Infertilidade").Should().BeTrue();
        InfertilidadeNaTela.EhInfertilidade(ehConsulta: true, "Ginecologia").Should().BeFalse();
        InfertilidadeNaTela.EhInfertilidade(ehConsulta: true, "SuplementacaoGestante").Should().BeFalse();
        InfertilidadeNaTela.EhInfertilidade(ehConsulta: true, null).Should().BeFalse();
        InfertilidadeNaTela.EhInfertilidade(ehConsulta: false, "Infertilidade").Should().BeFalse("só consulta tem tipo de atendimento");
    }

    [Fact]
    public async Task Os_tres_tipos_de_atendimento_estao_no_catalogo_e_a_clinica_que_desativa_um_e_obedecida()
    {
        // A direção: "pode dividir o meu atendimento em três: infertilidade, ginecologia e
        // suplementação para gestante". Os dois novos entram pelo catálogo, nunca pelo enum.
        var catalogo = new EspecialidadeCatalogoService(new ClinicaRepositorio(_db));

        var tipos = await catalogo.ListarAsync();
        tipos.Should().Contain(e => e.Codigo == "Infertilidade" && e.Nome == "Infertilidade" && e.Ativo);
        tipos.Should().Contain(e => e.Codigo == "SuplementacaoGestante" && e.Nome == "Suplementação para gestante" && e.Ativo);
        tipos.Should().Contain(e => e.Codigo == nameof(Especialidade.Ginecologia));
        Enum.GetNames<Especialidade>().Should().NotContain("Infertilidade", "tipo novo não entra no enum embutido");

        await catalogo.SalvarAsync([new EspecialidadeCadastro { Codigo = "SuplementacaoGestante", Nome = "Suplementação", Ativo = false }]);
        _db.ChangeTracker.Clear();

        var depois = (await catalogo.ListarAsync()).Single(e => e.Codigo == "SuplementacaoGestante");
        depois.Ativo.Should().BeFalse();
        depois.Nome.Should().Be("Suplementação");
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

    // ==================== o relatório ====================

    private Task<Agendamento> ConsultaDoTipoAsync(int pacienteId, DateTime quando, string tipo, string? guiaDe = null, bool particular = false)
        => _agenda.AgendarAsync(pacienteId, quando, ModalidadeAtendimento.Consulta, null,
            especialidadeConsultaCodigo: tipo, profissionalId: _medica, operador: "recepcao",
            particular: particular, infertilidadeGuiaDe: guiaDe);

    [Fact]
    public async Task O_relatorio_por_profissional_conta_por_tipo_de_atendimento_e_por_guia_interna()
    {
        // "Precisamos lastrear e deixar nos relatórios": o que foi marcado tem de sair contado.
        var catalogo = new EspecialidadeCatalogoService(new ClinicaRepositorio(_db));
        await catalogo.RecarregarCacheAsync();
        var p = await PacienteAsync();
        var horarios = new[]
        {
            await ConsultaDoTipoAsync(p.Id, Segunda, "Infertilidade", "Geriatria"),
            await ConsultaDoTipoAsync(p.Id, Segunda.AddDays(1), "Infertilidade", "Psiquiatria", particular: true),
            await ConsultaDoTipoAsync(p.Id, Segunda.AddDays(2), "Infertilidade", "Geriatria"),
            await ConsultaDoTipoAsync(p.Id, Segunda.AddDays(3), "Ginecologia"),
            await ConsultaDoTipoAsync(p.Id, Segunda.AddDays(4), "SuplementacaoGestante")
        };
        foreach (var h in horarios) await _agenda.ConfirmarPresencaAsync(h.Id, operador: "recepcao");
        _db.ChangeTracker.Clear();

        var repo = new ClinicaRepositorio(_db);
        var relatorio = await new AtendimentosPorProfissionalService(repo, new PrecoConvenioService(repo))
            .GerarAsync(DateOnly.FromDateTime(Segunda), DateOnly.FromDateTime(Segunda.AddDays(6)));

        relatorio.Quantidade.Should().Be(5);
        relatorio.PorTipo.Select(c => (c.Nome, c.Quantidade, c.Particulares)).Should().Equal(
            ("Infertilidade", 3, 1), ("Ginecologia", 1, 0), ("Suplementação para gestante", 1, 0));
        relatorio.PorGuiaInterna.Select(c => (c.Nome, c.Quantidade)).Should().Equal(("Geriatria", 2), ("Psiquiatria", 1));

        var linhas = relatorio.Profissionais.Single().Atendimentos;
        linhas[0].Procedimento.Should().Be("Consulta · Infertilidade");
        linhas[0].GuiaInterna.Should().Be("Geriatria");
        linhas[3].GuiaInterna.Should().BeNull("ginecologia não tem guia interna");
        AtendimentosPorProfissionalService.ProcedimentoComGuiaInterna(linhas[1])
            .Should().Be("Consulta · Infertilidade (guia interna: Psiquiatria)");

        // A planilha: as duas colunas novas no FIM, sem deslocar as que já existiam.
        AtendimentosPorProfissionalService.CabecalhoCsv.TakeLast(2)
            .Should().Equal("Tipo de atendimento", "Guia interna (infertilidade)");
        var csv = AtendimentosPorProfissionalService.LinhasCsv(relatorio);
        csv[0].TakeLast(2).Should().Equal("Infertilidade", "Geriatria");
        csv[4].TakeLast(2).Should().Equal("Suplementação para gestante", "");
        AtendimentosPorProfissionalService.ResumoDasContagens(relatorio.PorGuiaInterna).Should().Be("Geriatria 2 · Psiquiatria 1");
        AtendimentosPorProfissionalService.Pdf(relatorio, null, "Clínica").Should().NotBeEmpty();
    }

    [Fact]
    public async Task Sem_consulta_no_periodo_os_quadros_novos_ficam_vazios()
    {
        var p = await PacienteAsync();
        var ag = await _agenda.AgendarAsync(p.Id, Segunda, ModalidadeAtendimento.BsvApenas, null, profissionalId: _medica, operador: "recepcao");
        await _agenda.ConfirmarPresencaAsync(ag.Id, operador: "recepcao");
        _db.ChangeTracker.Clear();

        var repo = new ClinicaRepositorio(_db);
        var relatorio = await new AtendimentosPorProfissionalService(repo, new PrecoConvenioService(repo))
            .GerarAsync(DateOnly.FromDateTime(Segunda), DateOnly.FromDateTime(Segunda));

        relatorio.Quantidade.Should().Be(1);
        relatorio.PorTipo.Should().BeEmpty("BSV não é consulta: não tem tipo de atendimento");
        relatorio.PorGuiaInterna.Should().BeEmpty();
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
