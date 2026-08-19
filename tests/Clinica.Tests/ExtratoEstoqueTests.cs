using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Clinica.Application.Servicos;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// As duas regras que a varredura do Financeiro subiu de lugar (parcela 69):
/// a recusa da perda sem motivo no PONTO ÚNICO, e o sinal do movimento no DOMÍNIO.
/// </summary>
public class ExtratoEstoqueTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly EstoqueService _estoque;

    public ExtratoEstoqueTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _estoque = new EstoqueService(new ClinicaRepositorio(_db));
    }

    private async Task<int> CriarItemAsync()
    {
        var item = await _estoque.SalvarItemAsync(
            new ItemEstoque { Nome = "Agulha 0,25", Unidade = "un" }, "teste");
        return item.Id;
    }

    /// <summary>
    /// A recusa da perda sem motivo morava SÓ no wrapper `PerderAsync` — que nenhuma tela
    /// chama. A janela genérica de movimento entra por `MovimentarAsync`, e a única
    /// barreira dela era a validação da TELA: o defeito recorrente vestido de validação
    /// (quem valida na tela cobre uma porta e deixa as outras passando). Quem impede é o
    /// serviço.
    /// </summary>
    [Fact]
    public async Task Perda_sem_motivo_e_recusada_no_ponto_unico()
    {
        var itemId = await CriarItemAsync();
        await _estoque.EntrarAsync(itemId, 10, operador: "teste");

        var agir = () => _estoque.MovimentarAsync(new MovimentoEstoque
        {
            ItemEstoqueId = itemId,
            Tipo = TipoMovimentoEstoque.Perda,
            Quantidade = 1,
            Data = new DateOnly(2026, 8, 18)
            // sem Observacao — o motivo que a clínica exige
        }, "teste");

        await agir.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*motivo*", "perda é uma AFIRMAÇÃO, e afirmação sem motivo escrito vira estoque que não bate");
    }

    /// <summary>
    /// O sinal do movimento mora no DOMÍNIO, e o AJUSTE segue a direção gravada: a
    /// contagem que acha A MAIS soma. O par antigo (`Sinal`/`QuantidadeComSinal`) dizia
    /// "−1 para tudo que não é entrada" e erraria exatamente aqui — nunca doeu porque
    /// nunca teve chamador, e é por isso que ele foi substituído em vez de consertado:
    /// agora o saldo do repositório e o extrato do item usam ESTA conta.
    /// </summary>
    [Theory]
    [InlineData(TipoMovimentoEstoque.Entrada, null, 5.0, 5.0)]
    [InlineData(TipoMovimentoEstoque.Saida, null, 5.0, -5.0)]
    [InlineData(TipoMovimentoEstoque.Perda, null, 2.0, -2.0)]
    [InlineData(TipoMovimentoEstoque.Ajuste, true, 3.0, 3.0)]
    [InlineData(TipoMovimentoEstoque.Ajuste, false, 3.0, -3.0)]
    public void Delta_respeita_a_direcao_do_ajuste(
        TipoMovimentoEstoque tipo, bool? paraCima, double quantidade, double esperado)
        => MovimentoEstoque.DeltaDe(tipo, paraCima, (decimal)quantidade)
            .Should().Be((decimal)esperado);

    /// <summary>
    /// O extrato tem de bater com o saldo da tela de trás — as duas somas são a MESMA
    /// função. Este teste percorre o caminho inteiro: movimentos de todos os tipos, o
    /// saldo oficial (`SaldosAsync`) e a soma por `Delta` chegam ao mesmo número.
    /// </summary>
    [Fact]
    public async Task O_saldo_oficial_e_a_soma_dos_Deltas_sao_o_mesmo_numero()
    {
        var itemId = await CriarItemAsync();
        await _estoque.EntrarAsync(itemId, 10, operador: "teste");
        await _estoque.BaixarAsync(itemId, 3, operador: "teste");
        await _estoque.PerderAsync(itemId, 1, "quebrou", operador: "teste");
        // Contagem achou A MAIS: o caso que o sinal antigo erraria.
        await _estoque.AjustarInventarioAsync(itemId, 8, "contagem física", "teste");

        var saldos = await _estoque.SaldosAsync();
        var oficial = saldos.Single(s => s.ItemId == itemId);

        var movimentos = await _estoque.MovimentosAsync(itemId);
        var somaDeltas = movimentos.Sum(m => m.Delta);

        somaDeltas.Should().Be(8m, "a contagem final foi 8");
        oficial.Saldo.Should().Be(somaDeltas,
            "o extrato que explica o saldo não pode desmenti-lo — as duas somas são a mesma conta");
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
