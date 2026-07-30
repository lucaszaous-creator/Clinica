using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Tests;

/// <summary>
/// A trilha de auditoria (parcela 21).
///
/// `EventoAuditoria` era gravado por praticamente tudo o que mexe em dinheiro ou permissão
/// e **nenhuma tela o lia** — quarta ocorrência do mesmo defeito no projeto, e a mais grave:
/// as outras eram dinheiro que ninguém via; esta é a resposta para "quem fez isso?", que é
/// o que se pergunta justamente quando algo deu errado.
///
/// O que estes testes protegem:
/// 1. **O filtro vai para o SQL**, com o corte no banco — esta é a tabela que mais cresce.
/// 2. **A ação casa por PREFIXO**: "Conta" acha ContaCriada e ContaReagendada.
/// 3. **O dia final entra inteiro** — senão um evento das 14h de hoje ficaria fora de um
///    filtro que pede "até hoje".
/// 4. **O resumo conta sobre o resultado do filtro**, não sobre a tabela inteira.
/// </summary>
public class AuditoriaServiceTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly AuditoriaService _auditoria;

    private static readonly DateOnly Hoje = new(2026, 11, 10);

    public AuditoriaServiceTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _auditoria = new AuditoriaService(_repo);
    }

    private async Task RegistrarAsync(
        string acao, string operador = "maria", string? detalhe = null,
        DateOnly? dia = null, TimeOnly? hora = null, int? pacienteId = null, int? codigoId = null)
    {
        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Acao = acao,
            Operador = operador,
            Detalhe = detalhe,
            DataHora = (dia ?? Hoje).ToDateTime(hora ?? new TimeOnly(9, 0)),
            PacienteId = pacienteId,
            CodigoId = codigoId
        });
        await _repo.SalvarAsync();
    }

    // ---------------- O filtro ----------------

    [Fact]
    public async Task SemFiltro_TrazTudoDoMaisRecenteAoMaisAntigo()
    {
        await RegistrarAsync("BaixaGuia", hora: new TimeOnly(8, 0));
        await RegistrarAsync("Glosa", hora: new TimeOnly(15, 0));

        var eventos = await _auditoria.ConsultarAsync(new FiltroAuditoria());

        eventos.Should().HaveCount(2);
        eventos[0].Acao.Should().Be("Glosa");
    }

    [Fact]
    public async Task Acao_CasaPorPREFIXO()
    {
        await RegistrarAsync("ContaCriada");
        await RegistrarAsync("ContaReagendada");
        await RegistrarAsync("BaixaGuia");

        var eventos = await _auditoria.ConsultarAsync(new FiltroAuditoria { Acao = "Conta" });

        // As ações são nomes compostos por família, e o prefixo é a forma natural de pedir
        // a família inteira.
        eventos.Should().HaveCount(2);
        eventos.Should().OnlyContain(e => e.Acao.StartsWith("Conta"));
    }

    [Fact]
    public async Task Operador_CasaPorTrecho()
    {
        await RegistrarAsync("BaixaGuia", operador: "maria.silva");
        await RegistrarAsync("BaixaGuia", operador: "joao");

        // O operador é digitado, não escolhido de uma lista fechada.
        (await _auditoria.ConsultarAsync(new FiltroAuditoria { Operador = "maria" }))
            .Should().ContainSingle();
    }

    [Fact]
    public async Task Termo_ProcuraNoDetalhe()
    {
        await RegistrarAsync("BaixaGuia", detalhe: "guia 12345 — Maria Souza");
        await RegistrarAsync("BaixaGuia", detalhe: "guia 99999 — João");

        (await _auditoria.ConsultarAsync(new FiltroAuditoria { Termo = "12345" }))
            .Should().ContainSingle();
    }

    [Fact]
    public async Task Periodo_ODiaFinalEntraINTEIRO()
    {
        // Um evento das 14h de hoje NÃO pode ficar fora de um filtro que pede "até hoje".
        await RegistrarAsync("BaixaGuia", hora: new TimeOnly(14, 30));

        var eventos = await _auditoria.ConsultarAsync(new FiltroAuditoria
        {
            Inicio = Hoje,
            Fim = Hoje
        });

        eventos.Should().ContainSingle();
    }

    [Fact]
    public async Task Periodo_IgnoraOQueEstaFora()
    {
        await RegistrarAsync("BaixaGuia", dia: Hoje.AddDays(-10));
        await RegistrarAsync("Glosa", dia: Hoje);

        var eventos = await _auditoria.ConsultarAsync(new FiltroAuditoria
        {
            Inicio = Hoje.AddDays(-1),
            Fim = Hoje
        });

        eventos.Should().ContainSingle().Which.Acao.Should().Be("Glosa");
    }

    [Fact]
    public async Task Limite_CortaNoBanco()
    {
        for (var i = 0; i < 10; i++)
            await RegistrarAsync("BaixaGuia", hora: new TimeOnly(8, i));

        // Esta é a tabela que mais cresce no sistema: o corte precisa ir para o SQL, e não
        // materializar tudo para descartar depois.
        (await _auditoria.ConsultarAsync(new FiltroAuditoria { Limite = 3 })).Should().HaveCount(3);
    }

    [Fact]
    public async Task Limite_ZeroOuNegativo_CaiNoPadrao()
    {
        await RegistrarAsync("BaixaGuia");

        (await _auditoria.ConsultarAsync(new FiltroAuditoria { Limite = 0 }))
            .Should().ContainSingle();
    }

    [Fact]
    public async Task FiltrosSeCombinam()
    {
        await RegistrarAsync("ContaCriada", operador: "maria", dia: Hoje);
        await RegistrarAsync("ContaCriada", operador: "joao", dia: Hoje);
        await RegistrarAsync("ContaCriada", operador: "maria", dia: Hoje.AddDays(-30));

        var eventos = await _auditoria.ConsultarAsync(new FiltroAuditoria
        {
            Acao = "Conta",
            Operador = "maria",
            Inicio = Hoje,
            Fim = Hoje
        });

        eventos.Should().ContainSingle();
    }

    // ---------------- A trilha de uma coisa só ----------------

    [Fact]
    public async Task DaGuia_TrazSoOHistoricoDela()
    {
        await RegistrarAsync("BaixaGuia", codigoId: 7, detalhe: "baixada");
        await RegistrarAsync("EstornoBaixa", codigoId: 7, detalhe: "estornada");
        await RegistrarAsync("BaixaGuia", codigoId: 99);

        var eventos = await _auditoria.DaGuiaAsync(7);

        // É a pergunta que se faz quando a guia está errada: o que aconteceu com ela.
        eventos.Should().HaveCount(2);
        eventos.Should().OnlyContain(e => e.CodigoId == 7);
    }

    [Fact]
    public async Task DoPaciente_TrazSoOHistoricoDele()
    {
        await RegistrarAsync("LancamentoCriado", pacienteId: 3);
        await RegistrarAsync("LancamentoCriado", pacienteId: 4);

        (await _auditoria.DoPacienteAsync(3)).Should().ContainSingle();
    }

    // ---------------- O resumo ----------------

    [Fact]
    public async Task Resumo_ContaPorAcaoEPorOperador_DoMaisFrequente()
    {
        await RegistrarAsync("BaixaGuia", operador: "maria");
        await RegistrarAsync("BaixaGuia", operador: "maria");
        await RegistrarAsync("BaixaGuia", operador: "joao");
        await RegistrarAsync("Glosa", operador: "joao");
        // Uma quarta de maria: sem ela os dois operadores empatam em 2, o desempate
        // alfabético decide, e o teste passaria a afirmar sobre a ordem do alfabeto em vez
        // de sobre a frequência — que é o que a tela precisa mostrar.
        await RegistrarAsync("Glosa", operador: "maria");

        var r = await _auditoria.ResumoAsync(new FiltroAuditoria());

        r.Eventos.Should().Be(5);
        r.PorAcao[0].Should().Be(("BaixaGuia", 3));
        r.PorOperador[0].Operador.Should().Be("maria");
        r.PorOperador[0].Vezes.Should().Be(3);
        r.Vazio.Should().BeFalse();
    }

    [Fact]
    public async Task Resumo_ContaSobreORESULTADODoFiltro_NaoSobreABaseInteira()
    {
        await RegistrarAsync("BaixaGuia", dia: Hoje.AddDays(-60));
        await RegistrarAsync("Glosa", dia: Hoje);

        var r = await _auditoria.ResumoAsync(new FiltroAuditoria { Inicio = Hoje, Fim = Hoje });

        // A pergunta é "no que eu estou olhando, quem fez o quê". Contar a base toda daria
        // um número que não corresponde ao que está na tela.
        r.Eventos.Should().Be(1);
        r.PorAcao.Should().ContainSingle();
    }

    [Fact]
    public async Task Resumo_SemOperadorInformado_ApareceMarcado()
    {
        await RegistrarAsync("BaixaGuia", operador: "  ");

        var r = await _auditoria.ResumoAsync(new FiltroAuditoria());

        // Sumir com o evento porque ninguém assinou seria perder justamente o caso que
        // interessa investigar.
        r.PorOperador[0].Operador.Should().Be("(não informado)");
    }

    [Fact]
    public async Task Resumo_TrilhaVazia_NaoQuebra()
    {
        var r = await _auditoria.ResumoAsync(new FiltroAuditoria());

        r.Vazio.Should().BeTrue();
        r.PorAcao.Should().BeEmpty();
        r.PorOperador.Should().BeEmpty();
    }

    [Fact]
    public async Task AcoesConhecidas_TrazAListaSemRepetir()
    {
        await RegistrarAsync("BaixaGuia");
        await RegistrarAsync("BaixaGuia");
        await RegistrarAsync("Glosa");

        var acoes = await _auditoria.AcoesConhecidasAsync();

        // Para o filtro oferecer a lista em vez de exigir que a pessoa saiba escrever
        // "ContasRecorrentesGeradas" de cabeça.
        acoes.Should().Equal("BaixaGuia", "Glosa");
    }

    // ---------------- Integração: as ações reais gravam ----------------

    [Fact]
    public async Task AcoesDeDinheiro_AparecemNaTrilha()
    {
        var financeiro = new FinanceiroService(_repo);
        var contas = new ContasService(_repo);

        await financeiro.LancarAsync(
            Hoje, TipoLancamento.Entrada, "Sessão", 150m, operador: "ana");
        await contas.LancarContaAsync(
            TipoLancamento.Saida, "Aluguel", 3200m, Hoje.AddDays(10), operador: "ana");

        var eventos = await _auditoria.ConsultarAsync(new FiltroAuditoria { Operador = "ana" });

        // Era isso que estava gravado e invisível: as ações existem, faltava quem lesse.
        eventos.Should().HaveCount(2);
        eventos.Should().Contain(e => e.Acao == "LancamentoCriado");
        eventos.Should().Contain(e => e.Acao == "ContaCriada");
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }
}
