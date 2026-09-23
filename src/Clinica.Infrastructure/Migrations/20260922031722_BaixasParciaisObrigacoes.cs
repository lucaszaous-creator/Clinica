using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BaixasParciaisObrigacoes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "GrupoObrigacao",
                table: "Lancamentos",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OrigemDesdobramentoId",
                table: "Lancamentos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ValorOriginalObrigacao",
                table: "Lancamentos",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Lancamentos_GrupoObrigacao",
                table: "Lancamentos",
                column: "GrupoObrigacao");

            migrationBuilder.CreateIndex(
                name: "IX_Lancamentos_OrigemDesdobramentoId",
                table: "Lancamentos",
                column: "OrigemDesdobramentoId");

            migrationBuilder.AddForeignKey(
                name: "FK_Lancamentos_Lancamentos_OrigemDesdobramentoId",
                table: "Lancamentos",
                column: "OrigemDesdobramentoId",
                principalTable: "Lancamentos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Lancamentos_Lancamentos_OrigemDesdobramentoId",
                table: "Lancamentos");

            migrationBuilder.DropIndex(
                name: "IX_Lancamentos_GrupoObrigacao",
                table: "Lancamentos");

            migrationBuilder.DropIndex(
                name: "IX_Lancamentos_OrigemDesdobramentoId",
                table: "Lancamentos");

            migrationBuilder.DropColumn(
                name: "GrupoObrigacao",
                table: "Lancamentos");

            migrationBuilder.DropColumn(
                name: "OrigemDesdobramentoId",
                table: "Lancamentos");

            migrationBuilder.DropColumn(
                name: "ValorOriginalObrigacao",
                table: "Lancamentos");
        }
    }
}
