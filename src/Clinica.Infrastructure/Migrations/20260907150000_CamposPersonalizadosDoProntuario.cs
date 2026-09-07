using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Clinica.Infrastructure.Migrations;

/// <summary>
/// Os CAMPOS PERSONALIZADOS do prontuário (set/2026): o que ESTA clínica anota e o sistema
/// não tem.
///
/// ADITIVA — duas tabelas novas e uma coluna anulável em VersoesEvolucao. Nada é tocado no
/// que já existe: a clínica que não cadastrar campo nenhum não vê diferença.
/// </summary>
public partial class CamposPersonalizadosDoProntuario : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "CamposPersonalizadosProntuario",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Rotulo = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                Tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                Opcoes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                Ajuda = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                ModalidadeCodigo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                Ordem = table.Column<int>(type: "integer", nullable: false),
                Ativo = table.Column<bool>(type: "boolean", nullable: false),
                CriadoEm = table.Column<System.DateTime>(type: "timestamp without time zone", nullable: false),
                CriadoPor = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CamposPersonalizadosProntuario", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "ValoresCampoPersonalizado",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                EvolucaoId = table.Column<int>(type: "integer", nullable: false),
                CampoId = table.Column<int>(type: "integer", nullable: false),
                Rotulo = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                Tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                Valor = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ValoresCampoPersonalizado", x => x.Id);
                // Apagar a evolução leva os valores junto: valor órfão não é prontuário.
                table.ForeignKey(
                    name: "FK_ValoresCampoPersonalizado_Evolucoes_EvolucaoId",
                    column: x => x.EvolucaoId,
                    principalTable: "Evolucoes",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                // A definição não se apaga (só se desativa) — e a procedência não pode
                // apontar para uma linha que sumiu.
                table.ForeignKey(
                    name: "FK_ValoresCampoPersonalizado_CamposPersonalizadosProntuario_Ca~",
                    column: x => x.CampoId,
                    principalTable: "CamposPersonalizadosProntuario",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ValoresCampoPersonalizado_CampoId",
            table: "ValoresCampoPersonalizado",
            column: "CampoId");

        migrationBuilder.CreateIndex(
            name: "IX_ValoresCampoPersonalizado_EvolucaoId",
            table: "ValoresCampoPersonalizado",
            column: "EvolucaoId");

        // O que os campos DIZIAM antes de uma correção — sem isto, corrigir a sessão
        // apagaria os campos personalizados sem rastro (art. 3º da Lei 13.787/2018).
        migrationBuilder.AddColumn<string>(
            name: "CamposPersonalizados",
            table: "VersoesEvolucao",
            type: "text",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ValoresCampoPersonalizado");
        migrationBuilder.DropTable(name: "CamposPersonalizadosProntuario");
        migrationBuilder.DropColumn(name: "CamposPersonalizados", table: "VersoesEvolucao");
    }
}
