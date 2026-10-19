using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnviosPacienteCrm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EnviosPacienteCrm",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PacienteId = table.Column<int>(type: "integer", nullable: false),
                    DocumentoClinicoId = table.Column<int>(type: "integer", nullable: true),
                    PrescricaoInternaId = table.Column<int>(type: "integer", nullable: true),
                    Nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Telefone = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Canal = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Origem = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Operador = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Detalhe = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ExpiraEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Texto = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    NomeArquivo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Arquivo = table.Column<byte[]>(type: "bytea", nullable: true),
                    Assunto = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Consumidor = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ReservadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    SubmetidoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Versao = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnviosPacienteCrm", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnviosPacienteCrm_DocumentosClinicos_DocumentoClinicoId",
                        column: x => x.DocumentoClinicoId,
                        principalTable: "DocumentosClinicos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EnviosPacienteCrm_Pacientes_PacienteId",
                        column: x => x.PacienteId,
                        principalTable: "Pacientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EnviosPacienteCrm_PrescricoesInternas_PrescricaoInternaId",
                        column: x => x.PrescricaoInternaId,
                        principalTable: "PrescricoesInternas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EnviosPacienteCrm_DocumentoClinicoId",
                table: "EnviosPacienteCrm",
                column: "DocumentoClinicoId");

            migrationBuilder.CreateIndex(
                name: "IX_EnviosPacienteCrm_Estado_AtualizadoEm",
                table: "EnviosPacienteCrm",
                columns: new[] { "Estado", "AtualizadoEm" });

            migrationBuilder.CreateIndex(
                name: "IX_EnviosPacienteCrm_PacienteId",
                table: "EnviosPacienteCrm",
                column: "PacienteId");

            migrationBuilder.CreateIndex(
                name: "IX_EnviosPacienteCrm_PrescricaoInternaId",
                table: "EnviosPacienteCrm",
                column: "PrescricaoInternaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EnviosPacienteCrm");
        }
    }
}
