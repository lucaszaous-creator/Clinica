using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <summary>
    /// Os ARQUIVOS DA FICHA (set/2026): a receita, o laudo, o exame em PDF que pertence à
    /// pessoa e não a uma sessão — e o acervo do sistema anterior (756 receitas de 113
    /// pacientes), que entra pela importação. ADITIVA: duas tabelas NOVAS, nada existente é
    /// tocado.
    ///
    /// Os BYTES ficam em tabela 1:1 pelo padrão do laudo em arquivo e do retrato do
    /// paciente: a lista é lida a cada abertura de ficha, e um PDF por linha tornaria a
    /// leitura impraticável num banco remoto (a lição da parcela 74).
    ///
    /// Escrita à mão (não há dotnet ef neste ambiente): carimbo MAIOR que todas as
    /// migrations existentes, e principalTable é o nome do DbSet ("Pacientes",
    /// "AnexosPaciente"), nunca o da classe (42P01, checagem 41).
    /// </summary>
    public partial class ArquivosDaFicha : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnexosPaciente",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PacienteId = table.Column<int>(type: "integer", nullable: false),
                    Data = table.Column<DateOnly>(type: "date", nullable: false),
                    Titulo = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    NomeArquivo = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    TipoConteudo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Tamanho = table.Column<int>(type: "integer", nullable: false),
                    Observacoes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ChaveImportacao = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CriadoPor = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    CanceladoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CanceladoPor = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    MotivoCancelamento = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnexosPaciente", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnexosPaciente_Pacientes_PacienteId",
                        column: x => x.PacienteId,
                        principalTable: "Pacientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ArquivosAnexoPaciente",
                columns: table => new
                {
                    AnexoPacienteId = table.Column<int>(type: "integer", nullable: false),
                    Conteudo = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArquivosAnexoPaciente", x => x.AnexoPacienteId);
                    table.ForeignKey(
                        name: "FK_ArquivosAnexoPaciente_AnexosPaciente_AnexoPacienteId",
                        column: x => x.AnexoPacienteId,
                        principalTable: "AnexosPaciente",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnexosPaciente_ChaveImportacao",
                table: "AnexosPaciente",
                column: "ChaveImportacao",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnexosPaciente_PacienteId_Data",
                table: "AnexosPaciente",
                columns: new[] { "PacienteId", "Data" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArquivosAnexoPaciente");

            migrationBuilder.DropTable(
                name: "AnexosPaciente");
        }
    }
}
