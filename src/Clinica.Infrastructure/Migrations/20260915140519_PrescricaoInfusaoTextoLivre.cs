using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    // MIGRATION-NAO-ADITIVA-CONSCIENTE(AlterColumn): amplia varchar(300) para text sem truncar dados; aplicativos anteriores continuam lendo a mesma coluna.
    public partial class PrescricaoInfusaoTextoLivre : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Descricao",
                table: "ItensPrescricaoInterna",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(300)",
                oldMaxLength: 300);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Mantém a capacidade ampliada: voltar a 300 caracteres perderia ou
            // recusaria prescrições livres já gravadas. O esquema antigo lê text.
        }
    }
}
