using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <summary>
    /// Bloqueio de agenda: ferias, feriado, congresso, folga, sala em manutencao.
    ///
    /// Ate aqui a agenda so sabia recusar choque ENTRE agendamentos. O que segurava a
    /// marcacao em cima da folga do profissional era a memoria de quem esta no balcao — e
    /// memoria de balcao falha justamente no dia cheio, que e quando o erro custa caro.
    ///
    /// Profissional e sala sao ANULAVEIS: sem os dois, o bloqueio e da clinica inteira
    /// (feriado). O indice e pelo Inicio porque a consulta acontece a cada marcacao —
    /// "o que fecha este intervalo?" — e varrer o historico inteiro toda vez sairia caro.
    ///
    /// PURAMENTE ADITIVA: uma tabela nova, nenhuma coluna existente tocada. O faturamento
    /// congelado nao le nem escreve nela.
    /// </summary>
    public partial class BloqueioDeAgenda : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BloqueiosAgenda",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProfissionalId = table.Column<int>(type: "integer", nullable: true),
                    SalaId = table.Column<int>(type: "integer", nullable: true),
                    Inicio = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Fim = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Motivo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CriadoPor = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BloqueiosAgenda", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BloqueiosAgenda_Profissionais_ProfissionalId",
                        column: x => x.ProfissionalId,
                        principalTable: "Profissionais",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BloqueiosAgenda_Salas_SalaId",
                        column: x => x.SalaId,
                        principalTable: "Salas",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_BloqueiosAgenda_Inicio",
                table: "BloqueiosAgenda",
                column: "Inicio");

            migrationBuilder.CreateIndex(
                name: "IX_BloqueiosAgenda_ProfissionalId",
                table: "BloqueiosAgenda",
                column: "ProfissionalId");

            migrationBuilder.CreateIndex(
                name: "IX_BloqueiosAgenda_SalaId",
                table: "BloqueiosAgenda",
                column: "SalaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BloqueiosAgenda");
        }
    }
}
