using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EvolucaoDeEnfermagem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EvolucoesEnfermagem",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PacienteId = table.Column<int>(type: "integer", nullable: false),
                    PrescricaoInternaId = table.Column<int>(type: "integer", nullable: true),
                    AgendamentoId = table.Column<int>(type: "integer", nullable: true),
                    Data = table.Column<DateOnly>(type: "date", nullable: false),
                    Hora = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    Texto = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Intercorrencia = table.Column<bool>(type: "boolean", nullable: false),
                    PressaoSistolica = table.Column<int>(type: "integer", nullable: true),
                    PressaoDiastolica = table.Column<int>(type: "integer", nullable: true),
                    FrequenciaCardiaca = table.Column<int>(type: "integer", nullable: true),
                    FrequenciaRespiratoria = table.Column<int>(type: "integer", nullable: true),
                    Temperatura = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: true),
                    SaturacaoOxigenio = table.Column<int>(type: "integer", nullable: true),
                    Dor = table.Column<int>(type: "integer", nullable: true),
                    AutorUsuarioId = table.Column<int>(type: "integer", nullable: true),
                    AutorNome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    AutorConselho = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    RegistradoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    RetificaEvolucaoId = table.Column<int>(type: "integer", nullable: true),
                    MotivoRetificacao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CanceladaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    MotivoCancelamento = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CanceladaPor = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvolucoesEnfermagem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvolucoesEnfermagem_Agendamentos_AgendamentoId",
                        column: x => x.AgendamentoId,
                        principalTable: "Agendamentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_EvolucoesEnfermagem_EvolucoesEnfermagem_RetificaEvolucaoId",
                        column: x => x.RetificaEvolucaoId,
                        principalTable: "EvolucoesEnfermagem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvolucoesEnfermagem_Pacientes_PacienteId",
                        column: x => x.PacienteId,
                        principalTable: "Pacientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvolucoesEnfermagem_PrescricoesInternas_PrescricaoInternaId",
                        column: x => x.PrescricaoInternaId,
                        principalTable: "PrescricoesInternas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_EvolucoesEnfermagem_Usuarios_AutorUsuarioId",
                        column: x => x.AutorUsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EvolucoesEnfermagem_AgendamentoId",
                table: "EvolucoesEnfermagem",
                column: "AgendamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_EvolucoesEnfermagem_AutorUsuarioId",
                table: "EvolucoesEnfermagem",
                column: "AutorUsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_EvolucoesEnfermagem_PacienteId_Data",
                table: "EvolucoesEnfermagem",
                columns: new[] { "PacienteId", "Data" });

            migrationBuilder.CreateIndex(
                name: "IX_EvolucoesEnfermagem_PrescricaoInternaId",
                table: "EvolucoesEnfermagem",
                column: "PrescricaoInternaId");

            migrationBuilder.CreateIndex(
                name: "IX_EvolucoesEnfermagem_RetificaEvolucaoId",
                table: "EvolucoesEnfermagem",
                column: "RetificaEvolucaoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EvolucoesEnfermagem");
        }
    }
}
