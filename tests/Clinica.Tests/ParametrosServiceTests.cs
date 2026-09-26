using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

public class ParametrosServiceTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly ParametrosService _parametros;

    public ParametrosServiceTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _parametros = new ParametrosService(_repo);
    }

    [Fact]
    public async Task ObterAsync_UsaDefaultsQuandoNaoHaOverride()
    {
        var snap = await _parametros.ObterAsync();
        snap.ValidadeConsultaDias(Convenio.Amil).Should().Be(30);
        snap.ValidadeConsultaDias(Convenio.UnimedIntercambio).Should().Be(22);
        snap.DiasSegundoCodigo(Convenio.UnimedIntercambio).Should().Be(1);
    }

    [Fact]
    public async Task Override_MudaSegundoCodigoNoLancamento()
    {
        await _parametros.SalvarAsync(new[]
        {
            new ParametroConvenio { Convenio = Convenio.UnimedIntercambio, ValidadeConsultaDias = 25, DiasSegundoCodigo = 3 }
        });

        var p = new Paciente { Nome = "P", Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();

        var service = new AtendimentoService(_repo, null, _parametros);
        var r = await service.LancarAsync(p.Id, new DateOnly(2026, 7, 10), ModalidadeAtendimento.AcupunturaComEletro);

        var segundo = r.Atendimento.Codigos.Single(c => c.Ordem == OrdemCodigo.Segundo);
        segundo.DataPrevistaFaturamento.Should().Be(new DateOnly(2026, 7, 13)); // +3 dias

        var snap = await _parametros.ObterAsync();
        snap.ValidadeConsultaDias(Convenio.UnimedIntercambio).Should().Be(25);
    }

    /// <summary>
    /// A tela do paciente é OPCIONAL, e "não configurada" tem de ser distinguível de
    /// "configurada com nada": o modo de uma janela só é o normal de quem tem um monitor,
    /// e uma string vazia devolvida como se fosse nome de dispositivo faria o sistema
    /// procurar um monitor chamado "" a cada coleta.
    /// </summary>
    [Fact]
    public async Task Tela_do_paciente_nao_configurada_e_nula()
    {
        (await _parametros.ObterTelaDoPacienteAsync()).Should().BeNull();

        await _parametros.SalvarTelaDoPacienteAsync(@"\\.\DISPLAY2");
        (await _parametros.ObterTelaDoPacienteAsync()).Should().Be(@"\\.\DISPLAY2");

        // Desligar a segunda tela volta ao modo de uma janela só.
        await _parametros.SalvarTelaDoPacienteAsync(null);
        (await _parametros.ObterTelaDoPacienteAsync()).Should().BeNull();
    }

    [Fact]
    public async Task Credenciais_de_integracao_ficam_cifradas_e_sao_lidas_com_a_chave()
    {
        var chave = new ProtecaoSegredoGlobal(Convert.ToBase64String(new byte[32]));
        var servico = new ParametrosService(_repo, chave);
        await servico.SalvarCredenciaisSafeIDAsync("id", "segredo-safeid", "producao");
        await servico.SalvarCredenciaisArmazenamentoAsync("https://objetos.example.com", "regiao",
            "bucket", "chave-s3", "segredo-s3");
        await servico.SalvarCamposEmailAsync(new CamposEmail("smtp.example.com", "587",
            "usuario", "senha-smtp", "remetente@example.com", "Clínica", true));

        foreach (var nome in new[] { ParametrosService.ChaveSafeIDClientSecret,
                     ParametrosService.ChaveArmazenamentoChave,
                     ParametrosService.ChaveArmazenamentoSegredo,
                     ParametrosService.ChaveEmailSmtpSenha })
            (await _repo.ObterConfiguracaoAsync(nome)).Should().StartWith("enc:v1:");

        (await servico.ObterCredenciaisSafeIDAsync()).ClientSecret.Should().Be("segredo-safeid");
        (await servico.ObterCredenciaisArmazenamentoAsync()).Segredo.Should().Be("segredo-s3");
        (await servico.ObterCamposEmailAsync()).Senha.Should().Be("senha-smtp");
    }

    [Fact]
    public async Task Credencial_antiga_e_migrada_na_primeira_leitura()
    {
        await _repo.SalvarConfiguracaoAsync(ParametrosService.ChaveSafeIDClientSecret, "legado");
        await _repo.SalvarAsync();
        var servico = new ParametrosService(_repo,
            new ProtecaoSegredoGlobal(Convert.ToBase64String(new byte[32])));

        (await servico.ObterCredenciaisSafeIDAsync()).ClientSecret.Should().Be("legado");
        (await _repo.ObterConfiguracaoAsync(ParametrosService.ChaveSafeIDClientSecret))
            .Should().StartWith("enc:v1:");
    }

    [Fact]
    public void Credencial_nao_pode_ser_movida_para_outro_campo()
    {
        var protetor = new ProtecaoSegredoGlobal(Convert.ToBase64String(new byte[32]));
        var cifrado = protetor.Proteger("CampoA", "segredo");
        Action lerCampoErrado = () => protetor.Revelar("CampoB", cifrado);
        lerCampoErrado.Should().Throw<InvalidOperationException>();
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
