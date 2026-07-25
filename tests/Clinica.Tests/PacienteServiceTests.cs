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
/// Cadastro do paciente: retrato e consultas. O ponto sensível do retrato é o
/// armazenamento partido — miniatura na linha do paciente (alimenta os avatares) e foto
/// cheia numa tabela à parte, para a busca de pacientes não arrastar imagem.
/// </summary>
public class PacienteServiceTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly PacienteService _pacientes;

    private static readonly byte[] FotoCheia = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
    private static readonly byte[] Miniatura = new byte[] { 9, 9, 9 };

    public PacienteServiceTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _pacientes = new PacienteService(new ClinicaRepositorio(_db));
    }

    private async Task<Paciente> CriarPacienteAsync(string nome = "Maria da Silva")
    {
        var p = new Paciente { Nome = nome, Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p;
    }

    [Fact]
    public async Task DefinirFoto_GuardaMiniaturaNaLinhaEFotoCheiaNaTabelaAParte()
    {
        var paciente = await CriarPacienteAsync();

        await _pacientes.DefinirFotoAsync(paciente.Id, FotoCheia, Miniatura);

        var salvo = await _db.Pacientes.AsNoTracking().FirstAsync(p => p.Id == paciente.Id);
        salvo.FotoMiniatura.Should().Equal(Miniatura);
        salvo.FotoAtualizadaEm.Should().NotBeNull();
        salvo.TemFoto.Should().BeTrue();

        (await _pacientes.ObterFotoAsync(paciente.Id)).Should().Equal(FotoCheia);
    }

    [Fact]
    public async Task DefinirFoto_SubstituiORetratoAnteriorSemDuplicarLinha()
    {
        var paciente = await CriarPacienteAsync();
        await _pacientes.DefinirFotoAsync(paciente.Id, FotoCheia, Miniatura);

        var nova = new byte[] { 7, 7 };
        await _pacientes.DefinirFotoAsync(paciente.Id, nova, nova);

        (await _db.PacientesFotos.AsNoTracking().CountAsync()).Should().Be(1);
        (await _pacientes.ObterFotoAsync(paciente.Id)).Should().Equal(nova);
    }

    [Fact]
    public async Task DefinirFoto_RecusaConteudoVazio()
    {
        var paciente = await CriarPacienteAsync();

        var acao = async () => await _pacientes.DefinirFotoAsync(paciente.Id, Array.Empty<byte>(), Miniatura);

        await acao.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task RemoverFoto_LimpaMiniaturaEFotoCheia()
    {
        var paciente = await CriarPacienteAsync();
        await _pacientes.DefinirFotoAsync(paciente.Id, FotoCheia, Miniatura);

        await _pacientes.RemoverFotoAsync(paciente.Id);

        var salvo = await _db.Pacientes.AsNoTracking().FirstAsync(p => p.Id == paciente.Id);
        salvo.FotoMiniatura.Should().BeNull();
        salvo.FotoAtualizadaEm.Should().BeNull();
        salvo.TemFoto.Should().BeFalse();
        (await _pacientes.ObterFotoAsync(paciente.Id)).Should().BeNull();
    }

    [Fact]
    public async Task RemoverPaciente_LevaAFotoJunto()
    {
        var paciente = await CriarPacienteAsync();
        await _pacientes.DefinirFotoAsync(paciente.Id, FotoCheia, Miniatura);

        await _pacientes.RemoverAsync(paciente.Id);

        (await _db.PacientesFotos.AsNoTracking().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Buscar_NaoTrazAFotoCheiaJuntoDaLista()
    {
        var paciente = await CriarPacienteAsync();
        await _pacientes.DefinirFotoAsync(paciente.Id, FotoCheia, Miniatura);
        _db.ChangeTracker.Clear();

        var lista = await _pacientes.BuscarAsync(null);

        lista.Should().HaveCount(1);
        lista[0].FotoMiniatura.Should().Equal(Miniatura, "o avatar da lista vem da miniatura");
        lista[0].Foto.Should().BeNull("a foto cheia só é carregada sob demanda");
    }

    [Fact]
    public async Task CarteirinhaVencida_ComparaComODiaDeHoje()
    {
        var paciente = await CriarPacienteAsync();
        paciente.ValidadeCarteirinha = DateOnly.FromDateTime(DateTime.Today).AddDays(-1);
        paciente.CarteirinhaVencida.Should().BeTrue();

        paciente.ValidadeCarteirinha = DateOnly.FromDateTime(DateTime.Today);
        paciente.CarteirinhaVencida.Should().BeFalse("vence no fim do dia, não no começo");

        paciente.ValidadeCarteirinha = null;
        paciente.CarteirinhaVencida.Should().BeFalse("sem validade cadastrada não há o que vencer");
    }

    [Fact]
    public async Task Consultas_VemDaMaisRecenteParaAMaisAntiga()
    {
        var paciente = await CriarPacienteAsync();
        _db.Consultas.AddRange(
            new Consulta
            {
                PacienteId = paciente.Id, Convenio = Convenio.UnimedIntercambio,
                DataEmissao = new DateOnly(2026, 3, 1), ValidadeDias = 22,
                DataVencimento = new DateOnly(2026, 3, 23)
            },
            new Consulta
            {
                PacienteId = paciente.Id, Convenio = Convenio.UnimedIntercambio,
                DataEmissao = new DateOnly(2026, 7, 1), ValidadeDias = 22,
                DataVencimento = new DateOnly(2026, 7, 23)
            });
        await _db.SaveChangesAsync();

        var consultas = await _pacientes.ConsultasAsync(paciente.Id);

        consultas.Should().HaveCount(2);
        consultas[0].DataEmissao.Should().Be(new DateOnly(2026, 7, 1));
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
