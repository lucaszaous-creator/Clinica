using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <summary>
    /// Recebiveis de cartao (parcela 16).
    ///
    /// PrevisaoRecebimento era gravada desde a parcela 9 e NENHUMA tela a lia. E o mesmo
    /// defeito que ja apareceu duas vezes neste projeto — o pacote que debitava e o insumo
    /// que baixava, ambos testados e sem chamador: dado gravado sem leitor passa no CI e
    /// nao faz nada na clinica.
    ///
    /// RecebimentoConfirmadoEm e a data em que o dinheiro CAIU DE FATO, separada da
    /// prevista de proposito: quando a adquirente atrasa, as duas divergem, e sobrescrever
    /// a previsao apagaria justamente a prova de que houve atraso.
    ///
    /// PURAMENTE ADITIVA: uma coluna ANULAVEL e um indice.
    /// </summary>
    public partial class RecebiveisDeCartao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "RecebimentoConfirmadoEm",
                table: "Lancamentos",
                type: "date",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Lancamentos_PrevisaoRecebimento",
                table: "Lancamentos",
                column: "PrevisaoRecebimento");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Lancamentos_PrevisaoRecebimento",
                table: "Lancamentos");

            migrationBuilder.DropColumn(
                name: "RecebimentoConfirmadoEm",
                table: "Lancamentos");
        }
    }
}
