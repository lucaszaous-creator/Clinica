using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <summary>
    /// Retencao na fonte por convenio (parcela 18).
    ///
    /// A operadora que paga servico de PJ RETEM IRRF, CSLL/PIS/COFINS e as vezes o ISS
    /// antes de depositar: a guia vale R$ 1.000 e caem R$ 943,50. Ate aqui o sistema
    /// gravava os R$ 1.000 e nao sabia da diferenca — o mesmo defeito que a parcela 9
    /// corrigiu para a maquininha, intocado justamente no convenio, que e onde esta
    /// clinica fatura mais.
    ///
    /// A retencao reaproveita o Tributo em vez de ganhar entidade propria: e o mesmo fato
    /// (percentual sobre o recebimento, com vigencia), so que quem recolhe e a operadora.
    /// ConvenioCodigo preenchido significa RETIDO NA FONTE por aquele convenio.
    ///
    /// Regra que o codigo carrega: a retencao SUBSTITUI os tributos gerais naquele
    /// recebimento, nao se soma a eles. Os dois representam o mesmo imposto, e soma-los
    /// contaria duas vezes — o erro que mais aparece em planilha de clinica de convenio.
    ///
    /// PURAMENTE ADITIVA: uma coluna ANULAVEL e um indice.
    /// </summary>
    public partial class RetencaoPorConvenio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConvenioCodigo",
                table: "Tributos",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tributos_ConvenioCodigo",
                table: "Tributos",
                column: "ConvenioCodigo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tributos_ConvenioCodigo",
                table: "Tributos");

            migrationBuilder.DropColumn(
                name: "ConvenioCodigo",
                table: "Tributos");
        }
    }
}
