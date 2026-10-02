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
            migrationBuilder.Sql("""
                DO $protege$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "PrescricoesInternas" WHERE "ModoSemAssinatura" OR "LiberadaSemAssinaturaEm" IS NOT NULL)
                       OR EXISTS (SELECT 1 FROM "ChecagensPrescricao" WHERE "ExecutanteCpf" IS NOT NULL) THEN
                        RAISE EXCEPTION 'Reversão recusada: há registros de continuidade ou identificação de executantes. Reverta apenas o aplicativo.';
                    END IF;
                END $protege$;
                """);
            migrationBuilder.DropColumn(name: "LiberadaSemAssinaturaEm", table: "PrescricoesInternas");
            migrationBuilder.DropColumn(name: "ModoSemAssinatura", table: "PrescricoesInternas");
            migrationBuilder.DropColumn(name: "ExecutanteCpf", table: "ChecagensPrescricao");
        }
    }
}
