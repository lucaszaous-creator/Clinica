using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <summary>
    /// Contas a pagar e a receber, com recorrencia (parcela 12).
    ///
    /// O caixa ja sabia o que ACONTECEU (Data, DataPagamento) e o que estava "previsto",
    /// mas previsto sem vencimento nao responde a pergunta que se faz toda segunda-feira:
    /// "o que vence esta semana?". DataVencimento e o que faltava.
    ///
    /// A tabela Recorrentes e o MOLDE das contas que se repetem — aluguel, luz, contador,
    /// mensalidade de software. Ela nao entra em total nenhum: quem entra e o lancamento
    /// PREVISTO que ela gera, e que dali em diante tem vida propria (a conta de luz nunca
    /// vem igual). OrigemRecorrencia, com indice UNICO, e a chave de idempotencia: o
    /// aluguel de agosto so pode nascer uma vez, mesmo com dois postos abrindo o app na
    /// mesma manha. Nulos ficam de fora do unico — lancamento avulso nao tem origem, e e
    /// a maioria.
    ///
    /// PURAMENTE ADITIVA: uma tabela nova e duas colunas ANULAVEIS em Lancamentos. Nada
    /// e renomeado, alterado ou removido, e o faturamento congelado segue sem saber que
    /// elas existem.
    /// </summary>
    public partial class ContasARecorrencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "DataVencimento",
                table: "Lancamentos",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrigemRecorrencia",
                table: "Lancamentos",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Recorrentes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Descricao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Valor = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    Periodicidade = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PrimeiroVencimento = table.Column<DateOnly>(type: "date", nullable: false),
                    UltimoVencimento = table.Column<DateOnly>(type: "date", nullable: true),
                    CategoriaFinanceiraId = table.Column<int>(type: "integer", nullable: true),
                    FormaPagamento = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Ativa = table.Column<bool>(type: "boolean", nullable: false),
                    Observacoes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CriadoPor = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recorrentes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Recorrentes_CategoriasFinanceiras_CategoriaFinanceiraId",
                        column: x => x.CategoriaFinanceiraId,
                        principalTable: "CategoriasFinanceiras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Lancamentos_DataVencimento",
                table: "Lancamentos",
                column: "DataVencimento");

            migrationBuilder.CreateIndex(
                name: "IX_Lancamentos_OrigemRecorrencia",
                table: "Lancamentos",
                column: "OrigemRecorrencia",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Recorrentes_Ativa",
                table: "Recorrentes",
                column: "Ativa");

            migrationBuilder.CreateIndex(
                name: "IX_Recorrentes_CategoriaFinanceiraId",
                table: "Recorrentes",
                column: "CategoriaFinanceiraId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Recorrentes");

            migrationBuilder.DropIndex(
                name: "IX_Lancamentos_DataVencimento",
                table: "Lancamentos");

            migrationBuilder.DropIndex(
                name: "IX_Lancamentos_OrigemRecorrencia",
                table: "Lancamentos");

            migrationBuilder.DropColumn(
                name: "DataVencimento",
                table: "Lancamentos");

            migrationBuilder.DropColumn(
                name: "OrigemRecorrencia",
                table: "Lancamentos");
        }
    }
}
