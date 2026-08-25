using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <summary>
    /// A COLETA DO TERMO PELO CELULAR — o link no WhatsApp (parcela 81; a decisão inteira
    /// em <c>docs/termo-pelo-whatsapp.md</c>).
    ///
    /// A tabela guarda a EVIDÊNCIA do canal (para qual telefone foi, quem enviou, quando
    /// e de onde o paciente respondeu); o traço e as respostas entram no documento pelo
    /// mesmo caminho da coleta no balcão.
    ///
    /// ADITIVA: tabela nova. Nenhuma coluna existente é tocada — o que importa na janela
    /// em que os cinco exes estão em versões diferentes.
    /// </summary>
    public partial class ColetaRemotaDoTermo : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ColetasRemotasTermo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentoClinicoId = table.Column<int>(type: "integer", nullable: false),
                    Token = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TelefoneDestino = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    EnviadaPor = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CriadaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ExpiraEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    RespondidaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    EvidenciaResposta = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true),
                    ConcluidaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CanceladaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CanceladaPor = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ColetasRemotasTermo", x => x.Id);
                    // ⚠️ A tabela do DocumentoClinico chama-se "DocumentosClinicos" — o nome
                    // sai do DbSet, nunca da classe (a lição do 42P01, checagem 41).
                    table.ForeignKey(
                        name: "FK_ColetasRemotasTermo_DocumentosClinicos_DocumentoClinicoId",
                        column: x => x.DocumentoClinicoId,
                        principalTable: "DocumentosClinicos", principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ColetasRemotasTermo_DocumentoClinicoId",
                table: "ColetasRemotasTermo", column: "DocumentoClinicoId");

            migrationBuilder.CreateIndex(
                name: "IX_ColetasRemotasTermo_Token",
                table: "ColetasRemotasTermo", column: "Token", unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ColetasRemotasTermo");
        }
    }
}
