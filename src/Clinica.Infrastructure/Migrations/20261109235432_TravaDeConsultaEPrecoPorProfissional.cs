using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TravaDeConsultaEPrecoPorProfissional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AprovaLiberacaoDeTrava",
                table: "Usuarios",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ConveniosNaoAtendidos",
                table: "Profissionais",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProfissionalId",
                table: "PrecosParticular",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LiberacoesDeTrava",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PacienteId = table.Column<int>(type: "integer", nullable: false),
                    PacienteNome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ConvenioNome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    DataHora = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ProfissionalId = table.Column<int>(type: "integer", nullable: true),
                    ProfissionalNome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SalaId = table.Column<int>(type: "integer", nullable: true),
                    DuracaoMinutos = table.Column<int>(type: "integer", nullable: true),
                    ModalidadeCodigo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    EspecialidadeConsultaCodigo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Encaixe = table.Column<bool>(type: "boolean", nullable: false),
                    ColetaPreventivo = table.Column<bool>(type: "boolean", nullable: false),
                    Observacoes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AgendamentoARemarcarId = table.Column<int>(type: "integer", nullable: true),
                    PrazoDias = table.Column<int>(type: "integer", nullable: false),
                    ConsultaAnteriorEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    AtendimentoAnteriorNumero = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Motivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    PedidoPorId = table.Column<int>(type: "integer", nullable: false),
                    PedidoPorNome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    PedidoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ExpiraEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DecididoPorId = table.Column<int>(type: "integer", nullable: true),
                    DecididoPorNome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    DecididoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DecididoPor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    AgendamentoId = table.Column<int>(type: "integer", nullable: true),
                    Desfecho = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LiberacoesDeTrava", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LiberacoesDeTrava_Pacientes_PacienteId",
                        column: x => x.PacienteId,
                        principalTable: "Pacientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AvisosDeLiberacao",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LiberacaoDeTravaId = table.Column<int>(type: "integer", nullable: false),
                    Destino = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Telefone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Texto = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    TratadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Entregue = table.Column<bool>(type: "boolean", nullable: false),
                    Motivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AvisosDeLiberacao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AvisosDeLiberacao_LiberacoesDeTrava_LiberacaoDeTravaId",
                        column: x => x.LiberacaoDeTravaId,
                        principalTable: "LiberacoesDeTrava",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PrecosParticular_ProfissionalId",
                table: "PrecosParticular",
                column: "ProfissionalId");

            migrationBuilder.CreateIndex(
                name: "IX_AvisosDeLiberacao_Destino_TratadoEm",
                table: "AvisosDeLiberacao",
                columns: new[] { "Destino", "TratadoEm" });

            migrationBuilder.CreateIndex(
                name: "IX_AvisosDeLiberacao_LiberacaoDeTravaId",
                table: "AvisosDeLiberacao",
                column: "LiberacaoDeTravaId");

            migrationBuilder.CreateIndex(
                name: "IX_LiberacoesDeTrava_Estado_ExpiraEm",
                table: "LiberacoesDeTrava",
                columns: new[] { "Estado", "ExpiraEm" });

            migrationBuilder.CreateIndex(
                name: "IX_LiberacoesDeTrava_PacienteId_DataHora",
                table: "LiberacoesDeTrava",
                columns: new[] { "PacienteId", "DataHora" });

            migrationBuilder.AddForeignKey(
                name: "FK_PrecosParticular_Profissionais_ProfissionalId",
                table: "PrecosParticular",
                column: "ProfissionalId",
                principalTable: "Profissionais",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PrecosParticular_Profissionais_ProfissionalId",
                table: "PrecosParticular");

            migrationBuilder.DropTable(
                name: "AvisosDeLiberacao");

            migrationBuilder.DropTable(
                name: "LiberacoesDeTrava");

            migrationBuilder.DropIndex(
                name: "IX_PrecosParticular_ProfissionalId",
                table: "PrecosParticular");

            migrationBuilder.DropColumn(
                name: "AprovaLiberacaoDeTrava",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "ConveniosNaoAtendidos",
                table: "Profissionais");

            migrationBuilder.DropColumn(
                name: "ProfissionalId",
                table: "PrecosParticular");
        }
    }
}
