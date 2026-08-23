using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ProcessoDeEnfermagemEAtendimento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Avaliacao",
                table: "EvolucoesEnfermagem",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExameFisico",
                table: "EvolucoesEnfermagem",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Historico",
                table: "EvolucoesEnfermagem",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CidSessao",
                table: "Evolucoes",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExameFisico",
                table: "Evolucoes",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HipoteseDiagnostica",
                table: "Evolucoes",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HistoriaDoencaAtual",
                table: "Evolucoes",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CuidadosEnfermagem",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EvolucaoEnfermagemId = table.Column<int>(type: "integer", nullable: false),
                    Codigo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Descricao = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    Frequencia = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    DiagnosticoEnfermagemId = table.Column<int>(type: "integer", nullable: true),
                    Ordem = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CuidadosEnfermagem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CuidadosEnfermagem_EvolucoesEnfermagem_EvolucaoEnfermagemId",
                        column: x => x.EvolucaoEnfermagemId,
                        principalTable: "EvolucoesEnfermagem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DiagnosticosEnfermagem",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EvolucaoEnfermagemId = table.Column<int>(type: "integer", nullable: false),
                    Codigo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RelacionadoA = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    EvidenciadoPor = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    ResultadoEsperado = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true),
                    Ordem = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiagnosticosEnfermagem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiagnosticosEnfermagem_EvolucoesEnfermagem_EvolucaoEnfermag~",
                        column: x => x.EvolucaoEnfermagemId,
                        principalTable: "EvolucoesEnfermagem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CuidadosEnfermagem_EvolucaoEnfermagemId",
                table: "CuidadosEnfermagem",
                column: "EvolucaoEnfermagemId");

            migrationBuilder.CreateIndex(
                name: "IX_DiagnosticosEnfermagem_EvolucaoEnfermagemId",
                table: "DiagnosticosEnfermagem",
                column: "EvolucaoEnfermagemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CuidadosEnfermagem");

            migrationBuilder.DropTable(
                name: "DiagnosticosEnfermagem");

            migrationBuilder.DropColumn(
                name: "Avaliacao",
                table: "EvolucoesEnfermagem");

            migrationBuilder.DropColumn(
                name: "ExameFisico",
                table: "EvolucoesEnfermagem");

            migrationBuilder.DropColumn(
                name: "Historico",
                table: "EvolucoesEnfermagem");

            migrationBuilder.DropColumn(
                name: "CidSessao",
                table: "Evolucoes");

            migrationBuilder.DropColumn(
                name: "ExameFisico",
                table: "Evolucoes");

            migrationBuilder.DropColumn(
                name: "HipoteseDiagnostica",
                table: "Evolucoes");

            migrationBuilder.DropColumn(
                name: "HistoriaDoencaAtual",
                table: "Evolucoes");
        }
    }
}
