using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PagamentoNaMarcacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AgendamentoId",
                table: "Lancamentos",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Lancamentos_AgendamentoId",
                table: "Lancamentos",
                column: "AgendamentoId");

            migrationBuilder.AddForeignKey(
                name: "FK_Lancamentos_Agendamentos_AgendamentoId",
                table: "Lancamentos",
                column: "AgendamentoId",
                principalTable: "Agendamentos",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Lancamentos_Agendamentos_AgendamentoId",
                table: "Lancamentos");

            migrationBuilder.DropIndex(
                name: "IX_Lancamentos_AgendamentoId",
                table: "Lancamentos");

            migrationBuilder.DropColumn(
                name: "AgendamentoId",
                table: "Lancamentos");
        }
    }
}
