using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// A lista de problemas do paciente (parcela 37).
///
/// As três regras que estes testes existem para travar:
/// **não se apaga — muda-se a situação** (como a NC do faturamento e o documento clínico
/// cancelado); **descartar exige motivo escrito** (única recusa do serviço, pela mesma
/// razão da justificativa do fechamento de caixa); e **alergia alerta mesmo resolvida**,
/// porque "resolvida" numa alergia é quase sempre "não reagiu da última vez".
/// </summary>
public class ListaProblemasTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly ProblemaPacienteService _problemas;

    public ListaProblemasTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _problemas = new ProblemaPacienteService(_repo);
    }

    private async Task<int> CriarPacienteAsync()
    {
        var p = new Paciente { Nome = "Paciente", Convenio = Convenio.UnimedIntercambio };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    private Task<ProblemaPaciente> RegistrarAsync(
        int pacienteId, string descricao,
        NaturezaProblema natureza = NaturezaProblema.Diagnostico,
        string? cid = null, DateOnly? inicio = null)
        => _problemas.SalvarAsync(new ProblemaPaciente
        {
            PacienteId = pacienteId,
            Descricao = descricao,
            Natureza = natureza,
            Cid = cid,
            Inicio = inicio
        }, "dra.ana");

    // ------------------------------------------------------------ o registro

    [Fact]
    public async Task O_cid_e_opcional_e_a_descricao_nao_e()
    {
        // Exigir CID faria o fisioterapeuta e o acupunturista pararem de usar a lista — e
        // lista pela metade é pior que nenhuma, porque seria lida como completa.
        var p = await CriarPacienteAsync();

        var sem = await RegistrarAsync(p, "Lombalgia crônica");
        sem.Cid.Should().BeNull();
        sem.Rotulo.Should().Be("Lombalgia crônica");

        var com = await RegistrarAsync(p, "Lombalgia crônica", cid: "m54.5");
        com.Cid.Should().Be("M54.5");
        com.Rotulo.Should().Be("Lombalgia crônica (M54.5)");

        var acao = () => RegistrarAsync(p, "   ");
        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*descrição*");
    }

    [Fact]
    public async Task A_lista_e_do_paciente_e_nasce_ativa()
    {
        var p = await CriarPacienteAsync();
        var problema = await RegistrarAsync(p, "Hipertensão");

        problema.EstaAtivo.Should().BeTrue();
        problema.Situacao.Should().Be(SituacaoProblema.Ativo);
        _db.Auditoria.Should().Contain(e => e.Acao == "ProblemaRegistrado");
    }

    [Fact]
    public async Task Fim_anterior_ao_inicio_e_recusado()
    {
        var p = await CriarPacienteAsync();

        var acao = () => _problemas.SalvarAsync(new ProblemaPaciente
        {
            PacienteId = p,
            Descricao = "Tendinite",
            Inicio = new DateOnly(2026, 5, 1),
            Fim = new DateOnly(2026, 4, 1)
        }, "dra.ana");

        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*anterior ao início*");
    }

    // ------------------------------------------------------------ o alerta

    [Fact]
    public async Task Alergia_alerta_mesmo_depois_de_dada_por_resolvida()
    {
        var p = await CriarPacienteAsync();
        var alergia = await RegistrarAsync(p, "Dipirona", NaturezaProblema.Alergia);

        await _problemas.ResolverAsync(alergia.Id, operador: "dra.ana");

        var alertas = await _problemas.AlertasAsync(p);
        alertas.Should().ContainSingle(a => a.Descricao == "Dipirona");
    }

    [Fact]
    public async Task Alergia_descartada_para_de_alertar()
    {
        // O descarte é a única coisa que cala o alerta: ele significa "esta linha nunca
        // devia ter existido", diferente de "o paciente melhorou".
        var p = await CriarPacienteAsync();
        var alergia = await RegistrarAsync(p, "Dipirona", NaturezaProblema.Alergia);

        await _problemas.DescartarAsync(alergia.Id, "paciente confundiu com outro remédio", "dra.ana");

        (await _problemas.AlertasAsync(p)).Should().BeEmpty();
    }

    [Fact]
    public async Task Diagnostico_e_antecedente_nao_viram_alerta_de_atendimento()
    {
        // Alerta que dispara para todo mundo é alerta que ninguém lê — a mesma razão pela
        // qual o ElegibilidadeService só alarma a partir de um atraso mínimo.
        var p = await CriarPacienteAsync();
        await RegistrarAsync(p, "Lombalgia");
        await RegistrarAsync(p, "Apendicectomia em 2019", NaturezaProblema.Antecedente);
        await RegistrarAsync(p, "Losartana 50 mg", NaturezaProblema.MedicacaoContinua);

        var alertas = await _problemas.AlertasAsync(p);

        alertas.Should().ContainSingle();
        alertas[0].Natureza.Should().Be(NaturezaProblema.MedicacaoContinua);
    }

    // ------------------------------------------------------------ o ciclo de vida

    [Fact]
    public async Task Resolver_nao_apaga_e_a_data_do_fim_e_informada()
    {
        // Quem registra na quarta o que se resolveu no sábado não pode ser obrigado a datar
        // errado — a mesma regra da data do crédito na conciliação de recebíveis.
        var p = await CriarPacienteAsync();
        var problema = await RegistrarAsync(p, "Tendinite", inicio: new DateOnly(2026, 1, 5));

        var fim = new DateOnly(2026, 6, 20);
        var resolvido = await _problemas.ResolverAsync(problema.Id, fim, "dra.ana");

        resolvido.Situacao.Should().Be(SituacaoProblema.Resolvido);
        resolvido.Fim.Should().Be(fim);

        // Sai dos ativos, NÃO da base.
        (await _problemas.DoPacienteAsync(p, somenteAtivos: true)).Should().BeEmpty();
        (await _problemas.DoPacienteAsync(p)).Should().ContainSingle();
    }

    [Fact]
    public async Task Descartar_sem_motivo_e_recusado()
    {
        var p = await CriarPacienteAsync();
        var problema = await RegistrarAsync(p, "Fibromialgia");

        var acao = () => _problemas.DescartarAsync(problema.Id, "  ", "dra.ana");

        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*por que*");
    }

    [Fact]
    public async Task Descartar_grava_o_motivo_e_a_linha_continua_na_base()
    {
        var p = await CriarPacienteAsync();
        var problema = await RegistrarAsync(p, "Fibromialgia");

        var descartado = await _problemas.DescartarAsync(
            problema.Id, "lançado na ficha errada", "dra.ana");

        descartado.Situacao.Should().Be(SituacaoProblema.Descartado);
        descartado.MotivoDescarte.Should().Be("lançado na ficha errada");
        (await _problemas.DoPacienteAsync(p)).Should().ContainSingle();
        _db.Auditoria.Should().Contain(e => e.Acao == "ProblemaDescartado");
    }

    [Fact]
    public async Task Reabrir_limpa_o_fim_e_o_motivo_do_descarte()
    {
        // Mantê-los faria a linha dizer "ativo desde sempre, encerrado em março".
        var p = await CriarPacienteAsync();
        var problema = await RegistrarAsync(p, "Cefaleia tensional");
        await _problemas.ResolverAsync(problema.Id, new DateOnly(2026, 3, 1), "dra.ana");

        var reaberto = await _problemas.ReabrirAsync(problema.Id, "dra.ana");

        reaberto.Situacao.Should().Be(SituacaoProblema.Ativo);
        reaberto.Fim.Should().BeNull();
        reaberto.MotivoDescarte.Should().BeNull();
    }

    [Fact]
    public async Task Resolver_um_descartado_e_recusado()
    {
        // "Encerrado" e "nunca existiu" são coisas diferentes, e passar de uma à outra sem
        // reabrir apagaria o motivo do descarte sem que ninguém o desdissesse.
        var p = await CriarPacienteAsync();
        var problema = await RegistrarAsync(p, "Bursite");
        await _problemas.DescartarAsync(problema.Id, "duplicado", "dra.ana");

        var acao = () => _problemas.ResolverAsync(problema.Id, operador: "dra.ana");

        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*descartado*");
    }

    [Fact]
    public async Task A_lista_traz_os_ativos_primeiro()
    {
        var p = await CriarPacienteAsync();
        var velho = await RegistrarAsync(p, "Resolvido");
        await _problemas.ResolverAsync(velho.Id, operador: "dra.ana");
        await RegistrarAsync(p, "Ativo agora");

        var lista = await _problemas.DoPacienteAsync(p);

        lista[0].Descricao.Should().Be("Ativo agora");
        lista[1].Descricao.Should().Be("Resolvido");
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }
}
