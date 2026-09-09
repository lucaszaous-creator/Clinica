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
/// O PARTICULAR EXISTE — a metade que faltava (set/2026).
///
/// O <c>ConvenioParticularTests</c> prova o COMPORTAMENTO do particular desde a parcela
/// 60: os códigos nascem <c>NaoAplicavel</c>, ele não entra em pendência nem na rodada
/// bloqueante, a prévia bate com o lançamento. E ele monta o catálogo À MÃO, com uma
/// entrada "Particular" que o teste inventa — então nunca provou o degrau anterior:
/// <b>que essa entrada existe no sistema de verdade</b>.
///
/// Ela não existia. A migration semeia quatro convênios (Unimed x2, Amil, Petrobras) e os
/// quatro geram guia; "particular" só passava a existir se alguém abrisse o app de
/// FATURAMENTO → Configurações → Convênios, criasse a linha à mão e desmarcasse "gera
/// guia" — e a recepção não tem essa tela. O relato do cliente foi exatamente o desfecho
/// disso: *"nem eu mesmo consegui entender como fazer um atendimento particular"*.
///
/// É a variante mais discreta do defeito recorrente do projeto: não é dado sem leitor nem
/// capacidade sem porta — é <b>comportamento testado que nenhum cadastro alcança</b>. A
/// lição de teste que ela deixa: quando o teste de uma feature MONTA o dado que a torna
/// possível, falta o teste de que alguém o cria.
/// </summary>
public class ParticularNoCatalogoTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ConvenioCatalogoService _catalogo;

    public ParticularNoCatalogoTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _catalogo = new ConvenioCatalogoService(new ClinicaRepositorio(_db));
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Base RECÉM-INSTALADA, sem uma linha de convênio gravada: o particular está lá. É o
    /// estado em que a clínica estava quando o cliente tentou fazer o atendimento.
    /// </summary>
    [Fact]
    public async Task O_particular_esta_no_catalogo_de_uma_base_sem_nenhum_convenio_gravado()
    {
        var lista = await _catalogo.ListarAsync();

        var particular = lista.Should()
            .ContainSingle(c => c.Codigo == ConvenioCadastro.CodigoParticular).Subject;

        particular.Ativo.Should().BeTrue("escondido do cadastro ele não resolve nada");
        particular.GeraGuia.Should().BeFalse(
            "é o campo que faz a sessão dele nascer sem guia — e sem ele o particular vira "
            + "pendência eterna no painel de quem fatura");
    }

    /// <summary>
    /// O CACHE que as telas leem de forma síncrona precisa saber que ele não gera guia:
    /// é <c>CatalogoConvenios.GeraGuia</c> que o <c>AtendimentoService</c> consulta para
    /// marcar os códigos como <c>NaoAplicavel</c>. Sem esta linha, o particular estaria na
    /// lista e a sessão dele geraria guia assim mesmo.
    /// </summary>
    [Fact]
    public async Task O_cache_das_telas_sabe_que_o_particular_nao_gera_guia()
    {
        await _catalogo.RecarregarCacheAsync();

        CatalogoConvenios.GeraGuia(ConvenioCadastro.CodigoParticular).Should().BeFalse();
        CatalogoConvenios.Nome(ConvenioCadastro.CodigoParticular).Should().Be("Particular");
    }

    /// <summary>
    /// A clínica que JÁ criou o particular dela — com outro nome, à mão, no app de
    /// faturamento — continua com a linha dela. A garantia acrescenta o que falta; ela não
    /// sobrescreve o que a clínica decidiu.
    /// </summary>
    [Fact]
    public async Task O_particular_ja_cadastrado_pela_clinica_nao_e_sobrescrito()
    {
        await _catalogo.SalvarAsync(
        [
            new ConvenioCadastro
            {
                Codigo = ConvenioCadastro.CodigoParticular,
                Nome = "Particular / Convênio próprio",
                Familia = Convenio.Personalizado,
                Ativo = true,
                GeraGuia = false
            }
        ]);

        var lista = await _catalogo.ListarAsync();

        lista.Should().ContainSingle(c => c.Codigo == ConvenioCadastro.CodigoParticular)
            .Which.Nome.Should().Be("Particular / Convênio próprio");
    }

    /// <summary>
    /// ⚠️ "A definir" e "Particular" são coisas DIFERENTES, e confundi-las faria a clínica
    /// inteira de particulares aparecer como pendência para sempre: "a definir" é a
    /// PERGUNTA que o <c>ElegibilidadeService</c> acusa em vermelho até alguém responder;
    /// "particular" é a RESPOSTA. Os dois não geram guia, e é só isso que têm em comum.
    ///
    /// O "a definir" continua NÃO sendo garantido aqui: ele nasce da importação, e uma
    /// clínica que nunca importou nada não deve ganhar um convênio chamado "importado sem
    /// convênio" no cadastro.
    /// </summary>
    [Fact]
    public async Task O_particular_nao_se_confunde_com_o_a_definir()
    {
        var lista = await _catalogo.ListarAsync();

        lista.Should().NotContain(c => c.Codigo == ConvenioCadastro.CodigoADefinir);

        ConvenioCadastro.Particular().Codigo.Should().NotBe(ConvenioCadastro.CodigoADefinir);
        ConvenioCadastro.ADefinir().GeraGuia.Should().BeFalse();
        ConvenioCadastro.Particular().GeraGuia.Should().BeFalse();
    }
}
