using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SegundoFatorDoPortal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CodigosRecuperacaoPortal",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UsuarioId = table.Column<int>(type: "integer", nullable: false),
                    Resumo = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UsadoEm = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodigosRecuperacaoPortal", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CodigosRecuperacaoPortal_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DispositivosConfiaveisPortal",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UsuarioId = table.Column<int>(type: "integer", nullable: false),
                    CriadoEm = table.Column<long>(type: "bigint", nullable: false),
                    ExpiraEm = table.Column<long>(type: "bigint", nullable: false),
                    Credencial = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Origem = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DispositivosConfiaveisPortal", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DispositivosConfiaveisPortal_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SegundoFatorPortal",
                columns: table => new
                {
                    UsuarioId = table.Column<int>(type: "integer", nullable: false),
                    SegredoProtegido = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    AtivadoEm = table.Column<long>(type: "bigint", nullable: false),
                    UltimoPasso = table.Column<long>(type: "bigint", nullable: false),
                    FalhasSeguidas = table.Column<int>(type: "integer", nullable: false),
                    Versao = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SegundoFatorPortal", x => x.UsuarioId);
                    table.ForeignKey(
                        name: "FK_SegundoFatorPortal_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CodigosRecuperacaoPortal_Resumo",
                table: "CodigosRecuperacaoPortal",
                column: "Resumo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CodigosRecuperacaoPortal_UsuarioId",
                table: "CodigosRecuperacaoPortal",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_DispositivosConfiaveisPortal_UsuarioId_ExpiraEm",
                table: "DispositivosConfiaveisPortal",
                columns: new[] { "UsuarioId", "ExpiraEm" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CodigosRecuperacaoPortal");

            migrationBuilder.DropTable(
                name: "DispositivosConfiaveisPortal");

            migrationBuilder.DropTable(
                name: "SegundoFatorPortal");
        }
    }
}
