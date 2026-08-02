using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <summary>
    /// Parcela 36 — o consultório do profissional.
    ///
    /// PURAMENTE ADITIVA: cria <c>AvaliacoesClinicas</c> e <c>RespostasAvaliacao</c> e não
    /// altera uma coluna sequer das tabelas existentes. É a regra de convivência entre
    /// versões diferentes instaladas na clínica (ver docs/arquitetura-multi-exe.md): o
    /// faturamento congelado, que desconhece estas tabelas, continua subindo sobre este
    /// banco sem saber que elas existem.
    ///
    /// A especialidade nova (Neurocirurgia) NÃO precisou de migration: o enum é gravado
    /// como texto, e o catálogo garante as embutidas na leitura.
    /// </summary>
    public partial class AvaliacoesClinicas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AvaliacoesClinicas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PacienteId = table.Column<int>(type: "integer", nullable: false),
                    ProfissionalId = table.Column<int>(type: "integer", nullable: true),
                    EvolucaoId = table.Column<int>(type: "integer", nullable: true),
                    Data = table.Column<DateOnly>(type: "date", nullable: false),
                    InstrumentoCodigo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    InstrumentoNome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    EspecialidadeCodigo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Pontuacao = table.Column<int>(type: "integer", nullable: false),
                    PontuacaoMaxima = table.Column<int>(type: "integer", nullable: false),
                    Unidade = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    FaixaNome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    FaixaInterpretacao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FaixaGravidade = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AlertaItem = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Observacoes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CriadoPor = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AvaliacoesClinicas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AvaliacoesClinicas_Evolucoes_EvolucaoId",
                        column: x => x.EvolucaoId,
                        principalTable: "Evolucoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AvaliacoesClinicas_Pacientes_PacienteId",
                        column: x => x.PacienteId,
                        principalTable: "Pacientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AvaliacoesClinicas_Profissionais_ProfissionalId",
                        column: x => x.ProfissionalId,
                        principalTable: "Profissionais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "RespostasAvaliacao",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AvaliacaoClinicaId = table.Column<int>(type: "integer", nullable: false),
                    Ordem = table.Column<int>(type: "integer", nullable: false),
                    ItemCodigo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Enunciado = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Valor = table.Column<int>(type: "integer", nullable: false),
                    OpcaoRotulo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RespostasAvaliacao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RespostasAvaliacao_AvaliacoesClinicas_AvaliacaoClinicaId",
                        column: x => x.AvaliacaoClinicaId,
                        principalTable: "AvaliacoesClinicas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AvaliacoesClinicas_EvolucaoId",
                table: "AvaliacoesClinicas",
                column: "EvolucaoId");

            migrationBuilder.CreateIndex(
                name: "IX_AvaliacoesClinicas_PacienteId_InstrumentoCodigo_Data",
                table: "AvaliacoesClinicas",
                columns: new[] { "PacienteId", "InstrumentoCodigo", "Data" });

            migrationBuilder.CreateIndex(
                name: "IX_AvaliacoesClinicas_ProfissionalId",
                table: "AvaliacoesClinicas",
                column: "ProfissionalId");

            migrationBuilder.CreateIndex(
                name: "IX_RespostasAvaliacao_AvaliacaoClinicaId",
                table: "RespostasAvaliacao",
                column: "AvaliacaoClinicaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RespostasAvaliacao");

            migrationBuilder.DropTable(
                name: "AvaliacoesClinicas");
        }
    }
}
