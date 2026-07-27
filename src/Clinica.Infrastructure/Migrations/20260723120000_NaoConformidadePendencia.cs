using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <summary>
    /// Rodada de pendências ("rodar as pendências"): guarda a justificativa e a data em que uma guia
    /// foi marcada como NÃO CONFORMIDADE — a pendência que, no fechamento de ciclo, não pôde ser baixada
    /// e foi documentada. As colunas ficam na tabela Codigos, ao lado da observação da pendência.
    /// </summary>
    [DbContext(typeof(ClinicaDbContext))]
    [Migration("20260723120000_NaoConformidadePendencia")]
    public partial class NaoConformidadePendencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NaoConformidadeJustificativa",
                table: "Codigos",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NaoConformidadeEm",
                table: "Codigos",
                type: "timestamp without time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NaoConformidadeJustificativa",
                table: "Codigos");

            migrationBuilder.DropColumn(
                name: "NaoConformidadeEm",
                table: "Codigos");
        }
    }
}
