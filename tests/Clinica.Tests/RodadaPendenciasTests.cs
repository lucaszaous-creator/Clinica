using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Tests;

/// <summary>
/// Rodada de pendências ("rodar as pendências"): fechamento de ciclo periódico. Ao vencer o prazo,
/// a secretária baixa o que puder e justifica o resto como NÃO CONFORMIDADE — que silencia a pendência
/// (sai do painel), fica no relatório e só volta a ser pendência se for reaberta.
/// </summary>
public class RodadaPendenciasTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly ParametrosService _parametros;
    private readonly PendenciaService _pendencias;
    private readonly RodadaPendenciasService _rodada;

    public RodadaPendenciasTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _parametros = new ParametrosService(_repo);
        _pendencias = new PendenciaService(_repo);
        _rodada = new RodadaPendenciasService(_repo, _pendencias, _parametros);
    }

    /// <summary>Cria um 2º código pendente (o historicamente esquecido) a partir de um atendimento.</summary>
    private async Task<CodigoFaturamento> CriarSegundoCodigoAsync()
    {
        var p = new Paciente { Nome = "Paciente", Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        var r = await new AtendimentoService(_repo).LancarAsync(
            p.Id, new DateOnly(2026, 7, 10), ModalidadeAtendimento.AcupunturaComEletro);
        return r.Atendimento.Codigos.First(c => c.Ordem == OrdemCodigo.Segundo);
    }

    private static readonly DateOnly Ref = new(2026, 7, 21);

    [Fact]
    public async Task MarcarNaoConformidade_SilenciaPendenciaEGravaAuditoria()
    {
        var codigo = await CriarSegundoCodigoAsync();

        await _rodada.MarcarNaoConformidadeAsync(codigo.Id, "Portal fora do ar; paciente não retornou", "maria");

        var salvo = await _repo.ObterCodigoAsync(codigo.Id);
        salvo!.Status.Should().Be(StatusCodigo.NaoConformidade);
        salvo.NaoConformidadeJustificativa.Should().Contain("Portal fora do ar");
        salvo.NaoConformidadeEm.Should().NotBeNull();
        salvo.EstaPendente(Ref).Should().BeFalse();

        // Sai do painel de pendências.
        var pendentes = await _pendencias.CodigosPendentesAsync(Ref);
        pendentes.Should().NotContain(p => p.CodigoId == codigo.Id);

        var evento = (await _repo.EventosAuditoriaAsync()).Single(e => e.Acao == "NaoConformidade");
        evento.Operador.Should().Be("maria");
        evento.CodigoId.Should().Be(codigo.Id);
    }

    [Fact]
    public async Task Reabrir_VoltaASerPendencia()
    {
        var codigo = await CriarSegundoCodigoAsync();
        await _rodada.MarcarNaoConformidadeAsync(codigo.Id, "aguardando o convênio", "maria");

        await _rodada.ReabrirNaoConformidadeAsync(codigo.Id, "joana");

        var salvo = await _repo.ObterCodigoAsync(codigo.Id);
        salvo!.Status.Should().Be(StatusCodigo.Aberto);
        salvo.NaoConformidadeJustificativa.Should().BeNull();
        salvo.NaoConformidadeEm.Should().BeNull();
        salvo.EstaPendente(Ref).Should().BeTrue();

        (await _pendencias.CodigosPendentesAsync(Ref)).Should().Contain(p => p.CodigoId == codigo.Id);
        (await _repo.EventosAuditoriaAsync()).Should().Contain(e => e.Acao == "NaoConformidadeReaberta");
    }

    [Fact]
    public async Task MarcarNaoConformidade_SemJustificativa_Recusa()
    {
        var codigo = await CriarSegundoCodigoAsync();

        var acao = () => _rodada.MarcarNaoConformidadeAsync(codigo.Id, "   ", "maria");

        await acao.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GuiaBaixada_NaoViraNaoConformidade()
    {
        var codigo = await CriarSegundoCodigoAsync();
        await new FaturamentoService(_repo).DarBaixaAsync(codigo.Id, Ref, "G-1", "maria", null);

        var acao = () => _rodada.MarcarNaoConformidadeAsync(codigo.Id, "tentando justificar", "maria");

        await acao.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Status_SemAncora_NaoVencida()
    {
        await CriarSegundoCodigoAsync();

        var status = await _rodada.ObterStatusAsync(Ref);

        status.UltimaRodada.Should().BeNull();
        status.ProximaRodada.Should().BeNull();
        status.Vencida.Should().BeFalse();
        status.ExigeDecisao.Should().BeFalse();
    }

    [Fact]
    public async Task Status_AposAncora_VenceUmIntervaloDepois()
    {
        await CriarSegundoCodigoAsync();
        await _rodada.GarantirAncoraAsync(new DateOnly(2026, 7, 10)); // intervalo padrão = 10 → próxima 2026-07-20

        var dentroDoPrazo = await _rodada.ObterStatusAsync(new DateOnly(2026, 7, 19));
        dentroDoPrazo.Vencida.Should().BeFalse();
        dentroDoPrazo.ProximaRodada.Should().Be(new DateOnly(2026, 7, 20));

        var vencida = await _rodada.ObterStatusAsync(new DateOnly(2026, 7, 21));
        vencida.Vencida.Should().BeTrue();
        vencida.DiasEmAtraso.Should().Be(1);
        vencida.GuiasParaDecisao.Should().BeGreaterThan(0);
        vencida.ExigeDecisao.Should().BeTrue();
    }

    [Fact]
    public async Task GarantirAncora_NaoSobrescreveExistente()
    {
        await _parametros.SalvarDataUltimaRodadaPendenciasAsync(new DateOnly(2026, 7, 1));

        await _rodada.GarantirAncoraAsync(new DateOnly(2026, 7, 10));

        (await _parametros.ObterDataUltimaRodadaPendenciasAsync()).Should().Be(new DateOnly(2026, 7, 1));
    }

    [Fact]
    public async Task ConcluirRodada_CarimbaHojeEZeraVencimento()
    {
        await CriarSegundoCodigoAsync();
        await _rodada.GarantirAncoraAsync(new DateOnly(2026, 7, 10));

        await _rodada.ConcluirRodadaAsync(new DateOnly(2026, 7, 21), "maria");

        (await _parametros.ObterDataUltimaRodadaPendenciasAsync()).Should().Be(new DateOnly(2026, 7, 21));
        var status = await _rodada.ObterStatusAsync(new DateOnly(2026, 7, 21));
        status.Vencida.Should().BeFalse();
        status.ProximaRodada.Should().Be(new DateOnly(2026, 7, 31));
        (await _repo.EventosAuditoriaAsync()).Should().Contain(e => e.Acao == "RodadaPendencias");
    }

    [Fact]
    public async Task NaoConformidadesAsync_ListaOBacklogJustificado()
    {
        var codigo = await CriarSegundoCodigoAsync();
        await _rodada.MarcarNaoConformidadeAsync(codigo.Id, "paciente mudou de convênio", "maria");

        var lista = await _rodada.NaoConformidadesAsync();

        lista.Should().ContainSingle(n => n.CodigoId == codigo.Id)
            .Which.Justificativa.Should().Be("paciente mudou de convênio");
    }

    [Fact]
    public async Task Relatorio_ContaNaoConformidadeSeparadaDasPendentes()
    {
        var codigo = await CriarSegundoCodigoAsync();
        await _rodada.MarcarNaoConformidadeAsync(codigo.Id, "portal fora do ar", "maria");

        var rel = await new RelatorioService(_repo).GerarAsync(
            new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), Ref);

        rel.Resumo.NaoConformidades.Should().Be(1);
        rel.NaoConformidades.Should().ContainSingle(n => n.CodigoId == codigo.Id);
        // Não conformidade não conta como pendência ativa.
        rel.NaoConformidades.Should().NotBeEmpty();
        rel.Resumo.Pendentes.Should().Be(rel.Resumo.TotalCodigos - rel.Resumo.Baixados - 1);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
