using System.Net;
using Clinica.Assinaturas.Api;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Clinica.Assinaturas.Tests;

public sealed class OrigemRateLimitTabletTests
{
    [Fact]
    public void Socket_separa_visitantes_e_recusa_origem_ausente_ou_invalida()
    {
        var primeiro=new DefaultHttpContext();
        primeiro.Request.Headers["CF-Connecting-IP"]="198.51.100.10";
        var segundo=new DefaultHttpContext();
        segundo.Request.Headers["CF-Connecting-IP"]="198.51.100.11";
        Assert.Equal("198.51.100.10",OrigemRateLimitTablet.Obter(primeiro,true));
        Assert.Equal("198.51.100.11",OrigemRateLimitTablet.Obter(segundo,true));
        Assert.Null(OrigemRateLimitTablet.Obter(new DefaultHttpContext(),true));
        primeiro.Request.Headers["CF-Connecting-IP"]="valor-invalido";
        Assert.Null(OrigemRateLimitTablet.Obter(primeiro,true));
    }

    [Fact]
    public void Fora_do_socket_nao_confia_no_cabecalho_da_borda()
    {
        var contexto=new DefaultHttpContext();
        contexto.Connection.RemoteIpAddress=IPAddress.Parse("192.0.2.42");
        contexto.Request.Headers["CF-Connecting-IP"]="198.51.100.10";
        Assert.Equal("192.0.2.42",OrigemRateLimitTablet.Obter(contexto,false));
        Assert.Null(OrigemRateLimitTablet.Obter(contexto,true));
    }
}
