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
/// A PORTA da cópia de segurança que a tela de Configurações em Blazor alcança (out/2026). Ela
/// não copia nada por conta própria — delega ao BackupService e à PoliticaBackupService —, e o
/// que estes testes fixam é a tradução: o resumo bate com o que foi gravado, o arquivo que não é
/// backup vira RECUSA com frase (e não a exceção crua do leitor de JSON), e a pasta que não existe
/// é dita como tal antes de virar destino da cópia automática.
/// </summary>
public class CopiaDeSegurancaDaClinicaTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly CopiaDeSegurancaDaClinica _porta;

    public CopiaDeSegurancaDaClinicaTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        _db = new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options);
        _db.Database.EnsureCreated();
        var backup = new BackupService(_db);
        _porta = new CopiaDeSegurancaDaClinica(new PoliticaBackupService(backup, new ParametrosService(new ClinicaRepositorio(_db))), backup);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }

    [Fact]
    public async Task Gerar_e_conferir_devolvem_o_mesmo_resumo_com_os_pacientes_contados()
    {
        _db.Pacientes.Add(new Paciente { Nome = "Paciente Fictício", Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino });
        await _db.SaveChangesAsync();

        using var arquivo = new MemoryStream();
        var gerado = await _porta.GerarAsync(arquivo);
        arquivo.Position = 0;
        var conferido = await _porta.ConferirAsync(arquivo);

        gerado.Pacientes.Should().Be(1);
        gerado.Vazio.Should().BeFalse();
        conferido.TotalLinhas.Should().Be(gerado.TotalLinhas);
        conferido.Pacientes.Should().Be(1);
    }

    [Fact]
    public async Task Arquivo_que_nao_e_backup_vira_recusa_com_frase_e_nao_o_erro_do_leitor_de_JSON()
    {
        using var ofx = new MemoryStream("OFXHEADER:100\nDATA:OFXSGML"u8.ToArray());
        var acao = () => _porta.ConferirAsync(ofx);
        (await acao.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("O arquivo escolhido não é um backup deste sistema*");
    }

    [Fact]
    public async Task Situacao_sem_pasta_e_a_copia_automatica_desligada()
    {
        var s = await _porta.SituacaoAsync();
        s.Configurada.Should().BeFalse();
        s.Pasta.Should().BeNull();
        s.Descricao.Should().StartWith("Cópia automática DESLIGADA");
        s.IntervaloDias.Should().Be(ParametrosService.IntervaloBackupPadrao);
    }

    [Fact]
    public void Pasta_que_nao_existe_nao_e_acessivel()
    {
        _porta.PastaAcessivel(Path.Combine(Path.GetTempPath(), "nao-existe-" + Guid.NewGuid().ToString("N"))).Should().BeFalse();
        _porta.PastaAcessivel(Path.GetTempPath()).Should().BeTrue();
        _porta.PastaAcessivel(" ").Should().BeFalse();
    }
}
