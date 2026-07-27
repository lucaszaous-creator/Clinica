using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <summary>
    /// Prontuário e LGPD (parcela 2): evolução clínica com escala EVA antes/depois,
    /// anexos por evolução e o registro datado de consentimento.
    ///
    /// PURAMENTE ADITIVA: só tabelas novas. Nenhuma coluna existente é renomeada,
    /// alterada ou removida — o faturamento em produção não vê diferença.
    /// </summary>
    [DbContext(typeof(ClinicaDbContext))]
    [Migration("20260727200000_ProntuarioELgpd")]
    public partial class ProntuarioELgpd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Evolucoes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PacienteId = table.Column<int>(type: "integer", nullable: false),
                    ProfissionalId = table.Column<int>(type: "integer", nullable: true),
                    AtendimentoId = table.Column<int>(type: "integer", nullable: true),
                    AgendamentoId = table.Column<int>(type: "integer", nullable: true),
                    Data = table.Column<DateOnly>(type: "date", nullable: false),
                    EvaAntes = table.Column<int>(type: "integer", nullable: true),
                    EvaDepois = table.Column<int>(type: "integer", nullable: true),
                    QueixaPrincipal = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Conduta = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    TextoEvolucao = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Orientacoes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CriadoPor = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Evolucoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Evolucoes_Pacientes_PacienteId",
                        column: x => x.PacienteId,
                        principalTable: "Pacientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Evolucoes_Profissionais_ProfissionalId",
                        column: x => x.ProfissionalId,
                        principalTable: "Profissionais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Evolucoes_Atendimentos_AtendimentoId",
                        column: x => x.AtendimentoId,
                        principalTable: "Atendimentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Evolucoes_Agendamentos_AgendamentoId",
                        column: x => x.AgendamentoId,
                        principalTable: "Agendamentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AnexosProntuario",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EvolucaoId = table.Column<int>(type: "integer", nullable: false),
                    NomeArquivo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TipoConteudo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Conteudo = table.Column<byte[]>(type: "bytea", nullable: false),
                    Tamanho = table.Column<int>(type: "integer", nullable: false),
                    Descricao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CriadoPor = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnexosProntuario", x => x.Id);
                    // Anexo órfão não é prontuário: apagar a evolução leva os arquivos.
                    table.ForeignKey(
                        name: "FK_AnexosProntuario_Evolucoes_EvolucaoId",
                        column: x => x.EvolucaoId,
                        principalTable: "Evolucoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Consentimentos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PacienteId = table.Column<int>(type: "integer", nullable: false),
                    Finalidade = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Concedido = table.Column<bool>(type: "boolean", nullable: false),
                    VersaoTermo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    RegistradoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    RegistradoPor = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    RevogadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Observacoes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Consentimentos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Consentimentos_Pacientes_PacienteId",
                        column: x => x.PacienteId,
                        principalTable: "Pacientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex("IX_Evolucoes_ProfissionalId", "Evolucoes", "ProfissionalId");
            migrationBuilder.CreateIndex("IX_Evolucoes_AtendimentoId", "Evolucoes", "AtendimentoId");
            migrationBuilder.CreateIndex("IX_Evolucoes_AgendamentoId", "Evolucoes", "AgendamentoId");
            migrationBuilder.CreateIndex(
                "IX_Evolucoes_PacienteId_Data", "Evolucoes", new[] { "PacienteId", "Data" });

            migrationBuilder.CreateIndex("IX_AnexosProntuario_EvolucaoId", "AnexosProntuario", "EvolucaoId");

            migrationBuilder.CreateIndex(
                "IX_Consentimentos_PacienteId_Finalidade", "Consentimentos",
                new[] { "PacienteId", "Finalidade" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable("AnexosProntuario");
            migrationBuilder.DropTable("Consentimentos");
            migrationBuilder.DropTable("Evolucoes");
        }
    }
}
