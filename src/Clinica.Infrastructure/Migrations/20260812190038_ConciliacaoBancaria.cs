using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConciliacaoBancaria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ConciliadoEm",
                table: "Lancamentos",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdBancario",
                table: "Lancamentos",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Lancamentos_IdBancario",
                table: "Lancamentos",
                column: "IdBancario");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Lancamentos_IdBancario",
                table: "Lancamentos");

            migrationBuilder.DropColumn(
                name: "ConciliadoEm",
                table: "Lancamentos");

            migrationBuilder.DropColumn(
                name: "IdBancario",
                table: "Lancamentos");
        }
    }
}
