using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class GuiaTissConsultaNaBaixa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GuiasTissConsulta",
                columns: table => new
                {
                    CodigoId = table.Column<int>(type: "integer", nullable: false),
                    RegistroAns = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    NumeroCarteira = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ValidadeCarteira = table.Column<DateOnly>(type: "date", nullable: true),
                    AtendimentoRn = table.Column<bool>(type: "boolean", nullable: false),
                    NomeSocial = table.Column<string>(type: "character varying(70)", maxLength: 70, nullable: true),
                    NomeBeneficiario = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CodigoPrestadorNaOperadora = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    NomeContratado = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Cnes = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    NomeProfissional = table.Column<string>(type: "character varying(70)", maxLength: 70, nullable: false),
                    ConselhoProfissional = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    NumeroConselho = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    UfConselho = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Cbos = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    IndicacaoAcidente = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false),
                    CoberturaEspecial = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    RegimeAtendimento = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    SaudeOcupacional = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    DataAtendimento = table.Column<DateOnly>(type: "date", nullable: false),
                    CodigoTabela = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    TipoConsulta = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false),
                    CodigoProcedimento = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ValorProcedimento = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    Observacao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ConferidaPor = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ConferidaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuiasTissConsulta", x => x.CodigoId);
                    table.ForeignKey(
                        name: "FK_GuiasTissConsulta_Codigos_CodigoId",
                        column: x => x.CodigoId,
                        principalTable: "Codigos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GuiasTissConsulta_NomeProfissional_ConferidaEm",
                table: "GuiasTissConsulta",
                columns: new[] { "NomeProfissional", "ConferidaEm" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GuiasTissConsulta");
        }
    }
}
