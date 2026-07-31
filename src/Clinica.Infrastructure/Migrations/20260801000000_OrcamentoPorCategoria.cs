using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <summary>
    /// Teto de gasto por categoria e mes (parcela 31).
    ///
    /// A clinica cadastra plano de contas desde a parcela 4 e ve o gasto por categoria no
    /// fluxo de caixa desde a 13 — as duas coisas respondem "quanto saiu de material este
    /// mes". Nenhuma responde "quanto PODIA sair". Sem teto, o gasto so e julgado contra o
    /// mes anterior, que e a mesma armadilha que a meta resolveu para o faturamento:
    /// gastar 10% menos que um mes ruim aparece como economia.
    ///
    /// Irmao da tabela Metas, com as mesmas regras — uma linha por mes (fato datado) e
    /// indice unico porque dois tetos para a mesma pergunta dariam duas reguas.
    ///
    /// PURAMENTE ADITIVA: tabela nova, nenhuma coluna existente tocada.
    /// </summary>
    public partial class OrcamentoPorCategoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Orcamentos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Ano = table.Column<int>(type: "integer", nullable: false),
                    Mes = table.Column<int>(type: "integer", nullable: false),
                    CategoriaFinanceiraId = table.Column<int>(type: "integer", nullable: false),
                    Teto = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CriadoPor = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orcamentos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Orcamentos_CategoriasFinanceiras_CategoriaFinanceiraId",
                        column: x => x.CategoriaFinanceiraId,
                        principalTable: "CategoriasFinanceiras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orcamentos_Ano_Mes_CategoriaFinanceiraId",
                table: "Orcamentos",
                columns: new[] { "Ano", "Mes", "CategoriaFinanceiraId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orcamentos_CategoriaFinanceiraId",
                table: "Orcamentos",
                column: "CategoriaFinanceiraId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "Orcamentos");
        }
    }
}
