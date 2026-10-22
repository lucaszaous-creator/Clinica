using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DocumentoPeloWhatsApp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PedidosDocumentoWhatsApp",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IdMensagem = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UsuarioId = table.Column<int>(type: "integer", nullable: false),
                    PacienteId = table.Column<int>(type: "integer", nullable: false),
                    AgendamentoId = table.Column<int>(type: "integer", nullable: true),
                    Tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Texto = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    DiasAfastamento = table.Column<int>(type: "integer", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ExpiraEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    AbertoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DocumentoClinicoId = table.Column<int>(type: "integer", nullable: true),
                    ConcluidoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    AssinadoComA1 = table.Column<bool>(type: "boolean", nullable: false),
                    EnviadoAoPacienteEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PedidosDocumentoWhatsApp", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PedidosDocumentoWhatsApp_Agendamentos_AgendamentoId",
                        column: x => x.AgendamentoId,
                        principalTable: "Agendamentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PedidosDocumentoWhatsApp_DocumentosClinicos_DocumentoClinic~",
                        column: x => x.DocumentoClinicoId,
                        principalTable: "DocumentosClinicos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PedidosDocumentoWhatsApp_Pacientes_PacienteId",
                        column: x => x.PacienteId,
                        principalTable: "Pacientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PedidosDocumentoWhatsApp_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PedidosDocumentoWhatsApp_AgendamentoId",
                table: "PedidosDocumentoWhatsApp",
                column: "AgendamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_PedidosDocumentoWhatsApp_DocumentoClinicoId",
                table: "PedidosDocumentoWhatsApp",
                column: "DocumentoClinicoId");

            migrationBuilder.CreateIndex(
                name: "IX_PedidosDocumentoWhatsApp_IdMensagem",
                table: "PedidosDocumentoWhatsApp",
                column: "IdMensagem",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PedidosDocumentoWhatsApp_PacienteId",
                table: "PedidosDocumentoWhatsApp",
                column: "PacienteId");

            migrationBuilder.CreateIndex(
                name: "IX_PedidosDocumentoWhatsApp_TokenHash",
                table: "PedidosDocumentoWhatsApp",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PedidosDocumentoWhatsApp_UsuarioId_CriadoEm",
                table: "PedidosDocumentoWhatsApp",
                columns: new[] { "UsuarioId", "CriadoEm" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PedidosDocumentoWhatsApp");
        }
    }
}
