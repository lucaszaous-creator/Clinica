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
/// A COBERTURA DO CONVÊNIO (nov/2026). O pedido da direção, sobre a Petrobras: "vamos parar
/// com a acupuntura — agora é só consulta de geriatria, ginecologia e psiquiatria, uma por
/// semana de cada; especialidades diferentes podem ser na mesma semana; o sistema não permite
/// marcar acupuntura, nem libera; os que já estão marcados ficam como estão".
///
/// O que estes testes prendem é cada frase do pedido — e o que NÃO pode acontecer: a regra
/// alcançar outro convênio, alcançar a sessão particular ou prender quem já estava na agenda.
/// </summary>
public class CoberturaDoConvenioTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ParametrosService _parametros;
    private readonly AgendaService _agenda;
    private readonly int _medica;

    private static readonly DateTime Segunda = new(2026, 11, 16, 14, 0, 0);
    private static readonly CoberturaDoConvenio DaPetrobras = new(SoConsulta: true,
        [nameof(Especialidade.Geriatria), nameof(Especialidade.Ginecologia), nameof(Especialidade.Psiquiatria)], IntervaloDias: 7);

    public CoberturaDoConvenioTests()
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
        _parametros = new ParametrosService(repo);
        _agenda = new AgendaService(repo, new AtendimentoService(repo, parametros: _parametros), _parametros);

        CatalogoConvenios.Atualizar(
        [
            new EntradaConvenio(nameof(Convenio.Petrobras), "Petrobras", Convenio.Petrobras, true, GeraGuia: true),
            new EntradaConvenio(nameof(Convenio.Amil), "Amil", Convenio.Amil, true, GeraGuia: true),
            new EntradaConvenio(ConvenioCadastro.CodigoParticular, "Particular", Convenio.Personalizado, true,
                new ConfiguracaoRegraGenerica(), FormatoNumeroGuia.SemValidacao, GeraGuia: false)
        ]);
    }

    public void Dispose()
    {
        CatalogoConvenios.Atualizar([]);
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<Paciente> PacienteAsync(Convenio convenio = Convenio.Petrobras)
    {
        var p = new Paciente { Nome = "Mariane Carvalho", Convenio = convenio, ConvenioCodigo = convenio.ToString(), Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p;
    }

    private Task<Agendamento> ConsultaAsync(int pacienteId, DateTime quando, Especialidade especialidade, bool particular = false)
        => _agenda.AgendarAsync(pacienteId, quando, ModalidadeAtendimento.Consulta, null,
            especialidadeConsultaCodigo: especialidade.ToString(), profissionalId: _medica, operador: "recepcao", particular: particular);

    private Task<Agendamento> AcupunturaAsync(int pacienteId, DateTime quando, bool particular = false)
        => _agenda.AgendarAsync(pacienteId, quando, ModalidadeAtendimento.AcupunturaSimples, null,
            profissionalId: _medica, operador: "recepcao", particular: particular);

    private Task LigarAsync() => _parametros.DefinirCoberturaDoConvenioAsync(nameof(Convenio.Petrobras), DaPetrobras);

    [Fact]
    public async Task Sem_a_configuracao_nada_muda()
    {
        var p = await PacienteAsync();

        (await AcupunturaAsync(p.Id, Segunda)).Id.Should().BeGreaterThan(0);
        (await ConsultaAsync(p.Id, Segunda.AddHours(1), Especialidade.Endocrinologia)).Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Acupuntura_pelo_plano_nao_se_marca_e_a_frase_diz_a_saida()
    {
        await LigarAsync();
        var p = await PacienteAsync();

        var recusa = await FluentActions.Awaiting(() => AcupunturaAsync(p.Id, Segunda))
            .Should().ThrowAsync<CoberturaDoConvenioException>();
        recusa.Which.Message.Should().Contain("Petrobras").And.Contain("só consulta").And.Contain("PARTICULAR");
        (await _db.Agendamentos.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task A_mesma_acupuntura_como_PARTICULAR_marca()
    {
        await LigarAsync();
        var p = await PacienteAsync();

        (await AcupunturaAsync(p.Id, Segunda, particular: true)).Particular.Should().BeTrue();
    }

    [Fact]
    public async Task Consulta_de_especialidade_fora_da_lista_e_recusada()
    {
        await LigarAsync();
        var p = await PacienteAsync();

        (await FluentActions.Awaiting(() => ConsultaAsync(p.Id, Segunda, Especialidade.Endocrinologia))
            .Should().ThrowAsync<CoberturaDoConvenioException>())
            .Which.Message.Should().Contain("Geriatria").And.Contain("Ginecologia").And.Contain("Psiquiatria");
    }

    [Fact]
    public async Task Especialidades_diferentes_cabem_na_mesma_semana()
    {
        await LigarAsync();
        var p = await PacienteAsync();

        await ConsultaAsync(p.Id, Segunda, Especialidade.Geriatria);
        await ConsultaAsync(p.Id, Segunda.AddDays(1), Especialidade.Ginecologia);
        await ConsultaAsync(p.Id, Segunda.AddDays(2), Especialidade.Psiquiatria);

        (await _db.Agendamentos.CountAsync()).Should().Be(3);
    }

    [Fact]
    public async Task A_mesma_especialidade_so_de_sete_em_sete_dias_para_tras_e_para_a_frente()
    {
        await LigarAsync();
        var p = await PacienteAsync();
        await ConsultaAsync(p.Id, Segunda, Especialidade.Geriatria);

        var depois = await FluentActions.Awaiting(() => ConsultaAsync(p.Id, Segunda.AddDays(6), Especialidade.Geriatria))
            .Should().ThrowAsync<CoberturaDoConvenioException>();
        depois.Which.Message.Should().Contain("16/11/2026").And.Contain("23/11/2026");
        // Marcar ANTES de uma consulta que já existe fura o intervalo do mesmo jeito.
        await FluentActions.Awaiting(() => ConsultaAsync(p.Id, Segunda.AddDays(-3), Especialidade.Geriatria))
            .Should().ThrowAsync<CoberturaDoConvenioException>();

        (await ConsultaAsync(p.Id, Segunda.AddDays(7), Especialidade.Geriatria)).Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Consulta_particular_nao_conta_no_intervalo_nem_e_barrada_por_ele()
    {
        await LigarAsync();
        var p = await PacienteAsync();
        await ConsultaAsync(p.Id, Segunda, Especialidade.Geriatria, particular: true);

        (await ConsultaAsync(p.Id, Segunda.AddDays(2), Especialidade.Geriatria)).Id.Should().BeGreaterThan(0);
        (await ConsultaAsync(p.Id, Segunda.AddDays(3), Especialidade.Geriatria, particular: true)).Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task A_trava_geral_de_consulta_cede_ao_intervalo_proprio_do_convenio()
    {
        // Com a trava ligada, o prazo geral da Petrobras (qualquer consulta) barraria a
        // ginecologia por causa da geriatria da mesma semana — o contrário do pedido.
        await LigarAsync();
        await _parametros.DefinirTravaDeConsultaAsync(true);
        var p = await PacienteAsync();

        await ConsultaAsync(p.Id, Segunda, Especialidade.Geriatria);
        (await ConsultaAsync(p.Id, Segunda.AddDays(1), Especialidade.Ginecologia)).Id.Should().BeGreaterThan(0);
        (await _agenda.TravaDeConsultaAsync(p.Id, Segunda.AddDays(2), ModalidadeAtendimento.Consulta, false)).Should().BeNull();
    }

    [Fact]
    public async Task Outro_convenio_nao_e_alcancado()
    {
        await LigarAsync();
        var daAmil = await PacienteAsync(Convenio.Amil);

        (await AcupunturaAsync(daAmil.Id, Segunda)).Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Quem_ja_estava_marcado_fica_como_esta_e_pode_ser_remarcado()
    {
        var p = await PacienteAsync();
        var antiga = await AcupunturaAsync(p.Id, Segunda);
        await LigarAsync();
        _db.ChangeTracker.Clear();

        // Outro dia, mesma modalidade: a acupuntura que já existia continua remarcável.
        await _agenda.RemarcarAsync(antiga.Id, Segunda.AddDays(2), null, operador: "recepcao");

        (await _db.Agendamentos.AsNoTracking().SingleAsync()).DataHora.Should().Be(Segunda.AddDays(2));
        // Mas uma acupuntura NOVA pelo plano já não entra.
        await FluentActions.Awaiting(() => AcupunturaAsync(p.Id, Segunda.AddDays(9)))
            .Should().ThrowAsync<CoberturaDoConvenioException>();
    }

    [Fact]
    public async Task Remarcar_uma_consulta_para_dentro_do_intervalo_de_outra_e_recusado()
    {
        await LigarAsync();
        var p = await PacienteAsync();
        await ConsultaAsync(p.Id, Segunda, Especialidade.Geriatria);
        var segunda = await ConsultaAsync(p.Id, Segunda.AddDays(7), Especialidade.Geriatria);
        _db.ChangeTracker.Clear();

        await FluentActions.Awaiting(() => _agenda.RemarcarAsync(segunda.Id, Segunda.AddDays(4), null, operador: "recepcao"))
            .Should().ThrowAsync<CoberturaDoConvenioException>();
        // Para a frente, ela mesma não conta contra si.
        await _agenda.RemarcarAsync(segunda.Id, Segunda.AddDays(8), null, operador: "recepcao");
    }

    [Fact]
    public async Task Voltar_do_particular_ao_plano_confere_a_cobertura()
    {
        await LigarAsync();
        var p = await PacienteAsync();
        var particular = await AcupunturaAsync(p.Id, Segunda, particular: true);
        _db.ChangeTracker.Clear();

        await FluentActions.Awaiting(() => _agenda.DefinirParticularAsync(particular.Id, false, "recepcao"))
            .Should().ThrowAsync<CoberturaDoConvenioException>();
    }

    [Fact]
    public async Task A_cobertura_que_nao_restringe_nada_sai_da_configuracao()
    {
        await LigarAsync();
        (await _parametros.CoberturaDosConveniosAsync()).Should().ContainKey("petrobras", "a chave não distingue caixa");

        await _parametros.DefinirCoberturaDoConvenioAsync(nameof(Convenio.Petrobras), new CoberturaDoConvenio(false, [], 0));

        (await _parametros.CoberturaDosConveniosAsync()).Should().BeEmpty();
        var p = await PacienteAsync();
        (await AcupunturaAsync(p.Id, Segunda)).Id.Should().BeGreaterThan(0);
    }
}
