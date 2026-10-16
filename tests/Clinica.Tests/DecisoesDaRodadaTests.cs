using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Tests;

/// <summary>
/// A rodada de pendências aplicada pela regra ÚNICA (out/2026): a janela bloqueante da abertura
/// (WPF) e o painel Blazor passam pelo mesmo <see cref="DecisoesDaRodada"/>. O que se fixa é o
/// que as duas portas não podem divergir: número = baixa, justificativa = não conformidade,
/// linha em branco intocada, e UMA falha não aborta as outras.
/// </summary>
/// <remarks>
/// <see cref="DecisoesDaRodada"/> lê o <see cref="SessaoUsuario.Atual"/> GLOBAL (as duas
/// barreiras). Por isso a classe entra na coleção de quem troca a sessão e parte da sessão
/// anônima: sem isso, um teste que deixasse uma Faturista logada a reprovaria conforme a ordem.
/// </remarks>
[Collection(ColecaoDiagnostico.Nome)]
public sealed class DecisoesDaRodadaTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly RodadaPendenciasService _rodada;
    private readonly FaturamentoService _faturamento;

    public DecisoesDaRodadaTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        _db = new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _rodada = new RodadaPendenciasService(_repo, new PendenciaService(_repo), new ParametrosService(_repo));
        _faturamento = new FaturamentoService(_repo);
        new SessaoUsuario().Entrar(new UsuarioSistema());
    }

    private async Task<IReadOnlyList<CodigoFaturamento>> CodigosAsync()
    {
        var p = new Paciente { Nome = "Paciente da rodada", Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        var r = await new AtendimentoService(_repo).LancarAsync(p.Id, new DateOnly(2026, 7, 10), ModalidadeAtendimento.AcupunturaComEletro);
        return r.Atendimento.Codigos.OrderBy(c => c.Ordem).ToList();
    }

    [Fact]
    public async Task Numero_baixa_justificativa_vira_NC_e_linha_em_branco_fica_intocada()
    {
        var codigos = await CodigosAsync();
        var p2 = new Paciente { Nome = "Outro", Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Masculino };
        _db.Pacientes.Add(p2);
        await _db.SaveChangesAsync();
        var terceiro = (await new AtendimentoService(_repo).LancarAsync(p2.Id, new DateOnly(2026, 7, 10), ModalidadeAtendimento.Consulta))
            .Atendimento.Codigos.First();

        var linhas = new List<LinhaDaRodada>
        {
            new() { CodigoId = codigos[0].Id, Descricao = "a", Situacao = "", NumeroGuia = " 123456 ", Justificativa = "ignorada" },
            new() { CodigoId = codigos[1].Id, Descricao = "b", Situacao = "", Justificativa = "portal fora do ar" },
            new() { CodigoId = terceiro.Id, Descricao = "c", Situacao = "" }
        };

        var falhas = await DecisoesDaRodada.AplicarAsync(linhas, new DateOnly(2026, 7, 21), _faturamento, _rodada);

        falhas.Should().BeEmpty();
        var a = await _repo.ObterCodigoAsync(codigos[0].Id);
        a!.Status.Should().Be(StatusCodigo.Baixado);
        a.NumeroGuiaReal.Should().Be("123456");
        (await _repo.ObterCodigoAsync(codigos[1].Id))!.Status.Should().Be(StatusCodigo.NaoConformidade);
        (await _repo.ObterCodigoAsync(terceiro.Id))!.Status.Should().Be(StatusCodigo.Aberto);
    }

    [Fact]
    public async Task Uma_falha_nao_aborta_as_outras_e_volta_escrita()
    {
        var codigos = await CodigosAsync();
        var linhas = new List<LinhaDaRodada>
        {
            new() { CodigoId = 999_999, Descricao = "guia que não existe", Situacao = "", NumeroGuia = "1" },
            new() { CodigoId = codigos[1].Id, Descricao = "b", Situacao = "", NumeroGuia = "777" }
        };

        var falhas = await DecisoesDaRodada.AplicarAsync(linhas, new DateOnly(2026, 7, 21), _faturamento, _rodada);

        falhas.Should().ContainSingle().Which.Should().Contain("guia que não existe");
        (await _repo.ObterCodigoAsync(codigos[1].Id))!.Status.Should().Be(StatusCodigo.Baixado);
        DecisoesDaRodada.ResumoDasFalhas(falhas).Should().StartWith("1 guia(s) não puderam ser aplicadas");
        DecisoesDaRodada.ResumoDasFalhas([]).Should().BeNull();
    }

    [Fact]
    public void Bloqueante_exige_decisao_em_toda_guia_e_o_formato_e_conferido_antes()
    {
        var linhas = new List<LinhaDaRodada>
        {
            new() { CodigoId = 1, Descricao = "a", Situacao = "", NumeroGuia = "1" },
            new() { CodigoId = 2, Descricao = "b", Situacao = "" }
        };
        DecisoesDaRodada.ImpedimentoParaConcluir(linhas, bloqueante: true).Should().Contain("decida todas as guias");
        DecisoesDaRodada.ImpedimentoParaConcluir(linhas, bloqueante: false).Should().BeNull();

        var soNumeros = new List<LinhaDaRodada>
        {
            new() { CodigoId = 1, Descricao = "Maria", Situacao = "", Formato = FormatoNumeroGuia.SomenteNumeros, NumeroGuia = "12O45" }
        };
        DecisoesDaRodada.ImpedimentoParaConcluir(soNumeros, bloqueante: false)
            .Should().Contain("fora do formato").And.Contain("Maria");
    }

    [Fact]
    public void Resumo_diz_o_prazo_no_bloqueante_e_lembra_consultas_e_carteirinhas()
    {
        var status = new RodadaPendenciasStatus(10, 3, 8, true, 2, true, 1);
        DecisoesDaRodada.Resumo(3, status, bloqueante: true)
            .Should().Contain("mais de 10 dias").And.Contain("2 consulta(s) a renovar").And.Contain("1 carteirinha(s)");
        DecisoesDaRodada.Resumo(3, status, bloqueante: false).Should().StartWith("3 guia(s) pendente(s).");
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
