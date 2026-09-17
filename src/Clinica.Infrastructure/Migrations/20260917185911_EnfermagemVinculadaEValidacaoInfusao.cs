using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnfermagemVinculadaEValidacaoInfusao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OrientacaoExterna",
                table: "PrescricoesInternas",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "OrigemEnfermagem",
                table: "PrescricoesInternas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "RegistradaPorUsuarioId",
                table: "PrescricoesInternas",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EnfermagemConferidaEm",
                table: "Agendamentos",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EnfermagemConferidaPorUsuarioId",
                table: "Agendamentos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HouveAtendimentoEnfermagem",
                table: "Agendamentos",
                type: "boolean",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OrientacaoExterna",
                table: "PrescricoesInternas");

            migrationBuilder.DropColumn(
                name: "OrigemEnfermagem",
                table: "PrescricoesInternas");

            migrationBuilder.DropColumn(
                name: "RegistradaPorUsuarioId",
                table: "PrescricoesInternas");

            migrationBuilder.DropColumn(
                name: "EnfermagemConferidaEm",
                table: "Agendamentos");

            migrationBuilder.DropColumn(
                name: "EnfermagemConferidaPorUsuarioId",
                table: "Agendamentos");

            migrationBuilder.DropColumn(
                name: "HouveAtendimentoEnfermagem",
                table: "Agendamentos");
        }
    }
}
