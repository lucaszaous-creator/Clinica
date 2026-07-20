using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using FluentAssertions;

namespace Clinica.Tests;

/// <summary>
/// O importador lê o demonstrativo XML da operadora e pré-preenche as decisões guia a
/// guia — o que era digitação manual vira conferência. Precisa ser tolerante a variações
/// entre operadoras e sinalizar guias fora do lote / sem retorno.
/// </summary>
public class TissRetornoImportTests
{
    private static CodigoFaturamento Guia(int id, string numeroGuiaReal)
    {
        var paciente = new Paciente { Id = id, Nome = $"Paciente {id}", Convenio = Convenio.UnimedPadrao };
        var atendimento = new Atendimento { Id = id, PacienteId = id, Paciente = paciente, Data = new DateOnly(2026, 7, 10) };
        var codigo = new CodigoFaturamento { Id = id, Atendimento = atendimento, Tipo = TipoCodigo.Acupuntura };
        codigo.DarBaixa(new DateOnly(2026, 7, 11), numeroGuiaReal, "sec", null);
        return codigo;
    }

    private static string Demonstrativo(params (string numeroGuia, string? codigoGlosa)[] guias)
    {
        var itens = string.Join("\n", guias.Select(g =>
            $@"<ans:guiaProcessada>
                 <ans:numeroGuiaPrestador>{g.numeroGuia}</ans:numeroGuiaPrestador>
                 {(g.codigoGlosa is null ? "" : $"<ans:codigoGlosa>{g.codigoGlosa}</ans:codigoGlosa><ans:descricaoGlosa>glosado</ans:descricaoGlosa>")}
               </ans:guiaProcessada>"));

        return $@"<?xml version=""1.0"" encoding=""UTF-8""?>
            <ans:mensagemTISS xmlns:ans=""http://www.ans.gov.br/padroes/tiss/schemas"">
              <ans:cabecalho><ans:dataRegistroTransacao>2026-07-25</ans:dataRegistroTransacao></ans:cabecalho>
              <ans:demonstrativoAnaliseConta>
                {itens}
              </ans:demonstrativoAnaliseConta>
            </ans:mensagemTISS>";
    }

    [Fact]
    public void GuiaGlosadaNoXml_VemComoGlosadaEComMotivo()
    {
        var lote = new[] { Guia(1, "G-1"), Guia(2, "G-2") };
        var xml = Demonstrativo(("G-1", "2001"), ("G-2", null));

        var r = TissRetornoImport.Ler(xml, lote);

        r.Decisoes.Should().HaveCount(2);
        r.Decisoes.Single(d => d.CodigoId == 1).Glosada.Should().BeTrue();
        r.Decisoes.Single(d => d.CodigoId == 1).MotivoCodigo.Should().Be("2001");
        r.Decisoes.Single(d => d.CodigoId == 2).Glosada.Should().BeFalse();
    }

    [Fact]
    public void DataDoDemonstrativo_ELida()
    {
        var r = TissRetornoImport.Ler(Demonstrativo(("G-1", null)), new[] { Guia(1, "G-1") });
        r.DataDemonstrativo.Should().Be(new DateOnly(2026, 7, 25));
    }

    [Fact]
    public void GuiaNoXmlQueNaoEstaNoLote_EReportada()
    {
        var r = TissRetornoImport.Ler(Demonstrativo(("G-99", "2001")), new[] { Guia(1, "G-1") });

        r.GuiasForaDoLote.Should().Contain("G-99");
        r.Decisoes.Should().BeEmpty();
    }

    [Fact]
    public void GuiaDoLoteAusenteNoXml_EReportadaComoSemRetorno()
    {
        var lote = new[] { Guia(1, "G-1"), Guia(2, "G-2") };
        var r = TissRetornoImport.Ler(Demonstrativo(("G-1", "2001")), lote);

        r.GuiasSemRetorno.Should().Contain("G-2");
    }

    [Fact]
    public void CodigoForaDoCatalogo_ViraOutrosComCodigoNoTexto()
    {
        var r = TissRetornoImport.Ler(Demonstrativo(("G-1", "7777")), new[] { Guia(1, "G-1") });

        var decisao = r.Decisoes.Single();
        decisao.MotivoCodigo.Should().Be(MotivosGlosaCodigoOutros);
        decisao.MotivoTexto.Should().Contain("7777");
    }

    [Fact]
    public void ArquivoInvalido_LancaMensagemAmigavel()
    {
        var acao = () => TissRetornoImport.Ler("não é xml", new[] { Guia(1, "G-1") });
        acao.Should().Throw<InvalidOperationException>().WithMessage("*não é um XML*");
    }

    [Fact]
    public void XmlSemMensagemTiss_LancaMensagemAmigavel()
    {
        var acao = () => TissRetornoImport.Ler("<outraCoisa/>", new[] { Guia(1, "G-1") });
        acao.Should().Throw<InvalidOperationException>().WithMessage("*TISS*");
    }

    // Espelha MotivosGlosa.CodigoOutros sem depender do namespace nos asserts acima.
    private const string MotivosGlosaCodigoOutros = "9999";
}
