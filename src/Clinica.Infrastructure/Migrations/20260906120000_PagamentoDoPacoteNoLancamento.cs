using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <summary>
    /// O PACOTE que um lançamento paga (set/2026) — ADITIVA.
    ///
    /// Uma coluna anulável em <c>Lancamentos</c> (<c>PacotePacienteId</c>, FK para
    /// <c>PacotesPaciente</c> com <c>SetNull</c>) e o índice dela. Nulo em toda linha já
    /// gravada, que é a verdade: até aqui a venda de pacote não produzia lançamento nenhum.
    /// Não há <c>defaultValue</c> — não existe pacote a atribuir ao que já está na base.
    ///
    /// Escrita à mão (sem <c>dotnet ef</c> neste ambiente). O nome das tabelas vem do
    /// <c>DbSet</c> — <c>Lancamentos</c> e <c>PacotesPaciente</c> —, nunca da classe
    /// (parcela 79).
    /// </summary>
    public partial class PagamentoDoPacoteNoLancamento : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PacotePacienteId",
                table: "Lancamentos",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Lancamentos_PacotePacienteId",
                table: "Lancamentos",
                column: "PacotePacienteId");

            migrationBuilder.AddForeignKey(
                name: "FK_Lancamentos_PacotesPaciente_PacotePacienteId",
                table: "Lancamentos",
                column: "PacotePacienteId",
                principalTable: "PacotesPaciente",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Lancamentos_PacotesPaciente_PacotePacienteId",
                table: "Lancamentos");

            migrationBuilder.DropIndex(
                name: "IX_Lancamentos_PacotePacienteId",
                table: "Lancamentos");

            migrationBuilder.DropColumn(
                name: "PacotePacienteId",
                table: "Lancamentos");
        }
    }
}
