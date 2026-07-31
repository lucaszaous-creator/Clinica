using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <summary>
    /// Metas mensais da direcao (parcela 28).
    ///
    /// Ate aqui o painel comparava o mes com o MES ANTERIOR — variacao, e nao desempenho.
    /// Variacao responde "melhorou?" e nunca responde "chegamos onde a gente disse que ia
    /// chegar?", que e a unica pergunta capaz de mudar a conduta no dia 12 em vez de
    /// revelar o problema no dia 31.
    ///
    /// E TABELA, e nao chave de parametro, porque meta e FATO DATADO: subir a meta em
    /// marco nao pode reescrever o alvo de janeiro, senao a clinica perde a prova do que
    /// tinha combinado na epoca. Mesma razao da vigencia da taxa de cartao e do preco do
    /// convenio.
    ///
    /// O indice unico (ano, mes, indicador, profissional) e a regra no BANCO: duas metas
    /// para a mesma pergunta dariam dois alvos, e a tela escolheria um sem dizer qual.
    /// ProfissionalId nulo = meta da clinica inteira.
    ///
    /// PURAMENTE ADITIVA: tabela nova, nenhuma coluna existente tocada. O faturamento
    /// congelado nao le nem escreve nela.
    /// </summary>
    public partial class MetasMensais : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Metas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Ano = table.Column<int>(type: "integer", nullable: false),
                    Mes = table.Column<int>(type: "integer", nullable: false),
                    Indicador = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Valor = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    ProfissionalId = table.Column<int>(type: "integer", nullable: true),
                    Observacoes = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CriadoPor = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Metas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Metas_Profissionais_ProfissionalId",
                        column: x => x.ProfissionalId,
                        principalTable: "Profissionais",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Metas_Ano_Mes_Indicador_ProfissionalId",
                table: "Metas",
                columns: new[] { "Ano", "Mes", "Indicador", "ProfissionalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Metas_ProfissionalId",
                table: "Metas",
                column: "ProfissionalId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Metas");
        }
    }
}
