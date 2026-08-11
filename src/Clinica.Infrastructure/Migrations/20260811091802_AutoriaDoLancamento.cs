using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AutoriaDoLancamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LancadoEm",
                table: "Atendimentos",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LancadoPor",
                table: "Atendimentos",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CriadoEm",
                table: "Agendamentos",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CriadoPor",
                table: "Agendamentos",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LancadoEm",
                table: "Atendimentos");

            migrationBuilder.DropColumn(
                name: "LancadoPor",
                table: "Atendimentos");

            migrationBuilder.DropColumn(
                name: "CriadoEm",
                table: "Agendamentos");

            migrationBuilder.DropColumn(
                name: "CriadoPor",
                table: "Agendamentos");
        }
    }
}
