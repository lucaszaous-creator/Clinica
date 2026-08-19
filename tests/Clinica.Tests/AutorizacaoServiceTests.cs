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
/// Cota de sessões autorizada pelo convênio. O que se testa aqui é a aritmética que
/// decide o aviso da recepção — errar para menos deixa passar a glosa 2006
/// ("quantidade executada acima da autorizada"); errar para mais trava atendimento
/// que o convênio autorizou.
/// </summary>
public class AutorizacaoServiceTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly AutorizacaoService _autorizacoes;

    private static readonly DateOnly Emissao = new(2026, 6, 1);
    private static readonly DateOnly Validade = new(2026, 8, 31);
    private static readonly DateOnly Hoje = new(2026, 7, 25);

    public AutorizacaoServiceTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _autorizacoes = new AutorizacaoService(new ClinicaRepositorio(_db));
    }

    private async Task<int> CriarPacienteAsync()
    {
        var p = new Paciente { Nome = "Paciente", Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    private async Task<AutorizacaoSessoes> CriarAutorizacaoAsync(
        int pacienteId, int quantidade = 10, int jaUsadas = 0,
        DateOnly? emissao = null, DateOnly? validade = null, bool encerrada = false)
    {
        var a = new AutorizacaoSessoes
        {
            PacienteId = pacienteId,
            Numero = "AUT-1",
            Convenio = Convenio.UnimedIntercambio,
            DataEmissao = emissao ?? Emissao,
            DataValidade = validade ?? Validade,
            QuantidadeAutorizada = quantidade,
            QuantidadeUtilizadaManual = jaUsadas,
            Encerrada = encerrada
        };
        _db.Autorizacoes.Add(a);
        await _db.SaveChangesAsync();
        return a;
    }

    /// <summary>Atendimentos são o que consome a cota — é assim que o saldo é derivado.</summary>
    private async Task LancarAtendimentosAsync(int pacienteId, params DateOnly[] datas)
    {
        foreach (var data in datas)
            _db.Atendimentos.Add(new Atendimento
            {
                PacienteId = pacienteId,
                Data = data,
                RealizadoEm = data.ToDateTime(TimeOnly.MinValue),
                Modalidade = ModalidadeAtendimento.AcupunturaComEletro
            });
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task Saldo_ContaAtendimentosDentroDaVigencia()
    {
        var pacienteId = await CriarPacienteAsync();
        await CriarAutorizacaoAsync(pacienteId, quantidade: 10);
        await LancarAtendimentosAsync(pacienteId,
            new DateOnly(2026, 6, 10), new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 20));

        var saldo = await _autorizacoes.VigenteAsync(pacienteId, Hoje);

        saldo.Should().NotBeNull();
        saldo!.Utilizadas.Should().Be(3);
        saldo.Restantes.Should().Be(7);
        saldo.Esgotada.Should().BeFalse();
        saldo.Resumo.Should().Be("3 de 10 usadas — restam 7");
    }

    [Fact]
    public async Task Saldo_IgnoraAtendimentosForaDaVigencia()
    {
        var pacienteId = await CriarPacienteAsync();
        await CriarAutorizacaoAsync(pacienteId, quantidade: 10);
        await LancarAtendimentosAsync(pacienteId,
            new DateOnly(2026, 5, 20),   // antes da emissão
            new DateOnly(2026, 7, 1),    // dentro
            new DateOnly(2026, 9, 15));  // depois da validade

        var saldo = await _autorizacoes.VigenteAsync(pacienteId, Hoje);

        saldo!.Utilizadas.Should().Be(1);
        saldo.Restantes.Should().Be(9);
    }

    [Fact]
    public async Task Saldo_SomaAsSessoesFeitasForaDoSistema()
    {
        var pacienteId = await CriarPacienteAsync();
        await CriarAutorizacaoAsync(pacienteId, quantidade: 10, jaUsadas: 4);
        await LancarAtendimentosAsync(pacienteId, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 8));

        var saldo = await _autorizacoes.VigenteAsync(pacienteId, Hoje);

        saldo!.Utilizadas.Should().Be(6);
        saldo.Restantes.Should().Be(4);
    }

    [Fact]
    public async Task Saldo_NaUltimaSessaoAvisa()
    {
        var pacienteId = await CriarPacienteAsync();
        await CriarAutorizacaoAsync(pacienteId, quantidade: 10, jaUsadas: 9);

        var saldo = await _autorizacoes.VigenteAsync(pacienteId, Hoje);

        saldo!.Restantes.Should().Be(1);
        saldo.NaUltima.Should().BeTrue();
        saldo.Esgotada.Should().BeFalse();
        saldo.Utilizavel.Should().BeTrue();
        saldo.Resumo.Should().Be("9 de 10 usadas — resta 1");
    }

    [Fact]
    public async Task Saldo_CotaEstouradaNaoEhUtilizavel()
    {
        var pacienteId = await CriarPacienteAsync();
        await CriarAutorizacaoAsync(pacienteId, quantidade: 2);
        await LancarAtendimentosAsync(pacienteId,
            new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 8), new DateOnly(2026, 7, 15));

        var saldo = await _autorizacoes.VigenteAsync(pacienteId, Hoje);

        saldo!.Utilizadas.Should().Be(3);
        saldo.Restantes.Should().Be(-1);
        saldo.Esgotada.Should().BeTrue();
        saldo.Utilizavel.Should().BeFalse();
        saldo.Resumo.Should().Be("3 de 2 usadas — cota esgotada");
    }

    [Fact]
    public async Task Vigente_IgnoraAutorizacaoVencidaOuEncerrada()
    {
        var pacienteId = await CriarPacienteAsync();
        await CriarAutorizacaoAsync(pacienteId, validade: new DateOnly(2026, 6, 30));           // vencida
        await CriarAutorizacaoAsync(pacienteId, encerrada: true);                               // encerrada

        var saldo = await _autorizacoes.VigenteAsync(pacienteId, Hoje);

        saldo.Should().BeNull("nenhuma das duas vale para um atendimento hoje");
    }

    [Fact]
    public async Task Vigente_PrefereAQueAindaTemSaldo()
    {
        var pacienteId = await CriarPacienteAsync();
        await CriarAutorizacaoAsync(pacienteId, quantidade: 1, jaUsadas: 1);   // esgotada
        var comSaldo = await CriarAutorizacaoAsync(pacienteId, quantidade: 8);

        var saldo = await _autorizacoes.VigenteAsync(pacienteId, Hoje);

        saldo!.Autorizacao.Id.Should().Be(comSaldo.Id);
        saldo.Utilizavel.Should().BeTrue();
    }

    [Fact]
    public async Task Vigente_SemSaldoEmNenhumaDevolveAEsgotadaParaATelaAvisar()
    {
        var pacienteId = await CriarPacienteAsync();
        await CriarAutorizacaoAsync(pacienteId, quantidade: 1, jaUsadas: 1);

        var saldo = await _autorizacoes.VigenteAsync(pacienteId, Hoje);

        saldo.Should().NotBeNull("a recepção precisa ver que a cota acabou, não um silêncio");
        saldo!.Esgotada.Should().BeTrue();
    }

    [Fact]
    public async Task Saldo_MarcaVencidaQuandoAValidadePassou()
    {
        var pacienteId = await CriarPacienteAsync();
        await CriarAutorizacaoAsync(pacienteId, validade: new DateOnly(2026, 7, 1));

        var saldos = await _autorizacoes.SaldosAsync(pacienteId, Hoje);

        saldos.Should().HaveCount(1);
        saldos[0].Vencida.Should().BeTrue();
        saldos[0].Utilizavel.Should().BeFalse();
    }

    [Fact]
    public async Task Salvar_RecusaQuantidadeInvalida()
    {
        var pacienteId = await CriarPacienteAsync();

        var acao = async () => await _autorizacoes.SalvarAsync(new AutorizacaoSessoes
        {
            PacienteId = pacienteId,
            DataEmissao = Emissao,
            DataValidade = Validade,
            QuantidadeAutorizada = 0
        });

        await acao.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Salvar_RecusaValidadeAnteriorAEmissao()
    {
        var pacienteId = await CriarPacienteAsync();

        var acao = async () => await _autorizacoes.SalvarAsync(new AutorizacaoSessoes
        {
            PacienteId = pacienteId,
            DataEmissao = new DateOnly(2026, 8, 1),
            DataValidade = new DateOnly(2026, 7, 1),
            QuantidadeAutorizada = 10
        });

        await acao.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Salvar_AtualizaAAutorizacaoExistenteEmVezDeCriarOutra()
    {
        var pacienteId = await CriarPacienteAsync();
        var original = await CriarAutorizacaoAsync(pacienteId, quantidade: 10);

        await _autorizacoes.SalvarAsync(new AutorizacaoSessoes
        {
            Id = original.Id,
            PacienteId = pacienteId,
            Numero = "AUT-2",
            DataEmissao = Emissao,
            DataValidade = Validade,
            QuantidadeAutorizada = 20
        });

        var todas = await _autorizacoes.DoPacienteAsync(pacienteId);
        todas.Should().HaveCount(1);
        todas[0].QuantidadeAutorizada.Should().Be(20);
        todas[0].Numero.Should().Be("AUT-2");
    }

    [Fact]
    public async Task Salvar_DeixaRastroNaAuditoria()
    {
        var pacienteId = await CriarPacienteAsync();
        var original = await CriarAutorizacaoAsync(pacienteId, quantidade: 10);

        await _autorizacoes.SalvarAsync(new AutorizacaoSessoes
        {
            Id = original.Id,
            PacienteId = pacienteId,
            Numero = "AUT-1",
            DataEmissao = Emissao,
            DataValidade = Validade,
            QuantidadeAutorizada = 20
        }, "secretaria");

        var evento = await _db.Auditoria.AsNoTracking().SingleAsync();
        evento.Acao.Should().Be("AutorizacaoAlterada");
        evento.Operador.Should().Be("secretaria");
        evento.PacienteId.Should().Be(pacienteId);
        evento.Detalhe.Should().Contain("20").And.Contain("antes 10",
            "aumentar a cota é a mudança que mais precisa de rastro");
    }

    [Fact]
    public async Task Remover_DeixaRastroNaAuditoria()
    {
        var pacienteId = await CriarPacienteAsync();
        var autorizacao = await CriarAutorizacaoAsync(pacienteId);

        await _autorizacoes.RemoverAsync(autorizacao.Id, "secretaria");

        var evento = await _db.Auditoria.AsNoTracking().SingleAsync();
        evento.Acao.Should().Be("AutorizacaoRemovida");
        evento.PacienteId.Should().Be(pacienteId);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
