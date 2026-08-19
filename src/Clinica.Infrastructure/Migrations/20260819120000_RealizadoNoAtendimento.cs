using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <summary>
    /// A âncora de "a sessão ACONTECEU" (parcela 70 — docs/guia-no-agendamento.md §5).
    ///
    /// Com a guia nascendo na MARCAÇÃO, existir atendimento deixa de significar sessão
    /// realizada. Coluna ADITIVA e anulável; o backfill diz o que as linhas JÁ GRAVADAS
    /// valem (a lição do defaultValue da parcela 60): tudo o que existe antes desta
    /// migration é sessão que aconteceu — o regime antigo só criava atendimento na
    /// presença. A ATIVAÇÃO da chave "GuiaNoAgendamento" repete o backfill, para cobrir
    /// as linhas que o app antigo gravar na janela de atualização.
    /// </summary>
    public partial class RealizadoNoAtendimento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "RealizadoEm",
                table: "Atendimentos",
                type: "timestamp without time zone",
                nullable: true);

            // Toda linha existente é sessão realizada: quem tem LancadoEm herda a hora
            // real; quem é anterior à parcela 58 recebe a data da sessão.
            migrationBuilder.Sql(
                "UPDATE \"Atendimentos\" SET \"RealizadoEm\" = COALESCE(\"LancadoEm\", \"Data\"::timestamp)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RealizadoEm",
                table: "Atendimentos");
        }
    }
}
