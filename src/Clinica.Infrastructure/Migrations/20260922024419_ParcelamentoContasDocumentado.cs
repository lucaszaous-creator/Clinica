using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ParcelamentoContasDocumentado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Contraparte",
                table: "Lancamentos",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DocumentoReferencia",
                table: "Lancamentos",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GrupoParcelamento",
                table: "Lancamentos",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NumeroParcelaConta",
                table: "Lancamentos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PedidoParcelamento",
                table: "Lancamentos",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TotalParcelasConta",
                table: "Lancamentos",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Lancamentos_GrupoParcelamento_NumeroParcelaConta",
                table: "Lancamentos",
                columns: new[] { "GrupoParcelamento", "NumeroParcelaConta" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Lancamentos_GrupoParcelamento_NumeroParcelaConta",
                table: "Lancamentos");

            migrationBuilder.DropColumn(
                name: "Contraparte",
                table: "Lancamentos");

            migrationBuilder.DropColumn(
                name: "DocumentoReferencia",
                table: "Lancamentos");

            migrationBuilder.DropColumn(
                name: "GrupoParcelamento",
                table: "Lancamentos");

            migrationBuilder.DropColumn(
                name: "NumeroParcelaConta",
                table: "Lancamentos");

            migrationBuilder.DropColumn(
                name: "PedidoParcelamento",
                table: "Lancamentos");

            migrationBuilder.DropColumn(
                name: "TotalParcelasConta",
                table: "Lancamentos");
        }
    }
}
