using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AutorizacaoTissDaAmil : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AutorizacaoId",
                table: "TransacoesTiss",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AutorizacoesTiss",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AgendamentoId = table.Column<int>(type: "integer", nullable: false),
                    PacienteId = table.Column<int>(type: "integer", nullable: false),
                    ConvenioCodigo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Situacao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NumeroCarteira = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CodigoTabela = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    CodigoProcedimento = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    DescricaoProcedimento = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Quantidade = table.Column<int>(type: "integer", nullable: false),
                    CaraterAtendimento = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false),
                    ProfissionalNome = table.Column<string>(type: "character varying(70)", maxLength: 70, nullable: false),
                    ConselhoProfissional = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    NumeroConselho = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    UfConselho = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Cbos = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    NumeroGuiaPrestador = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NumeroGuiaOperadora = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Senha = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    DataAutorizacao = table.Column<DateOnly>(type: "date", nullable: true),
                    ValidadeSenha = table.Column<DateOnly>(type: "date", nullable: true),
                    Motivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Token = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    AusenciaToken = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    TokenRegistradoPor = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    TokenRegistradoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ConsultasDeStatus = table.Column<int>(type: "integer", nullable: false),
                    PedidaPor = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PedidaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    AtualizadaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutorizacoesTiss", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TransacoesTiss_AutorizacaoId",
                table: "TransacoesTiss",
                column: "AutorizacaoId");

            migrationBuilder.CreateIndex(
                name: "IX_AutorizacoesTiss_AgendamentoId",
                table: "AutorizacoesTiss",
                column: "AgendamentoId");

            migrationBuilder.AddForeignKey(
                name: "FK_TransacoesTiss_AutorizacoesTiss_AutorizacaoId",
                table: "TransacoesTiss",
                column: "AutorizacaoId",
                principalTable: "AutorizacoesTiss",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TransacoesTiss_AutorizacoesTiss_AutorizacaoId",
                table: "TransacoesTiss");

            migrationBuilder.DropTable(
                name: "AutorizacoesTiss");

            migrationBuilder.DropIndex(
                name: "IX_TransacoesTiss_AutorizacaoId",
                table: "TransacoesTiss");

            migrationBuilder.DropColumn(
                name: "AutorizacaoId",
                table: "TransacoesTiss");
        }
    }
}
