using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgendaRecebiveisPorContrato : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "LiquidacaoMensal",
                table: "TaxasCartao",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ParcelasRecebiveisCartao",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LancamentoFinanceiroId = table.Column<int>(type: "integer", nullable: false),
                    Numero = table.Column<int>(type: "integer", nullable: false),
                    Bruto = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    Taxa = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    Previsao = table.Column<DateOnly>(type: "date", nullable: false),
                    RecebidoEm = table.Column<DateOnly>(type: "date", nullable: true),
                    ConciliadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    IdBancario = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ContaBancaria = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DataExtrato = table.Column<DateOnly>(type: "date", nullable: true),
                    RecebimentoAntesDaConciliacao = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParcelasRecebiveisCartao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParcelasRecebiveisCartao_Lancamentos_LancamentoFinanceiroId",
                        column: x => x.LancamentoFinanceiroId,
                        principalTable: "Lancamentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ParcelasRecebiveisCartao_ContaBancaria_IdBancario",
                table: "ParcelasRecebiveisCartao",
                columns: new[] { "ContaBancaria", "IdBancario" });

            migrationBuilder.CreateIndex(
                name: "IX_ParcelasRecebiveisCartao_LancamentoFinanceiroId_Numero",
                table: "ParcelasRecebiveisCartao",
                columns: new[] { "LancamentoFinanceiroId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParcelasRecebiveisCartao_Previsao",
                table: "ParcelasRecebiveisCartao",
                column: "Previsao");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ParcelasRecebiveisCartao");

            migrationBuilder.DropColumn(
                name: "LiquidacaoMensal",
                table: "TaxasCartao");
        }
    }
}
