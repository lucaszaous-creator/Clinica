using System.Net;
using Clinica.Application.Servicos;
using Clinica.Crm.Bridge;
using Clinica.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Clinica.Tests;

// As duas VPS (out/2026): a ponte sai do 127.0.0.1 só para o túnel privado até o CRM, e o
// lembrete de SMS roda nela 24h por dia. As recusas valem tanto quanto as aceitações: um
// erro no arquivo de configuração não pode pôr a ponte na internet.
public sealed class PonteRedeERelogioTests
{
    [Fact] public void Sem_configuracao_a_ponte_continua_no_127()
        => Assert.Equal(new Uri("http://127.0.0.1:5190"), RedeDaPonte.Escuta(null));

    [Theory]
    [InlineData("http://10.8.0.1:5190")]
    [InlineData("http://172.16.0.1:5190")]
    [InlineData("http://192.168.10.2:5190")]
    [InlineData("http://100.64.0.1:5190")]
    [InlineData("http://[fd00::1]:5190")]
    public void Escuta_no_ip_do_tunel(string url) => Assert.Equal(new Uri(url), RedeDaPonte.Escuta(url));

    [Theory]
    [InlineData("http://0.0.0.0:5190")]          // todas as interfaces = internet
    [InlineData("http://[::]:5190")]
    [InlineData("http://200.150.10.20:5190")]    // IP público
    [InlineData("http://172.32.0.1:5190")]       // fora da 172.16/12
    [InlineData("http://ponte.clinica:5190")]    // nome: resolve para o que o DNS quiser
    [InlineData("https://10.8.0.1:5190")]        // a ponte não tem certificado; o túnel cifra
    [InlineData("http://10.8.0.1")]              // sem porta explícita
    [InlineData("http://10.8.0.1:5190/crm")]     // com caminho
    public void Recusa_escutar_fora_da_rede_privada(string url)
        => Assert.Throws<InvalidOperationException>(() => RedeDaPonte.Escuta(url));

    [Fact] public void So_o_servidor_e_os_clientes_listados_passam()
    {
        var clientes = RedeDaPonte.Clientes("10.8.0.2");
        Assert.True(RedeDaPonte.Permitido(IPAddress.Loopback, clientes));
        Assert.True(RedeDaPonte.Permitido(IPAddress.Parse("10.8.0.2"), clientes));
        Assert.True(RedeDaPonte.Permitido(IPAddress.Parse("10.8.0.2").MapToIPv6(), clientes)); // Kestrel em dual-stack
        Assert.False(RedeDaPonte.Permitido(IPAddress.Parse("10.8.0.3"), clientes));
        Assert.False(RedeDaPonte.Permitido(IPAddress.Parse("200.150.10.20"), clientes));
        Assert.False(RedeDaPonte.Permitido(null, clientes));
        Assert.False(RedeDaPonte.Permitido(IPAddress.Parse("10.8.0.2"), RedeDaPonte.Clientes(null))); // sem lista, só o servidor
    }

    [Theory]
    [InlineData("200.150.10.20")]
    [InlineData("0.0.0.0")]
    [InlineData("10.8.0.0/24")]   // faixa não: endereço explícito
    [InlineData("crm.clinica")]
    public void Recusa_cliente_publico_ou_vago(string valor)
        => Assert.Throws<InvalidOperationException>(() => RedeDaPonte.Clientes(valor));

    [Fact] public void Lista_de_clientes_tem_teto()
        => Assert.Throws<InvalidOperationException>(() => RedeDaPonte.Clientes(string.Join(';', Enumerable.Range(2, 9).Select(i => $"10.8.0.{i}"))));

    // O relógio que a ponte liga com LembreteSms__Ligado=true resolve o MESMO serviço do
    // desktop, num escopo por batida (nunca o DbContext da raiz pela vida do processo).
    [Fact] public void O_relogio_do_servidor_resolve_o_lembrete_em_escopo_proprio()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddClinica("Host=127.0.0.1;Database=nao_conecta;Username=x;Password=y");
        services.AddHostedService<RelogioDoSmsNoServidor>();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = false });
        Assert.Contains(provider.GetServices<IHostedService>(), s => s is RelogioDoSmsNoServidor);
        using var escopo = provider.CreateScope();
        Assert.NotNull(escopo.ServiceProvider.GetRequiredService<LembreteSmsService>());
        Assert.Equal("LembreteSms:Ligado", RelogioDoSmsNoServidor.Chave);
    }
}
