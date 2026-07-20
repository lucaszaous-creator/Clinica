using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using FluentAssertions;

namespace Clinica.Tests;

/// <summary>
/// O validador barra ANTES do envio o que a operadora rejeitaria dias depois:
/// XML quebrado, campos obrigatórios em branco e hash do epílogo adulterado.
/// </summary>
public class TissValidadorTests
{
    private static DadosPrestador PrestadorCompleto() => new()
    {
        RegistroAnsOperadora = "123456",
        CodigoNaOperadora = "PRE-1",
        Cnpj = "12.345.678/0001-90",
        CodigosTuss = new()
        {
            [TipoCodigo.Acupuntura] = "31602029",
            [TipoCodigo.Eletroacupuntura] = "31602030"
        }
    };

    private static List<CodigoFaturamento> GuiasBaixadas(string? carteirinha = "CART-001")
    {
        var paciente = new Paciente { Nome = "Paciente", Convenio = Convenio.UnimedPadrao, Carteirinha = carteirinha };
        var atendimento = new Atendimento { Id = 1, Data = new DateOnly(2026, 7, 10), Paciente = paciente };
        var codigo = new CodigoFaturamento
        {
            Id = 10,
            AtendimentoId = 1,
            Atendimento = atendimento,
            Tipo = TipoCodigo.Acupuntura
        };
        codigo.DarBaixa(new DateOnly(2026, 7, 11), "G-123", "maria", null);
        return new List<CodigoFaturamento> { codigo };
    }

    [Fact]
    public void XmlGeradoPeloSistema_PassaSemProblemas()
    {
        var xml = new TissExportService().GerarLoteXml(GuiasBaixadas(), PrestadorCompleto(), "42");

        TissValidador.Validar(xml).Should().BeEmpty();
    }

    [Fact]
    public void XmlMalformado_EAcusadoSemLancarExcecao()
    {
        TissValidador.Validar("<isso não é xml").Should().ContainSingle(p => p.Contains("malformado"));
    }

    [Fact]
    public void HashAdulterado_EDetectado()
    {
        var xml = new TissExportService().GerarLoteXml(GuiasBaixadas(), PrestadorCompleto(), "42");
        var adulterado = xml.Replace("CART-001", "CART-999"); // conteúdo mudou, hash não

        TissValidador.Validar(adulterado).Should().Contain(p => p.Contains("Hash"));
    }

    [Fact]
    public void CarteirinhaEmBranco_EAcusada()
    {
        var xml = new TissExportService().GerarLoteXml(GuiasBaixadas(carteirinha: null), PrestadorCompleto(), "42");

        TissValidador.Validar(xml).Should().Contain(p => p.Contains("Carteirinha"));
    }

    [Fact]
    public void PrestadorSemDados_TemCamposObrigatoriosAcusados()
    {
        var xml = new TissExportService().GerarLoteXml(GuiasBaixadas(), new DadosPrestador(), "42");

        var problemas = TissValidador.Validar(xml);
        problemas.Should().Contain(p => p.Contains("registro ANS da operadora"));
        problemas.Should().Contain(p => p.Contains("código do prestador"));
        problemas.Should().Contain(p => p.Contains("TUSS"));
    }

    [Fact]
    public void RecursoDeGlosa_TambemPassaNaValidacaoEstrutural()
    {
        var guias = GuiasBaixadas();
        guias[0].RegistrarGlosa(new DateOnly(2026, 7, 20), "carteirinha inválida", "1801");

        var xml = new TissExportService().GerarRecursoGlosaXml(guias, PrestadorCompleto(), "REC-1");

        // Recurso não tem loteGuias; valem cabeçalho e hash.
        TissValidador.Validar(xml).Should().BeEmpty();
    }
}
