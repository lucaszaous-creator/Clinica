using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EstornoDeAtendimento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EstornadoEm",
                table: "Atendimentos",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EstornadoPor",
                table: "Atendimentos",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotivoEstorno",
                table: "Atendimentos",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EstornadoEm",
                table: "Atendimentos");

            migrationBuilder.DropColumn(
                name: "EstornadoPor",
                table: "Atendimentos");

            migrationBuilder.DropColumn(
                name: "MotivoEstorno",
                table: "Atendimentos");
        }
    }
}
