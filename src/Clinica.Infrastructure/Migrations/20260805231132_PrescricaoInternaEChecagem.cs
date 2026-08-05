using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <summary>
    /// Parcela 42 — a prescrição de execução interna, a checagem de enfermagem e a
    /// assinatura ICP-Brasil.
    ///
    /// PURAMENTE ADITIVA: cinco tabelas novas (<c>PrescricoesInternas</c>,
    /// <c>ItensPrescricaoInterna</c>, <c>ChecagensPrescricao</c>,
    /// <c>AssinaturasDocumento</c>, <c>ArquivosAssinados</c>) e uma coluna anulável em
    /// <c>Profissionais</c> (<c>Cpf</c>, usada para amarrar o certificado digital à
    /// pessoa). O faturamento congelado não conhece nenhuma delas e continua subindo
    /// sobre este banco sem saber que existem.
    ///
    /// <c>ArquivosAssinados</c> é tabela à parte do <c>AssinaturasDocumento</c> de
    /// propósito, pela mesma razão de <c>PacientesFotos</c>: o PDF assinado tem centenas
    /// de KB, e a lista da sala de infusão carrega dezenas de prescrições para desenhar
    /// linhas de texto. E os bytes ficam guardados EXATAMENTE como saíram — a assinatura
    /// PKCS#7 cobre uma faixa de bytes do arquivo, então regenerar o PDF "igual" na
    /// reimpressão produziria um arquivo cuja assinatura não confere.
    /// </summary>
    public partial class PrescricaoInternaEChecagem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Cpf",
                table: "Profissionais",
                type: "character varying(11)",
                maxLength: 11,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ArquivosAssinados",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Conteudo = table.Column<byte[]>(type: "bytea", nullable: false),
                    NomeArquivo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    GeradoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArquivosAssinados", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PrescricoesInternas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Numero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CodigoVerificacao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PacienteId = table.Column<int>(type: "integer", nullable: false),
                    ProfissionalId = table.Column<int>(type: "integer", nullable: true),
                    AgendamentoId = table.Column<int>(type: "integer", nullable: true),
                    EvolucaoId = table.Column<int>(type: "integer", nullable: true),
                    Data = table.Column<DateOnly>(type: "date", nullable: false),
                    Hora = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    Situacao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Indicacao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Observacoes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AssinadaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    EncerradaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CanceladaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    MotivoCancelamento = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CriadoPor = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    AtualizadoPor = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrescricoesInternas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrescricoesInternas_Agendamentos_AgendamentoId",
                        column: x => x.AgendamentoId,
                        principalTable: "Agendamentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PrescricoesInternas_Evolucoes_EvolucaoId",
                        column: x => x.EvolucaoId,
                        principalTable: "Evolucoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PrescricoesInternas_Pacientes_PacienteId",
                        column: x => x.PacienteId,
                        principalTable: "Pacientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PrescricoesInternas_Profissionais_ProfissionalId",
                        column: x => x.ProfissionalId,
                        principalTable: "Profissionais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AssinaturasDocumento",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PrescricaoInternaId = table.Column<int>(type: "integer", nullable: false),
                    Papel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    HashConteudo = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    AlgoritmoHash = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AssinadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UsuarioId = table.Column<int>(type: "integer", nullable: true),
                    NomeAssinante = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    RegistroConselho = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    CpfAssinante = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    CertificadoTitular = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CertificadoEmissor = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CertificadoSerie = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    CertificadoValidoDe = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CertificadoValidoAte = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CarimboTempoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CarimboTempoAutoridade = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ArquivoId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssinaturasDocumento", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssinaturasDocumento_ArquivosAssinados_ArquivoId",
                        column: x => x.ArquivoId,
                        principalTable: "ArquivosAssinados",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AssinaturasDocumento_PrescricoesInternas_PrescricaoInternaId",
                        column: x => x.PrescricaoInternaId,
                        principalTable: "PrescricoesInternas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssinaturasDocumento_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ItensPrescricaoInterna",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PrescricaoInternaId = table.Column<int>(type: "integer", nullable: false),
                    Ordem = table.Column<int>(type: "integer", nullable: false),
                    Descricao = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Dose = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Diluente = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Volume = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Via = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TempoInfusao = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    HoraPrevista = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    SeNecessario = table.Column<bool>(type: "boolean", nullable: false),
                    Observacoes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SuspensoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    MotivoSuspensao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SuspensoPor = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItensPrescricaoInterna", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItensPrescricaoInterna_PrescricoesInternas_PrescricaoIntern~",
                        column: x => x.PrescricaoInternaId,
                        principalTable: "PrescricoesInternas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChecagensPrescricao",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItemPrescricaoInternaId = table.Column<int>(type: "integer", nullable: false),
                    Situacao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    HoraRealizacao = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    Justificativa = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ExecutanteUsuarioId = table.Column<int>(type: "integer", nullable: true),
                    ExecutanteNome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ExecutanteConselho = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    RegistradoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    RetificaChecagemId = table.Column<int>(type: "integer", nullable: true),
                    MotivoRetificacao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChecagensPrescricao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChecagensPrescricao_ChecagensPrescricao_RetificaChecagemId",
                        column: x => x.RetificaChecagemId,
                        principalTable: "ChecagensPrescricao",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChecagensPrescricao_ItensPrescricaoInterna_ItemPrescricaoIn~",
                        column: x => x.ItemPrescricaoInternaId,
                        principalTable: "ItensPrescricaoInterna",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChecagensPrescricao_Usuarios_ExecutanteUsuarioId",
                        column: x => x.ExecutanteUsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssinaturasDocumento_ArquivoId",
                table: "AssinaturasDocumento",
                column: "ArquivoId");

            migrationBuilder.CreateIndex(
                name: "IX_AssinaturasDocumento_PrescricaoInternaId_Papel",
                table: "AssinaturasDocumento",
                columns: new[] { "PrescricaoInternaId", "Papel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssinaturasDocumento_UsuarioId",
                table: "AssinaturasDocumento",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_ChecagensPrescricao_ExecutanteUsuarioId",
                table: "ChecagensPrescricao",
                column: "ExecutanteUsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_ChecagensPrescricao_ItemPrescricaoInternaId",
                table: "ChecagensPrescricao",
                column: "ItemPrescricaoInternaId");

            migrationBuilder.CreateIndex(
                name: "IX_ChecagensPrescricao_RetificaChecagemId",
                table: "ChecagensPrescricao",
                column: "RetificaChecagemId");

            migrationBuilder.CreateIndex(
                name: "IX_ItensPrescricaoInterna_PrescricaoInternaId",
                table: "ItensPrescricaoInterna",
                column: "PrescricaoInternaId");

            migrationBuilder.CreateIndex(
                name: "IX_PrescricoesInternas_AgendamentoId",
                table: "PrescricoesInternas",
                column: "AgendamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_PrescricoesInternas_CodigoVerificacao",
                table: "PrescricoesInternas",
                column: "CodigoVerificacao",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PrescricoesInternas_Data_Situacao",
                table: "PrescricoesInternas",
                columns: new[] { "Data", "Situacao" });

            migrationBuilder.CreateIndex(
                name: "IX_PrescricoesInternas_EvolucaoId",
                table: "PrescricoesInternas",
                column: "EvolucaoId");

            migrationBuilder.CreateIndex(
                name: "IX_PrescricoesInternas_Numero",
                table: "PrescricoesInternas",
                column: "Numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PrescricoesInternas_PacienteId_Data",
                table: "PrescricoesInternas",
                columns: new[] { "PacienteId", "Data" });

            migrationBuilder.CreateIndex(
                name: "IX_PrescricoesInternas_ProfissionalId",
                table: "PrescricoesInternas",
                column: "ProfissionalId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssinaturasDocumento");

            migrationBuilder.DropTable(
                name: "ChecagensPrescricao");

            migrationBuilder.DropTable(
                name: "ArquivosAssinados");

            migrationBuilder.DropTable(
                name: "ItensPrescricaoInterna");

            migrationBuilder.DropTable(
                name: "PrescricoesInternas");

            migrationBuilder.DropColumn(
                name: "Cpf",
                table: "Profissionais");
        }
    }
}
