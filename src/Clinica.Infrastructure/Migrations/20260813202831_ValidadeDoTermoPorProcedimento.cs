using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ValidadeDoTermoPorProcedimento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ⚠️ `defaultValue: false` é o que queremos AQUI, e foi conferido (a lição da
            // parcela 60): false = "vale a partir da assinatura", que é a decisão da
            // clínica e o comportamento que as linhas existentes devem ter. O gerador põe
            // o default da LINGUAGEM sem saber o que a coluna significa — quando ele
            // coincide com a intenção, é coincidência que se verifica, não que se presume.
            migrationBuilder.AddColumn<bool>(
                name: "SoValeNoDiaDoProcedimento",
                table: "ExigenciasTermo",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SoValeNoDiaDoProcedimento",
                table: "ExigenciasTermo");
        }
    }
}
