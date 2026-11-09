using Clinica.Application.Modelos;
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
/// Os pedidos da gerência de nov/2026: pendências filtradas por paciente, período e forma de
/// obtenção, com relatório impresso; as baixas com os filtros da Consulta de guias e os atalhos
/// de período; e o relatório de atendimentos por profissional com os valores.
/// </summary>
public class PedidosGerenciaNov2026Tests
{
    private static readonly DateOnly Hoje = new(2026, 11, 9);

    private static PendenciaCodigo Pendencia(
        string nome, DateOnly? atendimento, int atraso = 0, string? telefone = null,
        FormaObtencao forma = FormaObtencao.Sistema, OrdemCodigo ordem = OrdemCodigo.Primeiro)
        => new(1, 1, nome, Convenio.Amil, TipoCodigo.Acupuntura, ordem, Hoje.AddDays(-atraso), forma, atraso,
               atraso > 0 ? NivelUrgencia.Vermelho : NivelUrgencia.Amarelo, null, telefone)
        {
            DataAtendimento = atendimento
        };

    // ==================== pendências ====================

    [Fact]
    public void Periodo_das_pendencias_e_pelo_atendimento_com_as_duas_pontas_e_sem_data_fica_fora()
    {
        var todas = new[]
        {
            Pendencia("Antes", new DateOnly(2026, 10, 31)),
            Pendencia("Primeiro dia", new DateOnly(2026, 11, 1)),
            Pendencia("Último dia", new DateOnly(2026, 11, 30)),
            Pendencia("Sem data", null),
        };
        var filtro = FiltroPendencias.Nenhum with { Inicio = new DateOnly(2026, 11, 1), Fim = new DateOnly(2026, 11, 30) };

        filtro.Aplicar(todas).Select(p => p.PacienteNome).Should().Equal("Primeiro dia", "Último dia");
        filtro.Ativo.Should().BeTrue();
        filtro.Partes().Should().Contain("atendidas de 01/11/2026 a 30/11/2026");
        filtro.PeriodoInvertido.Should().BeFalse();
        (filtro with { Fim = new DateOnly(2026, 10, 1) }).PeriodoInvertido.Should().BeTrue();
        (FiltroPendencias.Nenhum with { Inicio = new DateOnly(2026, 11, 1) }).RotuloPeriodo().Should().Be("atendidas desde 01/11/2026");
    }

    [Fact]
    public void Paciente_casa_sem_acento_e_pelo_telefone_so_com_quatro_digitos()
    {
        var todas = new[]
        {
            Pendencia("José Antônio", Hoje, telefone: "(22) 99876-5432"),
            Pendencia("Maria", Hoje, telefone: "(22) 91234-0000"),
        };

        (FiltroPendencias.Nenhum with { Paciente = "jose anto" }).Aplicar(todas).Single().PacienteNome.Should().Be("José Antônio");
        (FiltroPendencias.Nenhum with { Paciente = "5432" }).Aplicar(todas).Single().PacienteNome.Should().Be("José Antônio");
        // "22" casaria com os dois telefones (DDD): termo curto não procura no telefone.
        (FiltroPendencias.Nenhum with { Paciente = "22" }).Aplicar(todas).Should().BeEmpty();
    }

    [Fact]
    public void Forma_de_obtencao_filtra_e_aparece_no_recorte()
    {
        var todas = new[] { Pendencia("App", Hoje, forma: FormaObtencao.App), Pendencia("Portal", Hoje, forma: FormaObtencao.Sistema) };
        var filtro = FiltroPendencias.Nenhum with { Forma = FormaObtencao.App };

        filtro.Aplicar(todas).Single().PacienteNome.Should().Be("App");
        PainelPendenciasFaturamento.RecorteDoRelatorio(filtro, 1, 2).Should().Be("1 de 2 guia(s) pendentes — pelo app do paciente");
        PainelPendenciasFaturamento.RecorteDoRelatorio(FiltroPendencias.Nenhum, 2, 2).Should().Be("Todas as 2 guia(s) pendentes");
    }

    [Fact]
    public void Relatorio_de_pendentes_poe_a_mais_atrasada_primeiro_e_escreve_as_palavras_da_tela()
    {
        var linhas = new[]
        {
            Pendencia("Beatriz", new DateOnly(2026, 11, 2), atraso: 0),
            Pendencia("Ana", new DateOnly(2026, 10, 20), atraso: 12, ordem: OrdemCodigo.Segundo),
        };

        var ordenadas = PainelPendenciasFaturamento.OrdenarParaRelatorio(linhas);
        ordenadas.Select(p => p.PacienteNome).Should().Equal("Ana", "Beatriz");

        var linha = PainelPendenciasFaturamento.LinhaRelatorio(ordenadas[0], Hoje);
        linha.Should().HaveCount(PainelPendenciasFaturamento.CabecalhoRelatorio.Count);
        linha[1].Should().Be("—", "sem telefone, a célula assume a lacuna");
        linha[4].Should().Be("20/10/2026");
        linha[6].Should().Be("atrasada há 12 dia(s)");
        linha[7].Should().Be("no sistema do convênio");
        PainelPendenciasFaturamento.SituacaoDoPrazo(ordenadas[1], Hoje).Should().Be("vence hoje");

        var pdf = PainelPendenciasFaturamento.RelatorioPdf(linhas, FiltroPendencias.Nenhum, 2, Hoje, "Clínica Teste");
        System.Text.Encoding.ASCII.GetString(pdf, 0, 4).Should().Be("%PDF");
        // Relatório vazio também sai — com a frase, não com uma página em branco.
        PainelPendenciasFaturamento.RelatorioPdf([], FiltroPendencias.Nenhum, 0, Hoje, null).Should().NotBeEmpty();
    }

    // ==================== baixas ====================

    [Fact]
    public void Baixas_filtram_por_modalidade_e_pela_especialidade_da_guia_com_o_atendimento_por_baixo()
    {
        var consulta = new CodigoFaturamento
        {
            Tipo = TipoCodigo.ConsultaEspecialidade,
            Atendimento = new Atendimento { Modalidade = ModalidadeAtendimento.Consulta, EspecialidadeConsulta = Especialidade.Ginecologia, Paciente = new Paciente { Nome = "Ana" } }
        };
        var bsv = new CodigoFaturamento
        {
            Tipo = TipoCodigo.Bsv,
            Atendimento = new Atendimento { Modalidade = ModalidadeAtendimento.BsvApenas, Paciente = new Paciente { Nome = "Bia" } }
        };
        var todas = new[] { consulta, bsv };

        todas.Where((FiltroFaturados.Nenhum with { Modalidade = ModalidadeAtendimento.BsvApenas }).Casa).Should().Equal(bsv);
        todas.Where((FiltroFaturados.Nenhum with { Especialidade = Especialidade.Ginecologia }).Casa).Should().Equal(consulta);
        (FiltroFaturados.Nenhum with { Modalidade = ModalidadeAtendimento.Consulta }).Ativo.Should().BeTrue();
        // Recarregar com outro convênio não desfaz os combos novos.
        GuiasFaturadasNaTela.Preservar(FiltroFaturados.Nenhum with { Convenio = "Some", Modalidade = ModalidadeAtendimento.Consulta }, [GuiasFaturadasNaTela.TodosConvenios])
            .Modalidade.Should().Be(ModalidadeAtendimento.Consulta);
    }

    [Fact]
    public void Atalhos_de_periodo_das_baixas_comecam_no_dia_primeiro()
    {
        GuiasFaturadasNaTela.Periodo(AtalhoPeriodoFaturados.EsteMes, Hoje).Should().Be((new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30)));
        GuiasFaturadasNaTela.Periodo(AtalhoPeriodoFaturados.MesPassado, Hoje).Should().Be((new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31)));
        GuiasFaturadasNaTela.Periodo(AtalhoPeriodoFaturados.Ultimos3Meses, Hoje).Should().Be((new DateOnly(2026, 9, 1), new DateOnly(2026, 11, 30)));
        GuiasFaturadasNaTela.Periodo(AtalhoPeriodoFaturados.Ultimos12Meses, Hoje).Should().Be((new DateOnly(2025, 12, 1), new DateOnly(2026, 11, 30)));
    }

    // ==================== atendimentos do período ====================

    [Fact]
    public void Lancamentos_contam_pacientes_distintos_e_o_csv_sai_com_a_data_inteira()
    {
        var p1 = new Paciente { Id = 1, Nome = "Ana", Convenio = Convenio.Amil };
        var p2 = new Paciente { Id = 2, Nome = "Ana", Convenio = Convenio.Amil };
        var linhas = new[]
        {
            Clinica.Application.Formatacao.LancamentosNaTela.Montar(new Atendimento { Id = 10, PacienteId = 1, Paciente = p1, Data = new DateOnly(2026, 11, 3) }),
            Clinica.Application.Formatacao.LancamentosNaTela.Montar(new Atendimento { Id = 11, PacienteId = 1, Paciente = p1, Data = new DateOnly(2026, 11, 4) }),
            // Mesmo nome, outra pessoa: conta como outro paciente.
            Clinica.Application.Formatacao.LancamentosNaTela.Montar(new Atendimento { Id = 12, PacienteId = 2, Paciente = p2, Data = new DateOnly(2026, 11, 4) }),
        };

        Clinica.Application.Formatacao.LancamentosNaTela.PacientesDistintos(linhas).Should().Be(2);
        Clinica.Application.Formatacao.LancamentosNaTela.ResumoComPacientes(linhas, 5).Should().Be("3 de 5 atendimento(s) · 2 paciente(s)");
        var csv = Clinica.Application.Formatacao.LancamentosNaTela.LinhaCsv(linhas[0]);
        csv.Should().HaveCount(Clinica.Application.Formatacao.LancamentosNaTela.CabecalhoCsv.Count);
        csv[0].Should().Be("03/11/2026");
    }

    // ==================== agenda ====================

    [Fact]
    public void Periodo_personalizado_da_agenda_recusa_invertido_vazio_e_longo_e_anda_pelo_proprio_tamanho()
    {
        var (ok, erro) = Clinica.Application.Formatacao.VisaoGeralDaAgendaNaTela.PeriodoPersonalizado(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 15));
        ok.Should().Be((new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 15)));
        erro.Should().BeNull();

        Clinica.Application.Formatacao.VisaoGeralDaAgendaNaTela.PeriodoPersonalizado(new DateOnly(2026, 11, 15), new DateOnly(2026, 11, 1)).Erro.Should().Contain("antes da inicial");
        Clinica.Application.Formatacao.VisaoGeralDaAgendaNaTela.PeriodoPersonalizado(null, new DateOnly(2026, 11, 1)).Erro.Should().Contain("duas datas");
        Clinica.Application.Formatacao.VisaoGeralDaAgendaNaTela.PeriodoPersonalizado(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)).Erro.Should().Contain("93 dias");

        Clinica.Application.Formatacao.VisaoGeralDaAgendaNaTela.AndarPeriodo(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 15), 1)
            .Should().Be((new DateOnly(2026, 11, 16), new DateOnly(2026, 11, 30)));
        Clinica.Application.Formatacao.VisaoGeralDaAgendaNaTela.AndarPeriodo(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 15), -1)
            .Should().Be((new DateOnly(2026, 10, 17), new DateOnly(2026, 10, 31)));
    }

    [Fact]
    public void Procedimento_da_agenda_e_a_familia_da_modalidade_e_conta_so_o_que_ocupa()
    {
        using var _ = new FluentAssertions.Execution.AssertionScope();
        static Clinica.Application.Formatacao.LinhaHorarioOcupacao H(int id, ModalidadeAtendimento f, bool ocupa = true)
            => new(new Clinica.Application.Abstracoes.HorarioOcupacao(id, Hoje.ToDateTime(new TimeOnly(9, 0)), Hoje.ToDateTime(new TimeOnly(10, 0)),
                1, "Dra. Ana", "Paciente", f.ToString(), "Marcado", ocupa) { Familia = f }, "09:00", "até 10:00");
        var linhas = new[]
        {
            H(1, ModalidadeAtendimento.AcupunturaSimples), H(2, ModalidadeAtendimento.AcupunturaComEletro),
            H(3, ModalidadeAtendimento.BsvComAcupuntura), H(4, ModalidadeAtendimento.BsvApenas),
            H(5, ModalidadeAtendimento.Consulta), H(6, ModalidadeAtendimento.Consulta, ocupa: false),
        };

        var contagem = Clinica.Application.Formatacao.VisaoGeralDaAgendaNaTela.ContagemPorProcedimento(linhas);
        contagem[Clinica.Application.Formatacao.ProcedimentoDaAgenda.Todos].Should().Be(5);
        contagem[Clinica.Application.Formatacao.ProcedimentoDaAgenda.Acupuntura].Should().Be(2);
        // O BSV com acupuntura no mesmo dia é BSV — não conta duas vezes.
        contagem[Clinica.Application.Formatacao.ProcedimentoDaAgenda.Bsv].Should().Be(2);
        contagem[Clinica.Application.Formatacao.ProcedimentoDaAgenda.Consulta].Should().Be(1, "a consulta cancelada não é atendimento");
        Clinica.Application.Formatacao.VisaoGeralDaAgendaNaTela.ResumoDaLista(linhas[2..4], 6, Clinica.Application.Formatacao.ProcedimentoDaAgenda.Bsv)
            .Should().Be("2 de 6 horário(s) — BSV");
        Clinica.Application.Formatacao.VisaoGeralDaAgendaNaTela.ResumoDaLista(linhas, 6, Clinica.Application.Formatacao.ProcedimentoDaAgenda.Todos)
            .Should().Be("6 horário(s) na lista (com 1 cancelado(s) ou falta(s))");
    }

    [Fact]
    public void Dica_do_historico_do_paciente_so_aparece_com_paciente_digitado_e_periodo_curto()
    {
        var (ini, fim) = GuiasFaturadasNaTela.MesDe(Hoje);
        GuiasFaturadasNaTela.DicaHistoricoDoPaciente(FiltroFaturados.Nenhum, ini, fim).Should().BeNull();
        GuiasFaturadasNaTela.DicaHistoricoDoPaciente(FiltroFaturados.Nenhum with { Paciente = "ana" }, ini, fim).Should().Contain("Últimos 12 meses");
        var (a, b) = GuiasFaturadasNaTela.Periodo(AtalhoPeriodoFaturados.Ultimos12Meses, Hoje);
        GuiasFaturadasNaTela.DicaHistoricoDoPaciente(FiltroFaturados.Nenhum with { Paciente = "ana" }, a, b).Should().BeNull();
    }
}

/// <summary>O relatório de atendimentos por profissional, contra o banco (SQLite) — a leitura composta executada.</summary>
public sealed class AtendimentosPorProfissionalTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly AtendimentosPorProfissionalService _servico;
    private readonly PrecoConvenioService _precos;
    private readonly FinanceiroService _financeiro;

    private static readonly DateOnly Inicio = new(2026, 10, 1);
    private static readonly DateOnly Fim = new(2026, 10, 31);

    public AtendimentosPorProfissionalTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        _db = new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _precos = new PrecoConvenioService(_repo);
        _servico = new AtendimentosPorProfissionalService(_repo, _precos);
        _financeiro = new FinanceiroService(_repo);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }

    private async Task<(int Paciente, int Profissional)> BaseAsync(string profissional = "Dra. Ana")
    {
        var p = new Paciente { Nome = "Maria", Convenio = Convenio.Amil, Sexo = Sexo.Feminino };
        var prof = new Profissional { Nome = profissional, RegistroConselho = "CRM 1" };
        _db.AddRange(p, prof);
        await _db.SaveChangesAsync();
        return (p.Id, prof.Id);
    }

    private async Task<int> SessaoAsync(int paciente, int profissional, DateOnly dia, StatusAgendamento status = StatusAgendamento.Realizado,
        params (TipoCodigo Tipo, StatusCodigo Status)[] guias)
    {
        var a = new Atendimento { PacienteId = paciente, Data = dia, Modalidade = ModalidadeAtendimento.AcupunturaSimples };
        foreach (var (tipo, st) in guias)
            a.Codigos.Add(new CodigoFaturamento { Tipo = tipo, Status = st, DataPrevistaFaturamento = dia });
        _db.Atendimentos.Add(a);
        await _db.SaveChangesAsync();
        _db.Agendamentos.Add(new Agendamento
        {
            PacienteId = paciente, ProfissionalId = profissional, AtendimentoId = a.Id,
            DataHora = dia.ToDateTime(new TimeOnly(9, 0)), Status = status
        });
        await _db.SaveChangesAsync();
        return a.Id;
    }

    [Fact]
    public async Task Soma_a_tabela_das_guias_pagaveis_e_o_recebido_lado_a_lado()
    {
        var (paciente, prof) = await BaseAsync();
        await _precos.SalvarAsync(new PrecoConvenio { ConvenioCodigo = nameof(Convenio.Amil), Tipo = TipoCodigo.Acupuntura, Valor = 60m });
        await _precos.SalvarAsync(new PrecoConvenio { ConvenioCodigo = nameof(Convenio.Amil), Tipo = TipoCodigo.Eletroacupuntura, Valor = 25m });

        var dia1 = await SessaoAsync(paciente, prof, Inicio.AddDays(2), guias:
            [(TipoCodigo.Acupuntura, StatusCodigo.Baixado), (TipoCodigo.Eletroacupuntura, StatusCodigo.Aberto)]);
        // A guia justificada como não conformidade não vira valor de ninguém.
        await SessaoAsync(paciente, prof, Inicio.AddDays(3), guias:
            [(TipoCodigo.Acupuntura, StatusCodigo.Aberto), (TipoCodigo.Eletroacupuntura, StatusCodigo.NaoConformidade)]);
        // Horário cancelado que manteve o atendimento não é sessão dada.
        await SessaoAsync(paciente, prof, Inicio.AddDays(4), StatusAgendamento.Cancelado, (TipoCodigo.Acupuntura, StatusCodigo.Aberto));
        await _financeiro.LancarAsync(Inicio.AddDays(2), TipoLancamento.Entrada, "coparticipação", 30m, StatusLancamento.Realizado, atendimentoId: dia1);

        var r = await _servico.GerarAsync(Inicio, Fim);

        var p = r.Profissionais.Single();
        p.Quantidade.Should().Be(2);
        p.PacientesDistintos.Should().Be(1);
        p.Atendimentos[0].ValorTabela.Should().Be(85m);
        p.Atendimentos[1].ValorTabela.Should().Be(60m);
        p.TotalTabela.Should().Be(145m);
        p.TotalRecebido.Should().Be(30m);
        p.SemPreco.Should().Be(0);
        AtendimentosPorProfissionalService.LinhasCsv(r).Should().HaveCount(2)
            .And.AllSatisfy(l => l.Should().HaveCount(AtendimentosPorProfissionalService.CabecalhoCsv.Count));
    }

    [Fact]
    public async Task Guia_sem_preco_nao_vira_zero_e_o_total_diz_quantas_ficaram_de_fora()
    {
        var (paciente, prof) = await BaseAsync();
        await _precos.SalvarAsync(new PrecoConvenio { ConvenioCodigo = nameof(Convenio.Amil), Tipo = TipoCodigo.Acupuntura, Valor = 60m });
        await SessaoAsync(paciente, prof, Inicio.AddDays(2), guias:
            [(TipoCodigo.Acupuntura, StatusCodigo.Aberto), (TipoCodigo.Bsv, StatusCodigo.Aberto)]);

        var r = await _servico.GerarAsync(Inicio, Fim);

        var a = r.Profissionais.Single().Atendimentos.Single();
        a.ValorTabela.Should().BeNull("somar só a guia com preço afirmaria um valor menor com cara de exato");
        a.GuiasSemPreco.Should().Be(1);
        AtendimentosPorProfissionalService.ValorDeTabela(a).Should().Be("sem preço (1 guia)");
        r.SemPreco.Should().Be(1);
        AtendimentosPorProfissionalService.RessalvaSemPreco(r.SemPreco).Should().Contain("Preços por convênio");
        System.Text.Encoding.ASCII.GetString(AtendimentosPorProfissionalService.Pdf(r, null, "Clínica"), 0, 4).Should().Be("%PDF");
    }

    [Fact]
    public async Task Atendimento_estornado_sai_e_o_filtro_de_profissional_separa_cada_um()
    {
        var (paciente, ana) = await BaseAsync();
        var bruno = new Profissional { Nome = "Dr. Bruno", RegistroConselho = "CRM 2" };
        _db.Add(bruno);
        await _db.SaveChangesAsync();

        await SessaoAsync(paciente, ana, Inicio.AddDays(1));
        await SessaoAsync(paciente, bruno.Id, Inicio.AddDays(1));
        var estornado = await SessaoAsync(paciente, bruno.Id, Inicio.AddDays(2));
        var at = await _db.Atendimentos.SingleAsync(x => x.Id == estornado);
        at.EstornadoEm = DateTime.Now;
        await _db.SaveChangesAsync();

        var todos = await _servico.GerarAsync(Inicio, Fim);
        todos.Profissionais.Select(p => (p.Profissional, p.Quantidade)).Should().Equal(("Dr. Bruno", 1), ("Dra. Ana", 1));

        var soAna = await _servico.GerarAsync(Inicio, Fim, ana);
        soAna.Profissionais.Single().Profissional.Should().Be("Dra. Ana");
        // Sessão sem guia (sem tabela a somar) diz "sem guia", nunca R$ 0,00.
        AtendimentosPorProfissionalService.ValorDeTabela(soAna.Profissionais.Single().Atendimentos.Single()).Should().Be("sem guia");
    }

    [Fact]
    public async Task Atendimento_com_data_diferente_da_do_horario_nao_some_do_relatorio()
    {
        var (paciente, prof) = await BaseAsync();
        var id = await SessaoAsync(paciente, prof, Inicio.AddDays(10));
        // Lançado à mão antes de o horário existir: o atendimento ficou com outra data.
        var at = await _db.Atendimentos.SingleAsync(x => x.Id == id);
        at.Data = Inicio.AddDays(-20);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var r = await _servico.GerarAsync(Inicio, Fim);

        r.Profissionais.Single().Atendimentos.Single().AtendimentoId.Should().Be(id);
    }

    /// <summary>
    /// A leitura composta contra um banco LENTO (a regra do CabecalhoClinicoTests): as leituras
    /// vão em fila no mesmo DbContext — em paralelo, o Postgres recusaria "a second operation".
    /// </summary>
    [Fact]
    public async Task Relatorio_monta_contra_um_banco_LENTO_como_o_de_producao()
    {
        var (paciente, prof) = await BaseAsync();
        await _precos.SalvarAsync(new PrecoConvenio { ConvenioCodigo = nameof(Convenio.Amil), Tipo = TipoCodigo.Acupuntura, Valor = 60m });
        await SessaoAsync(paciente, prof, Inicio.AddDays(2), guias: [(TipoCodigo.Acupuntura, StatusCodigo.Aberto)]);

        var lento = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).AddInterceptors(new LentidaoDeRede()).Options;
        using var db = new ClinicaDbContext(lento);
        var repo = new ClinicaRepositorio(db);
        var r = await new AtendimentosPorProfissionalService(repo, new PrecoConvenioService(repo)).GerarAsync(Inicio, Fim);

        r.TotalTabela.Should().Be(60m);
    }

    private sealed class LentidaoDeRede : Microsoft.EntityFrameworkCore.Diagnostics.DbCommandInterceptor
    {
        public override async ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<System.Data.Common.DbDataReader>>
            ReaderExecutingAsync(System.Data.Common.DbCommand command, Microsoft.EntityFrameworkCore.Diagnostics.CommandEventData eventData,
                Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<System.Data.Common.DbDataReader> result, CancellationToken cancellationToken = default)
        {
            await Task.Delay(25, cancellationToken);
            return result;
        }
    }

    [Fact]
    public async Task Periodo_invertido_ou_longo_demais_e_recusado_com_a_frase()
    {
        await FluentActions.Awaiting(() => _servico.GerarAsync(Fim, Inicio)).Should().ThrowAsync<InvalidOperationException>();
        await FluentActions.Awaiting(() => _servico.GerarAsync(Inicio, Inicio.AddDays(400))).Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*366 dias*");
    }
}
