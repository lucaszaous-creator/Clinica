using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

public sealed partial class ParcelamentoContasProfissionalTests : IDisposable
{
    private readonly SqliteConnection _conexao = new("DataSource=:memory:");
    private readonly ClinicaDbContext _db;
    private readonly ContasService _contas;
    public ParcelamentoContasProfissionalTests()
    {
        _conexao.Open();
        _db = new(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conexao).Options);
        _db.Database.EnsureCreated();
        _contas = new(new ClinicaRepositorio(_db));
    }
    public void Dispose() { _db.Dispose(); _conexao.Dispose(); }

    [Theory]
    [InlineData(TipoLancamento.Entrada)]
    [InlineData(TipoLancamento.Saida)]
    public async Task Parcelas_preservam_total_competencia_documento_e_ultimo_dia_do_mes(TipoLancamento tipo)
    {
        var grupo = Guid.NewGuid();
        var contas = await _contas.LancarParcelamentoAsync(grupo, tipo, "Contrato", 100m, 3,
            new(2026, 1, 31), new(2026, 1, 1), "Contraparte", "DOC-123", operador: "Gerente");
        contas.Select(c => c.Valor).Should().Equal(33.34m, 33.33m, 33.33m);
        contas.Sum(c => c.Valor).Should().Be(100m);
        contas.Select(c => c.DataVencimento).Should().Equal(new DateOnly(2026,1,31), new DateOnly(2026,2,28), new DateOnly(2026,3,31));
        contas.Should().OnlyContain(c => c.Data == new DateOnly(2026,1,1) && c.Status == StatusLancamento.Previsto && c.Tipo == tipo && c.Contraparte == "Contraparte" && c.DocumentoReferencia == "DOC-123");
        _db.ChangeTracker.Clear();
        var salvo = await new ClinicaRepositorio(_db).ContasDoParcelamentoAsync(grupo);
        salvo.Select(c => c.NumeroParcelaConta).Should().Equal(1, 2, 3);
        var resumo = await _contas.ResumoAsync(new(2026, 1, 1), new(2026, 12, 31));
        (tipo == TipoLancamento.Entrada ? resumo.AReceberAVencer : resumo.APagarAVencer).Should().Be(100m);
    }

    [Fact]
    public async Task Reenvio_da_mesma_operacao_nao_duplica_e_mudanca_exige_nova_operacao()
    {
        var id = Guid.NewGuid();
        var primeiro = await _contas.LancarParcelamentoAsync(id, TipoLancamento.Saida, "Compra", 90m, 3, new(2026,8,10), new(2026,8,1));
        var segundo = await _contas.LancarParcelamentoAsync(id, TipoLancamento.Saida, "Compra", 90.00m, 3, new(2026,8,10), new(2026,8,1));
        segundo.Select(c => c.Id).Should().Equal(primeiro.Select(c => c.Id));
        var mudar = () => _contas.LancarParcelamentoAsync(id, TipoLancamento.Saida, "Compra", 91m, 3, new(2026,8,10), new(2026,8,1));
        await mudar.Should().ThrowAsync<InvalidOperationException>().WithMessage("*outros dados*");
        (await _db.Set<LancamentoFinanceiro>().CountAsync()).Should().Be(3);
    }

    [Fact]
    public async Task Erro_de_referencia_nao_deixa_parcelamento_incompleto()
    {
        var gravar = () => _contas.LancarParcelamentoAsync(Guid.NewGuid(), TipoLancamento.Saida, "Compra", 100m, 3,
            new(2026,8,10), new(2026,8,1), categoriaId: int.MaxValue);
        (await gravar.Should().ThrowAsync<InvalidOperationException>()).WithInnerException<DbUpdateException>();
        (await _db.Set<LancamentoFinanceiro>().CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData(0.01, 2)]
    [InlineData(10.001, 2)]
    [InlineData(100, 121)]
    public async Task Parcelas_invalidas_nao_geram_contas(decimal valor, int quantidade)
    {
        var gravar = () => _contas.LancarParcelamentoAsync(Guid.NewGuid(), TipoLancamento.Entrada, "Cobrança", valor, quantidade,
            new(2026,8,10), new(2026,8,1));
        await gravar.Should().ThrowAsync<ArgumentException>();
        (await _db.Set<LancamentoFinanceiro>().CountAsync()).Should().Be(0);
    }
}
