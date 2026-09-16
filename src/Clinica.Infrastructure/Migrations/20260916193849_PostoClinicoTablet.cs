using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <inheritdoc />
    // MIGRATION-NAO-ADITIVA-CONSCIENTE(AlterColumn): permite recibos avulsos com AgendamentoId nulo; preserva todos os recibos antigos e suas FKs. Versões anteriores só consultam recibo por GUID de envio do próprio atendimento, sempre com AgendamentoId preenchido.
    public partial class PostoClinicoTablet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "AgendamentoId",
                table: "OperacoesClinicasTablet",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "PacienteId",
                table: "OperacoesClinicasTablet",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OperacoesClinicasTablet_PacienteId",
                table: "OperacoesClinicasTablet",
                column: "PacienteId");

            migrationBuilder.AddForeignKey(
                name: "FK_OperacoesClinicasTablet_Pacientes_PacienteId",
                table: "OperacoesClinicasTablet",
                column: "PacienteId",
                principalTable: "Pacientes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OperacoesClinicasTablet_Pacientes_PacienteId",
                table: "OperacoesClinicasTablet");

            migrationBuilder.DropIndex(
                name: "IX_OperacoesClinicasTablet_PacienteId",
                table: "OperacoesClinicasTablet");

            migrationBuilder.DropColumn(
                name: "PacienteId",
                table: "OperacoesClinicasTablet");

            migrationBuilder.AlterColumn<int>(
                name: "AgendamentoId",
                table: "OperacoesClinicasTablet",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
