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
/// O formato do número da guia por convênio (parcela 45): Unimed só números; Petrobras,
/// Amil e as operadoras que a clínica cadastrar como alfanuméricas aceitam letra.
///
/// A cobertura anda em DUAS direções com o mesmo peso, como a conferência de alergia da
/// parcela 40: recusar o que está errado é metade do valor; a outra metade é NÃO recusar o
/// que está certo. Validação apertada demais trava o faturamento do dia, e o faturista não
/// tem como contornar — ele não inventa o número, quem emite é a operadora.
/// </summary>
public class NumeroGuiaPorConvenioTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;

    public NumeroGuiaPorConvenioTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
    }

    // ------------------------------------------------------------------ a regra pura

    [Theory]
    [InlineData("123456")]
    [InlineData("000000000001")]
    public void SomenteNumeros_aceita_digito(string numero)
        => RegraNumeroGuia.Criticar(numero, FormatoNumeroGuia.SomenteNumeros).Should().BeNull();

    [Theory]
    [InlineData("O12345")]   // o "O" no lugar do zero — o erro que a regra existe para pegar
    [InlineData("G-1234")]
    [InlineData("1234 5678")]
    public void SomenteNumeros_recusa_o_que_nao_e_digito(string numero)
        => RegraNumeroGuia.Criticar(numero, FormatoNumeroGuia.SomenteNumeros).Should().NotBeNull();

    [Theory]
    [InlineData("AB12345")]
    [InlineData("123456")]   // só dígito também é alfanumérico — recusar seria absurdo
    public void Alfanumerico_aceita_letra_e_digito(string numero)
        => RegraNumeroGuia.Criticar(numero, FormatoNumeroGuia.Alfanumerico).Should().BeNull();

    [Theory]
    [InlineData("AB-12345")]
    [InlineData("AB 12345")]
    [InlineData("----")]     // só pontuação não é número de guia nenhum
    public void Alfanumerico_recusa_pontuacao(string numero)
        => RegraNumeroGuia.Criticar(numero, FormatoNumeroGuia.Alfanumerico).Should().NotBeNull();

    /// <summary>
    /// Em branco passa em QUALQUER formato. Quem valida forma não decide obrigatoriedade:
    /// "ainda não tenho o número" é resposta legítima na fila do Gerente, e é a tela de
    /// baixa do faturamento que exige o preenchimento.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Numero_em_branco_passa_em_qualquer_formato(string? numero)
    {
        RegraNumeroGuia.Criticar(numero, FormatoNumeroGuia.SomenteNumeros).Should().BeNull();
        RegraNumeroGuia.Criticar(numero, FormatoNumeroGuia.Alfanumerico).Should().BeNull();
    }

    [Fact]
    public void SemValidacao_aceita_qualquer_coisa()
        => RegraNumeroGuia.Criticar("G-77/2026 A", FormatoNumeroGuia.SemValidacao).Should().BeNull();

    /// <summary>
    /// O padrão de cada família embutida é o que a migration semeia. Este teste é o que
    /// impede alguém de "arrumar" o padrão da Unimed sem perceber que está mudando a regra
    /// que a cliente pediu.
    /// </summary>
    [Fact]
    public void Padrao_das_familias_embutidas()
    {
        RegraNumeroGuia.PadraoDaFamilia(Convenio.UnimedPadrao)
            .Should().Be(FormatoNumeroGuia.SomenteNumeros);
        RegraNumeroGuia.PadraoDaFamilia(Convenio.UnimedIntercambio)
            .Should().Be(FormatoNumeroGuia.SomenteNumeros);
        RegraNumeroGuia.PadraoDaFamilia(Convenio.Petrobras)
            .Should().Be(FormatoNumeroGuia.Alfanumerico);
        RegraNumeroGuia.PadraoDaFamilia(Convenio.Amil)
            .Should().Be(FormatoNumeroGuia.Alfanumerico);

        // Personalizado é a operadora que a clínica cadastrou (a Sul América entrou por
        // aqui). Não se chuta formato para quem o sistema não conhece.
        RegraNumeroGuia.PadraoDaFamilia(Convenio.Personalizado)
            .Should().Be(FormatoNumeroGuia.SemValidacao);
    }

    // ------------------------------------------------------- resolução pelo catálogo

    /// <summary>
    /// O que a clínica marcou no catálogo VENCE o padrão da família — inclusive para
    /// desligar a validação de um convênio embutido, se a operadora mudar a numeração. Sem
    /// isso, a tela de Configurações mostraria uma escolha que o sistema ignora.
    /// </summary>
    [Fact]
    public void Catalogo_vence_o_padrao_da_familia()
    {
        try
        {
            CatalogoConvenios.Atualizar(
            [
                new EntradaConvenio("UnimedPadrao", "Unimed", Convenio.UnimedPadrao, true,
                    FormatoNumeroGuia: FormatoNumeroGuia.SemValidacao),
                new EntradaConvenio("SulAmerica", "Sul América", Convenio.Personalizado, true,
                    FormatoNumeroGuia: FormatoNumeroGuia.Alfanumerico)
            ]);

            CatalogoConvenios.FormatoDoNumeroDaGuia("UnimedPadrao")
                .Should().Be(FormatoNumeroGuia.SemValidacao);

            // A Sul América não existe no enum Convenio: ela é do catálogo, e é por isso
            // que o formato é dado e não código.
            CatalogoConvenios.FormatoDoNumeroDaGuia("SulAmerica")
                .Should().Be(FormatoNumeroGuia.Alfanumerico);
        }
        finally
        {
            CatalogoConvenios.Atualizar([]); // o cache é estático: não vaza para os demais testes
        }
    }

    /// <summary>
    /// Fora do catálogo, família embutida cai no padrão dela — é o banco anterior ao
    /// catálogo, e é o caminho que os testes usam.
    /// </summary>
    [Fact]
    public void Fora_do_catalogo_familia_embutida_usa_o_padrao()
        => CatalogoConvenios.FormatoDoNumeroDaGuia("Amil")
            .Should().Be(FormatoNumeroGuia.Alfanumerico);

    /// <summary>
    /// Código DESCONHECIDO não valida nada. Aqui não se sabe de que operadora é a guia, e
    /// exigir dígito porque <c>Familia()</c> devolve UnimedPadrao por falta de resposta
    /// melhor recusaria baixa legítima por um dado que falta do nosso lado.
    /// </summary>
    [Fact]
    public void Codigo_desconhecido_nao_valida()
        => CatalogoConvenios.FormatoDoNumeroDaGuia("OperadoraQueFoiExcluida")
            .Should().Be(FormatoNumeroGuia.SemValidacao);

    // ------------------------------------------------------------ a baixa de verdade

    /// <summary>
    /// A crítica mora no <c>FaturamentoService</c>, e não na tela, porque a baixa tem
    /// QUATRO portas (tela de baixa, baixa em lote, rodada de pendências e fila do
    /// Gerente). Este teste é o que garante que todas as quatro herdam a regra.
    /// </summary>
    [Fact]
    public async Task Baixa_de_guia_da_Unimed_recusa_numero_com_letra()
    {
        var codigoId = await SemearGuiaAsync(Convenio.UnimedIntercambio);
        var faturamento = new FaturamentoService(_repo);

        var acao = () => faturamento.DarBaixaAsync(
            codigoId, new DateOnly(2026, 8, 7), "G-777", "sec", null);

        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*só com números*");

        // E a guia continua EM ABERTO: recusar tem de recusar de verdade, não gravar
        // metade do fato.
        (await _db.Codigos.FindAsync(codigoId))!.DataBaixa.Should().BeNull();
    }

    [Fact]
    public async Task Baixa_de_guia_da_Unimed_aceita_numero_so_com_digitos()
    {
        var codigoId = await SemearGuiaAsync(Convenio.UnimedIntercambio);

        await new FaturamentoService(_repo).DarBaixaAsync(
            codigoId, new DateOnly(2026, 8, 7), "90777", "sec", null);

        var codigo = await _db.Codigos.FindAsync(codigoId);
        codigo!.NumeroGuiaReal.Should().Be("90777");
        codigo.Status.Should().Be(StatusCodigo.Baixado);
    }

    [Fact]
    public async Task Baixa_de_guia_da_Amil_aceita_letra_e_numero()
    {
        var codigoId = await SemearGuiaAsync(Convenio.Amil);

        await new FaturamentoService(_repo).DarBaixaAsync(
            codigoId, new DateOnly(2026, 8, 7), "AMI2026X41", "sec", null);

        (await _db.Codigos.FindAsync(codigoId))!.NumeroGuiaReal.Should().Be("AMI2026X41");
    }

    [Fact]
    public async Task Baixa_sem_numero_de_guia_continua_permitida()
    {
        var codigoId = await SemearGuiaAsync(Convenio.UnimedPadrao);

        await new FaturamentoService(_repo).DarBaixaAsync(
            codigoId, new DateOnly(2026, 8, 7), null, "sec", null);

        (await _db.Codigos.FindAsync(codigoId))!.Status.Should().Be(StatusCodigo.Baixado);
    }

    /// <summary>
    /// O Include que a parcela 45 acrescentou ao <c>ObterCodigoAsync</c> conserta um
    /// defeito silencioso: a auditoria da baixa gravava o paciente do
    /// <c>codigo.Atendimento?.PacienteId</c>, e sem o atendimento carregado isso era null.
    /// A trilha registrava a ação sem dizer de quem era a guia.
    /// </summary>
    [Fact]
    public async Task Auditoria_da_baixa_grava_de_quem_e_a_guia()
    {
        var codigoId = await SemearGuiaAsync(Convenio.UnimedPadrao);

        await new FaturamentoService(_repo).DarBaixaAsync(
            codigoId, new DateOnly(2026, 8, 7), "4242", "ana", null);

        var evento = await _db.Auditoria.FirstAsync(e => e.Acao == "BaixaGuia");
        evento.PacienteId.Should().NotBeNull();
        evento.Operador.Should().Be("ana");
    }

    private async Task<int> SemearGuiaAsync(Convenio convenio)
    {
        var paciente = new Paciente
        {
            Nome = "Maria Silva",
            Convenio = convenio,
            Sexo = Sexo.Feminino
        };
        _db.Add(paciente);
        await _db.SaveChangesAsync();

        var resultado = await new AtendimentoService(_repo).LancarAsync(
            paciente.Id, new DateOnly(2026, 8, 5), ModalidadeAtendimento.AcupunturaSimples);

        return resultado.Atendimento.Codigos.First().Id;
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
