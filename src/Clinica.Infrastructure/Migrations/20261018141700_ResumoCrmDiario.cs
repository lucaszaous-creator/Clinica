using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ResumoCrmDiario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ResumosCrmDiarios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Dia = table.Column<DateOnly>(type: "date", nullable: false),
                    GeradoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    RecebidoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ConversasIniciadas = table.Column<int>(type: "integer", nullable: false),
                    ConversasEncerradas = table.Column<int>(type: "integer", nullable: false),
                    ConversasAbertas = table.Column<int>(type: "integer", nullable: false),
                    MensagensRecebidas = table.Column<int>(type: "integer", nullable: false),
                    MensagensEnviadas = table.Column<int>(type: "integer", nullable: false),
                    RespostaMedianaSegundos = table.Column<int>(type: "integer", nullable: true),
                    RespostasMedidas = table.Column<int>(type: "integer", nullable: false),
                    SmsEnviados = table.Column<int>(type: "integer", nullable: false),
                    SmsSegmentos = table.Column<int>(type: "integer", nullable: false),
                    SmsFalhas = table.Column<int>(type: "integer", nullable: false),
                    ConfirmacoesPedidas = table.Column<int>(type: "integer", nullable: false),
                    ConfirmacoesRespondidas = table.Column<int>(type: "integer", nullable: false),
                    Resolvidas = table.Column<int>(type: "integer", nullable: false),
                    Agendadas = table.Column<int>(type: "integer", nullable: false),
                    SemResposta = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResumosCrmDiarios", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ResumosCrmDiarios_Dia",
                table: "ResumosCrmDiarios",
                column: "Dia",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ResumosCrmDiarios");
        }
    }
}
