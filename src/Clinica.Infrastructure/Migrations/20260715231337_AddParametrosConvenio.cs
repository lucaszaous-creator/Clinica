using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddParametrosConvenio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Parametros",
                columns: table => new
                {
                    Convenio = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ValidadeConsultaDias = table.Column<int>(type: "integer", nullable: true),
                    DiasSegundoCodigo = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parametros", x => x.Convenio);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Parametros");
        }
    }
}
