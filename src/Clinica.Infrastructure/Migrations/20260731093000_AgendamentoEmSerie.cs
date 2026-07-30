using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <summary>
    /// Agendamento em serie: as sessoes marcadas de uma vez compartilham uma chave.
    ///
    /// O Financeiro vende pacote de dez sessoes e a agenda marcava UMA por vez — dez
    /// aberturas do mesmo formulario, dez conferencias de conflito, dez chances de digitar
    /// a hora errada. A chave e o que permite tratar o bloco depois: cancelar o que sobrou
    /// quando o paciente desiste no meio do tratamento.
    ///
    /// PURAMENTE ADITIVA: uma coluna nova ANULAVEL numa tabela existente. Todo agendamento
    /// ja gravado continua valido com SerieId nulo (horario avulso), e o faturamento
    /// congelado nunca le esta coluna.
    /// </summary>
    public partial class AgendamentoEmSerie : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SerieId",
                table: "Agendamentos",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Agendamentos_SerieId",
                table: "Agendamentos",
                column: "SerieId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Agendamentos_SerieId",
                table: "Agendamentos");

            migrationBuilder.DropColumn(
                name: "SerieId",
                table: "Agendamentos");
        }
    }
}
