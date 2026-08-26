using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <summary>
    /// O CÓDIGO DA LINHA DO DOCUMENTO (parcela 89) — o que faz a resposta do paciente no
    /// termo LGPD poder ser LIDA DE VOLTA como consentimento.
    ///
    /// Casar a resposta pela ORDEM seria o contrato de índice que a parcela 41 trocou por
    /// nome: acrescentar uma finalidade no meio empurraria as outras, e o "Sim" do uso de
    /// imagem viraria autorização para compartilhar com o convênio — sem quebrar build.
    ///
    /// ADITIVA: coluna nova e ANULÁVEL. Nulo é o caso normal (receita, atestado e as
    /// outras impressões não são lidas de volta) e é também o que as linhas já gravadas
    /// valem — não há `defaultValue` a escolher, que é a armadilha da parcela 60.
    /// </summary>
    public partial class CodigoDoItemDeDocumento : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
            => migrationBuilder.AddColumn<string>(
                name: "Codigo",
                table: "ItensDocumento",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

        protected override void Down(MigrationBuilder migrationBuilder)
            => migrationBuilder.DropColumn(name: "Codigo", table: "ItensDocumento");
    }
}
