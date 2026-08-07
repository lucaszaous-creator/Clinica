using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <summary>
    /// Formato do número da guia por convênio (parcela 45).
    ///
    /// ADITIVA, como toda migration que alcança o app de faturamento: acrescenta coluna e
    /// não renomeia nem remove nada (checagem 18 do verificar-suite).
    ///
    /// Duas decisões moram aqui e não no C#:
    ///
    /// 1. O default é "SemValidacao", NUNCA a string vazia que o EF gera sozinho para
    ///    coluna de texto obrigatória. O enum é gravado como TEXTO, e uma linha com ""
    ///    estouraria na leitura — a tela de Configurações não abriria mais, num app em
    ///    produção, por causa de um valor que ninguém escolheu.
    ///
    /// 2. O UPDATE semeia os embutidos com o que a cliente informou (Unimed só números;
    ///    Petrobras e Amil alfanumérico). Sem ele, a clínica atualizaria, a regra nasceria
    ///    desligada em todos os convênios e a validação pedida só passaria a valer no dia
    ///    em que alguém abrisse Configurações — que pode ser nunca.
    ///    Convênio PERSONALIZADO fica de fora de propósito: "Sul América" também é
    ///    alfanumérico, mas quem sabe quais linhas personalizadas existem nesta base é a
    ///    clínica, e adivinhar o formato de uma operadora pelo tipo dela recusaria baixa
    ///    legítima de outra. Ele fica em "sem validação" e a direção marca na tela.
    /// </summary>
    public partial class FormatoNumeroGuiaPorConvenio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FormatoNumeroGuia",
                table: "Convenios",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "SemValidacao");

            migrationBuilder.Sql(
                """
                UPDATE "Convenios"
                   SET "FormatoNumeroGuia" = 'SomenteNumeros'
                 WHERE "Familia" IN ('UnimedPadrao', 'UnimedIntercambio');
                """);

            migrationBuilder.Sql(
                """
                UPDATE "Convenios"
                   SET "FormatoNumeroGuia" = 'Alfanumerico'
                 WHERE "Familia" IN ('Petrobras', 'Amil');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FormatoNumeroGuia",
                table: "Convenios");
        }
    }
}
