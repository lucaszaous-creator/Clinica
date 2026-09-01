using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <summary>
    /// O resultado de exame passa a poder dizer QUAL pedido ele responde (set/2026 — a
    /// tela de Exames do handoff). Coluna nullable + FK, ADITIVA: linha antiga fica com
    /// nulo, que é a verdade ("ninguém amarrou"). Escrita à mão (não há dotnet ef neste
    /// ambiente): carimbo MAIOR que todas as migrations existentes, e principalTable é o
    /// nome do DbSet ("DocumentosClinicos"), nunca o da classe (42P01, checagem 41).
    /// SetNull, nunca cascata: o resultado é registro clínico próprio e não pode ir de
    /// arrasto com o documento (a cascata da parcela 60).
    /// </summary>
    public partial class ResultadoRespondePedido : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PedidoDocumentoId",
                table: "ResultadosExame",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResultadosExame_PedidoDocumentoId",
                table: "ResultadosExame",
                column: "PedidoDocumentoId");

            migrationBuilder.AddForeignKey(
                name: "FK_ResultadosExame_DocumentosClinicos_PedidoDocumentoId",
                table: "ResultadosExame",
                column: "PedidoDocumentoId",
                principalTable: "DocumentosClinicos",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ResultadosExame_DocumentosClinicos_PedidoDocumentoId",
                table: "ResultadosExame");

            migrationBuilder.DropIndex(
                name: "IX_ResultadosExame_PedidoDocumentoId",
                table: "ResultadosExame");

            migrationBuilder.DropColumn(
                name: "PedidoDocumentoId",
                table: "ResultadosExame");
        }
    }
}
