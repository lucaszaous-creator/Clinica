using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ContinuidadeSemAssinatura : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LiberadaSemAssinaturaEm",
                table: "PrescricoesInternas",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ModoSemAssinatura",
                table: "PrescricoesInternas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ExecutanteCpf",
                table: "ChecagensPrescricao",
                type: "character varying(14)",
                maxLength: 14,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new InvalidOperationException("Esta migration preserva registros clínicos. Reverta apenas o aplicativo, mantendo as colunas e os dados.");
        }
    }
}
