using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AuditoriaEConcorrencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Pacientes",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "LotesTiss",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Consultas",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Codigos",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Atendimentos",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Agendamentos",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateTable(
                name: "Auditoria",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DataHora = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Operador = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Acao = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Detalhe = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CodigoId = table.Column<int>(type: "integer", nullable: true),
                    LoteTissId = table.Column<int>(type: "integer", nullable: true),
                    PacienteId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Auditoria", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Auditoria_CodigoId",
                table: "Auditoria",
                column: "CodigoId");

            migrationBuilder.CreateIndex(
                name: "IX_Auditoria_DataHora",
                table: "Auditoria",
                column: "DataHora");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Auditoria");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Pacientes");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "LotesTiss");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Consultas");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Codigos");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Atendimentos");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Agendamentos");
        }
    }
}
