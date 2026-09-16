using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AtendimentoClinicoTablet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "AtividadeClinicaEm",
                table: "SessoesTablet",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OperacoesClinicasTablet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<int>(type: "integer", nullable: false),
                    AgendamentoId = table.Column<int>(type: "integer", nullable: false),
                    PedidoHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResultadoJson = table.Column<string>(type: "text", nullable: false),
                    CriadaEm = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperacoesClinicasTablet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OperacoesClinicasTablet_Agendamentos_AgendamentoId",
                        column: x => x.AgendamentoId,
                        principalTable: "Agendamentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperacoesClinicasTablet_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OperacoesClinicasTablet_AgendamentoId",
                table: "OperacoesClinicasTablet",
                column: "AgendamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_OperacoesClinicasTablet_UsuarioId_AgendamentoId",
                table: "OperacoesClinicasTablet",
                columns: new[] { "UsuarioId", "AgendamentoId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OperacoesClinicasTablet");

            migrationBuilder.DropColumn(
                name: "AtividadeClinicaEm",
                table: "SessoesTablet");
        }
    }
}
