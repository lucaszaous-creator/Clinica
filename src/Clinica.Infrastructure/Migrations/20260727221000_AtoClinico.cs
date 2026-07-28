using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <summary>
    /// Ato clínico (parcela 3): mapa corporal com protocolo reutilizável, e os
    /// documentos impressos da página 21 da proposta (receita, atestado, comparecimento,
    /// pedido de exame, relatório de evolução, consentimento e anamnese) com seus
    /// modelos.
    ///
    /// PURAMENTE ADITIVA: só tabelas novas. Nenhuma coluna existente é renomeada,
    /// alterada ou removida — o faturamento em produção não vê diferença.
    /// </summary>
    [DbContext(typeof(ClinicaDbContext))]
    [Migration("20260727221000_AtoClinico")]
    public partial class AtoClinico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---------- Mapa corporal ----------
            migrationBuilder.CreateTable(
                name: "MapasCorporais",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EvolucaoId = table.Column<int>(type: "integer", nullable: false),
                    ProtocoloOrigemId = table.Column<int>(type: "integer", nullable: true),
                    Observacoes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CriadoPor = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MapasCorporais", x => x.Id);
                    // Mapa órfão não diz de quem nem de quando: some com a sessão.
                    table.ForeignKey(
                        name: "FK_MapasCorporais_Evolucoes_EvolucaoId",
                        column: x => x.EvolucaoId,
                        principalTable: "Evolucoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProtocolosCorporais",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Descricao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PacienteId = table.Column<int>(type: "integer", nullable: true),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CriadoPor = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProtocolosCorporais", x => x.Id);
                    // PacienteId nulo = protocolo da clínica, que nenhuma exclusão alcança.
                    table.ForeignKey(
                        name: "FK_ProtocolosCorporais_Pacientes_PacienteId",
                        column: x => x.PacienteId,
                        principalTable: "Pacientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PontosMapa",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MapaCorporalId = table.Column<int>(type: "integer", nullable: false),
                    Face = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    X = table.Column<double>(type: "double precision", nullable: false),
                    Y = table.Column<double>(type: "double precision", nullable: false),
                    Nome = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Tecnica = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Observacao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Ordem = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PontosMapa", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PontosMapa_MapasCorporais_MapaCorporalId",
                        column: x => x.MapaCorporalId,
                        principalTable: "MapasCorporais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PontosProtocolo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProtocoloCorporalId = table.Column<int>(type: "integer", nullable: false),
                    Face = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    X = table.Column<double>(type: "double precision", nullable: false),
                    Y = table.Column<double>(type: "double precision", nullable: false),
                    Nome = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Tecnica = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Observacao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Ordem = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PontosProtocolo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PontosProtocolo_ProtocolosCorporais_ProtocoloCorporalId",
                        column: x => x.ProtocoloCorporalId,
                        principalTable: "ProtocolosCorporais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // ---------- Documentos clínicos ----------
            migrationBuilder.CreateTable(
                name: "DocumentosClinicos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Numero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CodigoVerificacao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PacienteId = table.Column<int>(type: "integer", nullable: false),
                    ProfissionalId = table.Column<int>(type: "integer", nullable: true),
                    EvolucaoId = table.Column<int>(type: "integer", nullable: true),
                    Data = table.Column<DateOnly>(type: "date", nullable: false),
                    Titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Corpo = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Observacoes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DiasAfastamento = table.Column<int>(type: "integer", nullable: true),
                    Cid = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CidAutorizado = table.Column<bool>(type: "boolean", nullable: false),
                    PeriodoInicio = table.Column<DateOnly>(type: "date", nullable: true),
                    PeriodoFim = table.Column<DateOnly>(type: "date", nullable: true),
                    HoraChegada = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    HoraSaida = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CriadoPor = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    CanceladoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    MotivoCancelamento = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentosClinicos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentosClinicos_Pacientes_PacienteId",
                        column: x => x.PacienteId,
                        principalTable: "Pacientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DocumentosClinicos_Profissionais_ProfissionalId",
                        column: x => x.ProfissionalId,
                        principalTable: "Profissionais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DocumentosClinicos_Evolucoes_EvolucaoId",
                        column: x => x.EvolucaoId,
                        principalTable: "Evolucoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ItensDocumento",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentoClinicoId = table.Column<int>(type: "integer", nullable: false),
                    Ordem = table.Column<int>(type: "integer", nullable: false),
                    Descricao = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Detalhe = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Quantidade = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItensDocumento", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItensDocumento_DocumentosClinicos_DocumentoClinicoId",
                        column: x => x.DocumentoClinicoId,
                        principalTable: "DocumentosClinicos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ModelosDocumento",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Nome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Corpo = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    Ordem = table.Column<int>(type: "integer", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CriadoPor = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelosDocumento", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ItensModelo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ModeloDocumentoId = table.Column<int>(type: "integer", nullable: false),
                    Ordem = table.Column<int>(type: "integer", nullable: false),
                    Descricao = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Detalhe = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Quantidade = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItensModelo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItensModelo_ModelosDocumento_ModeloDocumentoId",
                        column: x => x.ModeloDocumentoId,
                        principalTable: "ModelosDocumento",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // ---------- Índices ----------
            // Um mapa por sessão.
            migrationBuilder.CreateIndex(
                "IX_MapasCorporais_EvolucaoId", "MapasCorporais", "EvolucaoId", unique: true);
            migrationBuilder.CreateIndex("IX_PontosMapa_MapaCorporalId", "PontosMapa", "MapaCorporalId");
            migrationBuilder.CreateIndex(
                "IX_ProtocolosCorporais_PacienteId", "ProtocolosCorporais", "PacienteId");
            migrationBuilder.CreateIndex(
                "IX_PontosProtocolo_ProtocoloCorporalId", "PontosProtocolo", "ProtocoloCorporalId");

            // Número e código de verificação são a identidade da via em papel.
            migrationBuilder.CreateIndex(
                "IX_DocumentosClinicos_Numero", "DocumentosClinicos", "Numero", unique: true);
            migrationBuilder.CreateIndex(
                "IX_DocumentosClinicos_CodigoVerificacao", "DocumentosClinicos",
                "CodigoVerificacao", unique: true);
            migrationBuilder.CreateIndex(
                "IX_DocumentosClinicos_PacienteId_Data", "DocumentosClinicos",
                new[] { "PacienteId", "Data" });
            migrationBuilder.CreateIndex(
                "IX_DocumentosClinicos_ProfissionalId", "DocumentosClinicos", "ProfissionalId");
            migrationBuilder.CreateIndex(
                "IX_DocumentosClinicos_EvolucaoId", "DocumentosClinicos", "EvolucaoId");
            migrationBuilder.CreateIndex(
                "IX_ItensDocumento_DocumentoClinicoId", "ItensDocumento", "DocumentoClinicoId");

            // Salvar modelo com nome já usado sobrescreve em vez de duplicar.
            migrationBuilder.CreateIndex(
                "IX_ModelosDocumento_Tipo_Nome", "ModelosDocumento",
                new[] { "Tipo", "Nome" }, unique: true);
            migrationBuilder.CreateIndex(
                "IX_ItensModelo_ModeloDocumentoId", "ItensModelo", "ModeloDocumentoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable("PontosMapa");
            migrationBuilder.DropTable("PontosProtocolo");
            migrationBuilder.DropTable("ItensDocumento");
            migrationBuilder.DropTable("ItensModelo");
            migrationBuilder.DropTable("MapasCorporais");
            migrationBuilder.DropTable("ProtocolosCorporais");
            migrationBuilder.DropTable("DocumentosClinicos");
            migrationBuilder.DropTable("ModelosDocumento");
        }
    }
}
