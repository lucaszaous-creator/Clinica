using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConvenioParticular : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ⚠️ `defaultValue: TRUE`, e o EF gerou `false`.
            //
            // O gerador não sabe o que a coluna significa: ele viu um `bool` não-anulável e
            // pôs o default da linguagem. Aplicada assim, esta migration desligaria a guia
            // de TODOS os convênios já cadastrados — Unimed, Amil, Petrobras e as variantes
            // da clínica — na primeira abertura do app depois da atualização. O sistema
            // continuaria abrindo, os atendimentos continuariam nascendo, e as guias
            // simplesmente parariam de existir. Ninguém perceberia até faturar o mês.
            //
            // `true` é o que as linhas já gravadas VALEM: todo convênio cadastrado até aqui
            // fatura. Quem não fatura é o PARTICULAR, que ainda não existe e que a clínica
            // vai cadastrar em Configurações desmarcando esta caixa.
            migrationBuilder.AddColumn<bool>(
                name: "GeraGuia",
                table: "Convenios",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GeraGuia",
                table: "Convenios");
        }
    }
}
