using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Tests;

/// <summary>
/// Observação da pendência: quando a guia não pôde ser baixada na hora, o responsável
/// anota o motivo e ele fica visível na pendência (e na tela de baixa) para consulta futura.
/// </summary>
public class ObservacaoPendenciaTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly FaturamentoService _faturamento;

    public ObservacaoPendenciaTests()
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
        return r.Atendimento.Codigos.First(c => c.Ordem == OrdemCodigo.Segundo);
    }

    [Fact]
    public async Task Registrar_GravaTextoDataEAuditoria()
    {
        var codigo = await CriarCodigoAsync();

        await _faturamento.RegistrarObservacaoPendenciaAsync(
            codigo.Id, "Portal da Unimed fora do ar", "maria");

        var salvo = await _repo.ObterCodigoAsync(codigo.Id);
        salvo!.ObservacaoPendencia.Should().Be("Portal da Unimed fora do ar");
        salvo.ObservacaoPendenciaEm.Should().NotBeNull();

        var evento = (await _repo.EventosAuditoriaAsync()).Single(e => e.Acao == "ObservacaoPendencia");
        evento.Operador.Should().Be("maria");
        evento.Detalhe.Should().Contain("Portal da Unimed");
        evento.CodigoId.Should().Be(codigo.Id);
    }

    [Fact]
    public async Task TextoVazio_LimpaObservacaoERegistraRemocao()
    {
        var codigo = await CriarCodigoAsync();
        await _faturamento.RegistrarObservacaoPendenciaAsync(codigo.Id, "aguardando QR Code", "maria");

        await _faturamento.RegistrarObservacaoPendenciaAsync(codigo.Id, "   ", "joana");

        var salvo = await _repo.ObterCodigoAsync(codigo.Id);
        salvo!.ObservacaoPendencia.Should().BeNull();
        salvo.ObservacaoPendenciaEm.Should().BeNull();
        (await _repo.EventosAuditoriaAsync()).Should().Contain(e => e.Acao == "ObservacaoPendenciaRemovida");
    }

    [Fact]
    public async Task ObservacaoApareceNaPendencia()
    {
        var codigo = await CriarCodigoAsync();
        await _faturamento.RegistrarObservacaoPendenciaAsync(codigo.Id, "paciente em viagem", "maria");

        // A data prevista do 2º código é +1 dia; consulto numa referência que já a inclui.
        var pendencias = await new PendenciaService(_repo).CodigosPendentesAsync(new DateOnly(2026, 7, 15));

        var linha = pendencias.Single(p => p.CodigoId == codigo.Id);
        linha.ObservacaoPendencia.Should().Be("paciente em viagem");
        linha.TemObservacao.Should().BeTrue();
        linha.ObservacaoPendenciaEm.Should().NotBeNull();
    }

    [Fact]
    public async Task SemObservacao_TemObservacaoEhFalso()
    {
        var codigo = await CriarCodigoAsync();

        var pendencias = await new PendenciaService(_repo).CodigosPendentesAsync(new DateOnly(2026, 7, 15));

        pendencias.Single(p => p.CodigoId == codigo.Id).TemObservacao.Should().BeFalse();
    }

    [Fact]
    public async Task ConsultaCentral_FiltraPelaObservacao()
    {
        var comObs = await CriarCodigoAsync();
        var semObs = await CriarCodigoAsync();
        await _faturamento.RegistrarObservacaoPendenciaAsync(comObs.Id, "Portal da Unimed fora do ar", "maria");

        var achados = await _repo.ConsultarCodigosAsync(new Clinica.Application.Modelos.FiltroConsultaGuias
        {
            TermoObservacao = "portal"
        });

        achados.Should().Contain(c => c.Id == comObs.Id);
        achados.Should().NotContain(c => c.Id == semObs.Id);
    }

    [Fact]
    public async Task Observacao_NaoDaBaixa_GuiaContinuaPendente()
    {
        var codigo = await CriarCodigoAsync();

        await _faturamento.RegistrarObservacaoPendenciaAsync(codigo.Id, "portal fora do ar", "maria");

        var salvo = await _repo.ObterCodigoAsync(codigo.Id);
        salvo!.Baixado.Should().BeFalse();
        salvo.EstaPendente(new DateOnly(2026, 7, 15)).Should().BeTrue();
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
