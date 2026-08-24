using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <summary>
    /// A ETAPA 4 do Processo de Enfermagem — a IMPLEMENTAÇÃO (parcela 76).
    ///
    /// A COFEN 358/2009 divide o processo em cinco etapas e o sistema cobria as três
    /// primeiras; a quarta existia só como TEXTO ("curativo a cada 24h") e nada registrava
    /// que o cuidado tinha sido feito. Cuidado que não se registra é, para qualquer
    /// fiscalização, cuidado que não aconteceu.
    ///
    /// ADITIVA: tabela NOVA. Nenhuma coluna existente é tocada, e nenhum app anterior a esta
    /// versão precisa conhecê-la — o que importa na janela em que os cinco exes estão em
    /// versões diferentes.
    /// </summary>
    public partial class ExecucaoDoCuidadoDeEnfermagem : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ⚠️ `defaultValue: false` é o que as linhas JÁ GRAVADAS valem: nenhum cuidado
            // do plano existente foi prescrito como condicional — quem o marcar assim vai
            // ser a enfermeira, daqui em diante. Aqui o padrão da linguagem coincide com a
            // verdade; na coluna `GeraGuia` da parcela 60 ele teria desligado a guia de
            // todos os convênios, e é por isso que se confere um a um.
            migrationBuilder.AddColumn<bool>(
                name: "SeNecessario", table: "CuidadosEnfermagem",
                type: "boolean", nullable: false, defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ChecagensCuidado",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CuidadoEnfermagemId = table.Column<int>(type: "integer", nullable: false),
                    Data = table.Column<DateOnly>(type: "date", nullable: false),
                    HoraRealizacao = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    Situacao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Justificativa = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true),
                    Observacao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ExecutanteUsuarioId = table.Column<int>(type: "integer", nullable: true),
                    ExecutanteNome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ExecutanteConselho = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    RegistradoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    RetificaChecagemId = table.Column<int>(type: "integer", nullable: true),
                    MotivoRetificacao = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChecagensCuidado", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChecagensCuidado_CuidadosEnfermagem_CuidadoEnfermagemId",
                        column: x => x.CuidadoEnfermagemId,
                        principalTable: "CuidadosEnfermagem", principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChecagensCuidado_UsuariosSistema_ExecutanteUsuarioId",
                        column: x => x.ExecutanteUsuarioId,
                        principalTable: "UsuariosSistema", principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    // RESTRICT: apagar a linha corrigida quebraria a cadeia que prova o que a
                    // folha dizia ANTES da retificação.
                    table.ForeignKey(
                        name: "FK_ChecagensCuidado_ChecagensCuidado_RetificaChecagemId",
                        column: x => x.RetificaChecagemId,
                        principalTable: "ChecagensCuidado", principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChecagensCuidado_CuidadoEnfermagemId_Data",
                table: "ChecagensCuidado", columns: new[] { "CuidadoEnfermagemId", "Data" });

            migrationBuilder.CreateIndex(
                name: "IX_ChecagensCuidado_ExecutanteUsuarioId",
                table: "ChecagensCuidado", column: "ExecutanteUsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_ChecagensCuidado_RetificaChecagemId",
                table: "ChecagensCuidado", column: "RetificaChecagemId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ChecagensCuidado");
            migrationBuilder.DropColumn(name: "SeNecessario", table: "CuidadosEnfermagem");
        }
    }
}
