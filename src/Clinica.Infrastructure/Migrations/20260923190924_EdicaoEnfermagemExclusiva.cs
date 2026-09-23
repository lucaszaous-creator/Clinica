using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EdicaoEnfermagemExclusiva : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EdicoesEnfermagemTablet",
                columns: table => new
                {
                    AgendamentoId = table.Column<int>(type: "integer", nullable: false),
                    PacienteId = table.Column<int>(type: "integer", nullable: false),
                    UsuarioId = table.Column<int>(type: "integer", nullable: false),
                    SessaoId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EditorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpiraEm = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EdicoesEnfermagemTablet", x => x.AgendamentoId);
                    table.ForeignKey(
                        name: "FK_EdicoesEnfermagemTablet_Agendamentos_AgendamentoId",
                        column: x => x.AgendamentoId,
                        principalTable: "Agendamentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EdicoesEnfermagemTablet_SessoesTablet_SessaoId",
                        column: x => x.SessaoId,
                        principalTable: "SessoesTablet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EdicoesEnfermagemTablet_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EdicoesEnfermagemTablet_ExpiraEm",
                table: "EdicoesEnfermagemTablet",
                column: "ExpiraEm");

            migrationBuilder.CreateIndex(
                name: "IX_EdicoesEnfermagemTablet_SessaoId",
                table: "EdicoesEnfermagemTablet",
                column: "SessaoId");

            migrationBuilder.CreateIndex(
                name: "IX_EdicoesEnfermagemTablet_UsuarioId",
                table: "EdicoesEnfermagemTablet",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EdicoesEnfermagemTablet");
        }
    }
}
