using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <summary>
    /// O MODELO DE EVOLUÇÃO passa a cobrir os NOVE campos da sessão (parcela 76).
    ///
    /// Ele nasceu na parcela 63 com quatro, e a evolução cresceu para nove nas parcelas 73
    /// e 75 sem que o roteiro crescesse junto: quem montava "sessão de acupuntura — lombar"
    /// recebia quatro campos prontos e redigitava os outros cinco toda sessão.
    ///
    /// ADITIVA: cinco colunas anuláveis. Modelo criado antes desta versão fica com NULL, que
    /// é a verdade — não havia onde escrever aqueles campos quando ele foi montado.
    ///
    /// ⚠️ Não há coluna nova em Evolucoes nem em VersoesEvolucao: os cinco campos JÁ existem
    /// lá (parcelas 73 e 75). O que faltava era o roteiro que os preenche.
    /// </summary>
    public partial class ModeloEvolucaoCompleto : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HistoriaDoencaAtual", table: "ModelosEvolucao",
                type: "character varying(4000)", maxLength: 4000, nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExameFisico", table: "ModelosEvolucao",
                type: "character varying(4000)", maxLength: 4000, nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HipoteseDiagnostica", table: "ModelosEvolucao",
                type: "character varying(1000)", maxLength: 1000, nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CidSessao", table: "ModelosEvolucao",
                type: "character varying(20)", maxLength: 20, nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlanoTerapeutico", table: "ModelosEvolucao",
                type: "character varying(1000)", maxLength: 1000, nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "HistoriaDoencaAtual", table: "ModelosEvolucao");
            migrationBuilder.DropColumn(name: "ExameFisico", table: "ModelosEvolucao");
            migrationBuilder.DropColumn(name: "HipoteseDiagnostica", table: "ModelosEvolucao");
            migrationBuilder.DropColumn(name: "CidSessao", table: "ModelosEvolucao");
            migrationBuilder.DropColumn(name: "PlanoTerapeutico", table: "ModelosEvolucao");
        }
    }
}
