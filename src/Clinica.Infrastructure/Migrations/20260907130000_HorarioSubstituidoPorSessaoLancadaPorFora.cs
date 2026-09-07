using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <summary>
    /// O HORÁRIO SUBSTITUÍDO POR UMA SESSÃO LANÇADA POR FORA (set/2026) — ADITIVA.
    ///
    /// A terceira resposta da conciliação da agenda ("aconteceu e já foi lançada por
    /// fora") ganhou o status <c>StatusAgendamento.Substituido</c> — gravado como TEXTO,
    /// então sem coluna nova — e esta coluna, que diz QUAL sessão encerrou o horário:
    /// <c>AtendimentoSubstitutoId</c>, FK anulável para <c>Atendimentos</c> com
    /// <c>SetNull</c>, e o índice dela. Nula em toda linha já gravada, que é a verdade.
    ///
    /// Não é <c>AtendimentoId</c> de propósito: o backfill de <c>RealizadoEm</c> só carimba
    /// atendimento cujos horários estão todos em Realizado, e um segundo horário apontando
    /// para o mesmo atendimento por aquela coluna o deixaria de fora para sempre.
    ///
    /// Escrita à mão (sem <c>dotnet ef</c> neste ambiente). O nome das tabelas vem do
    /// <c>DbSet</c> — <c>Agendamentos</c> e <c>Atendimentos</c> —, nunca da classe
    /// (parcela 79).
    /// </summary>
    public partial class HorarioSubstituidoPorSessaoLancadaPorFora : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AtendimentoSubstitutoId",
                table: "Agendamentos",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Agendamentos_AtendimentoSubstitutoId",
                table: "Agendamentos",
                column: "AtendimentoSubstitutoId");

            migrationBuilder.AddForeignKey(
                name: "FK_Agendamentos_Atendimentos_AtendimentoSubstitutoId",
                table: "Agendamentos",
                column: "AtendimentoSubstitutoId",
                principalTable: "Atendimentos",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Agendamentos_Atendimentos_AtendimentoSubstitutoId",
                table: "Agendamentos");

            migrationBuilder.DropIndex(
                name: "IX_Agendamentos_AtendimentoSubstitutoId",
                table: "Agendamentos");

            migrationBuilder.DropColumn(
                name: "AtendimentoSubstitutoId",
                table: "Agendamentos");
        }
    }
}
