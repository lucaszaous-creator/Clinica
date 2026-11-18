using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConclusaoAutomaticaPorProfissional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ConcluirAutomaticamenteHoras",
                table: "Profissionais",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConcluidoAutomaticamenteEm",
                table: "Agendamentos",
                type: "timestamp without time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConcluirAutomaticamenteHoras",
                table: "Profissionais");

            migrationBuilder.DropColumn(
                name: "ConcluidoAutomaticamenteEm",
                table: "Agendamentos");
        }
    }
}
