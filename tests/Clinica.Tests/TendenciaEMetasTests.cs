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
/// A tendência por modalidade/especialidade e as metas por recorte (out/2026 — a cliente:
/// *"filtros para sabermos as categorias e especialidades que estamos tendo, as que estão
/// em alta, as que estão em baixa"*, como base do sistema de metas).
/// </summary>
public class TendenciaEMetasTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly MetaService _metas;
    private readonly TendenciaAtendimentosService _tendencia;
    private Paciente _paciente = null!;
    private Profissional _psiquiatra = null!;
    private Profissional _acupunturista = null!;

    public TendenciaEMetasTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        var parametros = new ParametrosService(_repo);
        _metas = new MetaService(_repo, new FinanceiroService(_repo), new IndicadoresService(_repo, parametros));
        _tendencia = new TendenciaAtendimentosService(_repo);
        Semear();
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }

    private void Semear()
    {
        _paciente = new Paciente { Nome = "Maria Souza", Convenio = Convenio.UnimedIntercambio, ConvenioCodigo = "UnimedIntercambio" };
        _psiquiatra = new Profissional { Nome = "Dr. Psiq", EspecialidadeCodigo = "Psiquiatria", Ativo = true };
        _acupunturista = new Profissional { Nome = "Dra. Agulha", Ativo = true };
        _db.AddRange(_paciente, _psiquiatra, _acupunturista);
        _db.SaveChanges();
    }

    private void Sessoes(int quantas, DateOnly dia, string modalidade, Profissional quem,
        StatusAgendamento status = StatusAgendamento.Realizado, string? especialidadeDaConsulta = null)
    {
        for (var i = 0; i < quantas; i++)
            _db.Agendamentos.Add(new Agendamento
            {
                PacienteId = _paciente.Id, ProfissionalId = quem.Id,
                DataHora = dia.ToDateTime(new TimeOnly(8, 0)).AddMinutes(30 * i),
                Status = status, ModalidadePrevista = CatalogoBase(modalidade), ModalidadeCodigo = modalidade,
                EspecialidadeConsultaCodigo = especialidadeDaConsulta
            });
        _db.SaveChanges();
    }

    private static ModalidadeAtendimento CatalogoBase(string codigo)
        => Enum.TryParse<ModalidadeAtendimento>(codigo, out var m) ? m : ModalidadeAtendimento.Consulta;

    // ==================== As regras puras ====================

    [Fact]
    public void Mes_cheio_compara_com_o_mes_cheio_anterior_e_recorte_livre_com_o_mesmo_numero_de_dias()
    {
        TendenciaAtendimentos.PeriodoAnterior(new(2026, 10, 1), new(2026, 10, 31))
            .Should().Be((new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30)));
        TendenciaAtendimentos.PeriodoAnterior(new(2026, 10, 8), new(2026, 10, 14))
            .Should().Be((new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 7)));
    }

    [Theory]
    [InlineData(20, 10, SituacaoTendencia.EmAlta)]
    [InlineData(5, 10, SituacaoTendencia.EmBaixa)]
    [InlineData(11, 10, SituacaoTendencia.Estavel)]
    [InlineData(2, 1, SituacaoTendencia.PoucaBase)]   // 1 → 2 é "+100%" e não diz nada
    [InlineData(6, 0, SituacaoTendencia.Nova)]
    [InlineData(0, 6, SituacaoTendencia.Sumiu)]
    [InlineData(12, 10, SituacaoTendencia.Estavel)]  // +20%, mas só 2 sessões de diferença
    public void Alta_e_baixa_exigem_percentual_e_diferenca(int atual, int anterior, SituacaoTendencia esperado)
        => TendenciaAtendimentos.Classificar(atual, anterior, MetricaTendencia.Sessoes).Should().Be(esperado);

    [Fact]
    public void Variacao_sem_base_e_nula_nunca_infinita()
        => TendenciaAtendimentos.Variacao(5, 0).Should().BeNull();

    // ==================== O serviço ====================

    [Fact]
    public async Task Diz_o_que_esta_em_alta_e_em_baixa_por_modalidade_contando_so_o_realizado()
    {
        Sessoes(4, new(2026, 9, 10), "Acupuntura", _acupunturista);
        Sessoes(12, new(2026, 10, 10), "Acupuntura", _acupunturista);
        Sessoes(10, new(2026, 9, 12), "BSV", _acupunturista);
        Sessoes(3, new(2026, 10, 12), "BSV", _acupunturista);
        Sessoes(9, new(2026, 10, 13), "Acupuntura", _acupunturista, StatusAgendamento.Faltou);

        var p = await _tendencia.MontarAsync(new FiltroTendencia(new(2026, 10, 1), new(2026, 10, 31)));

        var acup = p.Linhas.Single(l => l.Chave == "Acupuntura");
        acup.Atual.Should().Be(12);   // a falta não conta
        acup.Anterior.Should().Be(4);
        acup.Situacao.Should().Be(SituacaoTendencia.EmAlta);
        p.Linhas.Single(l => l.Chave == "BSV").Situacao.Should().Be(SituacaoTendencia.EmBaixa);
        p.TotalAtual.Should().Be(15);
        TendenciaAtendimentos.Resumo(p).Should().Contain("Em alta").And.Contain("Em baixa");
        acup.SerieMensal.Should().HaveCount(TendenciaAtendimentos.MesesDaSerie);
        acup.SerieMensal[^1].Should().Be(12);
    }

    [Fact]
    public async Task Especialidade_e_a_da_consulta_ou_a_de_quem_atendeu_e_o_filtro_recorta()
    {
        Sessoes(5, new(2026, 10, 5), "Acupuntura", _psiquiatra);                       // conta em Psiquiatria
        Sessoes(4, new(2026, 10, 6), "Consulta", _acupunturista, especialidadeDaConsulta: "Geriatria");
        Sessoes(2, new(2026, 10, 7), "Acupuntura", _acupunturista);                     // sem especialidade

        var p = await _tendencia.MontarAsync(new FiltroTendencia(new(2026, 10, 1), new(2026, 10, 31),
            Dimensao: DimensaoTendencia.Especialidade));

        p.Linhas.Single(l => l.Chave == "Psiquiatria").Atual.Should().Be(5);
        p.Linhas.Single(l => l.Chave == "Geriatria").Atual.Should().Be(4);
        p.Linhas.Single(l => l.Chave == "").Rotulo.Should().Be("Sem especialidade informada");

        var soPsiq = await _tendencia.MontarAsync(new FiltroTendencia(new(2026, 10, 1), new(2026, 10, 31),
            Dimensao: DimensaoTendencia.Modalidade, EspecialidadeCodigo: "Psiquiatria"));
        soPsiq.TotalAtual.Should().Be(5);
    }

    // ==================== Metas por recorte ====================

    [Fact]
    public async Task Meta_de_sessoes_por_modalidade_e_apurada_so_com_aquela_modalidade()
    {
        Sessoes(6, new(2026, 8, 3), "Acupuntura", _acupunturista);
        Sessoes(4, new(2026, 8, 4), "BSV", _acupunturista);
        await _metas.DefinirAsync(2026, 8, IndicadorMeta.Sessoes, 20, modalidadeCodigo: "Acupuntura");
        await _metas.DefinirAsync(2026, 8, IndicadorMeta.Sessoes, 30);

        var apuradas = await _metas.ApurarMesAsync(new DateOnly(2026, 8, 31));

        apuradas.Single(m => m.Segmento is null).Realizado.Should().Be(10);
        var acup = apuradas.Single(m => m.Segmento is not null);
        acup.Realizado.Should().Be(6);
        acup.Segmento.Should().Contain("Acupuntura");
    }

    [Fact]
    public async Task Meta_de_um_profissional_nao_e_apurada_com_o_numero_da_clinica()
    {
        Sessoes(6, new(2026, 8, 3), "Acupuntura", _acupunturista);
        Sessoes(4, new(2026, 8, 4), "Consulta", _psiquiatra);
        await _metas.DefinirAsync(2026, 8, IndicadorMeta.Sessoes, 10, profissionalId: _psiquiatra.Id);

        var apurada = (await _metas.ApurarMesAsync(new DateOnly(2026, 8, 31))).Single();

        apurada.Realizado.Should().Be(4);
    }

    [Fact]
    public async Task Duas_metas_de_recortes_diferentes_no_mesmo_mes_convivem_e_repetir_corrige()
    {
        await _metas.DefinirAsync(2026, 8, IndicadorMeta.Sessoes, 20, modalidadeCodigo: "Acupuntura");
        await _metas.DefinirAsync(2026, 8, IndicadorMeta.Sessoes, 10, modalidadeCodigo: "BSV");
        await _metas.DefinirAsync(2026, 8, IndicadorMeta.Sessoes, 25, modalidadeCodigo: "Acupuntura");

        var metas = await _db.Metas.AsNoTracking().ToListAsync();
        metas.Should().HaveCount(2);
        metas.Single(m => m.ModalidadeCodigo == "Acupuntura").Valor.Should().Be(25);
    }

    [Fact]
    public async Task Ocupacao_nao_se_recorta_por_modalidade()
    {
        var act = () => _metas.DefinirAsync(2026, 8, IndicadorMeta.Ocupacao, 80, modalidadeCodigo: "Acupuntura");
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*jornada não tem modalidade*");
    }
    [Fact]
    public async Task Receita_por_modalidade_soma_o_realizado_ligado_a_atendimento_direto_ou_pela_guia()
    {
        var acup = new Atendimento { PacienteId = _paciente.Id, Data = new(2026, 10, 5), Modalidade = ModalidadeAtendimento.AcupunturaSimples, ModalidadeCodigo = "Acupuntura" };
        var estornado = new Atendimento { PacienteId = _paciente.Id, Data = new(2026, 10, 5), Modalidade = ModalidadeAtendimento.BsvApenas, ModalidadeCodigo = "BSV", EstornadoEm = DateTime.Now };
        _db.AddRange(acup, estornado);
        _db.SaveChanges();
        _db.Agendamentos.Add(new Agendamento { PacienteId = _paciente.Id, ProfissionalId = _acupunturista.Id, AtendimentoId = acup.Id,
            DataHora = new DateTime(2026, 10, 5, 9, 0, 0), Status = StatusAgendamento.Realizado, ModalidadePrevista = ModalidadeAtendimento.AcupunturaSimples });
        var guia = new CodigoFaturamento { AtendimentoId = acup.Id };
        _db.Add(guia);
        _db.SaveChanges();
        _db.Lancamentos.AddRange(
            new LancamentoFinanceiro { Tipo = TipoLancamento.Entrada, Status = StatusLancamento.Realizado, Valor = 150m, Data = new(2026, 10, 5), DataPagamento = new(2026, 10, 5), Descricao = "s", AtendimentoId = acup.Id },
            new LancamentoFinanceiro { Tipo = TipoLancamento.Entrada, Status = StatusLancamento.Realizado, Valor = 90m, Data = new(2026, 10, 9), DataPagamento = new(2026, 10, 9), Descricao = "g", CodigoFaturamentoId = guia.Id },
            new LancamentoFinanceiro { Tipo = TipoLancamento.Entrada, Status = StatusLancamento.Previsto, Valor = 999m, Data = new(2026, 10, 9), Descricao = "p", AtendimentoId = acup.Id },
            new LancamentoFinanceiro { Tipo = TipoLancamento.Entrada, Status = StatusLancamento.Realizado, Valor = 500m, Data = new(2026, 10, 9), DataPagamento = new(2026, 10, 9), Descricao = "e", AtendimentoId = estornado.Id });
        _db.SaveChanges();

        var p = await _tendencia.MontarAsync(new FiltroTendencia(new(2026, 10, 1), new(2026, 10, 31), Metrica: MetricaTendencia.Receita,
            Dimensao: DimensaoTendencia.Profissional));

        p.TotalAtual.Should().Be(240m);   // o previsto e o estornado ficam de fora
        p.Linhas.Single().Rotulo.Should().Be(_acupunturista.Rotulo);
    }
}
