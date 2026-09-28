using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DiluicaoUnicaInfusao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DiluenteGlobal",
                table: "PrescricoesInternas",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DiluicaoUnica",
                table: "PrescricoesInternas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "VolumeTotal",
                table: "PrescricoesInternas",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiluenteGlobal",
                table: "PrescricoesInternas");

            migrationBuilder.DropColumn(
                name: "DiluicaoUnica",
                table: "PrescricoesInternas");

            migrationBuilder.DropColumn(
                name: "VolumeTotal",
                table: "PrescricoesInternas");
        }
    }
}
