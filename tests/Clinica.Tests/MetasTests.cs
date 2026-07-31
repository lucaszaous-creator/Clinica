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
/// Metas da direção (parcela 28).
///
/// O painel comparava o mês com o MÊS ANTERIOR — variação, não desempenho. Variação
/// responde "melhorou?" e nunca "chegamos onde a gente disse que ia chegar?", que é a
/// única pergunta capaz de mudar a conduta no dia 12.
///
/// O que estes testes protegem:
///
/// 1. **O realizado vem do serviço dono do indicador**, não de conta refeita aqui — dois
///    números para a mesma pergunta é como a direção perde a confiança na tela.
/// 2. **Não se projeta o mês com dois dias.** Projetar cedo multiplica ruído: um único
///    dia forte diria que a clínica vai fechar o dobro.
/// 3. **Ocupação não se projeta por regra de três** — ela já é uma média, e multiplicá-la
///    pelos dias que faltam daria 300%.
/// 4. **Meta ausente é diferente de meta zero**: sem linha, a apuração devolve vazio em
///    vez de "meta batida".
/// </summary>
public class MetasTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly FinanceiroService _financeiro;
    private readonly MetaService _metas;

    /// <summary>Dia 10: sobra mês para trás e para frente nos recortes.</summary>
    private static readonly DateOnly Hoje = new(2026, 8, 10);

    public MetasTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _financeiro = new FinanceiroService(_repo);
        var parametros = new ParametrosService(_repo);
        _metas = new MetaService(_repo, _financeiro, new IndicadoresService(_repo, parametros));
    }

    private Task EntradaAsync(DateOnly dia, decimal valor)
        => _financeiro.LancarAsync(dia, TipoLancamento.Entrada, "Sessão", valor,
            StatusLancamento.Realizado, FormaPagamento.Dinheiro);

    // ==================== Cadastro ====================

    [Fact]
    public async Task Meta_definida_fica_gravada_e_auditada()
    {
        await _metas.DefinirAsync(2026, 8, IndicadorMeta.Faturamento, 30000m, operador: "direcao");

        var doAno = await _metas.DoAnoAsync(2026);
        doAno.Should().ContainSingle();
        doAno[0].Valor.Should().Be(30000m);

        (await _repo.EventosAuditoriaAsync())
            .Should().Contain(e => e.Acao == "MetaDefinida" && e.Operador == "direcao");
    }

    [Fact]
    public async Task Redefinir_o_mesmo_mes_substitui_em_vez_de_duplicar()
    {
        await _metas.DefinirAsync(2026, 8, IndicadorMeta.Faturamento, 30000m);
        await _metas.DefinirAsync(2026, 8, IndicadorMeta.Faturamento, 35000m, operador: "direcao");

        // Duas linhas dariam dois alvos para a mesma pergunta, e a tela escolheria um
        // deles sem dizer qual.
        var doAno = await _metas.DoAnoAsync(2026);
        doAno.Should().ContainSingle();
        doAno[0].Valor.Should().Be(35000m);

        (await _repo.EventosAuditoriaAsync())
            .Should().Contain(e => e.Acao == "MetaAlterada");
    }

    [Fact]
    public async Task Meta_de_ocupacao_acima_de_cem_e_recusada()
    {
        var definir = () => _metas.DefinirAsync(2026, 8, IndicadorMeta.Ocupacao, 120m);

        await definir.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*percentual*");
    }

    [Fact]
    public async Task Metas_de_profissionais_diferentes_convivem_no_mesmo_mes()
    {
        var ana = new Profissional { Nome = "Ana", RegistroConselho = "CRM-SP 1" };
        var bia = new Profissional { Nome = "Bia", RegistroConselho = "CRM-SP 2" };
        _db.Profissionais.AddRange(ana, bia);
        await _db.SaveChangesAsync();

        await _metas.DefinirAsync(2026, 8, IndicadorMeta.Sessoes, 40m, ana.Id);
        await _metas.DefinirAsync(2026, 8, IndicadorMeta.Sessoes, 60m, bia.Id);
        await _metas.DefinirAsync(2026, 8, IndicadorMeta.Sessoes, 100m);

        // "A clínica bateu" costuma esconder uma agenda lotada compensando outra vazia —
        // é essa diferença que a meta por profissional revela.
        (await _metas.DoAnoAsync(2026)).Should().HaveCount(3);
    }

    // ==================== Apuração ====================

    [Fact]
    public async Task Sem_meta_definida_a_apuracao_devolve_vazio()
    {
        await EntradaAsync(Hoje, 500m);

        // Vazio, e não "meta batida": ausência é "não decidimos", e inventar zero faria
        // todo mês não planejado aparecer como alvo alcançado.
        (await _metas.ApurarMesAsync(Hoje)).Should().BeEmpty();
    }

    [Fact]
    public async Task Faturamento_realizado_vem_do_financeiro()
    {
        await _metas.DefinirAsync(2026, 8, IndicadorMeta.Faturamento, 30000m);
        await EntradaAsync(new DateOnly(2026, 8, 2), 4000m);
        await EntradaAsync(new DateOnly(2026, 8, 9), 6000m);
        // Fora do recorte: mês anterior não entra.
        await EntradaAsync(new DateOnly(2026, 7, 30), 9000m);

        var apuradas = await _metas.ApurarMesAsync(Hoje);

        apuradas.Should().ContainSingle();
        apuradas[0].Realizado.Should().Be(10000m);
        apuradas[0].Atingida.Should().BeFalse();
    }

    [Fact]
    public async Task Projecao_usa_o_ritmo_do_mes_e_acusa_o_risco()
    {
        await _metas.DefinirAsync(2026, 8, IndicadorMeta.Faturamento, 40000m);
        await EntradaAsync(new DateOnly(2026, 8, 5), 10000m);

        var apurada = (await _metas.ApurarMesAsync(Hoje))[0];

        // 10.000 em 10 dias → 31.000 em 31 dias. Abaixo dos 40.000 combinados.
        apurada.Projecao.Should().Be(31000m);
        apurada.EmRisco.Should().BeTrue();
        apurada.DesvioProjetado.Should().BeApproximately(-22.5, 0.1);
    }

    [Fact]
    public async Task Mes_com_poucos_dias_nao_e_projetado()
    {
        await _metas.DefinirAsync(2026, 8, IndicadorMeta.Faturamento, 40000m);
        await EntradaAsync(new DateOnly(2026, 8, 1), 3000m);

        // No dia 2, projetar seria multiplicar ruído por quinze — um único dia forte
        // diria que a clínica vai fechar o dobro.
        var apurada = (await _metas.ApurarMesAsync(new DateOnly(2026, 8, 2)))[0];

        apurada.Projecao.Should().BeNull();
        apurada.EmRisco.Should().BeFalse();
    }

    [Fact]
    public async Task Meta_batida_nao_entra_em_risco()
    {
        await _metas.DefinirAsync(2026, 8, IndicadorMeta.Faturamento, 5000m);
        await EntradaAsync(new DateOnly(2026, 8, 3), 8000m);

        var apurada = (await _metas.ApurarMesAsync(Hoje))[0];

        apurada.Atingida.Should().BeTrue();
        apurada.EmRisco.Should().BeFalse();
        apurada.PercentualAtingido.Should().Be(160.0);
    }

    [Fact]
    public async Task Ocupacao_sem_agenda_fica_nula_e_nao_vira_meta_perdida()
    {
        await _metas.DefinirAsync(2026, 8, IndicadorMeta.Ocupacao, 70m);

        var apurada = (await _metas.ApurarMesAsync(Hoje))[0];

        // 0% de ocupação e "não medido" são coisas diferentes; a segunda não pode virar
        // meta perdida, senão a clínica que ainda não usa a agenda começa no vermelho.
        apurada.Realizado.Should().BeNull();
        apurada.EmRisco.Should().BeFalse();
        apurada.PercentualAtingido.Should().BeNull();
    }

    [Fact]
    public async Task Excluir_meta_apaga_o_alvo_e_deixa_rastro()
    {
        var meta = await _metas.DefinirAsync(2026, 8, IndicadorMeta.Sessoes, 120m);

        await _metas.ExcluirAsync(meta.Id, "direcao");

        (await _metas.DoAnoAsync(2026)).Should().BeEmpty();
        (await _repo.EventosAuditoriaAsync())
            .Should().Contain(e => e.Acao == "MetaExcluida" && e.Operador == "direcao");
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }
}
