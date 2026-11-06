using System.Text;
using Clinica.Application.Formatacao;

namespace Clinica.Tests;

/// <summary>O CSV das telas (nov/2026): o formato do Excel em pt-BR e a neutralização de fórmula.</summary>
public class CsvParaExcelTests
{
    [Theory]
    [InlineData("=HYPERLINK(\"https://x\";\"clique\")", "\"'=HYPERLINK(\"\"https://x\"\";\"\"clique\"\")\"")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    [InlineData("+55 22 9999", "'+55 22 9999")]
    [InlineData("-cmd|' /C calc'!A0", "'-cmd|' /C calc'!A0")]
    [InlineData("\tmaliciosa", "'\tmaliciosa")]
    public void Texto_que_o_excel_leria_como_formula_ganha_apostrofo(string campo, string esperado)
        => Assert.Equal(esperado, CsvParaExcel.Escapar(campo));

    [Theory]
    [InlineData("-12,50")]
    [InlineData("-R$ 3.714,30")]
    [InlineData("-82,9%")]
    [InlineData("+R$ 2.700,00")]
    [InlineData("Maria da Silva")]
    [InlineData("")]
    public void Numero_com_sinal_e_texto_comum_ficam_como_estao(string campo)
        => Assert.Equal(campo, CsvParaExcel.Escapar(campo));

    [Fact]
    public void Ponto_e_virgula_aspas_e_bom()
    {
        var bytes = CsvParaExcel.Montar(["Nome", "Obs"], [["Ana; Bia", "disse \"oi\""]]);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
        var texto = Encoding.UTF8.GetString(bytes[3..]);
        Assert.Equal("Nome;Obs" + Environment.NewLine + "\"Ana; Bia\";\"disse \"\"oi\"\"\"" + Environment.NewLine, texto);
    }
}
