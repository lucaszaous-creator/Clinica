using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <summary>
    /// Cota de sessões autorizada pelo convênio (senha, validade e quantidade), para
    /// avisar antes de estourar o limite — a glosa 2006 "quantidade executada acima da
    /// autorizada" já era registrada pelo sistema, mas nunca prevenida.
    /// </summary>
    [DbContext(typeof(ClinicaDbContext))]
    [Migration("20260725120000_AutorizacaoSessoes")]
    public partial class AutorizacaoSessoes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Autorizacoes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PacienteId = table.Column<int>(type: "integer", nullable: false),
                    Numero = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Convenio = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ConvenioCodigo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    DataEmissao = table.Column<DateOnly>(type: "date", nullable: false),
                    DataValidade = table.Column<DateOnly>(type: "date", nullable: false),
                    QuantidadeAutorizada = table.Column<int>(type: "integer", nullable: false),
                    QuantidadeUtilizadaManual = table.Column<int>(type: "integer", nullable: false),
                    Observacoes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Encerrada = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Autorizacoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Autorizacoes_Pacientes_PacienteId",
                        column: x => x.PacienteId,
                        principalTable: "Pacientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex("IX_Autorizacoes_PacienteId", "Autorizacoes", "PacienteId");
            migrationBuilder.CreateIndex("IX_Autorizacoes_DataValidade", "Autorizacoes", "DataValidade");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
            => migrationBuilder.DropTable("Autorizacoes");
    }
}
