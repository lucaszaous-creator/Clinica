using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <summary>
    /// Convênios dinâmicos: cria o catálogo de convênios (Convenios), semeia os quatro
    /// embutidos, adiciona Paciente.ConvenioCodigo e o preenche com o convênio atual.
    /// </summary>
    [DbContext(typeof(ClinicaDbContext))]
    [Migration("20260718130000_ConveniosDinamicos")]
    public partial class ConveniosDinamicos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Convenios",
                columns: table => new
                {
                    Codigo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Nome = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Familia = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Convenios", x => x.Codigo);
                });

            // Semeia os quatro convênios embutidos (código = família).
            // Tipos explícitos: estas migrações são escritas à mão e não têm um
            // BuildTargetModel (arquivo .Designer.cs), então o EF não consegue
            // inferir os tipos das colunas pelo modelo ao aplicar o InsertData.
            migrationBuilder.InsertData(
                table: "Convenios",
                columns: new[] { "Codigo", "Nome", "Familia", "Ativo" },
                columnTypes: new[] { "character varying(40)", "character varying(80)", "character varying(40)", "boolean" },
                values: new object[,]
                {
                    { "UnimedPadrao", "Unimed Costa do Sol (Padrão)", "UnimedPadrao", true },
                    { "UnimedIntercambio", "Unimed Costa do Sol Intercâmbio", "UnimedIntercambio", true },
                    { "Amil", "Amil", "Amil", true },
                    { "Petrobras", "Petrobras", "Petrobras", true }
                });

            migrationBuilder.AddColumn<string>(
                name: "ConvenioCodigo",
                table: "Pacientes",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            // Pacientes existentes: código = a família atual (convênio embutido).
            migrationBuilder.Sql(@"UPDATE ""Pacientes"" SET ""ConvenioCodigo"" = ""Convenio"" WHERE ""ConvenioCodigo"" IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ConvenioCodigo", table: "Pacientes");
            migrationBuilder.DropTable(name: "Convenios");
        }
    }
}
