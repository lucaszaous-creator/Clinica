using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <summary>
    /// Convênio personalizado: adiciona ao catálogo (Convenios) os campos da regra
    /// genérica configurável, usados quando a família é "Personalizado".
    /// </summary>
    [DbContext(typeof(ClinicaDbContext))]
    [Migration("20260718140000_ConvenioPersonalizado")]
    public partial class ConvenioPersonalizado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>("FazEletro", "Convenios", "boolean", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<bool>("TemSegundoCodigo", "Convenios", "boolean", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<string>("FormaSegundoCodigo", "Convenios", "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Sistema");
            migrationBuilder.AddColumn<bool>("SegundoCodigoDependeApp", "Convenios", "boolean", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<int>("DiasSegundoCodigo", "Convenios", "integer", nullable: false, defaultValue: 1);
            migrationBuilder.AddColumn<bool>("FaturaBsv", "Convenios", "boolean", nullable: false, defaultValue: true);
            migrationBuilder.AddColumn<bool>("InverteDatasBsv", "Convenios", "boolean", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<int>("ValidadeConsultaDias", "Convenios", "integer", nullable: true);
            migrationBuilder.AddColumn<string>("CategoriaComApp", "Convenios", "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Verde");
            migrationBuilder.AddColumn<string>("CategoriaSemApp", "Convenios", "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Amarela");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn("FazEletro", "Convenios");
            migrationBuilder.DropColumn("TemSegundoCodigo", "Convenios");
            migrationBuilder.DropColumn("FormaSegundoCodigo", "Convenios");
            migrationBuilder.DropColumn("SegundoCodigoDependeApp", "Convenios");
            migrationBuilder.DropColumn("DiasSegundoCodigo", "Convenios");
            migrationBuilder.DropColumn("FaturaBsv", "Convenios");
            migrationBuilder.DropColumn("InverteDatasBsv", "Convenios");
            migrationBuilder.DropColumn("ValidadeConsultaDias", "Convenios");
            migrationBuilder.DropColumn("CategoriaComApp", "Convenios");
            migrationBuilder.DropColumn("CategoriaSemApp", "Convenios");
        }
    }
}
