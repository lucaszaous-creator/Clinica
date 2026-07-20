using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Tests;

/// <summary>
/// O radar de glosas cruza as candidatas a um lote com o histórico da clínica e avisa,
/// ANTES do envio, o que provavelmente voltará glosado — o diferencial de mercado.
/// </summary>
public class PrevencaoGlosaTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly PrevencaoGlosaService _radar;

    public PrevencaoGlosaTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _radar = new PrevencaoGlosaService(_repo);
    }

    private async Task<Paciente> NovoPacienteAsync(string nome, DateOnly? validadeCarteirinha = null)
    {
        var p = new Paciente
        {
            Nome = nome,
            Convenio = Convenio.UnimedPadrao,
            Sexo = Sexo.Feminino,
            Carteirinha = "CART-" + nome,
            ValidadeCarteirinha = validadeCarteirinha
        };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p;
    }

    /// <summary>Cria uma guia já baixada; opcionalmente já glosada (para montar histórico).</summary>
    private async Task<CodigoFaturamento> GuiaBaixadaAsync(
        Paciente paciente, DateOnly data, TipoCodigo tipo, bool glosada, string? motivo = null)
    {
        var atendimento = new Atendimento { PacienteId = paciente.Id, Data = data, Modalidade = ModalidadeAtendimento.AcupunturaSimples };
        var codigo = new CodigoFaturamento
        {
            Atendimento = atendimento,
            Tipo = tipo,
            DataPrevistaFaturamento = data,
            Status = StatusCodigo.Aberto
        };
        atendimento.Codigos.Add(codigo);
        _db.Atendimentos.Add(atendimento);
        await _db.SaveChangesAsync();

        codigo.DarBaixa(data, "G-" + codigo.Id, "sec", null);
        if (glosada)
            codigo.RegistrarGlosa(data.AddDays(5), "motivo", motivo);
        await _db.SaveChangesAsync();
        return codigo;
    }

    [Fact]
    public async Task CarteirinhaVencida_GeraAlerta()
    {
        var hoje = new DateOnly(2026, 7, 20);
        var paciente = await NovoPacienteAsync("Ana", validadeCarteirinha: hoje.AddDays(-1));
        var guia = await GuiaBaixadaAsync(paciente, hoje, TipoCodigo.Acupuntura, glosada: false);

        var alertas = await _radar.AnalisarAsync(new[] { guia }, hoje);

        alertas.Should().Contain(a => a.Contains("VENCIDA") && a.Contains("Ana"));
    }

    [Fact]
    public async Task GuiasDuplicadas_GeramAlertaDeDuplicidade()
    {
        var hoje = new DateOnly(2026, 7, 20);
        var paciente = await NovoPacienteAsync("Bia");
        var g1 = await GuiaBaixadaAsync(paciente, hoje, TipoCodigo.Acupuntura, glosada: false);
        var g2 = await GuiaBaixadaAsync(paciente, hoje, TipoCodigo.Acupuntura, glosada: false);

        var alertas = await _radar.AnalisarAsync(new[] { g1, g2 }, hoje);

        alertas.Should().Contain(a => a.Contains("duplicidade"));
    }

    [Fact]
    public async Task PadraoComAltaTaxaHistorica_GeraAlertaEstatisticoComMotivo()
    {
        var hoje = new DateOnly(2026, 7, 20);
        var paciente = await NovoPacienteAsync("Cida");

        // Histórico: 4 guias de acupuntura, 3 glosadas por 2001 (taxa 75% > 20%, ≥3 ocorrências).
        for (var i = 0; i < 3; i++)
            await GuiaBaixadaAsync(paciente, hoje.AddDays(-30 - i), TipoCodigo.Acupuntura, glosada: true, motivo: "2001");
        await GuiaBaixadaAsync(paciente, hoje.AddDays(-20), TipoCodigo.Acupuntura, glosada: false);

        // Candidata do mesmo padrão (convênio + tipo).
        var candidata = await GuiaBaixadaAsync(paciente, hoje, TipoCodigo.Acupuntura, glosada: false);

        var alertas = await _radar.AnalisarAsync(new[] { candidata }, hoje);

        alertas.Should().Contain(a => a.Contains("glosadas em") && a.Contains("2001"));
    }

    [Fact]
    public async Task PadraoSaudavel_NaoGeraAlerta()
    {
        var hoje = new DateOnly(2026, 7, 20);
        var paciente = await NovoPacienteAsync("Dora", validadeCarteirinha: hoje.AddYears(1));

        // Histórico limpo: várias baixas, nenhuma glosa.
        for (var i = 0; i < 5; i++)
            await GuiaBaixadaAsync(paciente, hoje.AddDays(-30 - i), TipoCodigo.Acupuntura, glosada: false);

        var candidata = await GuiaBaixadaAsync(paciente, hoje, TipoCodigo.Acupuntura, glosada: false);

        var alertas = await _radar.AnalisarAsync(new[] { candidata }, hoje);

        alertas.Should().BeEmpty();
    }

    [Fact]
    public async Task PoucasOcorrencias_NaoDisparaEstatistica()
    {
        var hoje = new DateOnly(2026, 7, 20);
        var paciente = await NovoPacienteAsync("Eva", validadeCarteirinha: hoje.AddYears(1));

        // Só 2 glosas históricas (< MinimoOcorrencias = 3): não vira alerta estatístico.
        await GuiaBaixadaAsync(paciente, hoje.AddDays(-30), TipoCodigo.Acupuntura, glosada: true, motivo: "2001");
        await GuiaBaixadaAsync(paciente, hoje.AddDays(-29), TipoCodigo.Acupuntura, glosada: true, motivo: "2001");

        var candidata = await GuiaBaixadaAsync(paciente, hoje, TipoCodigo.Acupuntura, glosada: false);

        var alertas = await _radar.AnalisarAsync(new[] { candidata }, hoje);

        alertas.Should().NotContain(a => a.Contains("glosadas em"));
    }

    [Fact]
    public async Task ListaVazia_NaoQuebra()
    {
        var alertas = await _radar.AnalisarAsync(Array.Empty<CodigoFaturamento>(), new DateOnly(2026, 7, 20));
        alertas.Should().BeEmpty();
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
