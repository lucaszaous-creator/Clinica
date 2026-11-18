using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EscutaPosSessao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EscutasPosSessao",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PacienteId = table.Column<int>(type: "integer", nullable: false),
                    AtendimentoId = table.Column<int>(type: "integer", nullable: false),
                    SessaoEm = table.Column<DateOnly>(type: "date", nullable: false),
                    Modalidade = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ProfissionalId = table.Column<int>(type: "integer", nullable: true),
                    Motivo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Como = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Relato = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Canal = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RegistradoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    RegistradoPor = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    CanceladaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CanceladaPor = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    MotivoCancelamento = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EscutasPosSessao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EscutasPosSessao_Pacientes_PacienteId",
                        column: x => x.PacienteId,
                        principalTable: "Pacientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EscutasPosSessao_AtendimentoId",
                table: "EscutasPosSessao",
                column: "AtendimentoId",
                unique: true,
                filter: "\"CanceladaEm\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EscutasPosSessao_PacienteId_SessaoEm",
                table: "EscutasPosSessao",
                columns: new[] { "PacienteId", "SessaoEm" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EscutasPosSessao");
        }
    }
}
