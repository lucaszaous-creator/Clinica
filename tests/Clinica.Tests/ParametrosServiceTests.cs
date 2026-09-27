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
    public async Task Credenciais_sem_habilitacao_explicita_preservam_formato_compativel_com_desktops_atuais()
    {
        var habilitacaoAnterior = Environment.GetEnvironmentVariable(ProtecaoSegredoGlobal.VariavelHabilitacao);
        try
        {
            Environment.SetEnvironmentVariable(ProtecaoSegredoGlobal.VariavelHabilitacao, null);
            await _parametros.SalvarCredenciaisSafeIDAsync("id", "segredo-legado", "producao");

            (await _repo.ObterConfiguracaoAsync(ParametrosService.ChaveSafeIDClientSecret))
                .Should().Be("segredo-legado");
            (await _parametros.ObterCredenciaisSafeIDAsync()).ClientSecret.Should().Be("segredo-legado");
        }
        finally
        {
            Environment.SetEnvironmentVariable(ProtecaoSegredoGlobal.VariavelHabilitacao, habilitacaoAnterior);
        }
    }

    [Fact]
    public async Task Credencial_cifrada_nao_pode_ser_rebaixada_sem_a_chave()
    {
        var habilitacaoAnterior = Environment.GetEnvironmentVariable(ProtecaoSegredoGlobal.VariavelHabilitacao);
        try
        {
            Environment.SetEnvironmentVariable(ProtecaoSegredoGlobal.VariavelHabilitacao, null);
            var protegida = new ProtecaoSegredoGlobal(Convert.ToBase64String(new byte[32]))
                .Proteger(ParametrosService.ChaveSafeIDClientSecret, "segredo-original");
            await _repo.SalvarConfiguracaoAsync(ParametrosService.ChaveSafeIDClientSecret, protegida);
            await _repo.SalvarAsync();

            var gravar = () => _parametros.SalvarCredenciaisSafeIDAsync("id", "novo-segredo", "producao");
            await gravar.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*credencial protegida*");

            (await _repo.ObterConfiguracaoAsync(ParametrosService.ChaveSafeIDClientSecret))
                .Should().Be(protegida);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ProtecaoSegredoGlobal.VariavelHabilitacao, habilitacaoAnterior);
        }
    }

    [Fact]
    public async Task Credencial_cifrada_nao_pode_ser_apagada_sem_a_chave_mesmo_com_flag_ativa()
    {
        var habilitacaoAnterior = Environment.GetEnvironmentVariable(ProtecaoSegredoGlobal.VariavelHabilitacao);
        var chaveAnterior = Environment.GetEnvironmentVariable(ProtecaoSegredoGlobal.VariavelChave);
        try
        {
            Environment.SetEnvironmentVariable(ProtecaoSegredoGlobal.VariavelHabilitacao, null);
            var protegida = new ProtecaoSegredoGlobal(Convert.ToBase64String(new byte[32]))
                .Proteger(ParametrosService.ChaveSafeIDClientSecret, "segredo-original");
            await _repo.SalvarConfiguracaoAsync(ParametrosService.ChaveSafeIDClientSecret, protegida);
            await _repo.SalvarAsync();

            Environment.SetEnvironmentVariable(ProtecaoSegredoGlobal.VariavelHabilitacao, "true");
            Environment.SetEnvironmentVariable(ProtecaoSegredoGlobal.VariavelChave, null);
            Func<Task> limpar = () => _parametros.SalvarCredenciaisSafeIDAsync("id", "", "producao");
            await limpar.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*chave de proteção*");

            (await _repo.ObterConfiguracaoAsync(ParametrosService.ChaveSafeIDClientSecret))
                .Should().Be(protegida);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ProtecaoSegredoGlobal.VariavelHabilitacao, habilitacaoAnterior);
            Environment.SetEnvironmentVariable(ProtecaoSegredoGlobal.VariavelChave, chaveAnterior);
        }
    }

    [Fact]
    public async Task Credencial_cifrada_por_outra_conexao_nao_e_sobrescrita_por_gravacao_obsoleta()
    {
        var habilitacaoAnterior = Environment.GetEnvironmentVariable(ProtecaoSegredoGlobal.VariavelHabilitacao);
        try
        {
            Environment.SetEnvironmentVariable(ProtecaoSegredoGlobal.VariavelHabilitacao, null);
            await _repo.SalvarConfiguracaoAsync(ParametrosService.ChaveSafeIDClientSecret, "legado");
            await _repo.SalvarAsync();

            // A primeira conexão prepara a edição em texto legado, mas ainda não a salva.
            await _repo.SalvarSegredoSemRebaixarProtecaoAsync(
                ParametrosService.ChaveSafeIDClientSecret, "segredo-editado-sem-chave");

            // Outra conexão protege a mesma credencial antes do SaveChanges da primeira.
            using var outroDb = new ClinicaDbContext(
                new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options);
            var outroRepo = new ClinicaRepositorio(outroDb);
            var protegida = new ProtecaoSegredoGlobal(Convert.ToBase64String(new byte[32]))
                .Proteger(ParametrosService.ChaveSafeIDClientSecret, "segredo-protegido");
            await outroRepo.SalvarConfiguracaoAsync(ParametrosService.ChaveSafeIDClientSecret, protegida);
            await outroRepo.SalvarAsync();

            Func<Task> salvar = () => _repo.SalvarAsync();
            var conflito = await salvar.Should().ThrowAsync<InvalidOperationException>();
            conflito.Which.InnerException.Should().BeOfType<DbUpdateConcurrencyException>();
            (await _repo.ObterConfiguracaoAsync(ParametrosService.ChaveSafeIDClientSecret))
                .Should().Be(protegida);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ProtecaoSegredoGlobal.VariavelHabilitacao, habilitacaoAnterior);
        }
    }

    [Fact]
    public async Task Migracao_de_credencial_nao_sobrescreve_valor_alterado_apos_leitura()
    {
        const string chave = ParametrosService.ChaveSafeIDClientSecret;
        await _repo.SalvarConfiguracaoAsync(chave, "credencial-lida");
        await _repo.SalvarAsync();
        var valorLido = await _repo.ObterConfiguracaoAsync(chave);

        using var outroDb = new ClinicaDbContext(
            new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options);
        var outroRepo = new ClinicaRepositorio(outroDb);
        await outroRepo.SalvarConfiguracaoAsync(chave, "credencial-nova");
        await outroRepo.SalvarAsync();

        var cifradaAntiga = new ProtecaoSegredoGlobal(Convert.ToBase64String(new byte[32]))
            .Proteger(chave, valorLido!);
        async Task MigrarAsync()
        {
            await _repo.SalvarConfiguracaoSeValorIgualAsync(chave, valorLido!, cifradaAntiga);
            await _repo.SalvarAsync();
        }
        Func<Task> migrar = MigrarAsync;
        await migrar.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Outro computador alterou*");
        (await _repo.ObterConfiguracaoAsync(chave)).Should().Be("credencial-nova");
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
