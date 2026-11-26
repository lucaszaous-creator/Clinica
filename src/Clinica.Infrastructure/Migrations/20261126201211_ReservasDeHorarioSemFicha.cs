using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReservasDeHorarioSemFicha : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReservasDeHorario",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Telefone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ConversaId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    DataHora = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    DuracaoMinutos = table.Column<int>(type: "integer", nullable: false),
                    ProfissionalId = table.Column<int>(type: "integer", nullable: false),
                    SalaId = table.Column<int>(type: "integer", nullable: true),
                    ModalidadeCodigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    EspecialidadeConsultaCodigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Particular = table.Column<bool>(type: "boolean", nullable: false),
                    ConvenioNome = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Situacao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MotivoDesmarcacao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AgendamentoId = table.Column<int>(type: "integer", nullable: true),
                    PacienteId = table.Column<int>(type: "integer", nullable: true),
                    CriadaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CriadaPor = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    AlteradaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    AlteradaPor = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Versao = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReservasDeHorario", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReservasDeHorario_Agendamentos_AgendamentoId",
                        column: x => x.AgendamentoId,
                        principalTable: "Agendamentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReservasDeHorario_Pacientes_PacienteId",
                        column: x => x.PacienteId,
                        principalTable: "Pacientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReservasDeHorario_Profissionais_ProfissionalId",
                        column: x => x.ProfissionalId,
                        principalTable: "Profissionais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReservasDeHorario_Salas_SalaId",
                        column: x => x.SalaId,
                        principalTable: "Salas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReservasDeHorario_AgendamentoId",
                table: "ReservasDeHorario",
                column: "AgendamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_ReservasDeHorario_DataHora_Situacao",
                table: "ReservasDeHorario",
                columns: new[] { "DataHora", "Situacao" });

            migrationBuilder.CreateIndex(
                name: "IX_ReservasDeHorario_PacienteId",
                table: "ReservasDeHorario",
                column: "PacienteId");

            migrationBuilder.CreateIndex(
                name: "IX_ReservasDeHorario_ProfissionalId",
                table: "ReservasDeHorario",
                column: "ProfissionalId");

            migrationBuilder.CreateIndex(
                name: "IX_ReservasDeHorario_SalaId",
                table: "ReservasDeHorario",
                column: "SalaId");

            migrationBuilder.CreateIndex(
                name: "IX_ReservasDeHorario_Telefone",
                table: "ReservasDeHorario",
                column: "Telefone");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReservasDeHorario");
        }
    }
}
