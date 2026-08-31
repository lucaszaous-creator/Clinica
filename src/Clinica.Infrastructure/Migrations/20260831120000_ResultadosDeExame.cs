using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <summary>
    /// Resultados de exame estruturados (ago/2026): tabela nova, ADITIVA — nada existente
    /// é tocado. Escrita à mão (não há dotnet ef neste ambiente), com os dois cuidados que
    /// já morderam: o carimbo é MAIOR que o de todas as migrations existentes (o EF aplica
    /// na ordem do id), e o principalTable é o nome do DbSet ("Pacientes"), nunca o da
    /// classe — foi um nome de classe aqui que derrubou a abertura dos apps na clínica
    /// (42P01, checagem 41).
    /// </summary>
    public partial class ResultadosDeExame : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ResultadosExame",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PacienteId = table.Column<int>(type: "integer", nullable: false),
                    Data = table.Column<DateOnly>(type: "date", nullable: false),
                    Nome = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Valor = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Unidade = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Referencia = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Laboratorio = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Observacoes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CriadoPor = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    CanceladoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    MotivoCancelamento = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CanceladoPor = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResultadosExame", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResultadosExame_Pacientes_PacienteId",
                        column: x => x.PacienteId,
                        principalTable: "Pacientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ResultadosExame_PacienteId_Data",
                table: "ResultadosExame",
                columns: new[] { "PacienteId", "Data" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ResultadosExame");
        }
    }
}
