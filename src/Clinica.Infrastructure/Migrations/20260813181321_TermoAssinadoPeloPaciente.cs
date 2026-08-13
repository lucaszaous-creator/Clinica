using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TermoAssinadoPeloPaciente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ModeloOrigemId",
                table: "DocumentosClinicos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotivoRecusaPaciente",
                table: "DocumentosClinicos",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PacienteAssinadoEm",
                table: "DocumentosClinicos",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PacienteAssinaturaHash",
                table: "DocumentosClinicos",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PacienteAssinaturaMeio",
                table: "DocumentosClinicos",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PacienteAssinaturaTestemunha",
                table: "DocumentosClinicos",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PacienteDocumentoConferido",
                table: "DocumentosClinicos",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PacienteRecusouEm",
                table: "DocumentosClinicos",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TracoAssinaturaId",
                table: "DocumentosClinicos",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ExigenciasTermo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Modalidade = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ModalidadeCodigo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    ModeloDocumentoId = table.Column<int>(type: "integer", nullable: false),
                    Ativa = table.Column<bool>(type: "boolean", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CriadoPor = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExigenciasTermo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExigenciasTermo_ModelosDocumento_ModeloDocumentoId",
                        column: x => x.ModeloDocumentoId,
                        principalTable: "ModelosDocumento",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TracosAssinatura",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Conteudo = table.Column<byte[]>(type: "bytea", nullable: false),
                    Largura = table.Column<int>(type: "integer", nullable: false),
                    Altura = table.Column<int>(type: "integer", nullable: false),
                    ColhidoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TracosAssinatura", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentosClinicos_ModeloOrigemId",
                table: "DocumentosClinicos",
                column: "ModeloOrigemId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentosClinicos_TracoAssinaturaId",
                table: "DocumentosClinicos",
                column: "TracoAssinaturaId");

            migrationBuilder.CreateIndex(
                name: "IX_ExigenciasTermo_Modalidade_ModalidadeCodigo",
                table: "ExigenciasTermo",
                columns: new[] { "Modalidade", "ModalidadeCodigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExigenciasTermo_ModeloDocumentoId",
                table: "ExigenciasTermo",
                column: "ModeloDocumentoId");

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentosClinicos_ModelosDocumento_ModeloOrigemId",
                table: "DocumentosClinicos",
                column: "ModeloOrigemId",
                principalTable: "ModelosDocumento",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentosClinicos_TracosAssinatura_TracoAssinaturaId",
                table: "DocumentosClinicos",
                column: "TracoAssinaturaId",
                principalTable: "TracosAssinatura",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DocumentosClinicos_ModelosDocumento_ModeloOrigemId",
                table: "DocumentosClinicos");

            migrationBuilder.DropForeignKey(
                name: "FK_DocumentosClinicos_TracosAssinatura_TracoAssinaturaId",
                table: "DocumentosClinicos");

            migrationBuilder.DropTable(
                name: "ExigenciasTermo");

            migrationBuilder.DropTable(
                name: "TracosAssinatura");

            migrationBuilder.DropIndex(
                name: "IX_DocumentosClinicos_ModeloOrigemId",
                table: "DocumentosClinicos");

            migrationBuilder.DropIndex(
                name: "IX_DocumentosClinicos_TracoAssinaturaId",
                table: "DocumentosClinicos");

            migrationBuilder.DropColumn(
                name: "ModeloOrigemId",
                table: "DocumentosClinicos");

            migrationBuilder.DropColumn(
                name: "MotivoRecusaPaciente",
                table: "DocumentosClinicos");

            migrationBuilder.DropColumn(
                name: "PacienteAssinadoEm",
                table: "DocumentosClinicos");

            migrationBuilder.DropColumn(
                name: "PacienteAssinaturaHash",
                table: "DocumentosClinicos");

            migrationBuilder.DropColumn(
                name: "PacienteAssinaturaMeio",
                table: "DocumentosClinicos");

            migrationBuilder.DropColumn(
                name: "PacienteAssinaturaTestemunha",
                table: "DocumentosClinicos");

            migrationBuilder.DropColumn(
                name: "PacienteDocumentoConferido",
                table: "DocumentosClinicos");

            migrationBuilder.DropColumn(
                name: "PacienteRecusouEm",
                table: "DocumentosClinicos");

            migrationBuilder.DropColumn(
                name: "TracoAssinaturaId",
                table: "DocumentosClinicos");
        }
    }
}
