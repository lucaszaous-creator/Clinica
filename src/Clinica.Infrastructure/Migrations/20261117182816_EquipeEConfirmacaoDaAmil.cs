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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnexosNoPortal",
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
