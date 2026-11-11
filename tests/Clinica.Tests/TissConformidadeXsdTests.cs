using System.Text;
using System.Xml.Linq;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O XML TISS gerado é ACEITO pelo esquema oficial da ANS (TISS 4.01.00, embutido na
/// Application) — nov/2026. Até aqui os testes conferiam só o que o próprio gerador escrevia
/// ("tem guiaSP-SADT, tem hash"), e os três arquivos eram reprovados pelo esquema com a suíte
/// verde: garantia aparente. Esta é a rede que faltava — elemento fora de ordem, campo
/// obrigatório esquecido ou código fora da tabela reprova AQUI.
/// </summary>
public class TissConformidadeXsdTests
{
    private static readonly XNamespace Ans = "http://www.ans.gov.br/padroes/tiss/schemas";

    private static readonly Paciente Maria = new()
    {
        Id = 1, Nome = "Maria da Conceição", Documento = "52998224725",
        Carteirinha = "00641234567890123", Convenio = Convenio.UnimedIntercambio
    };

    private static DadosPrestador Prestador() => new()
    {
        CodigoNaOperadora = "12345", RegistroAnsOperadora = "326305", Cnpj = "12345678000190", Cnes = "1234567",
        RazaoSocial = "Clínica SemDor Ltda.",
        CodigosTuss =
        {
            [TipoCodigo.Acupuntura] = "31601014", [TipoCodigo.Eletroacupuntura] = "31602185",
            [TipoCodigo.Consulta] = "10101012", [TipoCodigo.Preventivo] = "40601137"
        }
    };

    private static CodigoFaturamento Guia(int id, int atendimento, TipoCodigo tipo) => new()
    {
        Id = id, AtendimentoId = atendimento,
        Atendimento = new Atendimento { Id = atendimento, Data = new DateOnly(2026, 7, 10), Paciente = Maria },
        Tipo = tipo, Ordem = OrdemCodigo.Primeiro, NumeroGuiaReal = "G" + id, DataBaixa = new DateOnly(2026, 7, 11)
    };

    private static ContextoTiss Contexto(params CodigoFaturamento[] guias)
    {
        var contexto = new ContextoTiss();
        foreach (var g in guias)
        {
            contexto.Valores[g.Id] = 85.5m;
            contexto.Profissionais[g.AtendimentoId] = new ProfissionalTiss("Dra. Ana Souza", "06", "521234", "33", "225105");
        }
        return contexto;
    }

    [Fact]
    public void Lote_de_SP_SADT_e_aceito_pelo_esquema_da_ANS()
    {
        // Acupuntura + eletro do mesmo atendimento = UMA guia com dois procedimentos.
        var guias = new[] { Guia(1, 10, TipoCodigo.Acupuntura), Guia(2, 10, TipoCodigo.Eletroacupuntura), Guia(3, 11, TipoCodigo.Preventivo) };

        var xml = new TissExportService().GerarLoteXml(guias, Prestador(), "7", Contexto(guias));

        ExportacaoLoteTiss.ValidarXml(xml).Should().BeEmpty();
        var doc = XDocument.Parse(xml);
        doc.Descendants(Ans + "guiaSP-SADT").Should().HaveCount(2);
        doc.Descendants(Ans + "valorTotalGeral").First().Value.Should().Be("171.00");
        doc.Descendants(Ans + "tipoAtendimento").Select(e => e.Value).Should().Equal("03", "23");
    }

    [Fact]
    public void Lote_de_consulta_e_aceito_pelo_esquema_da_ANS()
    {
        var guias = new[] { Guia(1, 10, TipoCodigo.Consulta) };

        var xml = new TissExportService().GerarLoteXml(guias, Prestador(), "8", Contexto(guias));

        ExportacaoLoteTiss.ValidarXml(xml).Should().BeEmpty();
        XDocument.Parse(xml).Descendants(Ans + "valorProcedimento").Single().Value.Should().Be("85.50");
    }

    [Fact]
    public void Clinica_sem_codigo_na_operadora_e_sem_CNES_ainda_gera_lote_aceito()
    {
        // A identificação do contratado é uma só: sem código, o CNPJ; e o CNES vira 9999999.
        var prestador = Prestador();
        prestador.CodigoNaOperadora = null;
        prestador.Cnes = null;
        var guias = new[] { Guia(1, 10, TipoCodigo.Acupuntura) };

        var xml = new TissExportService().GerarLoteXml(guias, prestador, "9", Contexto(guias));

        // O cabeçalho do lote continua exigindo o código (a pré-validação da tela avisa): aqui
        // o que se prova é a GUIA.
        var doc = XDocument.Parse(xml);
        doc.Descendants(Ans + "contratadoExecutante").Single().Elements().Single().Name.LocalName.Should().Be("cnpjContratado");
        doc.Descendants(Ans + "CNES").Single().Value.Should().Be(TabelasTiss.CnesNaoInformado);
        TissValidador.ValidarComXsdOficial(xml).Should().OnlyContain(p => p.Contains("codigoPrestadorNaOperadora"));
    }

    [Fact]
    public void Recurso_de_glosa_e_aceito_pelo_esquema_da_ANS()
    {
        var glosada = Guia(1, 10, TipoCodigo.Acupuntura);
        glosada.Glosa = StatusGlosa.Glosada;
        glosada.MotivoGlosaCodigo = "1001";
        glosada.MotivoGlosa = "Carteira válida na data do atendimento.";

        var xml = new TissExportService().GerarRecursoGlosaXml([glosada], Prestador(), RecursoDeGlosaTiss.NumeroDoRecurso(new RecursoDeGlosaTiss.Recurso(new LoteTiss { Numero = 7 }, "Unimed", []), new DateTime(2026, 11, 11, 12, 0, 0)),
            new TissExportService.LoteDoRecurso("7", "Protocolo 2026/000123", "Unimed Costa do Sol"),
            Contexto(glosada), new DateOnly(2026, 11, 11));

        ExportacaoLoteTiss.ValidarXml(xml).Should().BeEmpty();
        var doc = XDocument.Parse(xml);
        doc.Descendants(Ans + "numeroProtocolo").Single().Value.Should().Be("2026000123");
        doc.Descendants(Ans + "valorTotalRecursado").Single().Value.Should().Be("85.50");
    }

    [Fact]
    public void Sem_profissional_nem_preco_o_esquema_reprova_e_diz_o_campo()
    {
        // Sem contexto o gerador não inventa: conselho, número e UF saem em branco, e a
        // validação da exportação aponta — é o aviso que a tela mostra antes de o lote ir.
        var xml = new TissExportService().GerarLoteXml([Guia(1, 10, TipoCodigo.Acupuntura)], Prestador(), "7");

        var problemas = TissValidador.ValidarComXsdOficial(xml);
        problemas.Should().Contain(p => p.Contains("conselhoProfissional"));
        problemas.Should().Contain(p => p.Contains("UF"));
    }

    [Fact]
    public void O_arquivo_vai_em_ISO_8859_1_com_declaracao_e_o_hash_confere_com_acento()
    {
        var guias = new[] { Guia(1, 10, TipoCodigo.Bsv) };
        var prestador = Prestador();
        prestador.CodigosTuss[TipoCodigo.Bsv] = "31602010";

        var xml = new TissExportService().GerarLoteXml(guias, prestador, "7", Contexto(guias));
        var bytes = ExportacaoLoteTiss.Bytes(xml);

        xml.Should().StartWith("<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>");
        // "Bloqueio Simpático Venoso": um byte por letra acentuada — em UTF-8 seriam dois.
        bytes.Length.Should().Be(xml.Length);
        // Quem recebe lê os BYTES: o hash tem de conferir depois da ida e volta pelo disco.
        var lido = ExportacaoLoteTiss.LerDemonstrativo(bytes);
        lido.Should().Contain("Simpático");
        TissValidador.Validar(lido).Should().BeEmpty();
    }

    [Theory]
    [InlineData("CRM-RJ 52.1234", "06", "521234", "33")]
    [InlineData("CRM/SP 123456", "06", "123456", "35")]
    [InlineData("CRM 12345-6 RJ", "06", "123456", "33")]
    [InlineData("CRP 05/12345 RJ", "09", "12345", "33")]
    [InlineData("COREN-RJ 123.456", "02", "123456", "33")]
    [InlineData("crefito-2 12345-F RJ", "05", "12345", "33")]
    public void O_registro_do_conselho_da_Equipe_e_lido_para_os_codigos_do_padrao(string registro, string conselho, string numero, string uf)
        => TabelasTiss.LerConselho(registro).Should().Be(new ConselhoTiss(conselho, numero, uf));

    [Theory]
    [InlineData(null)]
    [InlineData("CRM 123456")]       // sem UF: o padrão exige, e adivinhar o estado seria inventar
    [InlineData("RJ 123456")]        // sem conselho
    [InlineData("Dra. Ana")]
    public void Registro_incompleto_nao_e_adivinhado(string? registro)
        => TabelasTiss.LerConselho(registro).Should().BeNull();

    [Fact]
    public void O_CBO_cadastrado_vence_o_da_especialidade_e_sem_nenhum_nao_ha_resposta()
    {
        TabelasTiss.Cbos("225.151", "Acupuntura", null).Should().Be("225151");
        TabelasTiss.Cbos(null, "Acupuntura", "Geriatria").Should().Be("225105");
        TabelasTiss.Cbos(" ", "ClinicaDaDor", "Geriatria").Should().Be("225180");
        TabelasTiss.Cbos(null, "ClinicaDaDor", null).Should().BeNull();
    }

    [Fact]
    public void Operadora_com_consulta_e_procedimento_vira_dois_lotes_e_cem_guias_e_o_teto()
    {
        // 1 consulta + 101 atendimentos de acupuntura (o 1º com eletro junto): o esquema não
        // mistura os tipos nem passa de cem guias — três lotes, e o atendimento não se parte.
        var guias = new List<CodigoFaturamento> { Guia(1, 1, TipoCodigo.Consulta), Guia(2, 2, TipoCodigo.Eletroacupuntura) };
        guias.AddRange(Enumerable.Range(2, 101).Select(a => Guia(100 + a, a, TipoCodigo.Acupuntura)));

        var grupos = ExportacaoLoteTiss.Agrupar(guias);

        grupos.Select(g => g.Nome).Should().Equal(
            "Unimed Costa do Sol Intercâmbio — consultas",
            "Unimed Costa do Sol Intercâmbio — procedimentos (1 de 2)",
            "Unimed Costa do Sol Intercâmbio — procedimentos (2 de 2)");
        grupos.Select(g => g.Guias.Select(c => c.AtendimentoId).Distinct().Count()).Should().Equal(1, 100, 1);
        grupos[1].Guias.Should().HaveCount(101, "a eletro do atendimento 2 vai na mesma guia da acupuntura dele");
        grupos.SelectMany(g => g.Guias).Should().HaveCount(guias.Count);
    }

    [Fact]
    public void Operadora_de_um_tipo_so_continua_com_o_nome_de_sempre()
        => ExportacaoLoteTiss.Agrupar([Guia(1, 1, TipoCodigo.Acupuntura), Guia(2, 2, TipoCodigo.Acupuntura)])
            .Should().ContainSingle().Which.Nome.Should().Be("Unimed Costa do Sol Intercâmbio");

    [Fact]
    public void O_recurso_sai_por_lote_e_o_que_nao_amarra_fica_de_fora_com_o_motivo()
    {
        CodigoFaturamento Glosa(int id, LoteTiss? lote, string? motivo = "1001")
        {
            var g = Guia(id, id, TipoCodigo.Acupuntura);
            g.Glosa = StatusGlosa.Glosada;
            g.MotivoGlosaCodigo = motivo;
            g.Lote = lote;
            return g;
        }
        var enviado = new LoteTiss { Id = 1, Numero = 7, ProtocoloOperadora = "123456" };
        var semProtocolo = new LoteTiss { Id = 2, Numero = 8 };

        var plano = RecursoDeGlosaTiss.Planejar([Glosa(1, enviado), Glosa(2, enviado), Glosa(3, semProtocolo), Glosa(4, null), Glosa(5, enviado, motivo: null)]);

        plano.Recursos.Should().ContainSingle().Which.Glosas.Select(g => g.Id).Should().Equal(1, 2);
        plano.DeFora.Should().HaveCount(3);
        plano.DeFora.Should().Contain(f => f.Contains("lote nº 8") && f.Contains("protocolo"));
    }
}
