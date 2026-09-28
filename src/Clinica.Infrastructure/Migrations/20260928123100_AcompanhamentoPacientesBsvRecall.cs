using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AcompanhamentoPacientesBsvRecall : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MotivosAcompanhamento",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MotivosAcompanhamento", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Acompanhamentos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PacienteId = table.Column<int>(type: "integer", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Modalidade = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ReferenciaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    AgendamentoOrigemId = table.Column<int>(type: "integer", nullable: true),
                    ProfissionalId = table.Column<int>(type: "integer", nullable: true),
                    ResponsavelId = table.Column<int>(type: "integer", nullable: true),
                    ProximoContato = table.Column<DateOnly>(type: "date", nullable: false),
                    Etapa = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CriadoPor = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    EncerradoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    MotivoEncerramento = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    EncerradoPor = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    MotivoId = table.Column<int>(type: "integer", nullable: true),
                    Versao = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Acompanhamentos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Acompanhamentos_Agendamentos_AgendamentoOrigemId",
                        column: x => x.AgendamentoOrigemId,
                        principalTable: "Agendamentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Acompanhamentos_MotivosAcompanhamento_MotivoId",
                        column: x => x.MotivoId,
                        principalTable: "MotivosAcompanhamento",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Acompanhamentos_Pacientes_PacienteId",
                        column: x => x.PacienteId,
                        principalTable: "Pacientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Acompanhamentos_Profissionais_ProfissionalId",
                        column: x => x.ProfissionalId,
                        principalTable: "Profissionais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Acompanhamentos_Usuarios_ResponsavelId",
                        column: x => x.ResponsavelId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContatosAcompanhamento",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AcompanhamentoPacienteId = table.Column<int>(type: "integer", nullable: false),
                    Idempotencia = table.Column<Guid>(type: "uuid", nullable: false),
                    Em = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Operador = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Canal = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Resultado = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Observacao = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ProximoContato = table.Column<DateOnly>(type: "date", nullable: true),
                    ResponsavelId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContatosAcompanhamento", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContatosAcompanhamento_Acompanhamentos_AcompanhamentoPacien~",
                        column: x => x.AcompanhamentoPacienteId,
                        principalTable: "Acompanhamentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContatosAcompanhamento_Usuarios_ResponsavelId",
                        column: x => x.ResponsavelId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Acompanhamentos_AgendamentoOrigemId",
                table: "Acompanhamentos",
                column: "AgendamentoOrigemId");

            migrationBuilder.CreateIndex(
                name: "IX_Acompanhamentos_MotivoId",
                table: "Acompanhamentos",
                column: "MotivoId");

            migrationBuilder.CreateIndex(
                name: "IX_Acompanhamentos_PacienteId_Tipo_Modalidade",
                table: "Acompanhamentos",
                columns: new[] { "PacienteId", "Tipo", "Modalidade" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Acompanhamentos_ProfissionalId",
                table: "Acompanhamentos",
                column: "ProfissionalId");

            migrationBuilder.CreateIndex(
                name: "IX_Acompanhamentos_ResponsavelId_ProximoContato",
                table: "Acompanhamentos",
                columns: new[] { "ResponsavelId", "ProximoContato" });

            migrationBuilder.CreateIndex(
                name: "IX_ContatosAcompanhamento_AcompanhamentoPacienteId_Idempotencia",
                table: "ContatosAcompanhamento",
                columns: new[] { "AcompanhamentoPacienteId", "Idempotencia" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContatosAcompanhamento_ResponsavelId",
                table: "ContatosAcompanhamento",
                column: "ResponsavelId");

            migrationBuilder.CreateIndex(
                name: "IX_MotivosAcompanhamento_Nome",
                table: "MotivosAcompanhamento",
                column: "Nome",
                unique: true);
            migrationBuilder.Sql("""
                INSERT INTO "MotivosAcompanhamento" ("Nome", "Ativo") VALUES
                ('Aguardando autorização do plano', TRUE),
                ('Plano negou autorização', TRUE),
                ('Paciente pediu adiamento', TRUE),
                ('Desistência informada pelo paciente', TRUE),
                ('Telefone desatualizado', TRUE);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContatosAcompanhamento");

            migrationBuilder.DropTable(
                name: "Acompanhamentos");

            migrationBuilder.DropTable(
                name: "MotivosAcompanhamento");
        }
    }
}
