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

/// <summary>Consultas renováveis: registro/renovação e alarme de expiração (item 7).</summary>
public class ConsultaServiceTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly ConsultaService _consultas;

    public ConsultaServiceTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _consultas = new ConsultaService(_repo); // sem Parâmetros → usa validades padrão do ConvenioInfo
    }

    private async Task<int> CriarPacienteAsync(Convenio convenio, string? convenioCodigo = null)
    {
        var p = new Paciente { Nome = "Paciente", Convenio = convenio, ConvenioCodigo = convenioCodigo, Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    /// <summary>Registra no catálogo um convênio personalizado SEM consulta renovável.</summary>
    private static void RegistrarPersonalizadoSemConsulta(string codigo)
        => CatalogoConvenios.Atualizar(new[]
        {
            new EntradaConvenio(codigo, "Convênio Sem Consulta", Convenio.Personalizado, Ativo: true,
                new ConfiguracaoRegraGenerica { ValidadeConsultaDias = null })
        });

    [Fact]
    public async Task Renovar_CriaConsultaAtivaComVencimentoPelaValidade()
    {
        var id = await CriarPacienteAsync(Convenio.UnimedIntercambio); // validade 22 dias
        var emissao = new DateOnly(2026, 7, 1);

        var c = await _consultas.RenovarAsync(id, emissao);

        c.Status.Should().Be(StatusConsulta.Ativa);
        c.ValidadeDias.Should().Be(22);
        c.DataVencimento.Should().Be(emissao.AddDays(22));
    }

    [Fact]
    public async Task Renovar_DuasVezes_MarcaAnteriorComoRenovada()
    {
        var id = await CriarPacienteAsync(Convenio.Amil);
        await _consultas.RenovarAsync(id, new DateOnly(2026, 7, 1));
        await _consultas.RenovarAsync(id, new DateOnly(2026, 7, 20));

        var todas = await _repo.ConsultasDoPacienteAsync(id);
        todas.Should().HaveCount(2);
        todas.Count(c => c.Status == StatusConsulta.Ativa).Should().Be(1);
        todas.Count(c => c.Status == StatusConsulta.Renovada).Should().Be(1);
    }

    [Fact]
    public async Task Renovar_Petrobras_Usa30Dias()
    {
        var id = await CriarPacienteAsync(Convenio.Petrobras); // consulta renova a cada 30 dias
        var emissao = new DateOnly(2026, 7, 1);

        var c = await _consultas.RenovarAsync(id, emissao);

        c.ValidadeDias.Should().Be(30);
        c.DataVencimento.Should().Be(emissao.AddDays(30));
    }

    [Fact]
    public async Task Renovar_ConvenioSemConsulta_Lanca()
    {
        // Personalizado sem validade configurada no catálogo → não usa consulta renovável.
        RegistrarPersonalizadoSemConsulta("CVSEMCONS");
        var id = await CriarPacienteAsync(Convenio.Personalizado, "CVSEMCONS");
        var acao = () => _consultas.RenovarAsync(id, new DateOnly(2026, 7, 1));
        await acao.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Listar_ConsultaVencida_EntraEmAlertaVermelho()
    {
        var id = await CriarPacienteAsync(Convenio.UnimedIntercambio); // 22 dias
        await _consultas.RenovarAsync(id, new DateOnly(2026, 6, 1));    // vence 23/06

        var lista = await _consultas.ListarAsync(new DateOnly(2026, 7, 16));

        var item = lista.Single(s => s.PacienteId == id);
        item.UsaConsulta.Should().BeTrue();
        item.Vencida.Should().BeTrue();
        item.PrecisaRenovar.Should().BeTrue();
        item.Urgencia.Should().Be(NivelUrgencia.Vermelho);
    }

    [Fact]
    public async Task Listar_ConvenioSemConsulta_NaoAlerta()
    {
        RegistrarPersonalizadoSemConsulta("CVSEMCONS");
        var id = await CriarPacienteAsync(Convenio.Personalizado, "CVSEMCONS");

        var item = (await _consultas.ListarAsync(new DateOnly(2026, 7, 16))).Single(s => s.PacienteId == id);

        item.UsaConsulta.Should().BeFalse();
        item.PrecisaRenovar.Should().BeFalse();
    }

    [Fact]
    public async Task Listar_ConsultaDentroDoPrazo_NaoAlerta()
    {
        var id = await CriarPacienteAsync(Convenio.UnimedIntercambio);
        await _consultas.RenovarAsync(id, new DateOnly(2026, 7, 15)); // vence 06/08

        var item = (await _consultas.ListarAsync(new DateOnly(2026, 7, 16))).Single(s => s.PacienteId == id);

        item.Vencida.Should().BeFalse();
        item.PrecisaRenovar.Should().BeFalse();
        item.Urgencia.Should().Be(NivelUrgencia.Verde);
    }

    // ---- Situação em lote: a agenda e o balcão lendo a MESMA regra da aba Consultas ----

    [Fact]
    public async Task SituacaoDe_UsaAMesmaRegraDaAbaConsultas()
    {
        // Duas definições de "precisa renovar" divergiriam na primeira correção, e a
        // agenda passaria a discordar da aba Consultas sobre o mesmo paciente.
        var vencido = await CriarPacienteAsync(Convenio.UnimedIntercambio);
        var emDia = await CriarPacienteAsync(Convenio.Amil);
        await _consultas.RenovarAsync(vencido, new DateOnly(2026, 6, 1));  // 22 dias → vence 23/06
        await _consultas.RenovarAsync(emDia, new DateOnly(2026, 7, 15));   // 30 dias → vence 14/08

        var referencia = new DateOnly(2026, 7, 16);
        var pacientes = await _repo.PacientesComConsultasAsync();
        var lote = await _consultas.SituacaoDeAsync(pacientes, referencia);
        var lista = await _consultas.ListarAsync(referencia);

        foreach (var esperado in lista)
        {
            var obtido = lote[esperado.PacienteId];
            obtido.PrecisaRenovar.Should().Be(esperado.PrecisaRenovar);
            obtido.Vencida.Should().Be(esperado.Vencida);
            obtido.Urgencia.Should().Be(esperado.Urgencia);
            obtido.DiasParaVencer.Should().Be(esperado.DiasParaVencer);
        }

        lote[vencido].ARenovar.Should().BeTrue();
        lote[emDia].ARenovar.Should().BeFalse();
    }

    [Fact]
    public async Task SituacaoDe_PacienteRepetidoNoDia_ApareceUmaVezSo()
    {
        // O mesmo paciente pode ter dois horários na agenda do dia.
        var id = await CriarPacienteAsync(Convenio.Amil);
        await _consultas.RenovarAsync(id, new DateOnly(2026, 7, 1));
        var paciente = (await _repo.PacientesComConsultasAsync()).Single(p => p.Id == id);

        var lote = await _consultas.SituacaoDeAsync(
            new[] { paciente, paciente }, new DateOnly(2026, 7, 16));

        lote.Should().HaveCount(1);
    }

    [Fact]
    public async Task SituacaoDe_SemPacientes_NaoVaiAoBanco()
    {
        var lote = await _consultas.SituacaoDeAsync(Array.Empty<Paciente>(), new DateOnly(2026, 7, 16));
        lote.Should().BeEmpty();
    }

    [Fact]
    public async Task ARenovar_PacienteQueNuncaEmitiuConsulta_NaoAcendeNaAgenda()
    {
        // PrecisaRenovar é verdadeiro (a aba Consultas mostra a linha), mas ARenovar é
        // falso: na agenda o selo acenderia para toda a base de convênio no primeiro dia,
        // e alerta que dispara para todo mundo é alerta que ninguém lê. É a mesma escolha
        // que o painel de pendências já fazia.
        var id = await CriarPacienteAsync(Convenio.UnimedIntercambio);

        var item = (await _consultas.ListarAsync(new DateOnly(2026, 7, 16))).Single(s => s.PacienteId == id);

        item.PrecisaRenovar.Should().BeTrue();
        item.ARenovar.Should().BeFalse();
        item.AvisoRenovacao.Should().BeNull();
        item.SeloRenovacao.Should().BeNull();
    }

    [Fact]
    public async Task AvisoRenovacao_DizSeVenceuOuQuandoVence()
    {
        var vencido = await CriarPacienteAsync(Convenio.UnimedIntercambio);
        var aVencer = await CriarPacienteAsync(Convenio.Amil);
        await _consultas.RenovarAsync(vencido, new DateOnly(2026, 6, 1));   // venceu 23/06
        await _consultas.RenovarAsync(aVencer, new DateOnly(2026, 6, 18));  // vence 18/07

        var referencia = new DateOnly(2026, 7, 16); // 2 dias para o segundo vencer
        var lista = await _consultas.ListarAsync(referencia);

        lista.Single(s => s.PacienteId == vencido).AvisoRenovacao
            .Should().Contain("vencida em 23/06/2026");
        lista.Single(s => s.PacienteId == vencido).SeloRenovacao.Should().Be("Consulta vencida");

        lista.Single(s => s.PacienteId == aVencer).AvisoRenovacao
            .Should().Contain("vence em 2 dia(s)");
        lista.Single(s => s.PacienteId == aVencer).SeloRenovacao.Should().Be("Consulta a renovar");
    }

    [Fact]
    public async Task DoPaciente_RespondeSobreUmSo()
    {
        var id = await CriarPacienteAsync(Convenio.UnimedIntercambio);
        await _consultas.RenovarAsync(id, new DateOnly(2026, 6, 1));

        var situacao = await _consultas.DoPacienteAsync(id, new DateOnly(2026, 7, 16));

        situacao.Should().NotBeNull();
        situacao!.Vencida.Should().BeTrue();
        situacao.ARenovar.Should().BeTrue();
    }

    [Fact]
    public async Task DoPaciente_Inexistente_DevolveNulo()
    {
        (await _consultas.DoPacienteAsync(9999, new DateOnly(2026, 7, 16))).Should().BeNull();
    }

    public void Dispose()
    {
        CatalogoConvenios.Atualizar(Array.Empty<EntradaConvenio>()); // não vaza o catálogo para outros testes
        _db.Dispose();
        _conn.Dispose();
    }
}
