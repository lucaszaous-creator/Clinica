using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <summary>
    /// A importação do PACOTE do Smart Clinic (set/2026): o prontuário antigo vira evolução
    /// importada e os horários futuros da agenda antiga viram horário aqui. Duas chaves de
    /// idempotência (colunas nullable + índice único sobre coluna que nasce VAZIA — não tem
    /// como falhar na abertura) e o alargamento de seis textos do prontuário.
    ///
    /// MIGRATION-NAO-ADITIVA-CONSCIENTE(AlterColumn): alarga quatro colunas de Evolucoes e
    /// duas de VersoesEvolucao de varchar(4000) para text — o prontuário antigo tem 88
    /// registros acima de 4.000 caracteres (o maior, 11.221), e cortar registro clínico na
    /// importação é perder o que a clínica pediu para não perder. Alargar nunca perde linha:
    /// tudo o que cabia em varchar(4000) cabe em text.
    ///
    /// Escrita à mão (não há dotnet ef neste ambiente): carimbo MAIOR que todas as
    /// migrations existentes; nomes de tabela são os dos DbSets ("Evolucoes",
    /// "VersoesEvolucao", "Agendamentos" — checagem 41).
    /// </summary>
    public partial class ImportacaoDoProntuarioEDaAgenda : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChaveImportacao",
                table: "Evolucoes",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Evolucoes_ChaveImportacao",
                table: "Evolucoes",
                column: "ChaveImportacao",
                unique: true);

            migrationBuilder.AddColumn<string>(
                name: "ChaveImportacao",
                table: "Agendamentos",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Agendamentos_ChaveImportacao",
                table: "Agendamentos",
                column: "ChaveImportacao",
                unique: true);

            foreach (var coluna in new[] { "HistoriaDoencaAtual", "ExameFisico", "Conduta", "TextoEvolucao" })
                migrationBuilder.AlterColumn<string>(
                    name: coluna,
                    table: "Evolucoes",
                    type: "text",
                    nullable: true,
                    oldClrType: typeof(string),
                    oldType: "character varying(4000)",
                    oldMaxLength: 4000,
                    oldNullable: true);

            foreach (var coluna in new[] { "HistoriaDoencaAtual", "ExameFisico" })
                migrationBuilder.AlterColumn<string>(
                    name: coluna,
                    table: "VersoesEvolucao",
                    type: "text",
                    nullable: true,
                    oldClrType: typeof(string),
                    oldType: "character varying(4000)",
                    oldMaxLength: 4000,
                    oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // O caminho de volta encolhe colunas: só é seguro enquanto nenhum texto passar
            // de 4.000 caracteres — depois da importação, não é.
            foreach (var coluna in new[] { "HistoriaDoencaAtual", "ExameFisico", "Conduta", "TextoEvolucao" })
                migrationBuilder.AlterColumn<string>(
                    name: coluna,
                    table: "Evolucoes",
                    type: "character varying(4000)",
                    maxLength: 4000,
                    nullable: true,
                    oldClrType: typeof(string),
                    oldType: "text",
                    oldNullable: true);

            foreach (var coluna in new[] { "HistoriaDoencaAtual", "ExameFisico" })
                migrationBuilder.AlterColumn<string>(
                    name: coluna,
                    table: "VersoesEvolucao",
                    type: "character varying(4000)",
                    maxLength: 4000,
                    nullable: true,
                    oldClrType: typeof(string),
                    oldType: "text",
                    oldNullable: true);

            migrationBuilder.DropIndex(name: "IX_Agendamentos_ChaveImportacao", table: "Agendamentos");
            migrationBuilder.DropColumn(name: "ChaveImportacao", table: "Agendamentos");
            migrationBuilder.DropIndex(name: "IX_Evolucoes_ChaveImportacao", table: "Evolucoes");
            migrationBuilder.DropColumn(name: "ChaveImportacao", table: "Evolucoes");
        }
    }
}
