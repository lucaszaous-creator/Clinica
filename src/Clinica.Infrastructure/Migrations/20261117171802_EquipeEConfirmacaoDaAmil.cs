using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EquipeEConfirmacaoDaAmil : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AnexosNoPortal",
                table: "GuiasTissConsulta",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EquipeCodigoNaOperadora",
                table: "GuiasTissConsulta",
                type: "character varying(14)",
                maxLength: 14,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EquipeCpf",
                table: "GuiasTissConsulta",
                type: "character varying(11)",
                maxLength: 11,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FormaConfirmacao",
                table: "GuiasTissConsulta",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GrauParticipacao",
                table: "GuiasTissConsulta",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "HoraFinal",
                table: "GuiasTissConsulta",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "HoraInicial",
                table: "GuiasTissConsulta",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CodigosProfissionalOperadora",
                columns: table => new
                {
                    ProfissionalId = table.Column<int>(type: "integer", nullable: false),
                    Operadora = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Codigo = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodigosProfissionalOperadora", x => new { x.ProfissionalId, x.Operadora });
                    table.ForeignKey(
                        name: "FK_CodigosProfissionalOperadora_Profissionais_ProfissionalId",
                        column: x => x.ProfissionalId,
                        principalTable: "Profissionais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CodigosProfissionalOperadora");

            migrationBuilder.DropColumn(
                name: "AnexosNoPortal",
                table: "GuiasTissConsulta");

            migrationBuilder.DropColumn(
                name: "EquipeCodigoNaOperadora",
                table: "GuiasTissConsulta");

            migrationBuilder.DropColumn(
                name: "EquipeCpf",
                table: "GuiasTissConsulta");

            migrationBuilder.DropColumn(
                name: "FormaConfirmacao",
                table: "GuiasTissConsulta");

            migrationBuilder.DropColumn(
                name: "GrauParticipacao",
                table: "GuiasTissConsulta");

            migrationBuilder.DropColumn(
                name: "HoraFinal",
                table: "GuiasTissConsulta");

            migrationBuilder.DropColumn(
                name: "HoraInicial",
                table: "GuiasTissConsulta");
        }
    }
}
