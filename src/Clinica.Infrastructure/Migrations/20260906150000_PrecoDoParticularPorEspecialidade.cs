using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <summary>
    /// A tabela de preço do PARTICULAR por especialidade atendida (set/2026) — ADITIVA:
    /// uma tabela nova, <c>PrecosParticular</c>, sem FK (os códigos de modalidade e
    /// especialidade são do catálogo em memória, como em <c>PrecosConvenio</c>).
    ///
    /// Escrita à mão (sem <c>dotnet ef</c> neste ambiente); o nome da tabela vem do
    /// <c>DbSet</c> (parcela 79).
    /// </summary>
    public partial class PrecoDoParticularPorEspecialidade : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PrecosParticular",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ModalidadeCodigo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    EspecialidadeCodigo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Valor = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    VigenteDe = table.Column<DateOnly>(type: "date", nullable: true),
                    VigenteAte = table.Column<DateOnly>(type: "date", nullable: true),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    Observacoes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CriadoPor = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrecosParticular", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PrecosParticular_Ativo",
                table: "PrecosParticular",
                column: "Ativo");

            migrationBuilder.CreateIndex(
                name: "IX_PrecosParticular_ModalidadeCodigo_EspecialidadeCodigo",
                table: "PrecosParticular",
                columns: new[] { "ModalidadeCodigo", "EspecialidadeCodigo" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PrecosParticular");
        }
    }
}
