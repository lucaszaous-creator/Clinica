using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConferenciaMateriaisProcedimento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConferenciaConsumoProcedimento",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AtendimentoId = table.Column<int>(type: "integer", nullable: false),
                    SemConsumo = table.Column<bool>(type: "boolean", nullable: false),
                    Pedido = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ConferidoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ConferidoPor = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConferenciaConsumoProcedimento", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConferenciaConsumoProcedimento_Atendimentos_AtendimentoId",
                        column: x => x.AtendimentoId,
                        principalTable: "Atendimentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConferenciaConsumoProcedimento_AtendimentoId",
                table: "ConferenciaConsumoProcedimento",
                column: "AtendimentoId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConferenciaConsumoProcedimento");
        }
    }
}
