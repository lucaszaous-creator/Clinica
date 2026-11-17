using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class IntervaloEntreProcedimentosDoConvenio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EletroNoMesmoDia",
                table: "Convenios",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "IntervaloAcupunturaDias",
                table: "Convenios",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "IntervaloEletroDias",
                table: "Convenios",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EletroNoMesmoDia",
                table: "Convenios");

            migrationBuilder.DropColumn(
                name: "IntervaloAcupunturaDias",
                table: "Convenios");

            migrationBuilder.DropColumn(
                name: "IntervaloEletroDias",
                table: "Convenios");
        }
    }
}
