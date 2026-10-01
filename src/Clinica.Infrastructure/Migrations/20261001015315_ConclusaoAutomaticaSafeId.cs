using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConclusaoAutomaticaSafeId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OperacoesAssinaturaTablet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessaoId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Agendamento = table.Column<int>(type: "integer", nullable: false),
                    Documento = table.Column<int>(type: "integer", nullable: false),
                    Situacao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ChaveAtiva = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    ExpiraEm = table.Column<long>(type: "bigint", nullable: false),
                    AtualizadaEm = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperacoesAssinaturaTablet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OperacoesAssinaturaTablet_SessoesTablet_SessaoId",
                        column: x => x.SessaoId,
                        principalTable: "SessoesTablet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OperacoesAssinaturaTablet_ChaveAtiva",
                table: "OperacoesAssinaturaTablet",
                column: "ChaveAtiva",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OperacoesAssinaturaTablet_SessaoId_Id",
                table: "OperacoesAssinaturaTablet",
                columns: new[] { "SessaoId", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OperacoesAssinaturaTablet");
        }
    }
}
