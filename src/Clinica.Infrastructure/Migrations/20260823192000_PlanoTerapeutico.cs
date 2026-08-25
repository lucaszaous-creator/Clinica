using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <summary>
    /// O PLANO TERAPÊUTICO da sessão, e a versão que o guarda (parcela 75).
    ///
    /// ADITIVA: duas colunas anuláveis. Sessão anterior a esta versão fica com NULL, que é a
    /// verdade — o sistema não tinha onde registrar o plano quando ela foi escrita.
    /// </summary>
    public partial class PlanoTerapeutico : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PlanoTerapeutico", table: "Evolucoes",
                type: "character varying(1000)", maxLength: 1000, nullable: true);

            // ⚠️ A VERSÃO junto, no MESMO commit (lugar 4 da auditoria de linha): sem ela,
            // corrigir a sessão apagaria o plano anterior sem rastro — contra o art. 3º da
            // Lei 13.787/2018, que exige que a retificação seja rastreável.
            migrationBuilder.AddColumn<string>(
                name: "PlanoTerapeutico", table: "VersoesEvolucao",
                type: "character varying(1000)", maxLength: 1000, nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "PlanoTerapeutico", table: "Evolucoes");
            migrationBuilder.DropColumn(name: "PlanoTerapeutico", table: "VersoesEvolucao");
        }
    }
}
