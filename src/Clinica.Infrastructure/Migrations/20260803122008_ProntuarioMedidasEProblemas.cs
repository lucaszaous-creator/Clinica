using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <summary>
    /// Parcela 37 — o prontuário que o Consultório precisava: medidas seriadas e lista de
    /// problemas.
    ///
    /// PURAMENTE ADITIVA: cria <c>MedidasClinicas</c> e <c>ProblemasPaciente</c> e não
    /// altera uma coluna sequer das tabelas existentes. É a regra de convivência entre
    /// versões diferentes instaladas na clínica (ver docs/arquitetura-multi-exe.md): o
    /// faturamento congelado, que desconhece estas tabelas, continua subindo sobre este
    /// banco sem saber que elas existem.
    ///
    /// O catálogo de medidas (peso, cintura, PA, glicemia, HbA1c) NÃO virou tabela, pelo
    /// mesmo desenho das escalas clínicas: o corte do IMC é definição publicada, não
    /// configuração da clínica — e a única coisa que precisa sobreviver a uma mudança dele
    /// é o que foi COPIADO na colheita, que são colunas desta tabela.
    /// </summary>
    public partial class ProntuarioMedidasEProblemas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MedidasClinicas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PacienteId = table.Column<int>(type: "integer", nullable: false),
                    ProfissionalId = table.Column<int>(type: "integer", nullable: true),
                    EvolucaoId = table.Column<int>(type: "integer", nullable: true),
                    Data = table.Column<DateOnly>(type: "date", nullable: false),
                    TipoCodigo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TipoNome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Unidade = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Valor = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: false),
                    ValorSecundario = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: true),
                    FaixaNome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    FaixaInterpretacao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FaixaGravidade = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Observacoes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CriadoPor = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedidasClinicas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MedidasClinicas_Evolucoes_EvolucaoId",
                        column: x => x.EvolucaoId,
                        principalTable: "Evolucoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MedidasClinicas_Pacientes_PacienteId",
                        column: x => x.PacienteId,
                        principalTable: "Pacientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MedidasClinicas_Profissionais_ProfissionalId",
                        column: x => x.ProfissionalId,
                        principalTable: "Profissionais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ProblemasPaciente",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PacienteId = table.Column<int>(type: "integer", nullable: false),
                    ProfissionalId = table.Column<int>(type: "integer", nullable: true),
                    EvolucaoId = table.Column<int>(type: "integer", nullable: true),
                    Natureza = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: false),
                    Descricao = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Cid = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    Situacao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Inicio = table.Column<DateOnly>(type: "date", nullable: true),
                    Fim = table.Column<DateOnly>(type: "date", nullable: true),
                    Observacoes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    MotivoDescarte = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CriadoPor = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    AtualizadoPor = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProblemasPaciente", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProblemasPaciente_Evolucoes_EvolucaoId",
                        column: x => x.EvolucaoId,
                        principalTable: "Evolucoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProblemasPaciente_Pacientes_PacienteId",
                        column: x => x.PacienteId,
                        principalTable: "Pacientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProblemasPaciente_Profissionais_ProfissionalId",
                        column: x => x.ProfissionalId,
                        principalTable: "Profissionais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MedidasClinicas_EvolucaoId",
                table: "MedidasClinicas",
                column: "EvolucaoId");

            migrationBuilder.CreateIndex(
                name: "IX_MedidasClinicas_PacienteId_TipoCodigo_Data",
                table: "MedidasClinicas",
                columns: new[] { "PacienteId", "TipoCodigo", "Data" });

            migrationBuilder.CreateIndex(
                name: "IX_MedidasClinicas_ProfissionalId",
                table: "MedidasClinicas",
                column: "ProfissionalId");

            migrationBuilder.CreateIndex(
                name: "IX_ProblemasPaciente_EvolucaoId",
                table: "ProblemasPaciente",
                column: "EvolucaoId");

            migrationBuilder.CreateIndex(
                name: "IX_ProblemasPaciente_PacienteId_Situacao",
                table: "ProblemasPaciente",
                columns: new[] { "PacienteId", "Situacao" });

            migrationBuilder.CreateIndex(
                name: "IX_ProblemasPaciente_ProfissionalId",
                table: "ProblemasPaciente",
                column: "ProfissionalId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MedidasClinicas");

            migrationBuilder.DropTable(
                name: "ProblemasPaciente");
        }
    }
}
