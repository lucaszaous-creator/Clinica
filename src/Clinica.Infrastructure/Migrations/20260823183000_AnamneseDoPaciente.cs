using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <summary>
    /// A ANAMNESE DO PACIENTE e as versões dela (parcela 75).
    ///
    /// ADITIVA: duas tabelas novas. Nada existente é tocado — nenhuma coluna renomeada,
    /// removida ou alterada —, então o app anterior continua abrindo a base normalmente.
    /// </summary>
    public partial class AnamneseDoPaciente : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Anamneses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PacienteId = table.Column<int>(type: "integer", nullable: false),
                    AntecedentesPessoais = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    AntecedentesFamiliares = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    HabitosDeVida = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    HistoriaObstetrica = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    RevisaoDeSistemas = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Observacoes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CriadaEm = table.Column<System.DateTime>(type: "timestamp without time zone", nullable: false),
                    CriadaPor = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    AtualizadaEm = table.Column<System.DateTime>(type: "timestamp without time zone", nullable: true),
                    AtualizadaPor = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Anamneses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Anamneses_Pacientes_PacienteId",
                        column: x => x.PacienteId,
                        principalTable: "Pacientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VersoesAnamnese",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AnamnesePacienteId = table.Column<int>(type: "integer", nullable: false),
                    Versao = table.Column<int>(type: "integer", nullable: false),
                    AntecedentesPessoais = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    AntecedentesFamiliares = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    HabitosDeVida = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    HistoriaObstetrica = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    RevisaoDeSistemas = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Observacoes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    SubstituidaEm = table.Column<System.DateTime>(type: "timestamp without time zone", nullable: false),
                    SubstituidaPor = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Motivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VersoesAnamnese", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VersoesAnamnese_Anamneses_AnamnesePacienteId",
                        column: x => x.AnamnesePacienteId,
                        principalTable: "Anamneses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // UMA anamnese por paciente: duas dariam duas verdades sobre a mesma pessoa.
            migrationBuilder.CreateIndex(
                name: "IX_Anamneses_PacienteId",
                table: "Anamneses",
                column: "PacienteId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VersoesAnamnese_AnamnesePacienteId",
                table: "VersoesAnamnese",
                column: "AnamnesePacienteId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "VersoesAnamnese");
            migrationBuilder.DropTable(name: "Anamneses");
        }
    }
}
