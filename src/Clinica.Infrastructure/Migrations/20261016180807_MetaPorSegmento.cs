using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    // MIGRATION-NAO-ADITIVA-CONSCIENTE(DropIndex): a chave única das metas ALARGA para incluir
    // modalidade e especialidade (as colunas novas nascem vazias = "todas"). Toda linha que
    // passava na chave antiga passa na nova; nenhuma linha se perde, e o app anterior lê as
    // mesmas colunas de sempre.
    /// <inheritdoc />
    public partial class MetaPorSegmento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Metas_Ano_Mes_Indicador_ProfissionalId",
                table: "Metas");

            migrationBuilder.AddColumn<string>(
                name: "EspecialidadeCodigo",
                table: "Metas",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ModalidadeCodigo",
                table: "Metas",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Metas_Ano_Mes_Indicador_ProfissionalId_ModalidadeCodigo_Esp~",
                table: "Metas",
                columns: new[] { "Ano", "Mes", "Indicador", "ProfissionalId", "ModalidadeCodigo", "EspecialidadeCodigo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Metas_Ano_Mes_Indicador_ProfissionalId_ModalidadeCodigo_Esp~",
                table: "Metas");

            migrationBuilder.DropColumn(
                name: "EspecialidadeCodigo",
                table: "Metas");

            migrationBuilder.DropColumn(
                name: "ModalidadeCodigo",
                table: "Metas");

            migrationBuilder.CreateIndex(
                name: "IX_Metas_Ano_Mes_Indicador_ProfissionalId",
                table: "Metas",
                columns: new[] { "Ano", "Mes", "Indicador", "ProfissionalId" },
                unique: true);
        }
    }
}
