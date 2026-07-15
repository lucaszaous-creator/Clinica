using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

public class CapaConclusaoTests
{
    private static Atendimento ComCodigos(params CodigoFaturamento[] codigos)
    {
        var a = new Atendimento { Id = 1, Data = new DateOnly(2026, 7, 10) };
        a.Codigos.AddRange(codigos);
        return a;
    }

    [Fact]
    public void EstaConcluido_False_QuandoHaCodigoEmAberto()
    {
        var a = ComCodigos(
            new CodigoFaturamento { Ordem = OrdemCodigo.Primeiro, DataBaixa = new DateOnly(2026, 7, 10) },
            new CodigoFaturamento { Ordem = OrdemCodigo.Segundo }); // sem baixa

        CapaFaturamentoService.EstaConcluido(a).Should().BeFalse();
    }

    [Fact]
    public void EstaConcluido_True_QuandoTodosBaixados()
    {
        var a = ComCodigos(
            new CodigoFaturamento { Ordem = OrdemCodigo.Primeiro, DataBaixa = new DateOnly(2026, 7, 10) },
            new CodigoFaturamento { Ordem = OrdemCodigo.Segundo, DataBaixa = new DateOnly(2026, 7, 11) });

        CapaFaturamentoService.EstaConcluido(a).Should().BeTrue();
    }

    [Fact]
    public void EstaConcluido_IgnoraCodigosNaoAplicaveis()
    {
        var a = ComCodigos(
            new CodigoFaturamento { Ordem = OrdemCodigo.Primeiro, DataBaixa = new DateOnly(2026, 7, 10) },
            new CodigoFaturamento { Ordem = OrdemCodigo.Primeiro, Status = StatusCodigo.NaoAplicavel });

        CapaFaturamentoService.EstaConcluido(a).Should().BeTrue();
    }
}
