using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TissFilaDeEnviosERegistroDoProfissional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Cbos",
                table: "Profissionais",
                type: "character varying(6)",
                maxLength: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConselhoProfissional",
                table: "Profissionais",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NumeroConselho",
                table: "Profissionais",
                type: "character varying(15)",
                maxLength: 15,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UfConselho",
                table: "Profissionais",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TransacoesTiss",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ConvenioCodigo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CodigoId = table.Column<int>(type: "integer", nullable: true),
                    TransacaoDeEnvioId = table.Column<int>(type: "integer", nullable: true),
                    PacienteId = table.Column<int>(type: "integer", nullable: true),
                    Estado = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    Tentativas = table.Column<int>(type: "integer", nullable: false),
                    PedidoPor = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    PedidoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ProcessadaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    NumeroCarteira = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ValidadeCarteira = table.Column<DateOnly>(type: "date", nullable: true),
                    NumeroGuiaPrestador = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Protocolo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    NumeroGuiaOperadora = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Resultado = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Erro = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    XmlEnviado = table.Column<string>(type: "text", nullable: true),
                    XmlRecebido = table.Column<string>(type: "text", nullable: true),
                    Consumidor = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    ReservadaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    EnvioIniciadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ProximaTentativaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Versao = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransacoesTiss", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransacoesTiss_Codigos_CodigoId",
                        column: x => x.CodigoId,
                        principalTable: "Codigos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TransacoesTiss_Pacientes_PacienteId",
                        column: x => x.PacienteId,
                        principalTable: "Pacientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TransacoesTiss_TransacoesTiss_TransacaoDeEnvioId",
                        column: x => x.TransacaoDeEnvioId,
                        principalTable: "TransacoesTiss",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TransacoesTissGuias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TransacaoId = table.Column<int>(type: "integer", nullable: false),
                    CodigoId = table.Column<int>(type: "integer", nullable: true),
                    NumeroGuiaPrestador = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NumeroGuiaOperadora = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Situacao = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    Motivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AtualizadaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransacoesTissGuias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransacoesTissGuias_Codigos_CodigoId",
                        column: x => x.CodigoId,
                        principalTable: "Codigos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TransacoesTissGuias_TransacoesTiss_TransacaoId",
                        column: x => x.TransacaoId,
                        principalTable: "TransacoesTiss",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TransacoesTiss_CodigoId",
                table: "TransacoesTiss",
                column: "CodigoId");

            migrationBuilder.CreateIndex(
                name: "IX_TransacoesTiss_Estado_PedidoEm",
                table: "TransacoesTiss",
                columns: new[] { "Estado", "PedidoEm" });

            migrationBuilder.CreateIndex(
                name: "IX_TransacoesTiss_PacienteId",
                table: "TransacoesTiss",
                column: "PacienteId");

            migrationBuilder.CreateIndex(
                name: "IX_TransacoesTiss_TransacaoDeEnvioId",
                table: "TransacoesTiss",
                column: "TransacaoDeEnvioId");

            migrationBuilder.CreateIndex(
                name: "IX_TransacoesTissGuias_CodigoId",
                table: "TransacoesTissGuias",
                column: "CodigoId");

            migrationBuilder.CreateIndex(
                name: "IX_TransacoesTissGuias_TransacaoId_NumeroGuiaPrestador",
                table: "TransacoesTissGuias",
                columns: new[] { "TransacaoId", "NumeroGuiaPrestador" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TransacoesTissGuias");

            migrationBuilder.DropTable(
                name: "TransacoesTiss");

            migrationBuilder.DropColumn(
                name: "Cbos",
                table: "Profissionais");

            migrationBuilder.DropColumn(
                name: "ConselhoProfissional",
                table: "Profissionais");

            migrationBuilder.DropColumn(
                name: "NumeroConselho",
                table: "Profissionais");

            migrationBuilder.DropColumn(
                name: "UfConselho",
                table: "Profissionais");
        }
    }
}
