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
}
