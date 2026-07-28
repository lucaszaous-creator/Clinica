using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <summary>
    /// CRM do paciente (parcela 8): de onde ele veio e quem o indicou — a pergunta que a
    /// direção faz no fim do mês e que o sistema não sabia responder.
    ///
    /// PURAMENTE ADITIVA: duas colunas novas e ANULÁVEIS numa tabela existente. Nada é
    /// renomeado, alterado ou removido, e o app de faturamento em produção continua
    /// lendo e gravando Pacientes sem saber que elas existem.
    ///
    /// Anuláveis não é detalhe: null significa "ninguém perguntou", que é diferente de
    /// "Outro". Um padrão preenchido sozinho inventaria a origem da carteira inteira já
    /// cadastrada e daria à direção um relatório de mentira no primeiro mês.
    /// </summary>
    public partial class CrmPaciente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IndicadoPor",
                table: "Pacientes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Origem",
                table: "Pacientes",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IndicadoPor",
                table: "Pacientes");

            migrationBuilder.DropColumn(
                name: "Origem",
                table: "Pacientes");
        }
    }
}
