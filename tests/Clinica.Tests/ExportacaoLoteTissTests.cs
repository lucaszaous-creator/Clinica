using System.Text;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O que a tela de lotes TISS decide e afirma ao exportar e ao registrar o retorno (subiu do
/// TissViewModel do WPF quando a tela virou Blazor, out/2026).
/// </summary>
public class ExportacaoLoteTissTests
{
    private static CodigoFaturamento Guia(int id, Convenio familia, string? codigo) => new()
    {
        Id = id,
        Tipo = TipoCodigo.ConsultaEspecialidade,
        Atendimento = new Atendimento { Paciente = new Paciente { Nome = $"Paciente {id}", Convenio = familia, ConvenioCodigo = codigo } }
    };

    [Fact]
    public void Agrupa_um_lote_por_operadora_pela_mesma_resolucao_do_servico()
    {
        var grupos = ExportacaoLoteTiss.Agrupar(
        [
            Guia(1, Convenio.Petrobras, "Petrobras"),
            Guia(2, Convenio.Amil, "Amil"),
            Guia(3, Convenio.Petrobras, "Petrobras"),
            // Cadastro anterior ao catálogo: a família é o caminho de baixo — o MESMO grupo.
            Guia(4, Convenio.Amil, null)
        ]);

        grupos.Should().HaveCount(2);
        grupos.Select(g => g.ConvenioCodigo).Should().BeEquivalentTo(["Amil", "Petrobras"]);
        grupos.Single(g => g.ConvenioCodigo == "Petrobras").Guias.Select(g => g.Id).Should().BeEquivalentTo([1, 3]);
        grupos.Single(g => g.ConvenioCodigo == "Amil").Guias.Select(g => g.Id).Should().BeEquivalentTo([2, 4]);
        grupos.Should().BeInAscendingOrder(g => g.Nome, StringComparer.CurrentCulture);
        grupos.Should().OnlyContain(g => g.Nome == CatalogoConvenios.Nome(g.ConvenioCodigo));
    }

    [Fact]
    public void A_pergunta_das_operadoras_lista_cada_uma_com_a_contagem()
    {
        var grupos = ExportacaoLoteTiss.Agrupar([Guia(1, Convenio.Amil, "Amil"), Guia(2, Convenio.Petrobras, "Petrobras"), Guia(3, Convenio.Petrobras, "Petrobras")]);
        var texto = ExportacaoLoteTiss.ConfirmacaoDeOperadoras(grupos);

        texto.Should().StartWith("As guias do período são de 2 operadoras");
        texto.Should().Contain($"{CatalogoConvenios.Nome("Petrobras")}: 2 guia(s)");
        texto.Should().Contain($"{CatalogoConvenios.Nome("Amil")}: 1 guia(s)");
    }

    [Fact]
    public void O_radar_mostra_no_maximo_oito_alertas_e_diz_quantos_ficaram_de_fora()
    {
        var alertas = Enumerable.Range(1, 11).Select(i => $"alerta {i}").ToList();
        var texto = ExportacaoLoteTiss.ConfirmacaoDoRadar(alertas);

        texto.Should().Contain("alerta 8").And.NotContain("alerta 9");
        texto.Should().Contain("e mais 3 alerta(s)");
        texto.Should().EndWith("Exportar mesmo assim?");
        ExportacaoLoteTiss.ConfirmacaoDoRadar(["um só"]).Should().NotContain("e mais");
    }

    [Fact]
    public void Nome_do_arquivo_unico_e_o_de_sempre_e_com_varias_operadoras_leva_o_nome_dela()
    {
        var fim = new DateOnly(2026, 10, 31);
        ExportacaoLoteTiss.NomeDoArquivo(42, fim, null).Should().Be("TISS-Lote-000042-2026-10.xml");
        ExportacaoLoteTiss.NomeDoArquivo(42, fim, "Unimed Costa do Sol/RJ").Should().Be("TISS-Lote-000042-Unimed-Costa-do-Sol-RJ-2026-10.xml");
        ExportacaoLoteTiss.NomeDaSegundaVia(7).Should().Be("TISS-Lote-000007.xml");
    }

    [Fact]
    public void O_XML_vai_para_o_disco_em_UTF8_sem_BOM()
    {
        ExportacaoLoteTiss.Bytes("<a>ção</a>").Should().Equal(Encoding.UTF8.GetBytes("<a>ção</a>")); // sem os três bytes do BOM
    }

    [Fact]
    public void Validacao_sem_pasta_de_schemas_faz_so_a_estrutural()
    {
        ExportacaoLoteTiss.ValidarXml("isto não é xml", null).Should().ContainSingle().Which.Should().StartWith("XML malformado");
        ExportacaoLoteTiss.ValidarXml("isto não é xml", Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())).Should().ContainSingle();
    }

    [Fact]
    public void O_aviso_de_validacao_so_existe_quando_ha_problema_e_corta_em_seis()
    {
        ExportacaoLoteTiss.ProblemasDoLote(3, "Amil", []).Should().BeNull();
        var texto = ExportacaoLoteTiss.ProblemasDoLote(3, "Amil", Enumerable.Range(1, 9).Select(i => $"p{i}").ToList());
        texto.Should().StartWith("Lote nº 3 (Amil):").And.Contain("p6").And.NotContain("p7");
    }

    [Fact]
    public void A_frase_depois_de_gerar_diz_onde_gravou()
    {
        ExportacaoLoteTiss.MensagemDeGerados([], @"C:\x").Should().Contain("outra máquina");
        ExportacaoLoteTiss.MensagemDeGerados(["nº 5 — Amil, 2 guia(s)"], @"C:\lote.xml").Should().Be(@"Lote nº 5 — Amil, 2 guia(s): C:\lote.xml");
        ExportacaoLoteTiss.MensagemDeGerados(["nº 5", "nº 6"], @"C:\tiss").Should().StartWith(@"2 lotes gerados em C:\tiss");
    }

    [Fact]
    public void Ressalvas_da_importacao_so_aparecem_quando_ha_e_sem_sobra_de_linha()
    {
        var limpa = new ResultadoImportacaoRetorno([], [], [], null);
        ExportacaoLoteTiss.RessalvasDaImportacao(limpa).Should().BeNull();

        var fora = new ResultadoImportacaoRetorno([], ["123", "456"], [], null);
        ExportacaoLoteTiss.RessalvasDaImportacao(fora).Should().Contain("NÃO são deste lote: 123, 456").And.NotEndWith("\n");

        var semRetorno = new ResultadoImportacaoRetorno([], [], ["789"], null);
        ExportacaoLoteTiss.RessalvasDaImportacao(semRetorno).Should().StartWith("Guias do lote sem retorno no XML: 789");
    }

    [Fact]
    public void A_frase_do_retorno_diz_se_houve_glosa()
    {
        ExportacaoLoteTiss.MensagemDoRetorno(9, 0).Should().Contain("todas as guias aceitas");
        ExportacaoLoteTiss.MensagemDoRetorno(9, 2).Should().Contain("2 guia(s) glosada(s)").And.Contain("Controle de glosas");
    }

    [Fact]
    public void O_demonstrativo_e_lido_pela_codificacao_que_ele_declara()
    {
        var latin1 = Encoding.Latin1.GetBytes("<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?><m>Glosa: cobrança</m>");
        ExportacaoLoteTiss.LerDemonstrativo(latin1).Should().Contain("cobrança");

        var utf8ComBom = new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes("<m>cobrança</m>")).ToArray();
        ExportacaoLoteTiss.LerDemonstrativo(utf8ComBom).Should().Be("<m>cobrança</m>");

        ExportacaoLoteTiss.LerDemonstrativo(Encoding.UTF8.GetBytes("<m>ação</m>")).Should().Be("<m>ação</m>");
    }

    [Fact]
    public void O_demonstrativo_importado_cruza_com_as_guias_do_lote()
    {
        // O circuito da tela: bytes do arquivo → texto → decisões casadas pelo nº real da guia.
        var guia = new CodigoFaturamento { Id = 10, Tipo = TipoCodigo.ConsultaEspecialidade, NumeroGuiaReal = "555" };
        var xml = "<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?><ans:mensagemTISS xmlns:ans=\"http://www.ans.gov.br/padroes/tiss/schemas\">" +
                  "<ans:guia><ans:numeroGuiaPrestador>555</ans:numeroGuiaPrestador><ans:codigoGlosa>1201</ans:codigoGlosa>" +
                  "<ans:descricaoGlosa>Cobrança recusada</ans:descricaoGlosa></ans:guia></ans:mensagemTISS>";

        var resultado = TissRetornoImport.Ler(ExportacaoLoteTiss.LerDemonstrativo(Encoding.Latin1.GetBytes(xml)), [guia]);

        var decisao = resultado.Decisoes.Should().ContainSingle().Which;
        decisao.CodigoId.Should().Be(10);
        decisao.Glosada.Should().BeTrue();
        decisao.MotivoCodigo.Should().Be("1201");
        decisao.MotivoTexto.Should().Be("Cobrança recusada"); // o acento do ISO-8859-1 sobreviveu
        resultado.GuiasForaDoLote.Should().BeEmpty();
    }
}
