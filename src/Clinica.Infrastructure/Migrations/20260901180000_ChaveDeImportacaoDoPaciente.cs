using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <summary>
    /// A chave de idempotência da importação do sistema anterior (set/2026 — a clínica
    /// migrou do Smart Clinic). ADITIVA: uma coluna nullable e um índice único sobre uma
    /// coluna que nasce VAZIA — o índice não tem como falhar na criação, porque NULL não
    /// é igual a NULL no Postgres, e toda ficha existente fica com nulo (é a verdade:
    /// nenhuma veio de importação).
    ///
    /// Escrita à mão (não há dotnet ef neste ambiente): carimbo MAIOR que todas as
    /// migrations existentes.
    /// </summary>
    public partial class ChaveDeImportacaoDoPaciente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChaveImportacao",
                table: "Pacientes",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pacientes_ChaveImportacao",
                table: "Pacientes",
                column: "ChaveImportacao",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Pacientes_ChaveImportacao",
                table: "Pacientes");

            migrationBuilder.DropColumn(
                name: "ChaveImportacao",
                table: "Pacientes");
        }
    }
}
