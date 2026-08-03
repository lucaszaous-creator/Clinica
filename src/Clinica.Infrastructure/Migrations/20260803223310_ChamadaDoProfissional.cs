using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <summary>
    /// Parcela 38 — o profissional avisa que quer o próximo paciente.
    ///
    /// PURAMENTE ADITIVA: uma coluna anulável em <c>Agendamentos</c>. O faturamento
    /// congelado não conhece a fila (ele só lê <c>StatusAgendamento</c>) e continua
    /// subindo sobre este banco sem saber que a coluna existe.
    ///
    /// O estágio "Chamado" do kanban NÃO virou coluna: ele é DERIVADO dos carimbos de
    /// hora, como os outros quatro — ver <c>Agendamento.Etapa</c>. O que precisou de
    /// banco foi só o fato novo: a hora em que a chamada aconteceu.
    /// </summary>
    public partial class ChamadaDoProfissional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ChamadoEm",
                table: "Agendamentos",
                type: "timestamp without time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChamadoEm",
                table: "Agendamentos");
        }
    }
}
