using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using FluentAssertions;

namespace Clinica.Tests;

public class TissValidacaoTests
{
    private static DadosPrestador PrestadorCompleto() => new()
    {
        RegistroAnsOperadora = "123456",
        CodigoNaOperadora = "PRE-1",
        Cnpj = "12.345.678/0001-90",
        CodigosTuss = new()
        {
            [TipoCodigo.Acupuntura] = "31602029",
            [TipoCodigo.Consulta] = "10101012"
        }
    };

    [Fact]
    public void PrestadorCompleto_NaoTemPendencias()
    {
        var pendencias = new TissExportService().ValidarPrestador(
            PrestadorCompleto(), new[] { TipoCodigo.Acupuntura, TipoCodigo.Consulta });

        pendencias.Should().BeEmpty();
    }

    [Fact]
    public void CamposObrigatoriosVazios_SaoListados()
    {
        var pendencias = new TissExportService().ValidarPrestador(
            new DadosPrestador(), new[] { TipoCodigo.Acupuntura });

        pendencias.Should().Contain(p => p.Contains("Registro ANS"));
        pendencias.Should().Contain(p => p.Contains("Código do prestador"));
        pendencias.Should().Contain(p => p.Contains("CNPJ"));
        pendencias.Should().Contain(p => p.Contains("Acupuntura"));
    }

    [Fact]
    public void TussFaltante_SoAcusaOsTiposUsadosNoLote()
    {
        var prestador = PrestadorCompleto();
        prestador.CodigosTuss.Remove(TipoCodigo.Consulta);

        var pendencias = new TissExportService().ValidarPrestador(
            prestador, new[] { TipoCodigo.Acupuntura, TipoCodigo.Consulta, TipoCodigo.Consulta });

        pendencias.Should().ContainSingle(p => p.Contains("Consulta"));
        pendencias.Should().NotContain(p => p.Contains("Bsv") || p.Contains("BSV"));
    }
}
