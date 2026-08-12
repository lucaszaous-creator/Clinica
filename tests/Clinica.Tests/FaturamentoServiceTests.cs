using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Tests;

public class FaturamentoServiceTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly FaturamentoService _faturamento;

    public FaturamentoServiceTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _faturamento = new FaturamentoService(_repo);
    }

    private async Task<CodigoFaturamento> CriarCodigoAsync()
    {
        var p = new Paciente { Nome = "Paciente", Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        var r = await new AtendimentoService(_repo).LancarAsync(
            p.Id, new DateOnly(2026, 7, 10), ModalidadeAtendimento.AcupunturaComEletro);
        return r.Atendimento.Codigos.First();
    }

    [Fact]
    public async Task DarBaixa_RegistraDataGuiaEUsuario()
    {
        var codigo = await CriarCodigoAsync();

        await _faturamento.DarBaixaAsync(codigo.Id, new DateOnly(2026, 7, 11), "90123", "secretaria", "ok");

        var salvo = await _db.Codigos.AsNoTracking().FirstAsync(c => c.Id == codigo.Id);
        salvo.Baixado.Should().BeTrue();
        salvo.Status.Should().Be(StatusCodigo.Baixado);
        salvo.DataBaixa.Should().Be(new DateOnly(2026, 7, 11));
        salvo.NumeroGuiaReal.Should().Be("90123");
        salvo.UsuarioBaixa.Should().Be("secretaria");
    }

    [Fact]
    public async Task EstornarBaixa_ReabreAPendencia()
    {
        var codigo = await CriarCodigoAsync();
        await _faturamento.DarBaixaAsync(codigo.Id, new DateOnly(2026, 7, 11), "90123", "secretaria", null);

        await _faturamento.EstornarBaixaAsync(codigo.Id, "baixado por engano", "gerente");

        var salvo = await _db.Codigos.AsNoTracking().FirstAsync(c => c.Id == codigo.Id);
        salvo.Baixado.Should().BeFalse();
        salvo.Status.Should().Be(StatusCodigo.Aberto);
        salvo.DataBaixa.Should().BeNull();
        salvo.NumeroGuiaReal.Should().BeNull();
        salvo.UsuarioBaixa.Should().BeNull();
    }

    [Fact]
    public async Task EstornarBaixa_Solta_a_guia_do_lote_TISS()
    {
        // O caso real: guia baixada com o número errado e JÁ exportada num lote. O
        // estorno precisa soltar o vínculo — senão a guia corrigida nunca mais entra em
        // lote (a guarda de duplicidade viraria a que impede o faturamento) e o número
        // certo não chega à operadora. O lote antigo vira história na observação.
        var codigo = await CriarCodigoAsync();
        await _faturamento.DarBaixaAsync(codigo.Id, new DateOnly(2026, 7, 11), "90123", "secretaria", null);

        var lote = new LoteTiss { Numero = 7, DataGeracao = new DateOnly(2026, 7, 12) };
        _db.LotesTiss.Add(lote);
        await _db.SaveChangesAsync();
        (await _db.Codigos.FirstAsync(c => c.Id == codigo.Id)).LoteTissId = lote.Id;
        await _db.SaveChangesAsync();

        await _faturamento.EstornarBaixaAsync(codigo.Id, "número errado", "gerente");

        var salvo = await _db.Codigos.AsNoTracking().FirstAsync(c => c.Id == codigo.Id);
        salvo.LoteTissId.Should().BeNull("a guia rebaixada precisa voltar às candidatas do próximo lote");
        salvo.ObservacaoBaixa.Should().Contain("lote anterior", "o vínculo desfeito vira história, não silêncio");
    }

    [Fact]
    public async Task EstornarBaixa_DeCodigoAberto_NaoFazNada()
    {
        var codigo = await CriarCodigoAsync();

        await _faturamento.EstornarBaixaAsync(codigo.Id, "sem efeito", "gerente");

        var salvo = await _db.Codigos.AsNoTracking().FirstAsync(c => c.Id == codigo.Id);
        salvo.Baixado.Should().BeFalse();
        salvo.Status.Should().Be(StatusCodigo.Aberto);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
