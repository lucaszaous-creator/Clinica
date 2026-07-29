using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <summary>
    /// Taxa da maquininha e imposto retido (parcela 9). Ate aqui o caixa so conhecia o
    /// BRUTO: a clinica passava R$ 150 no cartao, a tela dizia R$ 150, e o extrato da
    /// adquirente trazia R$ 145,20 trinta dias depois — a diferenca aparecia no fim do
    /// mes como um caixa que nao bate, sem ninguem saber de onde veio.
    ///
    /// PURAMENTE ADITIVA: uma tabela nova (TaxasCartao) e nove colunas ANULAVEIS em
    /// Lancamentos. Nada e renomeado, alterado ou removido, e o faturamento congelado
    /// segue sem saber que elas existem.
    ///
    /// NAO ha coluna de valor liquido: ele e calculado do bruto menos as deducoes.
    /// Grava-lo daria duas verdades sobre o mesmo dinheiro, e no dia em que divergissem
    /// ninguem saberia em qual acreditar.
    /// </summary>
    public partial class TaxasEImpostos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Adquirente",
                table: "Lancamentos",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AliquotaImposto",
                table: "Lancamentos",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Bandeira",
                table: "Lancamentos",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModalidadeCartao",
                table: "Lancamentos",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Parcelas",
                table: "Lancamentos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PrevisaoRecebimento",
                table: "Lancamentos",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxaPercentual",
                table: "Lancamentos",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ValorImposto",
                table: "Lancamentos",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ValorTaxa",
                table: "Lancamentos",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TaxasCartao",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Adquirente = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Bandeira = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Modalidade = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ParcelasDe = table.Column<int>(type: "integer", nullable: true),
                    ParcelasAte = table.Column<int>(type: "integer", nullable: true),
                    Percentual = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    DiasParaReceber = table.Column<int>(type: "integer", nullable: false),
                    VigenteDe = table.Column<DateOnly>(type: "date", nullable: true),
                    VigenteAte = table.Column<DateOnly>(type: "date", nullable: true),
                    Ativa = table.Column<bool>(type: "boolean", nullable: false),
                    Observacoes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CriadoPor = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxasCartao", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TaxasCartao_Ativa",
                table: "TaxasCartao",
                column: "Ativa");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TaxasCartao");

            migrationBuilder.DropColumn(
                name: "Adquirente",
                table: "Lancamentos");

            migrationBuilder.DropColumn(
                name: "AliquotaImposto",
                table: "Lancamentos");

            migrationBuilder.DropColumn(
                name: "Bandeira",
                table: "Lancamentos");

            migrationBuilder.DropColumn(
                name: "ModalidadeCartao",
                table: "Lancamentos");

            migrationBuilder.DropColumn(
                name: "Parcelas",
                table: "Lancamentos");

            migrationBuilder.DropColumn(
                name: "PrevisaoRecebimento",
                table: "Lancamentos");

            migrationBuilder.DropColumn(
                name: "TaxaPercentual",
                table: "Lancamentos");

            migrationBuilder.DropColumn(
                name: "ValorImposto",
                table: "Lancamentos");

            migrationBuilder.DropColumn(
                name: "ValorTaxa",
                table: "Lancamentos");
        }
    }
}
