using Microsoft.EntityFrameworkCore;
using Clinica.Assinaturas.Api;
using Npgsql;
using Xunit;

namespace Clinica.Assinaturas.Tests;

public sealed class FalhaPersistenciaTabletTests
{
    [Theory]
    [InlineData("42501",503)]
    [InlineData("23502",503)]
    [InlineData("40001",409)]
    [InlineData("23505",409)]
    public void Falha_do_banco_nao_inventa_edicao_de_outro_usuario(string codigo,int status)
    {
        var e=new DbUpdateException("Mensagem privada",new PostgresException("Detalhe privado","ERROR","ERROR",codigo));
        Assert.Equal(status,FalhaPersistenciaTablet.Status(e));
        Assert.DoesNotContain("privad",FalhaPersistenciaTablet.Mensagem(e));
        Assert.DoesNotContain("outro acesso",FalhaPersistenciaTablet.Mensagem(e));
    }
}
