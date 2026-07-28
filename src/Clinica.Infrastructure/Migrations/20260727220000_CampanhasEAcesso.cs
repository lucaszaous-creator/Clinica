using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <summary>
    /// Campanhas e controle de acesso (parcela 5): o contato com o paciente
    /// (confirmação de sessão, NPS e recall) e o usuário da suíte com perfil e
    /// permissões finas.
    ///
    /// PURAMENTE ADITIVA: só tabelas novas. Nenhuma coluna existente é renomeada,
    /// alterada ou removida — o faturamento em produção não vê diferença, e a regra de
    /// conviver com versões diferentes em campo continua valendo.
    /// </summary>
    [DbContext(typeof(ClinicaDbContext))]
    [Migration("20260727220000_CampanhasEAcesso")]
    public partial class CampanhasEAcesso : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContatosCampanha",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PacienteId = table.Column<int>(type: "integer", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Canal = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Origem = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    AgendamentoId = table.Column<int>(type: "integer", nullable: true),
                    AtendimentoId = table.Column<int>(type: "integer", nullable: true),
                    Referencia = table.Column<DateOnly>(type: "date", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EnviadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    EnviadoPor = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    RespondidoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Nota = table.Column<int>(type: "integer", nullable: true),
                    Comentario = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContatosCampanha", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContatosCampanha_Pacientes_PacienteId",
                        column: x => x.PacienteId,
                        principalTable: "Pacientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ContatosCampanha_Agendamentos_AgendamentoId",
                        column: x => x.AgendamentoId,
                        principalTable: "Agendamentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ContatosCampanha_Atendimentos_AtendimentoId",
                        column: x => x.AtendimentoId,
                        principalTable: "Atendimentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Login = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    SenhaHash = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SenhaSalt = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Perfil = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PermissoesExtras = table.Column<int>(type: "integer", nullable: false),
                    PermissoesNegadas = table.Column<int>(type: "integer", nullable: false),
                    ProfissionalId = table.Column<int>(type: "integer", nullable: true),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    DeveTrocarSenha = table.Column<bool>(type: "boolean", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UltimoAcessoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    TentativasFalhas = table.Column<int>(type: "integer", nullable: false),
                    BloqueadoAte = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Observacoes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.Id);
                    // Desligar o profissional não pode apagar o usuário: a auditoria
                    // precisa continuar sabendo quem era o operador das ações antigas.
                    table.ForeignKey(
                        name: "FK_Usuarios_Profissionais_ProfissionalId",
                        column: x => x.ProfissionalId,
                        principalTable: "Profissionais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            // A idempotência da campanha é regra de BANCO, não só de código: duas
            // máquinas gerando a rodada ao mesmo tempo passariam pela checagem em
            // memória e mandariam a mesma mensagem duas vezes ao mesmo paciente.
            migrationBuilder.CreateIndex(
                "IX_ContatosCampanha_Tipo_Origem", "ContatosCampanha",
                new[] { "Tipo", "Origem" }, unique: true);

            migrationBuilder.CreateIndex(
                "IX_ContatosCampanha_Status_Referencia", "ContatosCampanha",
                new[] { "Status", "Referencia" });

            migrationBuilder.CreateIndex(
                "IX_ContatosCampanha_PacienteId", "ContatosCampanha", "PacienteId");

            migrationBuilder.CreateIndex(
                "IX_ContatosCampanha_AgendamentoId", "ContatosCampanha", "AgendamentoId");

            migrationBuilder.CreateIndex(
                "IX_ContatosCampanha_AtendimentoId", "ContatosCampanha", "AtendimentoId");

            migrationBuilder.CreateIndex(
                "IX_Usuarios_Login", "Usuarios", "Login", unique: true);

            migrationBuilder.CreateIndex(
                "IX_Usuarios_ProfissionalId", "Usuarios", "ProfissionalId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable("ContatosCampanha");
            migrationBuilder.DropTable("Usuarios");
        }
    }
}
