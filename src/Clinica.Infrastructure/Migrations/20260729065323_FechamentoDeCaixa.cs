using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <summary>
    /// Conferencia da gaveta (parcela 14).
    ///
    /// O modulo sabia tudo sobre o dinheiro REGISTRADO e nada sobre o dinheiro FISICO. A
    /// diferenca entre os dois e onde some dinheiro numa clinica — a venda que ninguem
    /// lancou, o troco que saiu sem registro —, e ela so aparece quando alguem conta a
    /// gaveta e o sistema cobra a explicacao.
    ///
    /// ValorSistema e SaidasEspecie sao COPIADOS no fechamento, nao recalculados na
    /// leitura: um lancamento digitado amanha com a data de ontem reescreveria a
    /// conferencia de ontem e levaria junto a justificativa que alguem escreveu.
    ///
    /// Data NAO tem indice unico, e de proposito: reabrir para recontagem guarda o
    /// fechamento anterior e grava outro por cima. O que vale e o de maior Id — apagar o
    /// anterior apagaria a contagem que de fato foi feita antes.
    ///
    /// PURAMENTE ADITIVA: uma tabela nova, nenhuma coluna existente tocada.
    /// </summary>
    public partial class FechamentoDeCaixa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FechamentosCaixa",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Data = table.Column<DateOnly>(type: "date", nullable: false),
                    ValorSistema = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    ValorContado = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    SaidasEspecie = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    QuantidadeLancamentos = table.Column<int>(type: "integer", nullable: false),
                    Justificativa = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Observacoes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ConferidoPor = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    ConferidoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Reaberto = table.Column<bool>(type: "boolean", nullable: false),
                    MotivoReabertura = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FechamentosCaixa", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FechamentosCaixa_Data",
                table: "FechamentosCaixa",
                column: "Data");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FechamentosCaixa");
        }
    }
}
