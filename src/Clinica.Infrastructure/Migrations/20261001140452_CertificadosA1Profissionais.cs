using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CertificadosA1Profissionais : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CertificadosA1",
                columns: table => new
                {
                    UsuarioId = table.Column<int>(type: "integer", nullable: false),
                    ProfissionalId = table.Column<int>(type: "integer", nullable: false),
                    ArquivoProtegido = table.Column<byte[]>(type: "bytea", nullable: false),
                    ImpressaoDigital = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Titular = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ValidoAte = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Versao = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CertificadosA1", x => x.UsuarioId);
                    table.ForeignKey(
                        name: "FK_CertificadosA1_Profissionais_ProfissionalId",
                        column: x => x.ProfissionalId,
                        principalTable: "Profissionais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CertificadosA1_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CertificadosA1_ProfissionalId",
                table: "CertificadosA1",
                column: "ProfissionalId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CertificadosA1");
        }
    }
}
