using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LotesTissEGlosaRecurso : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "DataLimiteRecurso",
                table: "Codigos",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LoteTissId",
                table: "Codigos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotivoGlosaCodigo",
                table: "Codigos",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LotesTiss",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Numero = table.Column<int>(type: "integer", nullable: false),
                    DataGeracao = table.Column<DateOnly>(type: "date", nullable: false),
                    RegistroAnsOperadora = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DataEnvio = table.Column<DateOnly>(type: "date", nullable: true),
                    ProtocoloOperadora = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    DataRetorno = table.Column<DateOnly>(type: "date", nullable: true),
                    ObservacaoRetorno = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LotesTiss", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Codigos_LoteTissId",
                table: "Codigos",
                column: "LoteTissId");

            migrationBuilder.CreateIndex(
                name: "IX_LotesTiss_Numero",
                table: "LotesTiss",
                column: "Numero",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Codigos_LotesTiss_LoteTissId",
                table: "Codigos",
                column: "LoteTissId",
                principalTable: "LotesTiss",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Codigos_LotesTiss_LoteTissId",
                table: "Codigos");

            migrationBuilder.DropTable(
                name: "LotesTiss");

            migrationBuilder.DropIndex(
                name: "IX_Codigos_LoteTissId",
                table: "Codigos");

            migrationBuilder.DropColumn(
                name: "DataLimiteRecurso",
                table: "Codigos");

            migrationBuilder.DropColumn(
                name: "LoteTissId",
                table: "Codigos");

            migrationBuilder.DropColumn(
                name: "MotivoGlosaCodigo",
                table: "Codigos");
        }
    }
}
