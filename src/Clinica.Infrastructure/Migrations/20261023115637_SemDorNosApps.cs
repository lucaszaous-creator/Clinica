using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SemDorNosApps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AvisosDoSemDor",
                columns: table => new
                {
                    Chave = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PrimeiroNome = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Assunto = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Em = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Link = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AvisosDoSemDor", x => x.Chave);
                });

            migrationBuilder.CreateTable(
                name: "PerguntasAoSemDor",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UsuarioId = table.Column<int>(type: "integer", nullable: false),
                    Conversa = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Texto = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CriadaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Situacao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Reescrita = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ResolvidaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PerguntasAoSemDor", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PerguntasAoSemDor_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SemDorNoCrm",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    PerguntasLidasEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    AvisosRecebidosEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SemDorNoCrm", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PerguntasAoSemDor_Situacao_CriadaEm",
                table: "PerguntasAoSemDor",
                columns: new[] { "Situacao", "CriadaEm" });

            migrationBuilder.CreateIndex(
                name: "IX_PerguntasAoSemDor_UsuarioId",
                table: "PerguntasAoSemDor",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AvisosDoSemDor");

            migrationBuilder.DropTable(
                name: "PerguntasAoSemDor");

            migrationBuilder.DropTable(
                name: "SemDorNoCrm");
        }
    }
}
