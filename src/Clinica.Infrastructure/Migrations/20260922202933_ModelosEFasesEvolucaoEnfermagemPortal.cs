using Microsoft.EntityFrameworkCore.Migrations;

using System;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ModelosEFasesEvolucaoEnfermagemPortal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FaseAtendimento",
                table: "EvolucoesEnfermagem",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
            migrationBuilder.CreateTable(
                name: "ModelosEvolucaoEnfermagem",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NomeChave = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Texto = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Versao = table.Column<Guid>(type: "uuid", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CriadoPor = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    AtualizadoPor = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_ModelosEvolucaoEnfermagem", x => x.Id));
            migrationBuilder.CreateIndex(
                name: "IX_ModelosEvolucaoEnfermagem_NomeChave",
                table: "ModelosEvolucaoEnfermagem",
                column: "NomeChave",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ModelosEvolucaoEnfermagem");
            migrationBuilder.DropColumn(
                name: "FaseAtendimento",
                table: "EvolucoesEnfermagem");
        }
    }
}
