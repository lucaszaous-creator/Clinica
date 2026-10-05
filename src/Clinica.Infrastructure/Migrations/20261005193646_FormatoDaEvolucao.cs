using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FormatoDaEvolucao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TextoEvolucaoFormatado",
                table: "VersoesEvolucao",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TextoEvolucaoFormatado",
                table: "Evolucoes",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TextoEvolucaoFormatado",
                table: "VersoesEvolucao");

            migrationBuilder.DropColumn(
                name: "TextoEvolucaoFormatado",
                table: "Evolucoes");
        }
    }
}
