using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// A credencial do web service TISS por convênio (nov/2026). Um teste para cada "nunca":
/// nunca sem cifra, nunca de volta a quem cadastra, nunca em exceção, nunca na auditoria,
/// nunca no texto do objeto. A senha e o login daqui são fictícios.
/// </summary>
public sealed class CredenciaisTissServiceTests : IDisposable
{
    private const string Login = "LOGINTESTE";
    private const string Senha = "senha-ficticia-de-teste";
    private static readonly string Md5 = TissElegibilidadeWs.SenhaEmMd5(Senha);

    private readonly TissBancoDeTeste _b = new();

    public void Dispose() => _b.Dispose();

    private Task<List<ConfiguracaoGlobal>> LinhasAsync()
        => _b.NovoContexto().Configuracoes.AsNoTracking().Where(c => c.Chave.StartsWith("Tiss")).ToListAsync();

    [Fact]
    public async Task A_senha_e_gravada_cifrada_e_nem_o_md5_dela_aparece_no_banco()
    {
        await _b.Credenciais().SalvarAsync(TissBancoDeTeste.Petrobras, $" {Login} ", Senha, TissBancoDeTeste.Operador);

        var linhas = await LinhasAsync();
        linhas.Single(l => l.Chave == "TissLogin:Petrobras").Valor.Should().Be(Login);
        var senha = linhas.Single(l => l.Chave == "TissSenha:Petrobras").Valor;
        ProtecaoSegredoGlobal.EstaProtegido(senha).Should().BeTrue();
        foreach (var linha in linhas)
            linha.Valor.Should().NotContain(Senha).And.NotContain(Md5);
    }

    [Fact]
    public async Task O_conector_abre_o_md5_que_a_mensagem_precisa()
    {
        await _b.Credenciais().SalvarAsync(TissBancoDeTeste.Petrobras, Login, Senha, TissBancoDeTeste.Operador);

        var aberta = await _b.Credenciais(new ClinicaRepositorio(_b.NovoContexto())).AbrirParaEnvioAsync(TissBancoDeTeste.Petrobras);

        aberta!.Login.Should().Be(Login);
        aberta.SenhaMd5.Should().Be(Md5).And.HaveLength(32);
    }

    [Fact]
    public async Task Quem_cadastra_nao_le_a_senha_de_volta()
    {
        var credenciais = _b.Credenciais();
        (await credenciais.ParaATelaAsync(TissBancoDeTeste.Petrobras)).Should().Be(new CredencialTissNaTela(null, false));
        await credenciais.SalvarAsync(TissBancoDeTeste.Petrobras, Login, Senha, TissBancoDeTeste.Operador);

        var naTela = await credenciais.ParaATelaAsync(TissBancoDeTeste.Petrobras);

        naTela.Login.Should().Be(Login);
        naTela.TemSenha.Should().BeTrue();
        // O que a tela recebe não tem onde carregar a senha: só o login e um sim/não.
        typeof(CredencialTissNaTela).GetProperties().Select(p => p.Name).Should().BeEquivalentTo("Login", "TemSenha");
        naTela.ToString().Should().NotContain(Senha).And.NotContain(Md5);
    }

    [Fact]
    public void O_objeto_da_credencial_nao_se_imprime()
    {
        var credencial = new CredencialTiss(Login, Md5);

        credencial.ToString().Should().NotContain(Md5).And.NotContain(Login);
        $"{credencial}".Should().Be("[credencial TISS: conteúdo oculto]");
        // Record imprimiria as propriedades: tem de continuar sendo classe comum.
        typeof(CredencialTiss).GetMethod("<Clone>$").Should().BeNull();
    }

    [Fact]
    public async Task A_auditoria_registra_a_troca_e_quem_trocou_nunca_o_conteudo()
    {
        await _b.Credenciais().SalvarAsync(TissBancoDeTeste.Petrobras, Login, Senha, TissBancoDeTeste.Operador);

        var trilha = await _b.NovoContexto().Auditoria.AsNoTracking().SingleAsync(a => a.Acao == "CredencialTissAlterada");
        trilha.Operador.Should().Be(TissBancoDeTeste.Operador);
        trilha.Detalhe.Should().Contain("Petrobras").And.NotContain(Senha).And.NotContain(Md5).And.NotContain(Login);
    }

    [Fact]
    public async Task Sem_a_chave_de_cifra_o_cadastro_e_recusado_e_nada_e_gravado_em_claro()
    {
        if (Environment.GetEnvironmentVariable(ProtecaoSegredoGlobal.VariavelChave) is { Length: > 0 })
            return; // esta máquina TEM a chave no ambiente: o caso não se aplica aqui
        var semChave = new CredenciaisTissService(_b.Repo);

        var salvar = () => semChave.SalvarAsync(TissBancoDeTeste.Petrobras, Login, Senha, TissBancoDeTeste.Operador);

        var recusa = await salvar.Should().ThrowAsync<InvalidOperationException>();
        recusa.Which.Message.Should().Contain(ProtecaoSegredoGlobal.VariavelChave).And.NotContain(Senha).And.NotContain(Md5);
        (await LinhasAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Chave_de_cifra_errada_nao_abre_e_a_excecao_nao_carrega_segredo()
    {
        await _b.Credenciais().SalvarAsync(TissBancoDeTeste.Petrobras, Login, Senha, TissBancoDeTeste.Operador);
        var outraChave = new CredenciaisTissService(new ClinicaRepositorio(_b.NovoContexto()),
            new ProtecaoSegredoGlobal(Convert.ToBase64String(new byte[32])));

        var abrir = () => outraChave.AbrirParaEnvioAsync(TissBancoDeTeste.Petrobras);

        var recusa = await abrir.Should().ThrowAsync<InvalidOperationException>();
        recusa.Which.Message.Should().NotContain(Md5).And.NotContain(Senha).And.NotContain("enc:v1:");
        recusa.Which.ToString().Should().NotContain(Md5).And.NotContain(Senha);
    }

    [Fact]
    public async Task Senha_gravada_por_fora_sem_cifra_nao_e_usada_nem_repetida_na_recusa()
    {
        await _b.Repo.SalvarConfiguracaoAsync("TissLogin:Petrobras", Login);
        await _b.Repo.SalvarConfiguracaoAsync("TissSenha:Petrobras", Md5);
        await _b.Repo.SalvarAsync();

        var abrir = () => _b.Credenciais().AbrirParaEnvioAsync(TissBancoDeTeste.Petrobras);

        (await abrir.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().Contain("sem proteção").And.NotContain(Md5);
    }

    [Fact]
    public async Task Girar_a_credencial_troca_login_e_senha_e_remover_deixa_o_convenio_sem_credencial()
    {
        var credenciais = _b.Credenciais();
        await credenciais.SalvarAsync(TissBancoDeTeste.Petrobras, Login, Senha, TissBancoDeTeste.Operador);

        await credenciais.SalvarAsync(TissBancoDeTeste.Petrobras, "OUTROLOGIN", "outra-senha-ficticia", TissBancoDeTeste.Operador);
        var girada = await credenciais.AbrirParaEnvioAsync(TissBancoDeTeste.Petrobras);
        girada!.Login.Should().Be("OUTROLOGIN");
        girada.SenhaMd5.Should().Be(TissElegibilidadeWs.SenhaEmMd5("outra-senha-ficticia"));

        await credenciais.RemoverAsync(TissBancoDeTeste.Petrobras, TissBancoDeTeste.Operador);
        (await credenciais.AbrirParaEnvioAsync(TissBancoDeTeste.Petrobras)).Should().BeNull();
        (await credenciais.ParaATelaAsync(TissBancoDeTeste.Petrobras)).Should().Be(new CredencialTissNaTela(null, false));
        (await _b.NovoContexto().Auditoria.AsNoTracking().CountAsync(a => a.Acao == "CredencialTissRemovida")).Should().Be(1);
    }

    [Fact]
    public async Task Credencial_de_um_convenio_nao_serve_a_outro()
    {
        await _b.Credenciais().SalvarAsync(TissBancoDeTeste.Petrobras, Login, Senha, TissBancoDeTeste.Operador);

        (await _b.Credenciais().AbrirParaEnvioAsync("Amil")).Should().BeNull();
    }

    [Theory]
    [InlineData("", "senha", "faturista.teste")]
    [InlineData("LOGIN", "", "faturista.teste")]
    [InlineData("LOGIN", "senha", " ")]
    [InlineData("LOGIN-COM-MAIS-DE-VINTE-CARACTERES", "senha", "faturista.teste")]
    public async Task Cadastro_incompleto_ou_sem_operador_e_recusado(string login, string senha, string operador)
    {
        var salvar = () => _b.Credenciais().SalvarAsync(TissBancoDeTeste.Petrobras, login, senha, operador);

        await salvar.Should().ThrowAsync<InvalidOperationException>();
        (await LinhasAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task O_envio_pelo_web_service_nasce_desligado_e_liga_e_desliga_com_trilha()
    {
        var credenciais = _b.Credenciais();
        (await credenciais.EnvioLigadoAsync(TissBancoDeTeste.Petrobras)).Should().BeFalse("desligado por padrão");

        await credenciais.DefinirEnvioAsync(TissBancoDeTeste.Petrobras, true, TissBancoDeTeste.Operador);
        (await credenciais.EnvioLigadoAsync(TissBancoDeTeste.Petrobras)).Should().BeTrue();
        (await credenciais.EnvioLigadoAsync("Amil")).Should().BeFalse("a chave é por convênio");

        await credenciais.DefinirEnvioAsync(TissBancoDeTeste.Petrobras, false, TissBancoDeTeste.Operador);
        (await credenciais.EnvioLigadoAsync(TissBancoDeTeste.Petrobras)).Should().BeFalse();
        var trilha = await _b.NovoContexto().Auditoria.AsNoTracking().Where(a => a.Acao.StartsWith("EnvioTiss")).OrderBy(a => a.Id).ToListAsync();
        trilha.Select(a => a.Acao).Should().Equal("EnvioTissLigado", "EnvioTissDesligado");
        trilha.Should().OnlyContain(a => a.Operador == TissBancoDeTeste.Operador);
    }

    [Fact]
    public async Task O_endereco_do_web_service_so_aceita_https_e_e_por_tipo()
    {
        var credenciais = _b.Credenciais();
        (await credenciais.EnderecoAsync(TissBancoDeTeste.Petrobras, TipoTransacaoTiss.EnvioGuia)).Should().BeNull();

        var semHttps = () => credenciais.DefinirEnderecoAsync(TissBancoDeTeste.Petrobras, TipoTransacaoTiss.EnvioGuia, "http://exemplo.invalid/tiss", TissBancoDeTeste.Operador);
        var comSenha = () => credenciais.DefinirEnderecoAsync(TissBancoDeTeste.Petrobras, TipoTransacaoTiss.EnvioGuia, "https://usuario:senha@exemplo.invalid/tiss", TissBancoDeTeste.Operador);
        await semHttps.Should().ThrowAsync<InvalidOperationException>();
        await comSenha.Should().ThrowAsync<InvalidOperationException>();

        await credenciais.DefinirEnderecoAsync(TissBancoDeTeste.Petrobras, TipoTransacaoTiss.EnvioGuia, "https://exemplo.invalid/tiss/lote", TissBancoDeTeste.Operador);
        (await credenciais.EnderecoAsync(TissBancoDeTeste.Petrobras, TipoTransacaoTiss.EnvioGuia)).Should().Be(new Uri("https://exemplo.invalid/tiss/lote"));
        (await credenciais.EnderecoAsync(TissBancoDeTeste.Petrobras, TipoTransacaoTiss.StatusProtocolo)).Should().BeNull();
    }

    [Fact]
    public void As_chaves_cabem_na_coluna_da_configuracao_mesmo_com_o_maior_codigo_de_convenio()
    {
        var maior = new string('C', 40);
        CredenciaisTissService.ChaveLogin(maior).Length.Should().BeLessOrEqualTo(60);
        CredenciaisTissService.ChaveSenha(maior).Length.Should().BeLessOrEqualTo(60);
        CredenciaisTissService.ChaveEnvio(maior).Length.Should().BeLessOrEqualTo(60);
        foreach (var tipo in Enum.GetValues<TipoTransacaoTiss>())
            CredenciaisTissService.ChaveEndereco(maior, tipo).Length.Should().BeLessOrEqualTo(60);
        var grande = () => CredenciaisTissService.ChaveLogin(new string('C', 41));
        grande.Should().Throw<InvalidOperationException>();
    }
}
