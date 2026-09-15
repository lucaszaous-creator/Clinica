using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EtapasDoFechamentoDaSessao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EtapasFechamentoSessao",
                columns: table => new
                {
                    AtendimentoId = table.Column<int>(type: "integer", nullable: false),
                    Etapa = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Pedido = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ResultadoId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EtapasFechamentoSessao", x => new { x.AtendimentoId, x.Etapa });
                    table.ForeignKey(
                        name: "FK_EtapasFechamentoSessao_Atendimentos_AtendimentoId",
                        column: x => x.AtendimentoId,
                        principalTable: "Atendimentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EtapasFechamentoSessao");
        }
    }
}
