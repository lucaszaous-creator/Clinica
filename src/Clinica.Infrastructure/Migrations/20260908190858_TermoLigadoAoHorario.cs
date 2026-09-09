using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <summary>
    /// O TERMO LIGADO À SESSÃO (set/2026) — <c>DocumentoClinico.AgendamentoId</c>.
    ///
    /// ADITIVA: uma coluna anulável e a chave estrangeira dela. Nasce vazia em toda linha
    /// já gravada, e vazio é a verdade delas — nenhuma foi colhida sabendo a que horário se
    /// referia. Vazio CONTINUA legítimo depois desta versão: é o termo avulso, colhido
    /// semanas antes do procedimento, e é a receita emitida fora de sessão.
    ///
    /// <c>SetNull</c>, nunca cascata: apagar um horário não pode levar junto o termo que o
    /// paciente assinou. Registro clínico não se apaga (Lei 13.787/2018), e a cascata é
    /// justamente o caminho por onde ele sumiria sem ninguém pedir — foi o que a parcela 60
    /// achou no botão de excluir paciente.
    ///
    /// O nome das tabelas vem do <c>DbSet</c> — <c>DocumentosClinicos</c> e
    /// <c>Agendamentos</c> —, nunca da classe (a lição da parcela 79, hoje cobrada pela
    /// checagem 41).
    /// </summary>
    public partial class TermoLigadoAoHorario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AgendamentoId",
                table: "DocumentosClinicos",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentosClinicos_AgendamentoId",
                table: "DocumentosClinicos",
                column: "AgendamentoId");

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentosClinicos_Agendamentos_AgendamentoId",
                table: "DocumentosClinicos",
                column: "AgendamentoId",
                principalTable: "Agendamentos",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DocumentosClinicos_Agendamentos_AgendamentoId",
                table: "DocumentosClinicos");

            migrationBuilder.DropIndex(
                name: "IX_DocumentosClinicos_AgendamentoId",
                table: "DocumentosClinicos");

            migrationBuilder.DropColumn(
                name: "AgendamentoId",
                table: "DocumentosClinicos");
        }
    }
}
