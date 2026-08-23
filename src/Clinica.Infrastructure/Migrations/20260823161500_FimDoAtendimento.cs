using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <summary>
    /// O carimbo do FIM do atendimento clínico (parcela 74).
    ///
    /// ADITIVA: uma coluna anulável. Horário anterior a esta versão fica com NULL, e nulo
    /// é a resposta certa — "o sistema ainda não registrava o fim" não se confunde com
    /// "terminou às 00:00".
    /// </summary>
    public partial class FimDoAtendimento : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<System.DateTime>(
                name: "FimAtendimentoEm",
                table: "Agendamentos",
                type: "timestamp without time zone",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FimAtendimentoEm",
                table: "Agendamentos");
        }
    }
}
