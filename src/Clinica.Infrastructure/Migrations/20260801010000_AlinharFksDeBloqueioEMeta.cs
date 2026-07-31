using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <summary>
    /// Alinha o banco ao modelo nas FKs de `BloqueiosAgenda` e `Metas`.
    ///
    /// As duas tabelas nasceram de migrations escritas À MÃO (não havia `dotnet ef`
    /// neste ambiente), e as três chaves saíram sem o `onDelete` que o
    /// `ClinicaDbContext` declara: o modelo dizia `Cascade`, o banco ficou com
    /// `NO ACTION`. Divergência assim não dá erro nenhum — ela faz a PRÓXIMA migration
    /// nascer carregando o diff, e num banco que fatura a clínica todo dia isso é o
    /// pior defeito que este repositório pode produzir.
    ///
    /// `Cascade` é o comportamento certo aqui, e não a convenção `SetNull` do resto do
    /// projeto: nas duas tabelas o profissional NULO tem significado próprio — bloqueio
    /// sem profissional é da clínica inteira, e meta sem profissional é a meta da
    /// clínica. `SetNull` transformaria as férias de uma pessoa num fechamento geral da
    /// agenda, e a meta dela na meta da casa (que ainda esbarraria no índice único).
    ///
    /// Na prática ninguém deve chegar ao cascade: a guarda do `EquipeService` passou a
    /// recusar a exclusão de profissional com qualquer histórico. Isto é a rede de
    /// baixo, não a porta.
    ///
    /// Encontrada pelo teste `MontagemDoSistemaTests.Snapshot_do_EF_esta_em_dia_com_as_entidades`,
    /// que falhou na primeira vez em que rodou.
    /// </summary>
    public partial class AlinharFksDeBloqueioEMeta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BloqueiosAgenda_Profissionais_ProfissionalId",
                table: "BloqueiosAgenda");

            migrationBuilder.DropForeignKey(
                name: "FK_BloqueiosAgenda_Salas_SalaId",
                table: "BloqueiosAgenda");

            migrationBuilder.DropForeignKey(
                name: "FK_Metas_Profissionais_ProfissionalId",
                table: "Metas");

            migrationBuilder.AddForeignKey(
                name: "FK_BloqueiosAgenda_Profissionais_ProfissionalId",
                table: "BloqueiosAgenda",
                column: "ProfissionalId",
                principalTable: "Profissionais",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BloqueiosAgenda_Salas_SalaId",
                table: "BloqueiosAgenda",
                column: "SalaId",
                principalTable: "Salas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Metas_Profissionais_ProfissionalId",
                table: "Metas",
                column: "ProfissionalId",
                principalTable: "Profissionais",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BloqueiosAgenda_Profissionais_ProfissionalId",
                table: "BloqueiosAgenda");

            migrationBuilder.DropForeignKey(
                name: "FK_BloqueiosAgenda_Salas_SalaId",
                table: "BloqueiosAgenda");

            migrationBuilder.DropForeignKey(
                name: "FK_Metas_Profissionais_ProfissionalId",
                table: "Metas");

            migrationBuilder.AddForeignKey(
                name: "FK_BloqueiosAgenda_Profissionais_ProfissionalId",
                table: "BloqueiosAgenda",
                column: "ProfissionalId",
                principalTable: "Profissionais",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BloqueiosAgenda_Salas_SalaId",
                table: "BloqueiosAgenda",
                column: "SalaId",
                principalTable: "Salas",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Metas_Profissionais_ProfissionalId",
                table: "Metas",
                column: "ProfissionalId",
                principalTable: "Profissionais",
                principalColumn: "Id");
        }
    }
}
