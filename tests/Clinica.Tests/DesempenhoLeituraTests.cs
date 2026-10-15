using System.Data.Common;
using Clinica.Application.Servicos;
using Clinica.Application.Modelos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Prontuario;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace Clinica.Tests;

public sealed class DesempenhoLeituraTests : IDisposable
{
    private readonly SqliteConnection _conexao = new("DataSource=:memory:");
    private readonly Medidor _medidor = new();
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly Paciente _paciente = new() { Nome = "Paciente fictício", Convenio = Convenio.UnimedIntercambio };
    private readonly DateOnly _dia = DateOnly.FromDateTime(DateTime.Today);

    public DesempenhoLeituraTests()
    {
        _conexao.Open();
        _db = new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>()
            .UseSqlite(_conexao).AddInterceptors(_medidor).Options);
        _db.Database.EnsureCreated();
        _db.Pacientes.Add(_paciente);
        _db.SaveChanges();
        _repo = new ClinicaRepositorio(_db);
    }

    [Fact]
    public async Task Painel_reutiliza_a_agenda_e_rele_novos_dados_na_proxima_carga()
    {
        var agenda = new AgendaService(_repo, new AtendimentoService(_repo));
        var painel = new PainelRecepcaoService(_repo, agenda, new PendenciaService(_repo));
        _db.Agendamentos.Add(new Agendamento
        {
            PacienteId = _paciente.Id, DataHora = _dia.ToDateTime(new TimeOnly(9, 0)),
            Status = StatusAgendamento.Agendado
        });
        await _db.SaveChangesAsync();
        _medidor.Consultas.Clear();

        var resumo = await painel.ResumoAsync(_dia);
        await painel.PendenciasDosPacientesAsync(_dia, resumo.PacientesDoDia);

        _medidor.Consultas.Count(q => q.Contains("FROM \"Agendamentos\""))
            .Should().Be(1, "resumo, ocupação e pendências usam a mesma agenda");
        resumo.Agendados.Should().Be(1);
        resumo.PacientesDoDia.Should().Equal(_paciente.Id);
        resumo.Ocupacao.Should().ContainSingle();

        _db.Agendamentos.Add(new Agendamento
        {
            PacienteId = _paciente.Id, DataHora = _dia.ToDateTime(new TimeOnly(10, 0)),
            Status = StatusAgendamento.Agendado
        });
        await _db.SaveChangesAsync();
        (await painel.ResumoAsync(_dia)).Agendados.Should().Be(2,
            "não há cache clínico entre cargas");
    }

    [Fact]
    public async Task Resumo_da_dor_reutiliza_historico_sem_consulta_extra_e_preserva_a_conta()
    {
        _db.Evolucoes.AddRange(
            new Evolucao { PacienteId = _paciente.Id, Data = _dia, EvaAntes = 8, EvaDepois = 3 },
            new Evolucao { PacienteId = _paciente.Id, Data = _dia.AddDays(-1), EvaAntes = 9, EvaDepois = 4 },
            new Evolucao { PacienteId = _paciente.Id, Data = _dia.AddDays(-2), EvaAntes = 7 });
        await _db.SaveChangesAsync();
        var prontuario = new ProntuarioService(_repo);
        var esperado = await prontuario.EvolucaoDaDorAsync(_paciente.Id);
        _medidor.Consultas.Clear();
        var sessoes = await prontuario.DoPacienteAsync(_paciente.Id);
        var resultado = ProntuarioService.CalcularEvolucaoDaDor(sessoes);
        resultado.Should().BeEquivalentTo(esperado);
        _medidor.Consultas.Should().ContainSingle();
    }

    [Fact]
    public async Task Linha_do_tempo_restrita_faz_so_duas_consultas_mesmo_com_rede_lenta()
    {
        _medidor.Consultas.Clear();
        var resultado = await new LeituraLinhaDoTempoService(_repo).LerAsync(_paciente.Id,
            Permissao.VerProntuario,
            [NaturezaRegistroClinico.EvolucaoEnfermagem, NaturezaRegistroClinico.PrescricaoInterna], false);
        _medidor.Consultas.Should().HaveCount(2);
        _medidor.Consultas.Should().NotContain(q => q.Contains("FROM \"Evolucoes\"")
            || q.Contains("FROM \"AnexosPaciente\"") || q.Contains("FROM \"DocumentosClinicos\""));
        resultado[NaturezaRegistroClinico.SessaoMedica].Should().BeEmpty();
    }

    [Fact]
    public async Task Linha_do_tempo_sem_permissao_nao_consulta_o_banco()
    {
        _medidor.Consultas.Clear();
        await new LeituraLinhaDoTempoService(_repo).LerAsync(_paciente.Id, Permissao.VerFichaPaciente, []);
        _medidor.Consultas.Should().BeEmpty();
    }

    [Fact]
    public async Task Secoes_vazias_continuam_significando_todas_e_documentos_podem_ser_ocultados()
    {
        _db.Evolucoes.Add(new Evolucao { PacienteId = _paciente.Id, Data = _dia, QueixaPrincipal = "Fictícia" });
        await _db.SaveChangesAsync();
        _medidor.Consultas.Clear();
        var resultado = await new LeituraLinhaDoTempoService(_repo).LerAsync(_paciente.Id,
            Permissao.VerProntuario | Permissao.VerFichaPaciente, [], mostrarDocumentos: false);
        resultado[NaturezaRegistroClinico.SessaoMedica].Should().ContainSingle();
        _medidor.Consultas.Should().HaveCount(5);
        _medidor.Consultas.Should().NotContain(q => q.Contains("FROM \"DocumentosClinicos\""));
    }

    [Fact]
    public async Task Termos_conferidos_sao_reutilizaveis_e_data_fora_de_hoje_nao_vira_lista_vazia()
    {
        var servico = new ElegibilidadeService(_repo, new AutorizacaoService(_repo),
            new ConsentimentoService(_repo), new ConsultaService(_repo), termos: new TermoProcedimentoService(_repo));
        var hoje = await servico.ConferirAsync(_paciente.Id, _dia);
        hoje.TermosDoDia.Should().NotBeNull().And.BeEmpty();
        var futuro = await servico.ConferirAsync(_paciente.Id, _dia.AddDays(1));
        futuro.TermosDoDia.Should().BeNull("não conferido deve permitir a leitura específica da sessão");
    }

    [Fact]
    public async Task Paginas_alcancam_todos_sem_repetir_homonimos_e_busca_encontra_fora_da_primeira()
    {
        _db.Pacientes.AddRange(Enumerable.Range(1, 240).Select(i => new Paciente
        {
            Nome = "Homônimo", Documento = $"{i:00000000000}"
        }));
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        _medidor.Consultas.Clear();
        var a = await _repo.BuscarPaginaPacientesAsync(null, 0, 100);
        var b = await _repo.BuscarPaginaPacientesAsync(null, 100, 100);
        var c = await _repo.BuscarPaginaPacientesAsync(null, 200, 100);
        a.Concat(b).Concat(c).Select(p => p.Id).Should().OnlyHaveUniqueItems().And.HaveCount(241);
        _db.ChangeTracker.Entries().Should().BeEmpty();
        _medidor.Consultas.Should().HaveCount(3).And.OnlyContain(q => q.Contains("LIMIT") && q.Contains("OFFSET"));
        var busca = await _repo.BuscarPaginaPacientesAsync("00000000240", 0, 101);
        busca.Should().ContainSingle().Which.Documento.Should().Be("00000000240");
    }

    [Fact]
    public async Task Filtros_sao_aplicados_antes_do_limite_e_tratamento_conta_agendamento_realizado()
    {
        _db.Pacientes.AddRange(Enumerable.Range(1, 110).Select(i => new Paciente { Nome = $"A {i:000}", Documento = "123" }));
        _paciente.Nome = "Z alvo";
        _paciente.Documento = " ";
        _db.Agendamentos.Add(new Agendamento { PacienteId = _paciente.Id, DataHora = DateTime.Today, Status = StatusAgendamento.Realizado });
        await _db.SaveChangesAsync();
        _medidor.Consultas.Clear();
        var incompletos = await _repo.BuscarPaginaPacientesAsync(null, 0, 101, FiltroListaPacientes.CadastroIncompleto);
        var tratamento = await _repo.BuscarPaginaPacientesAsync(null, 0, 101, FiltroListaPacientes.EmTratamento);
        incompletos.Should().ContainSingle().Which.Id.Should().Be(_paciente.Id);
        tratamento.Should().ContainSingle().Which.Id.Should().Be(_paciente.Id);
        _medidor.Consultas.Should().HaveCount(2);
    }

    [Fact]
    public async Task Recortes_da_lista_contam_o_mesmo_que_a_pagina_mostra()
    {
        // O chip e a página leem o MESMO recorte: chip "2" sobre uma lista de 1 seria a
        // tela mentindo sobre ela mesma.
        var hoje = _dia;
        var aniversariante = new Paciente { Nome = "Aniversário", Documento = "12345678901",
            DataNascimento = new DateOnly(1980, hoje.AddDays(3).Month, hoje.AddDays(3).Day) };
        var semConvenio = new Paciente { Nome = "Importado", Documento = "98765432100", ConvenioCodigo = ConvenioCadastro.CodigoADefinir };
        var foraDaSemana = new Paciente { Nome = "Fora", Documento = "11122233344",
            DataNascimento = new DateOnly(1980, hoje.AddDays(20).Month, hoje.AddDays(20).Day) };
        _db.Pacientes.AddRange(aniversariante, semConvenio, foraDaSemana);
        await _db.SaveChangesAsync();
        _db.Agendamentos.AddRange(
            new Agendamento { PacienteId = semConvenio.Id, DataHora = hoje.ToDateTime(new TimeOnly(14, 0)), Status = StatusAgendamento.Agendado },
            // Cancelado hoje não é "com horário hoje".
            new Agendamento { PacienteId = foraDaSemana.Id, DataHora = hoje.ToDateTime(new TimeOnly(15, 0)), Status = StatusAgendamento.Cancelado });
        await _db.SaveChangesAsync();

        var recortes = await _repo.ContarRecortesDePacientesAsync(hoje);
        recortes.Total.Should().Be(4);
        recortes.ComHorarioHoje.Should().Be(1);
        recortes.SemConvenio.Should().Be(1);
        recortes.AniversarioNaSemana.Should().Be(1);
        recortes.CadastroIncompleto.Should().Be(1, "só a ficha do fixture não tem documento");

        (await _repo.BuscarPaginaPacientesAsync(null, 0, 101, FiltroListaPacientes.ComHorarioHoje))
            .Should().ContainSingle().Which.Id.Should().Be(semConvenio.Id);
        (await _repo.BuscarPaginaPacientesAsync(null, 0, 101, FiltroListaPacientes.SemConvenioDefinido))
            .Should().ContainSingle().Which.Id.Should().Be(semConvenio.Id);
        (await _repo.BuscarPaginaPacientesAsync(null, 0, 101, FiltroListaPacientes.AniversarioNaSemana))
            .Should().ContainSingle().Which.Id.Should().Be(aniversariante.Id);
    }

    [Fact]
    public async Task Visitas_trazem_a_ultima_realizada_e_o_proximo_marcado_numa_consulta()
    {
        var agora = _dia.ToDateTime(new TimeOnly(12, 0));
        var helena = new Profissional { Nome = "Helena Souza", NomeCurto = "Dra. Helena" };
        _db.Profissionais.Add(helena);
        await _db.SaveChangesAsync();
        _db.Agendamentos.AddRange(
            new Agendamento { PacienteId = _paciente.Id, DataHora = agora.AddDays(-30), Status = StatusAgendamento.Realizado,
                ModalidadePrevista = ModalidadeAtendimento.AcupunturaSimples },
            new Agendamento { PacienteId = _paciente.Id, DataHora = agora.AddDays(-5), Status = StatusAgendamento.Realizado,
                ModalidadePrevista = ModalidadeAtendimento.BsvApenas },
            new Agendamento { PacienteId = _paciente.Id, DataHora = agora.AddDays(-2), Status = StatusAgendamento.Faltou },
            new Agendamento { PacienteId = _paciente.Id, DataHora = agora.AddDays(9), Status = StatusAgendamento.Agendado, ProfissionalId = helena.Id },
            new Agendamento { PacienteId = _paciente.Id, DataHora = agora.AddDays(2), Status = StatusAgendamento.Agendado, ProfissionalId = helena.Id });
        await _db.SaveChangesAsync();
        _medidor.Consultas.Clear();

        var visitas = await _repo.VisitasDosPacientesAsync([_paciente.Id, 999], agora);

        _medidor.Consultas.Should().ContainSingle();
        var v = visitas[_paciente.Id];
        v.Ultima.Should().Be(agora.AddDays(-5));
        v.UltimaAtendimento.Should().NotBeNullOrWhiteSpace();
        v.Proximo.Should().Be(agora.AddDays(2));
        v.ProximoProfissional.Should().Be("Dra. Helena");
        visitas.Should().NotContainKey(999);
        ListaDePacientes.UltimaVisita(null, _dia).Should().Be("sem visita registrada");
    }

    [Fact]
    public async Task Carteirinhas_filtram_no_banco_sem_transferir_foto_ou_ficha_completa()
    {
        _paciente.ValidadeCarteirinha = _dia.AddDays(-10);
        _paciente.FotoMiniatura = new byte[8000];
        _db.Pacientes.Add(new Paciente { Nome = "Fora do prazo", ValidadeCarteirinha = _dia.AddDays(31) });
        await _db.SaveChangesAsync();
        _medidor.Consultas.Clear();
        var resultado = await new PendenciaService(_repo).CarteirinhasAVencerAsync(_dia);
        resultado.Should().ContainSingle().Which.PacienteId.Should().Be(_paciente.Id);
        _medidor.Consultas.Should().ContainSingle().Which.Should().NotContain("FotoMiniatura").And.Contain("WHERE");
    }

    [Fact]
    public async Task Contas_reutilizam_leitura_preservando_totais_dos_dois_tipos()
    {
        _db.Lancamentos.AddRange(
            new LancamentoFinanceiro { Descricao = "Saída fictícia", Tipo = TipoLancamento.Saida, Status = StatusLancamento.Previsto, Valor = 120, Data = _dia, DataVencimento = _dia.AddDays(-1) },
            new LancamentoFinanceiro { Descricao = "Entrada fictícia", Tipo = TipoLancamento.Entrada, Status = StatusLancamento.Previsto, Valor = 230, Data = _dia, DataVencimento = _dia.AddDays(1), PacienteId = _paciente.Id });
        await _db.SaveChangesAsync();
        var servico = new ContasService(_repo);
        var esperado = await servico.ResumoAsync(_dia, _dia.AddDays(30));
        _medidor.Consultas.Clear();
        var linhas = await servico.EmAbertoAsync(_dia.AddDays(30));
        var resumo = ContasService.Resumir(linhas, _dia);
        resumo.Should().BeEquivalentTo(esperado);
        resumo.APagarVencido.Should().Be(120);
        resumo.AReceberAVencer.Should().Be(230);
        _medidor.Consultas.Should().ContainSingle();
    }

    [Fact]
    public async Task Indicadores_somente_de_agenda_preservam_resultado_sem_ler_evolucoes()
    {
        _db.Agendamentos.Add(new Agendamento { PacienteId = _paciente.Id, DataHora = _dia.ToDateTime(new TimeOnly(9, 0)), Status = StatusAgendamento.Realizado });
        await _db.SaveChangesAsync();
        var servico = new IndicadoresService(_repo, new ParametrosService(_repo));
        var completo = await servico.GerarAsync(_dia, _dia);
        _medidor.Consultas.Clear();
        var agenda = await servico.AgendaAsync(_dia, _dia);
        agenda.Should().BeEquivalentTo(completo.Agenda);
        // Duas: a jornada configurada já veio na leitura única de configurações do escopo
        // (out/2026), feita pela carga completa logo acima.
        _medidor.Consultas.Should().HaveCount(2).And.NotContain(q => q.Contains("FROM \"Evolucoes\""));
    }

    [Fact]
    public async Task Custo_transacao_le_uma_vez_recebimentos_e_tabelas_preservando_todos_os_blocos()
    {
        foreach (var nome in new[] { "Rede", "Stone", "Cielo" })
        {
            _db.Lancamentos.Add(new LancamentoFinanceiro { Descricao = "Recebimento fictício", Tipo = TipoLancamento.Entrada,
                Status = StatusLancamento.Realizado, Valor = 200, ValorTaxa = 5, ValorImposto = 2, Adquirente = nome, Data = _dia });
            _db.TaxasCartao.Add(new TaxaCartao { Adquirente = nome.ToUpperInvariant(), Percentual = 2.5m });
        }
        await _db.SaveChangesAsync();
        var servico = new CustoTransacaoService(_repo);
        var inicio = new DateOnly(_dia.Year, _dia.Month, 1).AddMonths(-2);
        var fim = new DateOnly(_dia.Year, _dia.Month, 1).AddMonths(1).AddDays(-1);
        var serie = await servico.SerieAsync(_dia, 3);
        var resumo = await servico.ResumoAsync(inicio, fim);
        var adquirentes = await servico.PorAdquirenteAsync(inicio, fim);
        _medidor.Consultas.Clear();
        var painel = await servico.PainelAsync(_dia, 3);
        painel.Serie.Should().BeEquivalentTo(serie);
        painel.Resumo.Should().BeEquivalentTo(resumo);
        painel.Adquirentes.Should().BeEquivalentTo(adquirentes);
        painel.Faixas["Rede"].Should().Be((2.5m, 2.5m));
        _medidor.Consultas.Should().HaveCount(2);
    }

    [Fact]
    public async Task Resumos_de_inadimplencia_fluxo_e_recebiveis_nao_reconsultam_as_listas()
    {
        _db.Lancamentos.AddRange(
            new LancamentoFinanceiro { Descricao = "Dívida fictícia", PacienteId = _paciente.Id, Tipo = TipoLancamento.Entrada,
                Status = StatusLancamento.Previsto, Valor = 125, Data = _dia, DataVencimento = _dia.AddDays(-100) },
            new LancamentoFinanceiro { Descricao = "Cartão fictício", Tipo = TipoLancamento.Entrada, FormaPagamento = FormaPagamento.CartaoCredito,
                Status = StatusLancamento.Realizado, Valor = 210, ValorTaxa = 10, Adquirente = "Fictícia", Data = _dia, PrevisaoRecebimento = _dia.AddDays(-1) });
        await _db.SaveChangesAsync();
        var inad = new InadimplenciaService(_repo, new FinanceiroService(_repo));
        var fluxo = new FluxoCaixaService(_repo);
        var recebiveis = new RecebiveisService(_repo);
        var esperadoInad = await inad.ResumoAsync(_dia);
        var esperadoFluxo = await fluxo.ResumoAsync(_dia.AddDays(-120), _dia, true);
        var esperadoRecebiveis = await recebiveis.ResumoAsync(_dia, _dia.AddDays(30));
        _medidor.Consultas.Clear();
        var devedores = await inad.PorPacienteAsync(_dia);
        var categorias = await fluxo.PorCategoriaAsync(_dia.AddDays(-120), _dia, true);
        var depositos = await recebiveis.EsperadosAsync(_dia.AddDays(30));
        InadimplenciaService.Resumir(devedores).Should().BeEquivalentTo(esperadoInad);
        FluxoCaixaService.Resumir(categorias).Should().BeEquivalentTo(esperadoFluxo);
        RecebiveisService.Resumir(depositos, _dia).Should().BeEquivalentTo(esperadoRecebiveis);
        _medidor.Consultas.Should().HaveCount(4);
    }

    private sealed class Medidor : DbCommandInterceptor
    {
        public List<string> Consultas { get; } = [];
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Consultas.Add(command.CommandText);
            await Task.Delay(15, cancellationToken);
            return result;
        }
    }

    public void Dispose() { _db.Dispose(); _conexao.Dispose(); }
}
