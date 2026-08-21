using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <summary>
    /// O REGISTRO DE EXECUÇÃO passa a ser SELADO no mesmo ato da 2ª assinatura
    /// (decisão da direção, 20/08/2026).
    ///
    /// A folha da PRESCRIÇÃO é selada pela médica ANTES da execução: ela nunca poderá
    /// mostrar o ✓, a rodela e o suspenso. Acrescentar-lhe uma página foi MEDIDO no pyhanko
    /// e faz o validador acusar modificação ILEGAL na assinatura DELA — a garantia aparente
    /// pelo avesso, que nosso lado aprova e o mundo lá fora recusa. Quem mostra a execução
    /// é o registro, e para ele valer como prova precisa ser selado também.
    ///
    /// ADITIVA: uma coluna anulável e a chave estrangeira dela. Nada é renomeado nem
    /// removido, e a folha antiga fica com a coluna nula — que é a verdade sobre ela.
    /// </summary>
    public partial class RegistroDeExecucaoSelado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ArquivoRegistroId",
                table: "AssinaturasDocumento",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssinaturasDocumento_ArquivoRegistroId",
                table: "AssinaturasDocumento",
                column: "ArquivoRegistroId");

            migrationBuilder.AddForeignKey(
                name: "FK_AssinaturasDocumento_ArquivosAssinados_ArquivoRegistroId",
                table: "AssinaturasDocumento",
                column: "ArquivoRegistroId",
                principalTable: "ArquivosAssinados",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AssinaturasDocumento_ArquivosAssinados_ArquivoRegistroId",
                table: "AssinaturasDocumento");

            migrationBuilder.DropIndex(
                name: "IX_AssinaturasDocumento_ArquivoRegistroId",
                table: "AssinaturasDocumento");

            migrationBuilder.DropColumn(
                name: "ArquivoRegistroId",
                table: "AssinaturasDocumento");
        }
    }
}
