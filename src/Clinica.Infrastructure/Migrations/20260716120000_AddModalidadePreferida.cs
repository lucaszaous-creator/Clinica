using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddModalidadePreferida : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ModalidadePreferida",
                table: "Pacientes",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "AcupunturaComEletro");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ModalidadePreferida",
                table: "Pacientes");
        }
    }
}
