using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdocaoGradualMateriais : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "BaixadoEm",
                table: "ConferenciaConsumoProcedimento",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MateriaisJson",
                table: "ConferenciaConsumoProcedimento",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "MotivoPendencia",
                table: "ConferenciaConsumoProcedimento",
                type: "character varying(600)",
                maxLength: 600,
                nullable: true);
            migrationBuilder.Sql("UPDATE \"ConferenciaConsumoProcedimento\" SET \"BaixadoEm\" = \"ConferidoEm\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BaixadoEm",
                table: "ConferenciaConsumoProcedimento");

            migrationBuilder.DropColumn(
                name: "MateriaisJson",
                table: "ConferenciaConsumoProcedimento");

            migrationBuilder.DropColumn(
                name: "MotivoPendencia",
                table: "ConferenciaConsumoProcedimento");
        }
    }
}
