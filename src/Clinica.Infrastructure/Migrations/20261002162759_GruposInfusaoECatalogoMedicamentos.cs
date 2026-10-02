using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class GruposInfusaoECatalogoMedicamentos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GrupoInfusao",
                table: "ItensPrescricaoInterna",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MedicamentosCadastro",
                columns: table => new
                {
                    Codigo = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    Nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PrincipioAtivo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Apresentacao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Fabricante = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Fonte = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    AtualizadoPor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicamentosCadastro", x => x.Codigo);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
                migrationBuilder.Sql("""
                    DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "MedicamentosCadastro") OR EXISTS (SELECT 1 FROM "ItensPrescricaoInterna" WHERE "GrupoInfusao" IS NOT NULL) THEN
                        RAISE EXCEPTION 'Não é possível remover agrupamento ou catálogo com dados. Reverta somente a aplicação.';
                    END IF;
                    END $$;
                    """);
            migrationBuilder.DropTable(
                name: "MedicamentosCadastro");

            migrationBuilder.DropColumn(
                name: "GrupoInfusao",
                table: "ItensPrescricaoInterna");
        }
    }
}
