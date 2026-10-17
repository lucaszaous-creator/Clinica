using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CanalSms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TokenResposta",
                table: "ContatosCampanha",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MensagensSms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PacienteId = table.Column<int>(type: "integer", nullable: true),
                    ContatoCampanhaId = table.Column<int>(type: "integer", nullable: true),
                    Finalidade = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Situacao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ChaveIdempotencia = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Telefone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Texto = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Segmentos = table.Column<int>(type: "integer", nullable: false),
                    IdNoProvedor = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Erro = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    Tentativas = table.Column<int>(type: "integer", nullable: false),
                    CriadaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EnviadaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    EnviadaPor = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MensagensSms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MensagensSms_Pacientes_PacienteId",
                        column: x => x.PacienteId,
                        principalTable: "Pacientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContatosCampanha_TokenResposta",
                table: "ContatosCampanha",
                column: "TokenResposta",
                filter: "\"TokenResposta\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MensagensSms_ChaveIdempotencia",
                table: "MensagensSms",
                column: "ChaveIdempotencia",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MensagensSms_CriadaEm",
                table: "MensagensSms",
                column: "CriadaEm");

            migrationBuilder.CreateIndex(
                name: "IX_MensagensSms_PacienteId",
                table: "MensagensSms",
                column: "PacienteId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MensagensSms");

            migrationBuilder.DropIndex(
                name: "IX_ContatosCampanha_TokenResposta",
                table: "ContatosCampanha");

            migrationBuilder.DropColumn(
                name: "TokenResposta",
                table: "ContatosCampanha");
        }
    }
}
