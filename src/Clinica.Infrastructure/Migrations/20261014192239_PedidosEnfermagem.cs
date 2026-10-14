using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PedidosEnfermagem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SolicitacoesEnfermagem",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Idempotencia = table.Column<Guid>(type: "uuid", nullable: false),
                    PedidoHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AutoraId = table.Column<int>(type: "integer", nullable: false),
                    VistaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    VistaPor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Inicio = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Fim = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Validade = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CriadaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Modalidade = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Quantidade = table.Column<int>(type: "integer", nullable: false),
                    Observacao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Situacao = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    AgendaReferencia = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TotalReferencia = table.Column<int>(type: "integer", nullable: false),
                    Versao = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SolicitacoesEnfermagem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SolicitacoesEnfermagem_Usuarios_AutoraId",
                        column: x => x.AutoraId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EventosSolicitacaoEnfermagem",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SolicitacaoEnfermagemId = table.Column<int>(type: "integer", nullable: false),
                    Idempotencia = table.Column<Guid>(type: "uuid", nullable: false),
                    PedidoHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UsuarioId = table.Column<int>(type: "integer", nullable: false),
                    Autor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Em = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Acao = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Observacao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventosSolicitacaoEnfermagem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventosSolicitacaoEnfermagem_SolicitacoesEnfermagem_Solicit~",
                        column: x => x.SolicitacaoEnfermagemId,
                        principalTable: "SolicitacoesEnfermagem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EventosSolicitacaoEnfermagem_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EventosSolicitacaoEnfermagem_SolicitacaoEnfermagemId_Idempo~",
                table: "EventosSolicitacaoEnfermagem",
                columns: new[] { "SolicitacaoEnfermagemId", "Idempotencia" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EventosSolicitacaoEnfermagem_UsuarioId",
                table: "EventosSolicitacaoEnfermagem",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_SolicitacoesEnfermagem_AutoraId_Idempotencia",
                table: "SolicitacoesEnfermagem",
                columns: new[] { "AutoraId", "Idempotencia" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SolicitacoesEnfermagem_Inicio_Situacao",
                table: "SolicitacoesEnfermagem",
                columns: new[] { "Inicio", "Situacao" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventosSolicitacaoEnfermagem");

            migrationBuilder.DropTable(
                name: "SolicitacoesEnfermagem");
        }
    }
}
