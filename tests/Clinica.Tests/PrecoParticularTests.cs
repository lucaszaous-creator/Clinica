using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// A tabela de preço do PARTICULAR por especialidade atendida (set/2026): a regra de
/// resolução (a mais específica ganha, vigência, variante cai na família) e as recusas do
/// cadastro. Quem lê é o Finalizar do balcão e a Conciliação — e os dois precisam
/// propor o MESMO número para a mesma sessão, por isso a regra mora num serviço só.
/// </summary>
public class PrecoParticularTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly PrecoParticularService _precos;

    private static readonly DateOnly Hoje = new(2026, 9, 6);
    private const string Consulta = nameof(ModalidadeAtendimento.Consulta);
    private const string AcupEletro = nameof(ModalidadeAtendimento.AcupunturaComEletro);

    public PrecoParticularTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _precos = new PrecoParticularService(new ClinicaRepositorio(_db));

        // Uma variante da clínica ("acupuntura domiciliar") sobre a família com eletro.
        CatalogoModalidades.Atualizar(
        [
            new EntradaModalidade(AcupEletro, "Acupuntura + eletro", ModalidadeAtendimento.AcupunturaComEletro, true),
            new EntradaModalidade("AcupDomiciliar", "Acupuntura (domiciliar)", ModalidadeAtendimento.AcupunturaComEletro, true),
            new EntradaModalidade(Consulta, "Consulta", ModalidadeAtendimento.Consulta, true)
        ]);
    }

    public void Dispose()
    {
        CatalogoModalidades.Atualizar([]);
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }

    private Task<PrecoParticular> CriarAsync(
        string modalidade, decimal valor, string? especialidade = null,
        DateOnly? de = null, DateOnly? ate = null, bool ativo = true)
        => _precos.SalvarAsync(new PrecoParticular
        {
            ModalidadeCodigo = modalidade, EspecialidadeCodigo = especialidade,
            Valor = valor, VigenteDe = de, VigenteAte = ate, Ativo = ativo
        });

    [Fact]
    public async Task Sem_tabela_nao_se_inventa_valor()
    {
        var proposto = await _precos.ProporAsync(Consulta, ModalidadeAtendimento.Consulta, null, Hoje);
        proposto.Houve.Should().BeFalse();
    }

    [Fact]
    public async Task Preco_com_especialidade_vence_o_generico_da_modalidade()
    {
        await CriarAsync(Consulta, 300m);
        await CriarAsync(Consulta, 450m, nameof(Especialidade.Psiquiatria));

        var psiquiatria = await _precos.ResolverAsync(Consulta, ModalidadeAtendimento.Consulta, nameof(Especialidade.Psiquiatria), Hoje);
        var geriatria = await _precos.ResolverAsync(Consulta, ModalidadeAtendimento.Consulta, nameof(Especialidade.Geriatria), Hoje);
        var semEspecialidade = await _precos.ResolverAsync(Consulta, ModalidadeAtendimento.Consulta, null, Hoje);

        psiquiatria!.Valor.Should().Be(450m);
        geriatria!.Valor.Should().Be(300m, "sem preço próprio cai no genérico");
        semEspecialidade!.Valor.Should().Be(300m);
    }

    [Fact]
    public async Task Preco_so_com_especialidade_NAO_serve_a_outra_especialidade()
    {
        await CriarAsync(Consulta, 450m, nameof(Especialidade.Psiquiatria));

        (await _precos.ResolverAsync(Consulta, ModalidadeAtendimento.Consulta, nameof(Especialidade.Geriatria), Hoje))
            .Should().BeNull("preço de psiquiatria não é preço de geriatria — melhor pedir o valor do que inventar");
    }

    [Fact]
    public async Task Variante_da_modalidade_cai_no_preco_da_familia_e_o_proprio_vence()
    {
        await CriarAsync(AcupEletro, 180m);

        // A domiciliar não tem preço próprio: vale o da família.
        (await _precos.ResolverAsync("AcupDomiciliar", ModalidadeAtendimento.AcupunturaComEletro, null, Hoje))!
            .Valor.Should().Be(180m);

        // Cadastrado o próprio, ele vence.
        await CriarAsync("AcupDomiciliar", 260m);
        (await _precos.ResolverAsync("AcupDomiciliar", ModalidadeAtendimento.AcupunturaComEletro, null, Hoje))!
            .Valor.Should().Be(260m);
        // E a família continua com o dela.
        (await _precos.ResolverAsync(AcupEletro, ModalidadeAtendimento.AcupunturaComEletro, null, Hoje))!
            .Valor.Should().Be(180m);
    }

    [Fact]
    public async Task Reajuste_e_linha_nova_e_a_sessao_de_antes_segue_com_o_preco_de_antes()
    {
        await CriarAsync(AcupEletro, 180m, ate: new DateOnly(2026, 8, 31));
        await CriarAsync(AcupEletro, 200m, de: new DateOnly(2026, 9, 1));

        (await _precos.ResolverAsync(AcupEletro, ModalidadeAtendimento.AcupunturaComEletro, null, new DateOnly(2026, 8, 20)))!
            .Valor.Should().Be(180m);
        (await _precos.ResolverAsync(AcupEletro, ModalidadeAtendimento.AcupunturaComEletro, null, Hoje))!
            .Valor.Should().Be(200m);
    }

    [Fact]
    public async Task Inativo_nao_propoe()
    {
        await CriarAsync(AcupEletro, 180m, ativo: false);
        (await _precos.ResolverAsync(AcupEletro, ModalidadeAtendimento.AcupunturaComEletro, null, Hoje)).Should().BeNull();
    }

    [Fact]
    public async Task A_procedencia_escreve_o_nome_do_catalogo_nunca_o_enum()
    {
        await CriarAsync(Consulta, 450m, nameof(Especialidade.Psiquiatria));

        var proposto = await _precos.ProporAsync(Consulta, ModalidadeAtendimento.Consulta, nameof(Especialidade.Psiquiatria), Hoje);

        proposto.Procedencia.Should().Contain("Consulta").And.Contain("Psiquiatria")
            .And.NotContain("ModalidadeAtendimento");
    }

    [Theory]
    [InlineData("", 100)]
    [InlineData("Consulta", 0)]
    [InlineData("Consulta", -5)]
    public async Task Cadastro_sem_modalidade_ou_sem_valor_e_recusado(string modalidade, decimal valor)
    {
        var salvar = () => CriarAsync(modalidade, valor);
        await salvar.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Especialidade_em_branco_e_gravada_como_NULO_e_vale_para_todas()
    {
        var preco = await CriarAsync(Consulta, 300m, especialidade: "   ");
        preco.EspecialidadeCodigo.Should().BeNull();
        (await _precos.ResolverAsync(Consulta, ModalidadeAtendimento.Consulta, nameof(Especialidade.Geriatria), Hoje))
            .Should().NotBeNull();
    }
}
