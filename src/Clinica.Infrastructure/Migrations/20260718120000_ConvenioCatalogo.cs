using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <summary>
    /// Catálogo de convênios: adiciona à tabela Parametros os campos editáveis pela
    /// clínica (nome exibido, ativo/inativo e categoria-base com/sem app). Nulos =
    /// usa o padrão do código; convênios existentes começam ativos.
    /// </summary>
    [DbContext(typeof(ClinicaDbContext))]
    [Migration("20260718120000_ConvenioCatalogo")]
    public partial class ConvenioCatalogo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Nome",
                table: "Parametros",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Ativo",
                table: "Parametros",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "CategoriaComApp",
                table: "Parametros",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CategoriaSemApp",
                table: "Parametros",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Nome", table: "Parametros");
            migrationBuilder.DropColumn(name: "Ativo", table: "Parametros");
            migrationBuilder.DropColumn(name: "CategoriaComApp", table: "Parametros");
            migrationBuilder.DropColumn(name: "CategoriaSemApp", table: "Parametros");
        }
    }
}
