using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <summary>
    /// Tabela de configurações globais (chave/valor): preferências da clínica que
    /// valem para todas as máquinas (ex.: janela de alerta de consultas).
    /// </summary>
    [DbContext(typeof(ClinicaDbContext))]
    [Migration("20260718100000_AddConfiguracoesGlobais")]
    public partial class AddConfiguracoesGlobais : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Configuracoes",
                columns: table => new
                {
                    Chave = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Valor = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Configuracoes", x => x.Chave);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Configuracoes");
        }
    }
}
