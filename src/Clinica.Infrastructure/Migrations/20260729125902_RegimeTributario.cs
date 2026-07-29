using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <summary>
    /// Regime tributario com varios tributos (parcela 15).
    ///
    /// A parcela 9 resolveu o imposto com UMA aliquota global gravada em Configuracoes, e
    /// resolveu mal por dois motivos que so aparecem no escritorio do contador: um numero
    /// somado responde "quanto saiu" e nao "de que" (a clinica no Presumido tem cinco
    /// guias), e uma aliquota sem vigencia muda o passado no dia em que o municipio
    /// reajusta o ISS.
    ///
    /// BasePercentual existe porque nem todo tributo incide sobre a receita inteira: no
    /// Lucro Presumido o IRPJ de 15% incide sobre 32% da receita de servico, e a efetiva
    /// e 4,8%. Sem o campo, a clinica faria essa conta de cabeca — e digitaria 15%, que e
    /// o numero que ela conhece.
    ///
    /// DetalheImposto guarda de QUE e a retencao ("ISS 3% + PIS 0,65%"), COPIADO na
    /// emissao. Remontar o texto na leitura descreveria a retencao de marco com as
    /// aliquotas de setembro.
    ///
    /// A chave AliquotaImpostoRetido NAO e removida: ela continua valendo como fallback
    /// enquanto nao houver nenhum tributo cadastrado. O valor ja esta gravado na base do
    /// cliente, e trocar o mecanismo zerando a retencao faria a clinica emitir um mes
    /// inteiro sem imposto sem perceber.
    ///
    /// PURAMENTE ADITIVA: uma tabela nova e uma coluna ANULAVEL em Lancamentos.
    /// </summary>
    public partial class RegimeTributario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DetalheImposto",
                table: "Lancamentos",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Tributos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Sigla = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Nome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Percentual = table.Column<decimal>(type: "numeric(7,4)", precision: 7, scale: 4, nullable: false),
                    BasePercentual = table.Column<decimal>(type: "numeric(7,4)", precision: 7, scale: 4, nullable: false),
                    VigenteDe = table.Column<DateOnly>(type: "date", nullable: true),
                    VigenteAte = table.Column<DateOnly>(type: "date", nullable: true),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    Observacoes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CriadoPor = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tributos", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tributos_Ativo",
                table: "Tributos",
                column: "Ativo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Tributos");

            migrationBuilder.DropColumn(
                name: "DetalheImposto",
                table: "Lancamentos");
        }
    }
}
