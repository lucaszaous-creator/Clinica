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
/// JUNTAR DOIS CONVÊNIOS (nov/2026) — nasceu dos dois "Particular" da clínica (o criado à mão
/// e o garantido pelo catálogo), cada um com parte dos pacientes. Move o que decide o FUTURO
/// (pacientes, preços, autorizações em vigor) e deixa o PASSADO como foi lançado; o convênio
/// que sai fica desativado e renomeado, nunca apagado.
/// </summary>
public class JuntarConveniosTests : IDisposable
{
    private static readonly DateOnly Hoje = new(2026, 11, 8);

    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ConvenioCatalogoService _catalogo;

    public JuntarConveniosTests()
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

    private const string DaClinica = "CVparticularJuncao";

    /// <summary>Os dois "Particular" gravados, como na clínica: o da clínica com 2 pacientes e preço; o garantido com 1.</summary>
    private async Task MontarDoisParticularesAsync()
    {
        _db.Convenios.Add(new ConvenioCadastro { Codigo = DaClinica, Nome = "Particular", Familia = Convenio.Personalizado, Ativo = true, GeraGuia = false });
        _db.Convenios.Add(ConvenioCadastro.Particular());
        _db.Pacientes.AddRange(
            new Paciente { Nome = "Ana", Convenio = Convenio.Personalizado, ConvenioCodigo = DaClinica, Sexo = Sexo.Feminino },
            new Paciente { Nome = "Bia", Convenio = Convenio.Personalizado, ConvenioCodigo = DaClinica, Sexo = Sexo.Feminino },
            new Paciente { Nome = "Caio", Convenio = Convenio.Personalizado, ConvenioCodigo = ConvenioCadastro.CodigoParticular, Sexo = Sexo.Masculino });
        _db.PrecosConvenio.Add(new PrecoConvenio { ConvenioCodigo = DaClinica, Tipo = TipoCodigo.Acupuntura, Valor = 150m });
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task Juntar_o_particular_garantido_no_da_clinica_move_os_pacientes_e_aposenta_o_que_sai()
    {
        await MontarDoisParticularesAsync();
        var caio = await _db.Pacientes.SingleAsync(p => p.Nome == "Caio");
        _db.Autorizacoes.AddRange(
            new AutorizacaoSessoes { PacienteId = caio.Id, ConvenioCodigo = ConvenioCadastro.CodigoParticular, DataEmissao = Hoje.AddDays(-5), DataValidade = Hoje.AddDays(30), QuantidadeAutorizada = 10 },
            new AutorizacaoSessoes { PacienteId = caio.Id, ConvenioCodigo = ConvenioCadastro.CodigoParticular, DataEmissao = Hoje.AddDays(-90), DataValidade = Hoje.AddDays(-1), QuantidadeAutorizada = 10 });
        await _db.SaveChangesAsync();

        (await _catalogo.UsoAsync(ConvenioCadastro.CodigoParticular, Hoje)).Should().Be(new UsoDoConvenio(1, 0, 1));

        var resultado = await _catalogo.JuntarAsync(ConvenioCadastro.CodigoParticular, DaClinica, "ana", Hoje);

        resultado.Pacientes.Should().Be(1);
        resultado.Autorizacoes.Should().Be(1, "a vencida é passado e fica onde estava");
        _db.ChangeTracker.Clear();
        (await _db.Pacientes.CountAsync(p => p.ConvenioCodigo == DaClinica)).Should().Be(3);
        (await _db.Autorizacoes.CountAsync(a => a.ConvenioCodigo == DaClinica)).Should().Be(1);

        var saiu = await _db.Convenios.SingleAsync(c => c.Codigo == ConvenioCadastro.CodigoParticular);
        saiu.Ativo.Should().BeFalse();
        saiu.Nome.Should().StartWith("Particular (juntado em Particular, 08/11/2026)");

        var lista = await _catalogo.ListarAsync();
        ConfiguracoesFaturamentoNaTela.ConferirAntesDeSalvar(lista, [], []).Should().BeNull(
            "o nome repetido que travava o Salvar some com a junção");
        lista.Count(c => c.Ativo && c.Nome == "Particular").Should().Be(1);

        var auditoria = await _db.Auditoria.SingleAsync(a => a.Acao == "ConveniosJuntados");
        auditoria.Operador.Should().Be("ana");
        auditoria.Detalhe.Should().Contain("1 paciente movido");
    }

    [Fact]
    public async Task Precos_passam_so_se_quem_fica_ainda_nao_tem_tabela()
    {
        await MontarDoisParticularesAsync();

        // O garantido não tem preço: os da clínica passam para ele.
        var movendo = await _catalogo.JuntarAsync(DaClinica, ConvenioCadastro.CodigoParticular, "ana", Hoje);
        movendo.PrecosMovidos.Should().Be(1);
        movendo.Pacientes.Should().Be(2);
        _db.ChangeTracker.Clear();
        (await _db.PrecosConvenio.SingleAsync()).ConvenioCodigo.Should().Be(ConvenioCadastro.CodigoParticular);
    }

    [Fact]
    public async Task Quem_fica_com_tabela_propria_mantem_a_dele()
    {
        await MontarDoisParticularesAsync();
        _db.PrecosConvenio.Add(new PrecoConvenio { ConvenioCodigo = ConvenioCadastro.CodigoParticular, Tipo = TipoCodigo.Acupuntura, Valor = 180m });
        await _db.SaveChangesAsync();

        var resultado = await _catalogo.JuntarAsync(DaClinica, ConvenioCadastro.CodigoParticular, "ana", Hoje);

        resultado.PrecosMovidos.Should().Be(0);
        resultado.PrecosMantidos.Should().Be(1);
        resultado.Resumo().Should().Contain("já tem tabela própria");
        _db.ChangeTracker.Clear();
        (await _db.PrecosConvenio.SingleAsync(p => p.Valor == 150m)).ConvenioCodigo.Should().Be(DaClinica);
    }

    [Fact]
    public async Task Destino_garantido_que_so_existe_na_leitura_e_gravado_antes()
    {
        // A clínica com um convênio "Particulares" (outro nome): o garantido aparece só na leitura.
        _db.Convenios.Add(new ConvenioCadastro { Codigo = "CVparts", Nome = "Particulares", Familia = Convenio.Personalizado, Ativo = true, GeraGuia = false });
        _db.Pacientes.Add(new Paciente { Nome = "Duda", Convenio = Convenio.Personalizado, ConvenioCodigo = "CVparts", Sexo = Sexo.Feminino });
        await _db.SaveChangesAsync();

        await _catalogo.JuntarAsync("CVparts", ConvenioCadastro.CodigoParticular, "ana", Hoje);

        _db.ChangeTracker.Clear();
        (await _db.Convenios.AnyAsync(c => c.Codigo == ConvenioCadastro.CodigoParticular)).Should().BeTrue();
        (await _db.Pacientes.SingleAsync()).ConvenioCodigo.Should().Be(ConvenioCadastro.CodigoParticular);
    }

    [Fact]
    public async Task Recusas_dizem_por_que()
    {
        await MontarDoisParticularesAsync();
        _db.Convenios.Add(new ConvenioCadastro { Codigo = "CVdesligado", Nome = "Desligado", Familia = Convenio.Personalizado, Ativo = false });
        _db.Convenios.Add(ConvenioCadastro.ADefinir());
        await _db.SaveChangesAsync();

        async Task Recusa(string origem, string destino, string trecho)
        {
            var juntar = () => _catalogo.JuntarAsync(origem, destino, "ana", Hoje);
            await juntar.Should().ThrowAsync<InvalidOperationException>().WithMessage($"*{trecho}*");
        }

        await Recusa(DaClinica, DaClinica, "OUTRO convênio");
        await Recusa("Amil", DaClinica, "embutido");
        await Recusa(ConvenioCadastro.CodigoADefinir, DaClinica, "A definir");
        await Recusa(DaClinica, ConvenioCadastro.CodigoADefinir, "A definir");
        await Recusa(DaClinica, "CVdesligado", "desativado");

        _db.ChangeTracker.Clear();
        (await _db.Pacientes.CountAsync(p => p.ConvenioCodigo == DaClinica)).Should().Be(2, "recusa não move ninguém");
    }

    [Fact]
    public void A_tela_sugere_o_homonimo_e_nao_oferece_o_a_definir_nem_desativados()
    {
        var origem = new ConvenioCadastro { Codigo = DaClinica, Nome = "Particular" };
        var catalogo = new[]
        {
            origem,
            ConvenioCadastro.Particular(),
            ConvenioCadastro.ADefinir(),
            new ConvenioCadastro { Codigo = "Amil", Nome = "Amil", Familia = Convenio.Amil, Ativo = true },
            new ConvenioCadastro { Codigo = "CVoff", Nome = "Off", Ativo = false }
        };

        ConfiguracoesFaturamentoNaTela.DestinosDaJuncao(origem, catalogo).Select(c => c.Codigo)
            .Should().BeEquivalentTo(["Amil", ConvenioCadastro.CodigoParticular]);
        ConfiguracoesFaturamentoNaTela.DestinoSugerido(origem, catalogo)!.Codigo.Should().Be(ConvenioCadastro.CodigoParticular);
        ConfiguracoesFaturamentoNaTela.ConvenioPodeSerJuntado(catalogo[3]).Should().BeFalse("embutido não sai");
        ConfiguracoesFaturamentoNaTela.ConvenioPodeSerJuntado(catalogo[2]).Should().BeFalse("a definir não se junta");
        ConfiguracoesFaturamentoNaTela.ConvenioPodeSerJuntado(catalogo[1]).Should().BeTrue("o garantido pode sair");
    }
}
