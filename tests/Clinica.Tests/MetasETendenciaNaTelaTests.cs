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
/// O que as telas Blazor de Metas e de Tendências AFIRMAM (out/2026): as frases, a apuração
/// casada pelo Id da meta, o alvo zero, a permissão por métrica e o CSV.
/// </summary>
public class MetasETendenciaNaTelaTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly MetaService _metas;
    private readonly Paciente _paciente;
    private readonly Profissional _quem;

    public MetasETendenciaNaTelaTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        _db = new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options);
        _db.Database.EnsureCreated();
        var repo = new ClinicaRepositorio(_db);
        _metas = new MetaService(repo, new FinanceiroService(repo), new IndicadoresService(repo, new ParametrosService(repo)));
        _paciente = new Paciente { Nome = "Maria Souza", Convenio = Convenio.UnimedIntercambio, ConvenioCodigo = "UnimedIntercambio" };
        _quem = new Profissional { Nome = "Dra. Agulha", Ativo = true };
        _db.AddRange(_paciente, _quem);
        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }

    private void Sessoes(int quantas, DateOnly dia, string modalidade)
    {
        for (var i = 0; i < quantas; i++)
            _db.Agendamentos.Add(new Agendamento
            {
                PacienteId = _paciente.Id, ProfissionalId = _quem.Id,
                DataHora = dia.ToDateTime(new TimeOnly(8, 0)).AddMinutes(30 * i),
                Status = StatusAgendamento.Realizado,
                ModalidadePrevista = Enum.TryParse<ModalidadeAtendimento>(modalidade, out var m) ? m : ModalidadeAtendimento.Consulta,
                ModalidadeCodigo = modalidade
            });
        _db.SaveChanges();
    }

    // ==================== Metas ====================

    [Fact]
    public void Mes_futuro_nao_e_apurado_mes_em_curso_ate_hoje_e_mes_fechado_ate_o_ultimo_dia()
    {
        var hoje = new DateOnly(2026, 10, 16);
        MetasNaTela.ReferenciaDeApuracao(new(2026, 11, 1), hoje).Should().BeNull();
        MetasNaTela.ReferenciaDeApuracao(new(2026, 10, 1), hoje).Should().Be(hoje);
        MetasNaTela.ReferenciaDeApuracao(new(2026, 9, 1), hoje).Should().Be(new DateOnly(2026, 9, 30));
    }

    [Fact]
    public async Task Duas_metas_de_sessoes_do_mesmo_mes_recebem_cada_uma_o_SEU_realizado()
    {
        // O WPF casava a apuração por indicador + mês + profissional: com o recorte por
        // modalidade, as duas linhas abaixo mostravam o mesmo realizado.
        Sessoes(6, new(2026, 8, 3), "Acupuntura");
        Sessoes(4, new(2026, 8, 4), "BSV");
        await _metas.DefinirAsync(2026, 8, IndicadorMeta.Sessoes, 20, modalidadeCodigo: "Acupuntura");
        await _metas.DefinirAsync(2026, 8, IndicadorMeta.Sessoes, 4, modalidadeCodigo: "BSV");

        var doMes = (await _metas.DoAnoAsync(2026)).Where(m => m.Mes == 8).ToList();
        var apuradas = await _metas.ApurarMesAsync(new DateOnly(2026, 8, 31));
        var linhas = MetasNaTela.Montar(doMes, apuradas, new(2026, 8, 1), new(2026, 10, 16), apuracaoFalhou: false);

        linhas.Should().HaveCount(2);
        var acup = linhas.Single(l => l.ModalidadeCodigo == "Acupuntura");
        acup.RealizadoTexto.Should().Be("6 sessões");
        acup.Recorte.Should().Contain("Acupuntura");
        acup.Fracao.Should().BeApproximately(0.3, 0.001);
        acup.Situacao.Should().StartWith("não batida");     // mês fechado não fica "em risco"
        acup.EmRisco.Should().BeFalse();
        acup.ProjecaoTexto.Should().Be("—");                // num mês fechado a projeção é o realizado
        var bsv = linhas.Single(l => l.ModalidadeCodigo == "BSV");
        bsv.Situacao.Should().Be("meta batida");
        bsv.Tom.Should().Be(TomDaMeta.Sucesso);
    }

    [Fact]
    public void Mes_futuro_diz_que_ainda_nao_comecou_e_falha_diz_que_nao_apurou()
    {
        var meta = new MetaMensal { Id = 7, Ano = 2026, Mes = 12, Indicador = IndicadorMeta.Faturamento, Valor = 50_000m };
        MetasNaTela.Montar([meta], [], new(2026, 12, 1), new(2026, 10, 16), false).Single().Situacao
            .Should().Be("ainda não começou");

        var linha = MetasNaTela.Montar([NoMes(meta, 10)], [], new(2026, 10, 1), new(2026, 10, 16), apuracaoFalhou: true).Single();
        linha.Situacao.Should().Be("não foi possível apurar");
        linha.RealizadoTexto.Should().Be("—");
        MetasNaTela.Resumo([linha], new(2026, 10, 1), true).Should().Contain("não verificada");
    }

    private static MetaMensal NoMes(MetaMensal m, int mes) => new()
    {
        Id = m.Id, Ano = m.Ano, Mes = mes, Indicador = m.Indicador, Valor = m.Valor
    };

    [Fact]
    public void No_mes_em_curso_o_risco_e_a_projecao_aparecem()
    {
        var meta = new MetaMensal { Id = 3, Ano = 2026, Mes = 10, Indicador = IndicadorMeta.Sessoes, Valor = 100 };
        var apurada = new MetaApurada(IndicadorMeta.Sessoes, 2026, 10, 100, 30, 58, null, null, MetaId: 3);
        var l = MetasNaTela.Montar([meta], [apurada], new(2026, 10, 1), new(2026, 10, 16), false).Single();

        l.EmRisco.Should().BeTrue();
        l.Situacao.Should().Be("em risco: no ritmo atual fecha 42% abaixo");
        l.ProjecaoTexto.Should().Be("58 sessões");
        l.FracaoProjecao.Should().BeApproximately(0.58, 0.001);
        MetasNaTela.Resumo([l], new(2026, 10, 1), false).Should().Be("outubro de 2026: 1 meta · 1 em risco.");
    }

    [Fact]
    public void Sem_meta_o_resumo_diz_o_que_isso_significa()
        => MetasNaTela.Resumo([], new(2026, 10, 1), false).Should().Contain("Nenhuma meta para outubro de 2026");

    [Theory]
    [InlineData("0", true, 0)]           // zero é decisão — o diálogo de excluir manda usar
    [InlineData("45.000,00", true, 45000)]
    [InlineData("120", true, 120)]
    [InlineData("-5", false, 0)]
    [InlineData("abc", false, 0)]
    [InlineData("", false, 0)]
    public void Le_o_alvo_aceitando_o_zero(string texto, bool ok, decimal esperado)
    {
        MetasNaTela.TentarLerAlvo(texto, IndicadorMeta.Faturamento, out var v, out var erro).Should().Be(ok);
        if (ok) v.Should().Be(esperado);
        else erro.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Ocupacao_acima_de_100_e_recusada_antes_do_clique()
        => MetasNaTela.TentarLerAlvo("120", IndicadorMeta.Ocupacao, out _, out var erro).Should().BeFalse();

    [Fact]
    public void Acha_a_meta_que_ja_ocupa_a_mesma_chave_e_ignora_a_que_se_esta_editando()
    {
        List<MetaMensal> ano =
        [
            new() { Id = 1, Mes = 10, Indicador = IndicadorMeta.Sessoes, ModalidadeCodigo = "Acupuntura" },
            new() { Id = 2, Mes = 10, Indicador = IndicadorMeta.Sessoes }
        ];
        MetasNaTela.MesmaChave(ano, 10, IndicadorMeta.Sessoes, null, "Acupuntura", null, 0)!.Id.Should().Be(1);
        MetasNaTela.MesmaChave(ano, 10, IndicadorMeta.Sessoes, null, "Acupuntura", "", 1).Should().BeNull();
        MetasNaTela.MesmaChave(ano, 10, IndicadorMeta.Sessoes, null, "BSV", null, 0).Should().BeNull();
    }

    [Fact]
    public void Ocupacao_de_um_profissional_avisa_que_o_realizado_e_da_clinica()
    {
        var meta = new MetaMensal { Id = 9, Ano = 2026, Mes = 10, Indicador = IndicadorMeta.Ocupacao, Valor = 80, ProfissionalId = 4 };
        MetasNaTela.Montar([meta], [], new(2026, 10, 1), new(2026, 10, 16), false).Single().Nota
            .Should().Contain("clínica inteira");
        MetasNaTela.Recorte(IndicadorMeta.Ocupacao, null, null).Should().Be("Agenda inteira");
        MetasNaTela.Recorte(IndicadorMeta.Sessoes, null, null).Should().Be("Todas as modalidades");
    }

    [Fact]
    public void Valor_sai_em_pt_BR_fixo()
    {
        MetasNaTela.Formatar(IndicadorMeta.Faturamento, 1234.5m).Should().Be("R$ 1.234,50");
        MetasNaTela.Formatar(IndicadorMeta.Ocupacao, 72.5m).Should().Be("72,5%");
        MetasNaTela.Formatar(IndicadorMeta.Sessoes, 1).Should().Be("1 sessão");
    }

    // ==================== Tendência ====================

    [Fact]
    public void Receita_nao_se_le_com_o_bit_do_faturamento_e_sessoes_sim()
    {
        TendenciaNaTela.Leitura(MetricaTendencia.Receita).HasFlag(Permissao.VerFaturamento).Should().BeFalse();
        TendenciaNaTela.Leitura(MetricaTendencia.Receita).HasFlag(Permissao.VerFinanceiro).Should().BeTrue();
        TendenciaNaTela.Leitura(MetricaTendencia.Sessoes).HasFlag(Permissao.VerFaturamento).Should().BeTrue();
        TendenciaNaTela.Leitura(MetricaTendencia.Sessoes).HasFlag(Permissao.VerIndicadores).Should().BeTrue();
    }

    [Fact]
    public void Meta_so_nasce_de_modalidade_ou_especialidade_com_chave()
    {
        var l = new LinhaTendencia("Acupuntura", "Acupuntura", 10, 4, 150, SituacaoTendencia.EmAlta, [1, 2, 3, 4, 4, 10], 1);
        TendenciaNaTela.AceitaMeta(DimensaoTendencia.Modalidade, l).Should().BeTrue();
        TendenciaNaTela.AceitaMeta(DimensaoTendencia.Convenio, l).Should().BeFalse();
        TendenciaNaTela.AceitaMeta(DimensaoTendencia.Especialidade, l with { Chave = "" }).Should().BeFalse();
        TendenciaNaTela.Referencia(l, MetricaTendencia.Sessoes).Should().Be("No período: 10 · no anterior: 4.");
    }

    [Fact]
    public void Meta_criada_de_um_periodo_passado_cai_no_mes_corrente()
    {
        var hoje = new DateOnly(2026, 10, 16);
        TendenciaNaTela.MesSugeridoParaMeta(new(2026, 8, 31), hoje).Should().Be(new DateOnly(2026, 10, 1));
        TendenciaNaTela.MesSugeridoParaMeta(new(2026, 11, 30), hoje).Should().Be(new DateOnly(2026, 11, 1));
    }

    [Fact]
    public void Csv_traz_cada_linha_e_o_total_em_pt_BR()
    {
        var f = new FiltroTendencia(new(2026, 10, 1), new(2026, 10, 31), Metrica: MetricaTendencia.Receita);
        var p = new PainelTendencia(f, new(2026, 9, 1), new(2026, 9, 30), 1500m, 1000m,
            [new LinhaTendencia("A", "Acupuntura", 1500m, 1000m, 50, SituacaoTendencia.EmAlta, [], 1)], []);
        var csv = TendenciaNaTela.Csv(p);
        csv.Should().HaveCount(2);
        csv[0].Should().Equal("Acupuntura", "R$ 1.500,00", "R$ 1.000,00", "▲ 50%", "Em alta", "100%");
        csv[1][0].Should().Be("Total");
        TendenciaNaTela.CabecalhoCsv.Should().HaveCount(csv[0].Count);
    }

    [Fact]
    public void Pouca_base_explica_o_minimo()
        => TendenciaNaTela.Dica(SituacaoTendencia.PoucaBase, MetricaTendencia.Sessoes).Should().Contain("3 sessões");
}
