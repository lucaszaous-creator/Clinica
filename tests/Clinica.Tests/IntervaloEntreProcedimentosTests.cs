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
/// Sul América (nov/2026): acupuntura + eletroacupuntura no MESMO dia; a acupuntura libera
/// nova com 1 dia de intervalo e a eletro só autoriza a cada 7 dias. A guia que cairia antes
/// nasce com a data em que o convênio a libera.
///
/// A regra pura está em <see cref="RegraGenericaTests"/>; aqui é o caminho de verdade —
/// <c>AtendimentoService</c> lendo o histórico do paciente pelo repositório
/// (<c>CodigosDoPacienteDesdeAsync</c>), cruzando a virada do mês, na prévia e no lançamento.
/// </summary>
public class IntervaloEntreProcedimentosTests : IDisposable
{
    private const string CodigoSulAmerica = "SulAmericaDoTeste";

    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly AtendimentoService _atendimentos;
    private readonly ClinicaRepositorio _repo;
    private readonly ParametrosService _parametros;
    private readonly AgendaService _agenda;
    private readonly int _profissional;

    public IntervaloEntreProcedimentosTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _atendimentos = new AtendimentoService(new ClinicaRepositorio(_db));
        var prof = new Profissional { Nome = "Dra. Intervalo" };
        _db.Profissionais.Add(prof);
        _db.SaveChanges();
        _profissional = prof.Id;
        _repo = new ClinicaRepositorio(_db);
        _parametros = new ParametrosService(_repo);
        _agenda = new AgendaService(_repo, new AtendimentoService(_repo, parametros: _parametros), _parametros);

        // O catálogo é um cache estático: cada teste monta o seu.
        CatalogoConvenios.Atualizar(
        [
            new EntradaConvenio(CodigoSulAmerica, "Sul América", Convenio.Personalizado, true,
                new ConfiguracaoRegraGenerica
                {
                    FazEletro = true, EletroNoMesmoDia = true,
                    IntervaloAcupunturaDias = 1, IntervaloEletroDias = 7
                },
                FormatoNumeroGuia.SemValidacao)
        ]);
    }

    public void Dispose()
    {
        CatalogoConvenios.Atualizar([]);
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<int> PacienteAsync()
    {
        var p = new Paciente
        {
            Nome = "Paciente Sul América", Convenio = Convenio.Personalizado,
            ConvenioCodigo = CodigoSulAmerica, Sexo = Sexo.Feminino
        };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    [Fact]
    public async Task Acupuntura_e_eletro_saem_no_dia_e_a_eletro_seguinte_espera_os_7_dias_atravessando_o_mes()
    {
        var id = await PacienteAsync();

        var primeira = await _atendimentos.LancarAsync(id, new DateOnly(2026, 10, 29), ModalidadeAtendimento.AcupunturaComEletro);
        primeira.Atendimento.Codigos.Should().HaveCount(2)
            .And.OnlyContain(c => c.DataPrevistaFaturamento == new DateOnly(2026, 10, 29) && c.Ordem == OrdemCodigo.Primeiro);

        // 02/11: a acupuntura sai no dia; a eletro só a partir de 05/11 (29/10 + 7).
        var previa = await _atendimentos.PreverAsync(id, new DateOnly(2026, 11, 2), ModalidadeAtendimento.AcupunturaComEletro);
        previa.Codigos.Single(c => c.Tipo == TipoCodigo.Eletroacupuntura).DataPrevistaFaturamento
            .Should().Be(new DateOnly(2026, 11, 5), "a prévia mostra o mesmo que o lançamento vai gravar");

        var segunda = await _atendimentos.LancarAsync(id, new DateOnly(2026, 11, 2), ModalidadeAtendimento.AcupunturaComEletro);
        segunda.Atendimento.Codigos.Single(c => c.Tipo == TipoCodigo.Acupuntura).DataPrevistaFaturamento
            .Should().Be(new DateOnly(2026, 11, 2));
        var eletro = segunda.Atendimento.Codigos.Single(c => c.Tipo == TipoCodigo.Eletroacupuntura);
        eletro.DataPrevistaFaturamento.Should().Be(new DateOnly(2026, 11, 5));
        eletro.Ordem.Should().Be(OrdemCodigo.Segundo);
        segunda.Avisos.Should().Contain(a => a.Contains("05/11/2026"));
    }

    // ===== Guia no agendamento: remarcação, série e cancelamento em bloco =====

    /// <summary>Um dia útil qualquer daqui a duas semanas, às 14h — longe do "hoje" de quem roda.</summary>
    private static readonly DateTime D = DateTime.Today.AddDays(14).AddHours(14);
    private static DateOnly Dia(int mais) => DateOnly.FromDateTime(D).AddDays(mais);

    private Task<Agendamento> MarcarAsync(int pacienteId, DateTime quando)
        => _agenda.AgendarAsync(pacienteId, quando, ModalidadeAtendimento.AcupunturaComEletro, null, profissionalId: _profissional);

    private async Task<List<CodigoFaturamento>> GuiasAsync(int agendamentoId)
    {
        var ag = await _db.Agendamentos.AsNoTracking().SingleAsync(a => a.Id == agendamentoId);
        return await _db.Codigos.AsNoTracking().Where(c => c.AtendimentoId == ag.AtendimentoId).ToListAsync();
    }

    [Fact]
    public async Task Remarcar_por_data_recalcula_a_eletro_pelo_intervalo_e_nao_pelo_delta()
    {
        var id = await PacienteAsync();
        await _parametros.DefinirGuiaNoAgendamentoAsync(true);

        await MarcarAsync(id, D);
        var segunda = await MarcarAsync(id, D.AddDays(3));
        (await GuiasAsync(segunda.Id)).Single(c => c.Tipo == TipoCodigo.Eletroacupuntura)
            .DataPrevistaFaturamento.Should().Be(Dia(7), "a eletro espera os 7 dias da anterior");

        // Para depois da liberação: a eletro sai no próprio dia (o delta daria D+12).
        await _agenda.RemarcarAsync(segunda.Id, D.AddDays(8), null);
        var depois = await GuiasAsync(segunda.Id);
        var eletro = depois.Single(c => c.Tipo == TipoCodigo.Eletroacupuntura);
        eletro.DataPrevistaFaturamento.Should().Be(Dia(8));
        eletro.Ordem.Should().Be(OrdemCodigo.Primeiro);
        depois.Single(c => c.Tipo == TipoCodigo.Acupuntura).DataPrevistaFaturamento.Should().Be(Dia(8));
        depois.Should().HaveCount(2, "remarcar não substitui guia: são as mesmas linhas");

        // De volta para perto da anterior: adia de novo (o delta daria D+2).
        await _agenda.RemarcarAsync(segunda.Id, D.AddDays(2), null);
        var eletroDeNovo = (await GuiasAsync(segunda.Id)).Single(c => c.Tipo == TipoCodigo.Eletroacupuntura);
        eletroDeNovo.DataPrevistaFaturamento.Should().Be(Dia(7));
        eletroDeNovo.Ordem.Should().Be(OrdemCodigo.Segundo);
    }

    [Fact]
    public async Task Serie_marcada_de_uma_vez_respeita_o_intervalo_entre_as_proprias_sessoes()
    {
        var id = await PacienteAsync();
        await _parametros.DefinirGuiaNoAgendamentoAsync(true);

        var serie = await _agenda.AgendarSerieAsync(id, D, ModalidadeAtendimento.AcupunturaComEletro,
            quantidade: 3, intervaloDias: 3, profissionalId: _profissional);
        serie.Marcados.Should().HaveCount(3);

        var guias = new List<CodigoFaturamento>();
        foreach (var ag in serie.Marcados) guias.AddRange(await GuiasAsync(ag.Id));

        guias.Where(c => c.Tipo == TipoCodigo.Acupuntura).Select(c => c.DataPrevistaFaturamento)
            .Should().BeEquivalentTo([Dia(0), Dia(3), Dia(6)]);
        guias.Where(c => c.Tipo == TipoCodigo.Eletroacupuntura).Select(c => c.DataPrevistaFaturamento)
            .Should().BeEquivalentTo([Dia(0), Dia(7), Dia(14)], "cada sessão da série enxerga a gravada antes dela");
    }

    [Fact]
    public async Task Cancelar_a_serie_suspende_as_guias_e_elas_deixam_de_contar_no_intervalo()
    {
        var id = await PacienteAsync();
        await _parametros.DefinirGuiaNoAgendamentoAsync(true);

        var serie = await _agenda.AgendarSerieAsync(id, D, ModalidadeAtendimento.AcupunturaComEletro,
            quantidade: 2, intervaloDias: 7, profissionalId: _profissional);
        (await _agenda.CancelarSerieAsync(serie.SerieId, "ana")).Should().Be(2);

        foreach (var ag in serie.Marcados)
            (await GuiasAsync(ag.Id)).Should().OnlyContain(c => c.Status == StatusCodigo.NaoAplicavel,
                "sessão desistida não fatura — e guia aberta dela viraria pendência fantasma");

        var nova = await MarcarAsync(id, D.AddDays(3));
        (await GuiasAsync(nova.Id)).Single(c => c.Tipo == TipoCodigo.Eletroacupuntura)
            .DataPrevistaFaturamento.Should().Be(Dia(3), "a eletro cancelada não segura a próxima");
    }
}
