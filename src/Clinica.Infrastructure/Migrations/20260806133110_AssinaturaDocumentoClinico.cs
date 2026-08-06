using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AssinaturaDocumentoClinico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Endereco",
                table: "Pacientes",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ArquivoAssinadoId",
                table: "DocumentosClinicos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AssinadoEm",
                table: "DocumentosClinicos",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AssinadoPorUsuarioId",
                table: "DocumentosClinicos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssinanteCpf",
                table: "DocumentosClinicos",
                type: "character varying(11)",
                maxLength: 11,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssinanteNome",
                table: "DocumentosClinicos",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssinanteRegistroConselho",
                table: "DocumentosClinicos",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssinaturaAlgoritmo",
                table: "DocumentosClinicos",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssinaturaHash",
                table: "DocumentosClinicos",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssinaturaTipo",
                table: "DocumentosClinicos",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CarimboTempoAutoridade",
                table: "DocumentosClinicos",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CarimboTempoEm",
                table: "DocumentosClinicos",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CertificadoEmissor",
                table: "DocumentosClinicos",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CertificadoSerie",
                table: "DocumentosClinicos",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CertificadoTitular",
                table: "DocumentosClinicos",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CertificadoValidoAte",
                table: "DocumentosClinicos",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CertificadoValidoDe",
                table: "DocumentosClinicos",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentosClinicos_ArquivoAssinadoId",
                table: "DocumentosClinicos",
                column: "ArquivoAssinadoId");

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentosClinicos_ArquivosAssinados_ArquivoAssinadoId",
                table: "DocumentosClinicos",
                column: "ArquivoAssinadoId",
                principalTable: "ArquivosAssinados",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DocumentosClinicos_ArquivosAssinados_ArquivoAssinadoId",
                table: "DocumentosClinicos");

            migrationBuilder.DropIndex(
                name: "IX_DocumentosClinicos_ArquivoAssinadoId",
                table: "DocumentosClinicos");

            migrationBuilder.DropColumn(
                name: "Endereco",
                table: "Pacientes");

            migrationBuilder.DropColumn(
                name: "ArquivoAssinadoId",
                table: "DocumentosClinicos");

            migrationBuilder.DropColumn(
                name: "AssinadoEm",
                table: "DocumentosClinicos");

            migrationBuilder.DropColumn(
                name: "AssinadoPorUsuarioId",
                table: "DocumentosClinicos");

            migrationBuilder.DropColumn(
                name: "AssinanteCpf",
                table: "DocumentosClinicos");

            migrationBuilder.DropColumn(
                name: "AssinanteNome",
                table: "DocumentosClinicos");

            migrationBuilder.DropColumn(
                name: "AssinanteRegistroConselho",
                table: "DocumentosClinicos");

            migrationBuilder.DropColumn(
                name: "AssinaturaAlgoritmo",
                table: "DocumentosClinicos");

            migrationBuilder.DropColumn(
                name: "AssinaturaHash",
                table: "DocumentosClinicos");

            migrationBuilder.DropColumn(
                name: "AssinaturaTipo",
                table: "DocumentosClinicos");

            migrationBuilder.DropColumn(
                name: "CarimboTempoAutoridade",
                table: "DocumentosClinicos");

            migrationBuilder.DropColumn(
                name: "CarimboTempoEm",
                table: "DocumentosClinicos");

            migrationBuilder.DropColumn(
                name: "CertificadoEmissor",
                table: "DocumentosClinicos");

            migrationBuilder.DropColumn(
                name: "CertificadoSerie",
                table: "DocumentosClinicos");

            migrationBuilder.DropColumn(
                name: "CertificadoTitular",
                table: "DocumentosClinicos");

            migrationBuilder.DropColumn(
                name: "CertificadoValidoAte",
                table: "DocumentosClinicos");

            migrationBuilder.DropColumn(
                name: "CertificadoValidoDe",
                table: "DocumentosClinicos");
        }
    }
}
