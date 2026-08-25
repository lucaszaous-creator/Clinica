using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <summary>
    /// A versão anterior da evolução passa a guardar a ANAMNESE (parcela 74, 2ª rodada).
    ///
    /// Os quatro campos nasceram na parcela 73 dentro de <c>Evolucao</c> e não entraram em
    /// <c>VersaoEvolucao</c>: corrigir uma vírgula da evolução apagava a história da doença
    /// atual, o exame físico, a hipótese e o CID, e a versão guardada não os tinha — o dado
    /// sumia sem rastro, contra o ponto 2 do compromisso de conformidade e o art. 3º da Lei
    /// 13.787/2018.
    ///
    /// ADITIVA: quatro colunas anuláveis. Versão anterior a esta atualização fica com NULL,
    /// que é a verdade — o sistema não guardava esses campos quando ela foi criada.
    /// </summary>
    public partial class VersaoGuardaAAnamnese : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HistoriaDoencaAtual", table: "VersoesEvolucao",
                type: "character varying(4000)", maxLength: 4000, nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExameFisico", table: "VersoesEvolucao",
                type: "character varying(4000)", maxLength: 4000, nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HipoteseDiagnostica", table: "VersoesEvolucao",
                type: "character varying(1000)", maxLength: 1000, nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CidSessao", table: "VersoesEvolucao",
                type: "character varying(20)", maxLength: 20, nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "HistoriaDoencaAtual", table: "VersoesEvolucao");
            migrationBuilder.DropColumn(name: "ExameFisico", table: "VersoesEvolucao");
            migrationBuilder.DropColumn(name: "HipoteseDiagnostica", table: "VersoesEvolucao");
            migrationBuilder.DropColumn(name: "CidSessao", table: "VersoesEvolucao");
        }
    }
}
