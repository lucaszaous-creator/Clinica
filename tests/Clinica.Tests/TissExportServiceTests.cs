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
    private static readonly XNamespace Ans = "http://www.ans.gov.br/padroes/tiss/schemas";

    private static (CodigoFaturamento Codigo, DadosPrestador Prestador) CriarGuiaBaixada()
    {
        var paciente = new Paciente
        {
            Id = 1, Nome = "Maria Silva", Documento = "52998224725",
            Carteirinha = "0064.1234.5678", Convenio = Convenio.UnimedIntercambio
        };
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
            Cnpj = "12345678000190",
            CodigosTuss = { [TipoCodigo.Acupuntura] = "31602185" }
        };
        return (codigo, prestador);
    }

    [Fact]
    public void GerarLoteXml_Padrao401_ComGuiaSpSadtEEpilogo()
    {
        var (codigo, prestador) = CriarGuiaBaixada();

        var xml = new TissExportService().GerarLoteXml(new[] { codigo }, prestador, "7");
        var doc = XDocument.Parse(xml);

        doc.Root!.Name.Should().Be(Ans + "mensagemTISS");
        doc.Descendants(Ans + "Padrao").Single().Value.Should().Be("4.01.00");
        doc.Descendants(Ans + "numeroLote").Single().Value.Should().Be("7");

        // Acupuntura é procedimento → guia SP/SADT (não guia de consulta).
        doc.Descendants(Ans + "guiaSP-SADT").Should().HaveCount(1);
        doc.Descendants(Ans + "guiaConsulta").Should().BeEmpty();
        doc.Descendants(Ans + "codigoProcedimento").Single().Value.Should().Be("31602185");
        doc.Descendants(Ans + "numeroGuiaPrestador").Single().Value.Should().Be("GUIA-123");

        // TISS 4: beneficiário identificado pela carteirinha (sem nome).
        doc.Descendants(Ans + "numeroCarteira").Single().Value.Should().Be("0064.1234.5678");
        doc.Descendants(Ans + "nomeBeneficiario").Should().BeEmpty();

        // Epílogo com hash presente (MD5 = 32 hex).
        doc.Descendants(Ans + "hash").Single().Value.Should().MatchRegex("^[0-9a-f]{32}$");
    }

    [Fact]
    public void GerarLoteXml_ConsultaPura_ViraGuiaConsulta()
    {
        var (codigo, prestador) = CriarGuiaBaixada();
        codigo.Tipo = TipoCodigo.Consulta;
        prestador.CodigosTuss[TipoCodigo.Consulta] = "10101012";

        var xml = new TissExportService().GerarLoteXml(new[] { codigo }, prestador, "8");
        var doc = XDocument.Parse(xml);

        doc.Descendants(Ans + "guiaConsulta").Should().HaveCount(1);
        doc.Descendants(Ans + "guiaSP-SADT").Should().BeEmpty();
        doc.Descendants(Ans + "codigoProcedimento").Single().Value.Should().Be("10101012");
    }

    [Fact]
    public void GerarLoteXml_IgnoraGuiasNaoBaixadas()
    {
        var paciente = new Paciente { Id = 1, Nome = "X", Convenio = Convenio.Amil };
        var atendimento = new Atendimento { Id = 5, Data = new DateOnly(2026, 7, 10), Paciente = paciente };
        var aberto = new CodigoFaturamento { Id = 1, AtendimentoId = 5, Atendimento = atendimento, Tipo = TipoCodigo.Acupuntura };

        var xml = new TissExportService().GerarLoteXml(new[] { aberto }, new DadosPrestador(), "1");
        var doc = XDocument.Parse(xml);

        doc.Descendants(Ans + "guiaSP-SADT").Should().BeEmpty();
        doc.Descendants(Ans + "guiaConsulta").Should().BeEmpty();
    }

    [Fact]
    public void GerarRecursoGlosaXml_IncluiSoGlosasEmAberto_ComMotivoAns()
    {
        var (codigo, prestador) = CriarGuiaBaixada();
        codigo.RegistrarGlosa(new DateOnly(2026, 7, 20), "quantidade divergente", "2006", 30);

        var recuperada = new CodigoFaturamento
        {
            Id = 101, AtendimentoId = 10, Atendimento = codigo.Atendimento,
            Tipo = TipoCodigo.Acupuntura, NumeroGuiaReal = "GUIA-999",
            DataBaixa = new DateOnly(2026, 7, 11)
        };
        recuperada.RegistrarGlosa(new DateOnly(2026, 7, 15), "x", "1201", 30);
        recuperada.MarcarGlosaRecuperada();

        var xml = new TissExportService().GerarRecursoGlosaXml(
            new[] { codigo, recuperada }, prestador, "202607200001");
        var doc = XDocument.Parse(xml);

        doc.Descendants(Ans + "identificacaoTransacao")
            .Single().Element(Ans + "tipoTransacao")!.Value.Should().Be("RECURSO_GLOSA");
        doc.Descendants(Ans + "guiaRecurso").Should().HaveCount(1); // só a glosa em aberto
        doc.Descendants(Ans + "numeroGuiaOrigem").Single().Value.Should().Be("GUIA-123");
        doc.Descendants(Ans + "codigoGlosaGuia").Single().Value.Should().Be("2006");
        doc.Descendants(Ans + "hash").Should().HaveCount(1);
    }
}
