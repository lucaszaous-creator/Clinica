using System.Reflection;
using Clinica.Application.Servicos;
using Clinica.Assinaturas.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Clinica.Assinaturas.Tests;

/// <summary>
/// Nov/2026: a receita 2026/0484, assinada com SafeID pelo portal, foi recusada na farmácia — o QR pedia um código
/// que nada servia. O portal não registrava a publicação, e o assinador recebia NULL: sem token antes do PDF e sem
/// subir o arquivo. Este teste trava o registro (o parâmetro é opcional, então nada quebrava sem ele).
/// </summary>
public sealed class PublicacaoNoPortalTests
{
    private static WebApplicationFactory<Program> Portal()
    {
        var pasta = Path.Combine(Path.GetTempPath(), "clinica-publicacao-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pasta);
        File.WriteAllText(Path.Combine(pasta, "index.html"), "<!doctype html><title>Fictício</title>");
        return new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development"); b.UseSetting("Portal:Demo", "true");
            b.UseSetting("ConnectionStrings:Clinica", ""); b.UseSetting("Portal:Interface", pasta);
            b.UseSetting("Portal:BancoDemo", Path.Combine(pasta, "demo.db"));
            b.UseSetting("Portal:DiretorioChaves", Path.Combine(pasta, "chaves"));
        });
    }

    [Fact]
    public void O_assinador_do_portal_recebe_a_publicacao_e_a_fila_pendente_roda()
    {
        using var portal = Portal();
        using var escopo = portal.Services.CreateScope();
        var assinador = escopo.ServiceProvider.GetRequiredService<AssinaturaDeDocumentoClinicoService>();
        var publicacao = typeof(AssinaturaDeDocumentoClinicoService)
            .GetField("_publicacao", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(assinador);
        Assert.NotNull(publicacao);
        Assert.Contains(portal.Services.GetServices<IHostedService>(), h => h is PublicacaoPendenteDoPortal);
    }
}
