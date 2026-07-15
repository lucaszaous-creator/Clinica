using System.Xml.Linq;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

public class TissExportServiceTests
{
    [Fact]
    public void GerarLoteXml_IncluiCabecalhoLoteEGuiaComTuss()
    {
        var paciente = new Paciente { Id = 1, Nome = "Maria Silva", Documento = "52998224725", Convenio = Convenio.UnimedIntercambio };
        var atendimento = new Atendimento { Id = 10, Data = new DateOnly(2026, 7, 10), Paciente = paciente };
        var codigo = new CodigoFaturamento
        {
            Id = 100, AtendimentoId = 10, Atendimento = atendimento,
            Tipo = TipoCodigo.Acupuntura, Ordem = OrdemCodigo.Primeiro,
            NumeroGuiaReal = "GUIA-123", DataBaixa = new DateOnly(2026, 7, 11)
        };

        var prestador = new DadosPrestador
        {
            CodigoNaOperadora = "PREST-9",
            RegistroAnsOperadora = "326305",
            CodigosTuss = { [TipoCodigo.Acupuntura] = "31602185" }
        };

        var xml = new TissExportService().GerarLoteXml(new[] { codigo }, prestador, "LOTE-1");

        XNamespace ans = "http://www.ans.gov.br/padroes/tiss/schemas";
        var doc = XDocument.Parse(xml);
        doc.Root!.Name.Should().Be(ans + "mensagemTISS");
        doc.Descendants(ans + "numeroLote").Single().Value.Should().Be("LOTE-1");
        doc.Descendants(ans + "nomeBeneficiario").Single().Value.Should().Be("Maria Silva");
        doc.Descendants(ans + "codigoProcedimento").Single().Value.Should().Be("31602185");
        doc.Descendants(ans + "numeroGuiaPrestador").Single().Value.Should().Be("GUIA-123");
    }

    [Fact]
    public void GerarLoteXml_IgnoraGuiasNaoBaixadas()
    {
        var paciente = new Paciente { Id = 1, Nome = "X", Convenio = Convenio.Amil };
        var atendimento = new Atendimento { Id = 5, Data = new DateOnly(2026, 7, 10), Paciente = paciente };
        var aberto = new CodigoFaturamento { Id = 1, AtendimentoId = 5, Atendimento = atendimento, Tipo = TipoCodigo.Acupuntura };

        var xml = new TissExportService().GerarLoteXml(new[] { aberto }, new DadosPrestador(), "L1");

        XNamespace ans = "http://www.ans.gov.br/padroes/tiss/schemas";
        XDocument.Parse(xml).Descendants(ans + "guiaConsulta").Should().BeEmpty();
    }
}
