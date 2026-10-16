using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ComissaoRecuperacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ComissoesRecuperacao",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AcompanhamentoPacienteId = table.Column<int>(type: "integer", nullable: false),
                    AtendimentoId = table.Column<int>(type: "integer", nullable: false),
                    PacienteId = table.Column<int>(type: "integer", nullable: false),
                    UsuarioId = table.Column<int>(type: "integer", nullable: false),
                    AssumidoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    RetornoEm = table.Column<DateOnly>(type: "date", nullable: false),
                    Decisao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Valor = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    RegraDescricao = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Observacao = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DecididaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    DecididaPor = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    CanceladaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CanceladaPor = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    MotivoCancelamento = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComissoesRecuperacao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComissoesRecuperacao_Acompanhamentos_AcompanhamentoPaciente~",
                        column: x => x.AcompanhamentoPacienteId,
                        principalTable: "Acompanhamentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ComissoesRecuperacao_Atendimentos_AtendimentoId",
                        column: x => x.AtendimentoId,
                        principalTable: "Atendimentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ComissoesRecuperacao_Pacientes_PacienteId",
                        column: x => x.PacienteId,
                        principalTable: "Pacientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ComissoesRecuperacao_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RegrasComissaoRecuperacao",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Modalidade = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Valor = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    VigenteDe = table.Column<DateOnly>(type: "date", nullable: false),
                    VigenteAte = table.Column<DateOnly>(type: "date", nullable: true),
                    Ativa = table.Column<bool>(type: "boolean", nullable: false),
                    Observacao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CriadaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CriadaPor = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegrasComissaoRecuperacao", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ComissoesRecuperacao_AcompanhamentoPacienteId",
                table: "ComissoesRecuperacao",
                column: "AcompanhamentoPacienteId");

            migrationBuilder.CreateIndex(
                name: "IX_ComissoesRecuperacao_AtendimentoId",
                table: "ComissoesRecuperacao",
                column: "AtendimentoId",
                unique: true,
                filter: "\"CanceladaEm\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ComissoesRecuperacao_PacienteId",
                table: "ComissoesRecuperacao",
                column: "PacienteId");

            migrationBuilder.CreateIndex(
                name: "IX_ComissoesRecuperacao_UsuarioId_RetornoEm",
                table: "ComissoesRecuperacao",
                columns: new[] { "UsuarioId", "RetornoEm" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ComissoesRecuperacao");

            migrationBuilder.DropTable(
                name: "RegrasComissaoRecuperacao");
        }
    }
}
