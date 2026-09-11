using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <summary>
    /// A ASSINATURA QUE CHEGOU DO CELULAR PASSA A SER GUARDADA (set/2026).
    ///
    /// Até aqui o traço do paciente vivia SÓ no balde, e quem o trazia para dentro era a
    /// janela do termo aberta na hora em que a resposta chegou. Quem enviava o link e
    /// fechava a janela perdia a assinatura: a varredura de 24 h cancelava a coleta e
    /// apagava o objeto, com o paciente tendo assinado de verdade. Nada falhava — o termo
    /// continuava "pendente", como se ninguém tivesse assinado nada.
    ///
    /// ADITIVA: duas colunas anuláveis e a chave estrangeira do traço. Nascem vazias em
    /// toda linha já gravada, e vazio é a verdade delas — nenhuma coleta anterior guardou
    /// resposta nenhuma.
    ///
    /// <c>SetNull</c> no traço, como no documento assinado: apagar um traço não pode levar
    /// junto a linha da coleta, que é a EVIDÊNCIA do canal e não se apaga nunca.
    ///
    /// O nome das tabelas vem do <c>DbSet</c> — <c>ColetasRemotasTermo</c> e
    /// <c>TracosAssinatura</c> —, nunca da classe (a lição da parcela 79, hoje cobrada pela
    /// checagem 41).
    /// </summary>
    public partial class AssinaturaRemotaGuardadaAntesDaConferencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RespostasJson",
                table: "ColetasRemotasTermo",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TracoAssinaturaId",
                table: "ColetasRemotasTermo",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ColetasRemotasTermo_TracoAssinaturaId",
                table: "ColetasRemotasTermo",
                column: "TracoAssinaturaId");

            migrationBuilder.AddForeignKey(
                name: "FK_ColetasRemotasTermo_TracosAssinatura_TracoAssinaturaId",
                table: "ColetasRemotasTermo",
                column: "TracoAssinaturaId",
                principalTable: "TracosAssinatura",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ColetasRemotasTermo_TracosAssinatura_TracoAssinaturaId",
                table: "ColetasRemotasTermo");

            migrationBuilder.DropIndex(
                name: "IX_ColetasRemotasTermo_TracoAssinaturaId",
                table: "ColetasRemotasTermo");

            migrationBuilder.DropColumn(
                name: "TracoAssinaturaId",
                table: "ColetasRemotasTermo");

            migrationBuilder.DropColumn(
                name: "RespostasJson",
                table: "ColetasRemotasTermo");
        }
    }
}
