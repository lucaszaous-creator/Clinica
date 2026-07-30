using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <summary>
    /// Tabela de preco por convenio (parcela 20).
    ///
    /// O valor da guia era DIGITADO A MAO em cada conciliacao. Para quem fatura volume de
    /// convenio isso tem tres consequencias: erro de digitacao vira dinheiro perdido em
    /// silencio (um R$ 45 no lugar de R$ 145 nao e recusado por ninguem), a conciliacao e
    /// digitacao em vez de CONFERENCIA, e ninguem sabia o que a operadora deveria pagar.
    ///
    /// CADASTRADA NO GERENTE, LIDA PELO FINANCEIRO: quem negocia tabela e a direcao, quem
    /// concilia guia e o balcao. Mesmo banco, sem sincronizacao nem copia.
    ///
    /// A vigencia e o que faz o cadastro valer a pena: a operadora reajusta, e o que vale
    /// na guia de marco e o valor de marco. O valor daqui e so a ORIGEM do numero — o que
    /// vale no caixa e o valor COPIADO no lancamento.
    ///
    /// PURAMENTE ADITIVA: uma tabela nova, nenhuma coluna existente tocada.
    /// </summary>
    public partial class TabelaPrecoConvenio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PrecosConvenio",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ConvenioCodigo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Tipo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Especialidade = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Valor = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    VigenteDe = table.Column<DateOnly>(type: "date", nullable: true),
                    VigenteAte = table.Column<DateOnly>(type: "date", nullable: true),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    Observacoes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CriadoPor = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrecosConvenio", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PrecosConvenio_Ativo",
                table: "PrecosConvenio",
                column: "Ativo");

            migrationBuilder.CreateIndex(
                name: "IX_PrecosConvenio_ConvenioCodigo_Tipo",
                table: "PrecosConvenio",
                columns: new[] { "ConvenioCodigo", "Tipo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PrecosConvenio");
        }
    }
}
