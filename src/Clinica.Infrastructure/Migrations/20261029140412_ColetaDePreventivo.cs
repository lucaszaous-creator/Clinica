using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ColetaDePreventivo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LimitePreventivoPorDia",
                table: "Profissionais",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ColetaPreventivo",
                table: "Atendimentos",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ColetaPreventivo",
                table: "Agendamentos",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LimitePreventivoPorDia",
                table: "Profissionais");

            migrationBuilder.DropColumn(
                name: "ColetaPreventivo",
                table: "Atendimentos");

            migrationBuilder.DropColumn(
                name: "ColetaPreventivo",
                table: "Agendamentos");
        }
    }
}
