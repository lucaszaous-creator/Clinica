using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RepasseFechamentoPorGuiaRecebida : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TipoGuia",
                table: "RegrasRepasse",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RepassesFechamentos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProfissionalId = table.Column<int>(type: "integer", nullable: false),
                    Competencia = table.Column<DateOnly>(type: "date", nullable: false),
                    RecebidosAte = table.Column<DateOnly>(type: "date", nullable: false),
                    ProfissionalNome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProfissionalRegistro = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Quantidade = table.Column<int>(type: "integer", nullable: false),
                    Valor = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    ConferidoPor = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ConferidoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Observacoes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    LancamentoFinanceiroId = table.Column<int>(type: "integer", nullable: true),
                    CanceladoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CanceladoPor = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    MotivoCancelamento = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepassesFechamentos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RepassesFechamentos_Lancamentos_LancamentoFinanceiroId",
                        column: x => x.LancamentoFinanceiroId,
                        principalTable: "Lancamentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RepassesFechamentos_Profissionais_ProfissionalId",
                        column: x => x.ProfissionalId,
                        principalTable: "Profissionais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ItensRepasse",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RepasseFechamentoId = table.Column<int>(type: "integer", nullable: false),
                    Origem = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Natureza = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Chave = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CanceladoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    AtendimentoId = table.Column<int>(type: "integer", nullable: false),
                    CodigoFaturamentoId = table.Column<int>(type: "integer", nullable: true),
                    LancamentoReceitaId = table.Column<int>(type: "integer", nullable: true),
                    ItemEstornadoId = table.Column<int>(type: "integer", nullable: true),
                    PacienteId = table.Column<int>(type: "integer", nullable: false),
                    PacienteNome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DataAtendimento = table.Column<DateOnly>(type: "date", nullable: false),
                    ConvenioCodigo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ConvenioNome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    TipoGuia = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Descricao = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NumeroGuia = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    ReferenciaPagamento = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    DataRecebimento = table.Column<DateOnly>(type: "date", nullable: false),
                    ValorRecebido = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    RegraRepasseId = table.Column<int>(type: "integer", nullable: true),
                    RegraDescricao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Valor = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItensRepasse", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItensRepasse_RepassesFechamentos_RepasseFechamentoId",
                        column: x => x.RepasseFechamentoId,
                        principalTable: "RepassesFechamentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ItensRepasse_AtendimentoId",
                table: "ItensRepasse",
                column: "AtendimentoId");

            migrationBuilder.CreateIndex(
                name: "IX_ItensRepasse_Chave",
                table: "ItensRepasse",
                column: "Chave",
                unique: true,
                filter: "\"CanceladoEm\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ItensRepasse_CodigoFaturamentoId",
                table: "ItensRepasse",
                column: "CodigoFaturamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_ItensRepasse_RepasseFechamentoId",
                table: "ItensRepasse",
                column: "RepasseFechamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_RepassesFechamentos_Competencia",
                table: "RepassesFechamentos",
                column: "Competencia");

            migrationBuilder.CreateIndex(
                name: "IX_RepassesFechamentos_LancamentoFinanceiroId",
                table: "RepassesFechamentos",
                column: "LancamentoFinanceiroId");

            migrationBuilder.CreateIndex(
                name: "IX_RepassesFechamentos_ProfissionalId_Competencia",
                table: "RepassesFechamentos",
                columns: new[] { "ProfissionalId", "Competencia" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ItensRepasse");

            migrationBuilder.DropTable(
                name: "RepassesFechamentos");

            migrationBuilder.DropColumn(
                name: "TipoGuia",
                table: "RegrasRepasse");
        }
    }
}
