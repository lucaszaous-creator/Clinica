using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Tests;

/// <summary>
/// Tabela de preço por convênio (parcela 20).
///
/// O valor da guia era digitado à mão em cada conciliação. Um R$ 45 no lugar de R$ 145 não
/// é recusado por ninguém, e a diferença só apareceria numa conferência que a clínica não
/// faz.
///
/// O que estes testes protegem:
/// 1. **A mais específica ganha** — preço de especialidade vence o genérico do tipo, senão
///    a clínica cadastraria a exceção e continuaria vendo o número da regra geral.
/// 2. **Vigência** — reajuste da operadora não reescreve a guia de março.
/// 3. **Sem preço cadastrado não se inventa valor** — o campo fica vazio, como sempre foi.
/// 4. **O valor é COPIADO no lançamento** — reajustar a tabela não muda o que já foi pago.
/// </summary>
public class PrecoConvenioTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly PrecoConvenioService _precos;
    private readonly FinanceiroService _financeiro;
    private readonly AtendimentoService _atendimentos;

    private static readonly DateOnly Hoje = new(2026, 10, 20);

    public PrecoConvenioTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _precos = new PrecoConvenioService(_repo);
        _financeiro = new FinanceiroService(_repo);
        _atendimentos = new AtendimentoService(_repo);
    }

    private Task<PrecoConvenio> CriarAsync(
        string convenio, TipoCodigo tipo, decimal valor,
        Especialidade? especialidade = null, DateOnly? de = null, DateOnly? ate = null,
        bool ativo = true)
        => _precos.SalvarAsync(new PrecoConvenio
        {
            ConvenioCodigo = convenio,
            Tipo = tipo,
            Valor = valor,
            Especialidade = especialidade,
            VigenteDe = de,
            VigenteAte = ate,
            Ativo = ativo
        });

    // ---------------- O cadastro ----------------

    [Fact]
    public async Task Preco_SemConvenio_ERecusado()
    {
        var acao = () => CriarAsync("  ", TipoCodigo.Acupuntura, 100m);

        await acao.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Preco_ZeroOuNegativo_ERecusado()
    {
        // Preço zero é o mesmo que não ter tabela — e nesse caso é melhor não cadastrar,
        // porque um zero na tabela proporia R$ 0,00 com cara de valor conferido.
        var acao = () => CriarAsync("Unimed", TipoCodigo.Acupuntura, 0m);

        await acao.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Vigencia_QueTerminaAntesDeComecar_ERecusada()
    {
        var acao = () => CriarAsync(
            "Unimed", TipoCodigo.Acupuntura, 100m, de: Hoje, ate: Hoje.AddDays(-1));

        await acao.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Editar_MantemOMesmoRegistroEGravaAuditoria()
    {
        var p = await CriarAsync("Unimed", TipoCodigo.Acupuntura, 100m);

        p.Valor = 120m;
        var salvo = await _precos.SalvarAsync(p, operador: "ana");

        salvo.Id.Should().Be(p.Id);
        (await _precos.CatalogoAsync()).Should().ContainSingle();
        (await _repo.EventosAuditoriaAsync())
            .Should().Contain(e => e.Acao == "PrecoConvenioAtualizado" && e.Operador == "ana");
    }

    [Fact]
    public async Task Descricao_EEscritaEmPtBrFixo()
    {
        var p = await CriarAsync("Unimed", TipoCodigo.Acupuntura, 145.50m);

        // Este texto é copiado para a procedência e fica gravado: dois postos com culturas
        // diferentes escreveriam "R$ 145,50" e "R$145.50" na mesma coluna.
        p.Descricao.Should().Contain("145,50");
    }

    // ---------------- A resolução ----------------

    [Fact]
    public async Task Resolver_AchaOPrecoDoConvenioEDoTipo()
    {
        await CriarAsync("Unimed", TipoCodigo.Acupuntura, 100m);
        await CriarAsync("Unimed", TipoCodigo.Consulta, 250m);
        await CriarAsync("Amil", TipoCodigo.Acupuntura, 80m);

        (await _precos.ResolverAsync("Unimed", TipoCodigo.Acupuntura, Hoje))!.Valor.Should().Be(100m);
        (await _precos.ResolverAsync("Unimed", TipoCodigo.Consulta, Hoje))!.Valor.Should().Be(250m);
        (await _precos.ResolverAsync("Amil", TipoCodigo.Acupuntura, Hoje))!.Valor.Should().Be(80m);
    }

    [Fact]
    public async Task Resolver_AMaisESPECIFICAGanha()
    {
        await CriarAsync("Unimed", TipoCodigo.Consulta, 200m);
        await CriarAsync("Unimed", TipoCodigo.Consulta, 350m, especialidade: Especialidade.ClinicaDaDor);

        // Sem a especificidade, a clínica cadastraria a exceção e continuaria vendo 200.
        (await _precos.ResolverAsync("Unimed", TipoCodigo.Consulta, Hoje, Especialidade.ClinicaDaDor))!
            .Valor.Should().Be(350m);
        // Outra especialidade cai no genérico.
        (await _precos.ResolverAsync("Unimed", TipoCodigo.Consulta, Hoje, Especialidade.Geriatria))!
            .Valor.Should().Be(200m);
    }

    [Fact]
    public async Task Resolver_PrecoDeEspecialidade_NaoVazaParaOutra()
    {
        await CriarAsync("Unimed", TipoCodigo.Consulta, 350m, especialidade: Especialidade.ClinicaDaDor);

        // Não há genérico: outra especialidade fica sem preço, que é a verdade.
        (await _precos.ResolverAsync("Unimed", TipoCodigo.Consulta, Hoje, Especialidade.Geriatria))
            .Should().BeNull();
    }

    [Fact]
    public async Task Resolver_Reajuste_NaoReescreveOPassado()
    {
        await CriarAsync("Unimed", TipoCodigo.Acupuntura, 100m, ate: new DateOnly(2026, 9, 30));
        await CriarAsync("Unimed", TipoCodigo.Acupuntura, 120m, de: new DateOnly(2026, 10, 1));

        (await _precos.ResolverAsync("Unimed", TipoCodigo.Acupuntura, new DateOnly(2026, 9, 15)))!
            .Valor.Should().Be(100m);
        (await _precos.ResolverAsync("Unimed", TipoCodigo.Acupuntura, new DateOnly(2026, 10, 15)))!
            .Valor.Should().Be(120m);
    }

    [Fact]
    public async Task Resolver_EntreIguais_ORenegociadoMaisNovoGanha()
    {
        await CriarAsync("Unimed", TipoCodigo.Acupuntura, 100m, de: new DateOnly(2026, 1, 1));
        await CriarAsync("Unimed", TipoCodigo.Acupuntura, 130m, de: new DateOnly(2026, 6, 1));

        (await _precos.ResolverAsync("Unimed", TipoCodigo.Acupuntura, Hoje))!.Valor.Should().Be(130m);
    }

    [Fact]
    public async Task Resolver_Inativo_NaoEUsado()
    {
        await CriarAsync("Unimed", TipoCodigo.Acupuntura, 100m, ativo: false);

        (await _precos.ResolverAsync("Unimed", TipoCodigo.Acupuntura, Hoje)).Should().BeNull();
    }

    [Fact]
    public async Task Resolver_ConvenioSemTabela_ENulo()
    {
        await CriarAsync("Unimed", TipoCodigo.Acupuntura, 100m);

        (await _precos.ResolverAsync("Bradesco", TipoCodigo.Acupuntura, Hoje)).Should().BeNull();
    }

    [Fact]
    public async Task Resolver_SemCodigoDeConvenio_ENulo()
    {
        await CriarAsync("Unimed", TipoCodigo.Acupuntura, 100m);

        (await _precos.ResolverAsync(null, TipoCodigo.Acupuntura, Hoje)).Should().BeNull();
        (await _precos.ResolverAsync("  ", TipoCodigo.Acupuntura, Hoje)).Should().BeNull();
    }

    [Fact]
    public async Task Resolver_CasaOCodigoIgnorandoMaiusculas()
    {
        await CriarAsync("Unimed", TipoCodigo.Acupuntura, 100m);

        (await _precos.ResolverAsync("UNIMED", TipoCodigo.Acupuntura, Hoje))!.Valor.Should().Be(100m);
    }

    // ---------------- A proposta, com a guia real ----------------

    private async Task<GuiaSemLancamento> GuiaAsync(string convenioCodigo)
    {
        var paciente = new Paciente
        {
            Nome = "Maria Souza",
            Convenio = Convenio.UnimedPadrao,
            ConvenioCodigo = convenioCodigo
        };
        await _repo.AdicionarPacienteAsync(paciente);
        await _repo.SalvarAsync();

        var resultado = await _atendimentos.LancarAsync(
            paciente.Id, Hoje, ModalidadeAtendimento.AcupunturaSimples);

        var codigo = resultado.Atendimento.Codigos[0];
        codigo.Status = StatusCodigo.Baixado;
        codigo.DataBaixa = Hoje;
        foreach (var c in resultado.Atendimento.Codigos.Skip(1))
            c.Status = StatusCodigo.NaoAplicavel;
        await _repo.SalvarAsync();

        return (await _financeiro.GuiasSemLancamentoAsync(Hoje, Hoje))
            .First(g => g.CodigoId == codigo.Id);
    }

    [Fact]
    public async Task Propor_TrazOValorEDeOndeEleVeio()
    {
        await CriarAsync("Unimed", TipoCodigo.Acupuntura, 145m);
        var guia = await GuiaAsync("Unimed");

        var proposto = await _precos.ProporAsync(guia);

        proposto.Houve.Should().BeTrue();
        proposto.Valor.Should().Be(145m);
        // Um campo que se preenche sozinho sem explicar é pior que um campo vazio: a pessoa
        // confirma sem conferir, e o erro entra no caixa com aparência de conferido.
        proposto.Procedencia.Should().Contain("tabela:").And.Contain("Unimed");
    }

    [Fact]
    public async Task Propor_SemTabela_NaoInventaValor()
    {
        var guia = await GuiaAsync("Unimed");

        var proposto = await _precos.ProporAsync(guia);

        // O campo fica vazio para ser digitado, como sempre foi. Chutar um valor "de
        // mercado" produziria receita errada com aparência de exata.
        proposto.Should().Be(PrecoProposto.Nenhum);
        proposto.Houve.Should().BeFalse();
    }

    [Fact]
    public async Task Propor_UsaADataDaBAIXADaGuia_NaoADeHoje()
    {
        // Tabela encerrada ontem: a guia baixada hoje NÃO pode pegar o valor antigo, e a
        // guia baixada no mês passado tem de pegar o valor do mês passado.
        await CriarAsync("Unimed", TipoCodigo.Acupuntura, 100m, ate: Hoje.AddDays(-1));
        var guia = await GuiaAsync("Unimed");

        (await _precos.ProporAsync(guia)).Houve.Should().BeFalse();
    }

    [Fact]
    public async Task ReceitaDaGuia_COPIAOValorProposto()
    {
        await CriarAsync("Unimed", TipoCodigo.Acupuntura, 145m);
        var guia = await GuiaAsync("Unimed");

        var proposto = await _precos.ProporAsync(guia);
        var lancamento = await _financeiro.LancarReceitaDaGuiaAsync(guia, proposto.Valor);

        lancamento.Valor.Should().Be(145m);

        // O reajuste da tabela NÃO reescreve o que já foi lançado — mesma regra do preço do
        // pacote e da taxa da maquininha.
        await CriarAsync("Unimed", TipoCodigo.Acupuntura, 200m, de: Hoje.AddDays(1));
        (await _repo.ObterLancamentoAsync(lancamento.Id))!.Valor.Should().Be(145m);
    }

    [Fact]
    public async Task Excluir_TiraDoCatalogo()
    {
        var p = await CriarAsync("Unimed", TipoCodigo.Acupuntura, 100m);

        await _precos.ExcluirAsync(p.Id);

        (await _precos.CatalogoAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Catalogo_SomenteAtivos_FiltraODesativado()
    {
        await CriarAsync("Unimed", TipoCodigo.Acupuntura, 100m);
        await CriarAsync("Amil", TipoCodigo.Acupuntura, 80m, ativo: false);

        (await _precos.CatalogoAsync()).Should().HaveCount(2);
        (await _precos.CatalogoAsync(somenteAtivos: true))
            .Should().ContainSingle().Which.ConvenioCodigo.Should().Be("Unimed");
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }
}
