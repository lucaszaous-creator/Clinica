using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <summary>
    /// O laudo em ARQUIVO no resultado de exame (set/2026 — a clínica recebe o PDF por
    /// WhatsApp e precisa subi-lo). ADITIVA: três colunas nullable de metadados na linha
    /// do resultado e uma tabela NOVA para os bytes — nada existente é tocado, e linha
    /// antiga fica sem arquivo, que é a verdade.
    ///
    /// Os BYTES ficam à parte pelo padrão do retrato do paciente (PacientesFotos): a
    /// lista de resultados é lida a cada abertura de tela, e um PDF por linha tornaria a
    /// leitura impraticável num banco remoto (a lição da parcela 74).
    ///
    /// Escrita à mão (não há dotnet ef neste ambiente): carimbo MAIOR que todas as
    /// migrations existentes, e principalTable é o nome do DbSet ("ResultadosExame"),
    /// nunca o da classe (42P01, checagem 41).
    /// </summary>
    public partial class LaudoEmArquivoNoResultado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ArquivoNome",
                table: "ResultadosExame",
                type: "character varying(260)",
                maxLength: 260,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArquivoTipoConteudo",
                table: "ResultadosExame",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ArquivoTamanho",
                table: "ResultadosExame",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ArquivosResultadoExame",
                columns: table => new
                {
                    ResultadoExameId = table.Column<int>(type: "integer", nullable: false),
                    Conteudo = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArquivosResultadoExame", x => x.ResultadoExameId);
                    table.ForeignKey(
                        name: "FK_ArquivosResultadoExame_ResultadosExame_ResultadoExameId",
                        column: x => x.ResultadoExameId,
                        principalTable: "ResultadosExame",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArquivosResultadoExame");

            migrationBuilder.DropColumn(
                name: "ArquivoTamanho",
                table: "ResultadosExame");

            migrationBuilder.DropColumn(
                name: "ArquivoTipoConteudo",
                table: "ResultadosExame");

            migrationBuilder.DropColumn(
                name: "ArquivoNome",
                table: "ResultadosExame");
        }
    }
}
