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
/// As regras da tela de "Regras e catálogos TISS" (Blazor, out/2026), que no WPF moravam no
/// ParametrosViewModel e nas *Edicao — e o Salvar que RELÊ o prestador antes de gravar.
/// </summary>
public class ConfiguracoesFaturamentoNaTelaTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly DbContextOptions<ClinicaDbContext> _opcoes;

    public ConfiguracoesFaturamentoNaTelaTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        _opcoes = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        using var db = new ClinicaDbContext(_opcoes);
        db.Database.EnsureCreated();
    }

    public void Dispose() => _conn.Dispose();

    /// <summary>Um escopo por operação, como a tela faz (contexto NOVO a cada leitura).</summary>
    private ParametrosService Parametros(out ClinicaDbContext db)
    {
        db = new ClinicaDbContext(_opcoes);
        return new ParametrosService(new ClinicaRepositorio(db));
    }

    [Fact]
    public async Task Salvar_os_TUSS_preserva_o_Pix_gravado_por_outra_tela_depois_da_abertura()
    {
        // A tela abre e lê o prestador (sem Pix).
        var abertura = Parametros(out var db1);
        var lidoNaAbertura = await abertura.ObterPrestadorAsync();
        db1.Dispose();

        // Outra tela grava o Pix e o nome fantasia no meio.
        var outra = Parametros(out var db2);
        var prestador = await outra.ObterPrestadorAsync();
        prestador.ChavePix = "pix-ficticio";
        prestador.NomeFantasia = "Clínica demonstrativa";
        await outra.SalvarPrestadorAsync(prestador);
        db2.Dispose();

        // Esta tela salva só o TUSS que a pessoa editou.
        var tela = Parametros(out var db3);
        await tela.SalvarCodigosTussAsync(new Dictionary<TipoCodigo, string?> { [TipoCodigo.Acupuntura] = " 87654321 " });
        db3.Dispose();

        var final = await Parametros(out var db4).ObterPrestadorAsync();
        db4.Dispose();
        final.CodigoTuss(TipoCodigo.Acupuntura).Should().Be("87654321");
        final.ChavePix.Should().Be("pix-ficticio");
        final.NomeFantasia.Should().Be("Clínica demonstrativa");
        lidoNaAbertura.ChavePix.Should().BeNull("a tela leu antes da outra gravar");
    }

    [Fact]
    public async Task Salvar_os_dados_da_clinica_preserva_os_TUSS_gravados_por_outra_tela()
    {
        var outra = Parametros(out var db1);
        var prestador = await outra.ObterPrestadorAsync();
        prestador.CodigosTuss[TipoCodigo.Bsv] = "12345678";
        await outra.SalvarPrestadorAsync(prestador);
        db1.Dispose();

        var tela = Parametros(out var db2);
        await tela.SalvarDadosDaClinicaAsync(new DadosDaClinica(
            " Razão Ltda ", "Fantasia", null, "", null, null, null, "pix", "Macaé", null, null));
        db2.Dispose();

        var final = await Parametros(out var db3).ObterPrestadorAsync();
        db3.Dispose();
        final.CodigoTuss(TipoCodigo.Bsv).Should().Be("12345678");
        final.RazaoSocial.Should().Be("Razão Ltda");
        final.Cnes.Should().BeNull("em branco vira nulo, como no WPF");
        final.ChavePix.Should().Be("pix");
    }

    [Fact]
    public void MesclarTuss_troca_so_as_chaves_informadas()
    {
        var atual = new DadosPrestador { CodigosTuss = new() { [TipoCodigo.Consulta] = "111", [TipoCodigo.Bsv] = "222" } };
        var r = ConfiguracoesFaturamentoNaTela.MesclarTuss(atual, new Dictionary<TipoCodigo, string?> { [TipoCodigo.Consulta] = null });
        r.CodigoTuss(TipoCodigo.Consulta).Should().BeEmpty();
        r.CodigoTuss(TipoCodigo.Bsv).Should().Be("222");
    }

    [Fact]
    public void A_copia_do_convenio_leva_TODOS_os_campos()
    {
        // Lugar 3: o campo que ficar fora da cópia é apagado na primeira edição.
        var origem = new ConvenioCadastro
        {
            Codigo = "CVx", Nome = "Saúde X", Familia = Convenio.Personalizado, Ativo = false,
            FormatoNumeroGuia = FormatoNumeroGuia.Alfanumerico, RegistroAnsOperadora = "123456", GeraGuia = false,
            FazEletro = true, TemSegundoCodigo = true, FormaSegundoCodigo = FormaObtencao.Ligacao,
            SegundoCodigoDependeApp = true, DiasSegundoCodigo = 7, FaturaBsv = false, InverteDatasBsv = true,
            ValidadeConsultaDias = 45, CategoriaComApp = Categoria.Vermelha, CategoriaSemApp = Categoria.Verde
        };
        var copia = ConfiguracoesFaturamentoNaTela.Copia(origem);
        foreach (var prop in typeof(ConvenioCadastro).GetProperties().Where(p => p.CanWrite && p.CanRead))
            prop.GetValue(copia).Should().BeEquivalentTo(prop.GetValue(origem), $"{prop.Name} precisa entrar na cópia");
        copia.Should().NotBeSameAs(origem);
    }

    [Fact]
    public void A_copia_dos_outros_cadastros_leva_todos_os_campos()
    {
        void Conferir<T>(T origem, T copia)
        {
            foreach (var prop in typeof(T).GetProperties().Where(p => p.CanWrite && p.CanRead))
                prop.GetValue(copia).Should().BeEquivalentTo(prop.GetValue(origem), $"{typeof(T).Name}.{prop.Name}");
        }
        var m = new ModalidadeCadastro { Codigo = "MD1", Nome = "Auriculo", Base = ModalidadeAtendimento.BsvApenas, Ativo = false };
        Conferir(m, ConfiguracoesFaturamentoNaTela.Copia(m));
        var e = new EspecialidadeCadastro { Codigo = "ES1", Nome = "Sono", Ativo = false };
        Conferir(e, ConfiguracoesFaturamentoNaTela.Copia(e));
        var p = new ParametroConvenio
        {
            Convenio = Convenio.Amil, Nome = "Amil", Ativo = false, ValidadeConsultaDias = 10, DiasSegundoCodigo = 3,
            CategoriaComApp = Categoria.Amarela, CategoriaSemApp = Categoria.Vermelha
        };
        Conferir(p, ConfiguracoesFaturamentoNaTela.Copia(p));
    }

    [Fact]
    public void So_a_variante_da_clinica_pode_ser_excluida()
    {
        ConfiguracoesFaturamentoNaTela.ConvenioPodeSerExcluido(new ConvenioCadastro { Codigo = "Amil" }).Should().BeFalse();
        ConfiguracoesFaturamentoNaTela.ConvenioPodeSerExcluido(ConvenioCadastro.Particular()).Should().BeFalse();
        ConfiguracoesFaturamentoNaTela.ConvenioPodeSerExcluido(ConvenioCadastro.ADefinir()).Should().BeFalse();
        ConfiguracoesFaturamentoNaTela.ConvenioPodeSerExcluido(new ConvenioCadastro { Codigo = "CV12345678" }).Should().BeTrue();
    }

    [Fact]
    public void Registro_ANS_em_branco_vira_nulo_na_copia()
        => ConfiguracoesFaturamentoNaTela.Copia(new ConvenioCadastro { Codigo = "x", RegistroAnsOperadora = "  " })
            .RegistroAnsOperadora.Should().BeNull();

    [Fact]
    public void Item_novo_nasce_com_nome_unico_e_o_convenio_nasce_personalizado()
    {
        ConfiguracoesFaturamentoNaTela.NomeUnico("Novo convênio", ["novo convênio", "Novo convênio 2"])
            .Should().Be("Novo convênio 3");
        var c = ConfiguracoesFaturamentoNaTela.NovoConvenio(["Novo convênio"]);
        c.Nome.Should().Be("Novo convênio 2");
        c.Familia.Should().Be(Convenio.Personalizado);
        c.Ativo.Should().BeTrue();
        ConfiguracoesFaturamentoNaTela.ConvenioEmbutido(c).Should().BeFalse();
        ConfiguracoesFaturamentoNaTela.NovaModalidade([]).Base.Should().Be(ModalidadeAtendimento.AcupunturaSimples);
        ConfiguracoesFaturamentoNaTela.NovaEspecialidade(["Nova especialidade"]).Nome.Should().Be("Nova especialidade 2");
    }

    [Fact]
    public void Nomes_repetidos_sao_recusados_antes_de_gravar()
    {
        var convenios = new[] { new ConvenioCadastro { Codigo = "a", Nome = "Saúde" }, new ConvenioCadastro { Codigo = "b", Nome = " saúde " } };
        ConfiguracoesFaturamentoNaTela.ConferirAntesDeSalvar(convenios, [], [])
            .Should().Contain("convênios com o mesmo nome");
        ConfiguracoesFaturamentoNaTela.ConferirAntesDeSalvar([], [],
                [new EspecialidadeCadastro { Nome = "Sono" }, new EspecialidadeCadastro { Nome = "SONO" }])
            .Should().Contain("especialidades");
        ConfiguracoesFaturamentoNaTela.ConferirAntesDeSalvar(
                [new ConvenioCadastro { Nome = "A" }], [new ModalidadeCadastro { Nome = "A" }], [])
            .Should().BeNull("o mesmo nome em catálogos diferentes não confunde nada");
    }

    [Theory]
    [InlineData("5", "30", "10", true)]
    [InlineData("0", "1", "1", true)]
    [InlineData("-1", "30", "10", false)]
    [InlineData("5", "0", "10", false)]
    [InlineData("5", "30", "366", false)]
    [InlineData("cinco", "30", "10", false)]
    public void Prazos_tem_as_mesmas_faixas_do_WPF(string janela, string glosa, string rodada, bool aceita)
    {
        var p = ConfiguracoesFaturamentoNaTela.LerPrazos(janela, glosa, rodada, out var erro);
        (p is not null).Should().Be(aceita);
        (erro is null).Should().Be(aceita);
    }

    [Fact]
    public void Rotulos_nunca_sao_o_identificador_do_enum()
    {
        ConfiguracoesFaturamentoNaTela.Forma(FormaObtencao.App).Should().Be("Pelo app (QR Code)");
        ConfiguracoesFaturamentoNaTela.Modalidade(ModalidadeAtendimento.AcupunturaSimples).Should().Be("Acupuntura (apenas)");
        ConfiguracoesFaturamentoNaTela.Formato(FormatoNumeroGuia.SomenteNumeros).Should().Be("Somente números");
        ConfiguracoesFaturamentoNaTela.ResumoGuia(false).Should().Be("Particular (sem guia)");
        ConfiguracoesFaturamentoNaTela.Familia(Convenio.UnimedIntercambio).Should().NotBe("UnimedIntercambio");
        ConfiguracoesFaturamentoNaTela.Familia(Convenio.Personalizado).Should().NotBe("Personalizado");
        ConfiguracoesFaturamentoNaTela.Categoria(Categoria.Verde).Should().NotBeNullOrWhiteSpace();
    }
}
