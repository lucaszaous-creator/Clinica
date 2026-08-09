using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class GuardaProntuarioImutavel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CanceladaEm",
                table: "MedidasClinicas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CanceladaPor",
                table: "MedidasClinicas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotivoCancelamento",
                table: "MedidasClinicas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CanceladaEm",
                table: "Evolucoes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CanceladaPor",
                table: "Evolucoes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotivoCancelamento",
                table: "Evolucoes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CanceladaEm",
                table: "AvaliacoesClinicas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CanceladaPor",
                table: "AvaliacoesClinicas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotivoCancelamento",
                table: "AvaliacoesClinicas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CanceladoEm",
                table: "AnexosProntuario",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CanceladoPor",
                table: "AnexosProntuario",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotivoCancelamento",
                table: "AnexosProntuario",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "VersoesEvolucao",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EvolucaoId = table.Column<int>(type: "integer", nullable: false),
                    Versao = table.Column<int>(type: "integer", nullable: false),
                    Data = table.Column<DateOnly>(type: "date", nullable: false),
                    EvaAntes = table.Column<int>(type: "integer", nullable: true),
                    EvaDepois = table.Column<int>(type: "integer", nullable: true),
                    QueixaPrincipal = table.Column<string>(type: "text", nullable: true),
                    Conduta = table.Column<string>(type: "text", nullable: true),
                    TextoEvolucao = table.Column<string>(type: "text", nullable: true),
                    Orientacoes = table.Column<string>(type: "text", nullable: true),
                    ProfissionalId = table.Column<int>(type: "integer", nullable: true),
                    SubstituidaEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SubstituidaPor = table.Column<string>(type: "text", nullable: true),
                    Motivo = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VersoesEvolucao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VersoesEvolucao_Evolucoes_EvolucaoId",
                        column: x => x.EvolucaoId,
                        principalTable: "Evolucoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VersoesEvolucao_EvolucaoId",
                table: "VersoesEvolucao",
                column: "EvolucaoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VersoesEvolucao");

            migrationBuilder.DropColumn(
                name: "CanceladaEm",
                table: "MedidasClinicas");

            migrationBuilder.DropColumn(
                name: "CanceladaPor",
                table: "MedidasClinicas");

            migrationBuilder.DropColumn(
                name: "MotivoCancelamento",
                table: "MedidasClinicas");

            migrationBuilder.DropColumn(
                name: "CanceladaEm",
                table: "Evolucoes");

            migrationBuilder.DropColumn(
                name: "CanceladaPor",
                table: "Evolucoes");

            migrationBuilder.DropColumn(
                name: "MotivoCancelamento",
                table: "Evolucoes");

            migrationBuilder.DropColumn(
                name: "CanceladaEm",
                table: "AvaliacoesClinicas");

            migrationBuilder.DropColumn(
                name: "CanceladaPor",
                table: "AvaliacoesClinicas");

            migrationBuilder.DropColumn(
                name: "MotivoCancelamento",
                table: "AvaliacoesClinicas");

            migrationBuilder.DropColumn(
                name: "CanceladoEm",
                table: "AnexosProntuario");

            migrationBuilder.DropColumn(
                name: "CanceladoPor",
                table: "AnexosProntuario");

            migrationBuilder.DropColumn(
                name: "MotivoCancelamento",
                table: "AnexosProntuario");
        }
    }
}
