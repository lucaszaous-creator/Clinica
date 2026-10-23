using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RascunhoAutomaticoDaEvolucao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RascunhosEvolucao",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PacienteId = table.Column<int>(type: "integer", nullable: false),
                    ProfissionalId = table.Column<int>(type: "integer", nullable: true),
                    AgendamentoId = table.Column<int>(type: "integer", nullable: true),
                    Dia = table.Column<DateOnly>(type: "date", nullable: false),
                    EvolucaoId = table.Column<int>(type: "integer", nullable: true),
                    Conteudo = table.Column<string>(type: "text", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    AtualizadoPor = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EncerradoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Desfecho = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RascunhosEvolucao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RascunhosEvolucao_Pacientes_PacienteId",
                        column: x => x.PacienteId,
                        principalTable: "Pacientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RascunhosEvolucao_EncerradoEm_AtualizadoEm",
                table: "RascunhosEvolucao",
                columns: new[] { "EncerradoEm", "AtualizadoEm" });

            migrationBuilder.CreateIndex(
                name: "IX_RascunhosEvolucao_PacienteId_AgendamentoId_EncerradoEm",
                table: "RascunhosEvolucao",
                columns: new[] { "PacienteId", "AgendamentoId", "EncerradoEm" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RascunhosEvolucao");
        }
    }
}
