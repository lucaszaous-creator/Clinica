using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConsolidacaoGestao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LancamentoFinanceiroId",
                table: "MovimentosEstoque",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContaBancariaConciliacao",
                table: "Lancamentos",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DataExtrato",
                table: "Lancamentos",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "RecebimentoAntesDaConciliacao",
                table: "Lancamentos",
                type: "date",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MovimentosEstoque_LancamentoFinanceiroId",
                table: "MovimentosEstoque",
                column: "LancamentoFinanceiroId");

            migrationBuilder.AddForeignKey(
                name: "FK_MovimentosEstoque_Lancamentos_LancamentoFinanceiroId",
                table: "MovimentosEstoque",
                column: "LancamentoFinanceiroId",
                principalTable: "Lancamentos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MovimentosEstoque_Lancamentos_LancamentoFinanceiroId",
                table: "MovimentosEstoque");

            migrationBuilder.DropIndex(
                name: "IX_MovimentosEstoque_LancamentoFinanceiroId",
                table: "MovimentosEstoque");

            migrationBuilder.DropColumn(
                name: "LancamentoFinanceiroId",
                table: "MovimentosEstoque");

            migrationBuilder.DropColumn(
                name: "ContaBancariaConciliacao",
                table: "Lancamentos");

            migrationBuilder.DropColumn(
                name: "DataExtrato",
                table: "Lancamentos");

            migrationBuilder.DropColumn(
                name: "RecebimentoAntesDaConciliacao",
                table: "Lancamentos");
        }
    }
}
