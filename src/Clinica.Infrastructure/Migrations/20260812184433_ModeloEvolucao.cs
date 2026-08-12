using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ModeloEvolucao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ModelosEvolucao",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProfissionalId = table.Column<int>(type: "integer", nullable: true),
                    QueixaPrincipal = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Conduta = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    TextoEvolucao = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Orientacoes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    Ordem = table.Column<int>(type: "integer", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CriadoPor = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelosEvolucao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModelosEvolucao_Profissionais_ProfissionalId",
                        column: x => x.ProfissionalId,
                        principalTable: "Profissionais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ModelosEvolucao_ProfissionalId_Nome",
                table: "ModelosEvolucao",
                columns: new[] { "ProfissionalId", "Nome" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ModelosEvolucao");
        }
    }
}
