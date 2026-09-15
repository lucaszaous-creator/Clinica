using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <inheritdoc />
    // MIGRATION-NAO-ADITIVA-CONSCIENTE(AlterColumn): amplia as observações dos modelos de 500 para 1000 caracteres, sem truncar registros.
    public partial class ModelosCorpoObservacoesCompletas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Descricao",
                table: "ProtocolosCorporais",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Preserva os modelos já gravados: reduzir a capacidade poderia perder observações.
        }
    }
}
