using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <summary>
    /// MIGRATION-NAO-ADITIVA-CONSCIENTE(AlterColumn): amplia o detalhe da auditoria
    /// para preservar justificativas de até 1000 caracteres junto ao contexto da ação.
    /// A reversão recusa textos longos antes de reduzir a coluna, sem truncar registros.
    /// </summary>
    public partial class AuditoriaPreservaDetalheCompleto : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Detalhe",
                table: "Auditoria",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "Auditoria" WHERE char_length("Detalhe") > 500) THEN
                        RAISE EXCEPTION 'A auditoria contém detalhes maiores que 500 caracteres; reversão recusada para preservar o histórico.';
                    END IF;
                END $$;
                """);
            migrationBuilder.AlterColumn<string>(
                name: "Detalhe",
                table: "Auditoria",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
