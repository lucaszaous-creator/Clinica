using Clinica.Infrastructure;
using Npgsql;
using Xunit;

namespace Clinica.Tests;

/// <summary>O pool aquecido só preenche o que veio no PADRÃO, e nunca reescreve o que alguém decidiu.</summary>
public class ConexaoRemotaTests
{
    [Fact]
    public void Preenche_minimo_keepalive_e_ociosidade_quando_vieram_no_padrao()
    {
        var b = new NpgsqlConnectionStringBuilder(ConexaoRemota.Ajustar(
            "Host=vps;Database=clinica;Username=app;Password=segredo"));
        Assert.Equal(ConexaoRemota.MinimoNoPool, b.MinPoolSize);
        Assert.Equal(ConexaoRemota.KeepAliveSegundos, b.KeepAlive);
        Assert.Equal(ConexaoRemota.OciosaSegundos, b.ConnectionIdleLifetime);
        Assert.Equal("segredo", b.Password); // a senha atravessa: sem ela o pool não autentica
    }

    [Fact]
    public void Respeita_valores_explicitos_e_pool_desligado()
    {
        var b = new NpgsqlConnectionStringBuilder(ConexaoRemota.Ajustar(
            "Host=vps;Username=app;Minimum Pool Size=5;Keepalive=10;Connection Idle Lifetime=60"));
        Assert.Equal(5, b.MinPoolSize);
        Assert.Equal(10, b.KeepAlive);
        Assert.Equal(60, b.ConnectionIdleLifetime);

        const string semPool = "Host=vps;Username=app;Pooling=false";
        Assert.Equal(semPool, ConexaoRemota.Ajustar(semPool));
    }

    [Fact]
    public void Cadeia_invalida_volta_intacta_para_a_abertura_dizer_o_erro()
    {
        Assert.Equal("isto nao e conexao", ConexaoRemota.Ajustar("isto nao e conexao"));
    }

    [Fact]
    public void Prazo_de_abertura_padrao_dobra_e_o_explicito_e_respeitado()
    {
        var padrao = new NpgsqlConnectionStringBuilder(ConexaoRemota.Ajustar("Host=vps;Username=app"));
        Assert.Equal(ConexaoRemota.PrazoAberturaSegundos, padrao.Timeout);

        var explicito = new NpgsqlConnectionStringBuilder(ConexaoRemota.Ajustar("Host=vps;Username=app;Timeout=8"));
        Assert.Equal(8, explicito.Timeout);
    }

    /// <summary>
    /// O erro exato que a clínica viu em out/2026 (Npgsql envolvendo um TimeoutException na
    /// leitura) e as outras falhas de rede são passageiras: a abertura tenta de novo sozinha.
    /// </summary>
    [Fact]
    public void Falha_de_rede_e_passageira()
    {
        Assert.True(ConexaoRemota.EhFalhaPassageira(
            new NpgsqlException("Exception while reading from stream", new TimeoutException("Timeout during reading attempt"))));
        Assert.True(ConexaoRemota.EhFalhaPassageira(new System.Net.Sockets.SocketException(10060)));
        Assert.True(ConexaoRemota.EhFalhaPassageira(new System.IO.IOException("conexão derrubada")));
        Assert.True(ConexaoRemota.EhFalhaPassageira(
            new InvalidOperationException("envelope", new NpgsqlException("x", new TimeoutException()))));
    }

    /// <summary>
    /// Configuração errada NÃO é passageira: tentar de novo só atrasaria a pergunta que
    /// resolve (informar outra conexão). Senha errada e erro de código caem aqui.
    /// </summary>
    [Fact]
    public void Falha_de_configuracao_nao_e_passageira()
    {
        Assert.False(ConexaoRemota.EhFalhaPassageira(
            new PostgresException("password authentication failed", "FATAL", "FATAL", "28P01")));
        Assert.False(ConexaoRemota.EhFalhaPassageira(
            new PostgresException("database does not exist", "FATAL", "FATAL", "3D000")));
        Assert.False(ConexaoRemota.EhFalhaPassageira(new InvalidOperationException("erro de código")));
        Assert.False(ConexaoRemota.EhFalhaPassageira(new ArgumentException("cadeia inválida")));
    }

    [Fact]
    public void Servidor_subindo_ou_sem_vagas_e_passageiro()
    {
        Assert.True(ConexaoRemota.EhFalhaPassageira(
            new PostgresException("the database system is starting up", "FATAL", "FATAL", "57P03")));
        Assert.True(ConexaoRemota.EhFalhaPassageira(
            new PostgresException("too many connections", "FATAL", "FATAL", "53300")));
    }
}
