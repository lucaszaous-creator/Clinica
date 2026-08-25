using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// Os anexos vistos do PACIENTE, e não da sessão (parcela 74).
///
/// ⚠️ Esta suíte existe porque o método nasceu SEM UM ÚNICO TESTE, e com um defeito que só
/// aparece quando alguém o executa: a primeira versão filtrava por
/// <c>!a.Evolucao.Cancelada</c>, e <c>Cancelada</c> é propriedade DERIVADA
/// (<c>=&gt; CanceladaEm is not null</c>) — o EF não a traduz e a consulta ESTOURA
/// ("Translation of member 'Cancelada' on entity type 'Evolucao' failed").
///
/// Os 1778 testes ficaram verdes, as três redes locais ficaram verdes e o CI ficou verde. A
/// seção "Exames e anexos" simplesmente derrubaria a tela na primeira abertura.
///
/// A lição: <b>método de repositório sem chamador em teste é código que ninguém executou</b>
/// — e consulta LINQ só se prova executando, porque a tradução acontece em runtime.
/// </summary>
public class AnexosDoPacienteTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;

    public AnexosDoPacienteTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
    }

    private async Task<int> PacienteAsync()
    {
        var p = new Paciente { Nome = "Paciente", Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    private async Task<int> SessaoAsync(int pacienteId, DateOnly data, bool cancelada = false)
    {
        var e = new Evolucao
        {
            PacienteId = pacienteId,
            Data = data,
            CanceladaEm = cancelada ? DateTime.Now : null,
            CanceladaPor = cancelada ? "medica" : null,
            MotivoCancelamento = cancelada ? "paciente errado" : null
        };
        _db.Evolucoes.Add(e);
        await _db.SaveChangesAsync();
        return e.Id;
    }

    private async Task AnexoAsync(int evolucaoId, string nome, bool cancelado = false)
    {
        _db.AnexosProntuario.Add(new AnexoProntuario
        {
            EvolucaoId = evolucaoId,
            NomeArquivo = nome,
            Tipo = TipoAnexo.Documento,
            Conteudo = [1, 2, 3],
            Tamanho = 3,
            CanceladoEm = cancelado ? DateTime.Now : null
        });
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task A_consulta_TRADUZ_para_SQL()
    {
        var p = await PacienteAsync();
        await AnexoAsync(await SessaoAsync(p, new DateOnly(2026, 8, 1)), "laudo.pdf");

        // É a asserção que faltava. Sem executá-la, o defeito de tradução é invisível.
        var lista = await _repo.AnexosDoPacienteAsync(p);

        lista.Should().ContainSingle().Which.NomeArquivo.Should().Be("laudo.pdf");
    }

    [Fact]
    public async Task Traz_a_DATA_da_sessao_e_nao_a_do_upload()
    {
        var p = await PacienteAsync();
        await AnexoAsync(await SessaoAsync(p, new DateOnly(2026, 3, 12)), "resson.pdf");

        var a = (await _repo.AnexosDoPacienteAsync(p)).Single();

        // A data da sessão SITUA o exame no tratamento; a do upload responde outra coisa
        // (quando alguém digitalizou), e as duas divergem em semanas.
        a.DataSessao.Should().Be(new DateOnly(2026, 3, 12));
        a.Contexto.Should().StartWith("Sessão de 12/03/2026");
    }

    [Fact]
    public async Task O_mais_recente_vem_na_frente()
    {
        var p = await PacienteAsync();
        await AnexoAsync(await SessaoAsync(p, new DateOnly(2026, 1, 10)), "antigo.pdf");
        await AnexoAsync(await SessaoAsync(p, new DateOnly(2026, 7, 20)), "novo.pdf");

        // O laudo que acabou de chegar é o que se procura.
        (await _repo.AnexosDoPacienteAsync(p)).Select(a => a.NomeArquivo)
            .Should().Equal("novo.pdf", "antigo.pdf");
    }

    [Fact]
    public async Task Anexo_CANCELADO_fica_de_fora()
    {
        var p = await PacienteAsync();
        var s = await SessaoAsync(p, new DateOnly(2026, 8, 1));
        await AnexoAsync(s, "vale.pdf");
        await AnexoAsync(s, "cancelado.pdf", cancelado: true);

        // A lista responde "o que está valendo agora" — que é a pergunta de quem vai olhar
        // o laudo. O registro continua na base e a exportação o alcança.
        (await _repo.AnexosDoPacienteAsync(p)).Select(a => a.NomeArquivo)
            .Should().Equal("vale.pdf");
    }

    [Fact]
    public async Task Anexo_de_sessao_CANCELADA_fica_de_fora()
    {
        var p = await PacienteAsync();
        await AnexoAsync(await SessaoAsync(p, new DateOnly(2026, 8, 1), cancelada: true), "errado.pdf");

        (await _repo.AnexosDoPacienteAsync(p)).Should().BeEmpty();
    }

    [Fact]
    public async Task Nao_vaza_anexo_de_OUTRO_paciente()
    {
        var p = await PacienteAsync();
        var outro = await PacienteAsync();
        await AnexoAsync(await SessaoAsync(p, new DateOnly(2026, 8, 1)), "meu.pdf");
        await AnexoAsync(await SessaoAsync(outro, new DateOnly(2026, 8, 1)), "do-outro.pdf");

        (await _repo.AnexosDoPacienteAsync(p)).Select(a => a.NomeArquivo).Should().Equal("meu.pdf");
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
