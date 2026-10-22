using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SistemaEPedidosDaEquipe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Cartao",
                table: "MensagensEquipe",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DoSistema",
                table: "MensagensEquipe",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "PedidosRecepcao",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Idempotencia = table.Column<Guid>(type: "uuid", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PacienteId = table.Column<int>(type: "integer", nullable: true),
                    PacienteNome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AgendamentoId = table.Column<int>(type: "integer", nullable: true),
                    HorarioEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Detalhe = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CriadoPorId = table.Column<int>(type: "integer", nullable: false),
                    CriadoPorNome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Situacao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ResponsavelId = table.Column<int>(type: "integer", nullable: true),
                    ResponsavelNome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    AssumidoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Desfecho = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    EncerradoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    MensagemRecepcaoId = table.Column<int>(type: "integer", nullable: true),
                    MensagemAutorId = table.Column<int>(type: "integer", nullable: true),
                    Versao = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PedidosRecepcao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PedidosRecepcao_Agendamentos_AgendamentoId",
                        column: x => x.AgendamentoId,
                        principalTable: "Agendamentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PedidosRecepcao_Pacientes_PacienteId",
                        column: x => x.PacienteId,
                        principalTable: "Pacientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PedidosRecepcao_Usuarios_CriadoPorId",
                        column: x => x.CriadoPorId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EventosPedidoRecepcao",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PedidoRecepcaoId = table.Column<int>(type: "integer", nullable: false),
                    UsuarioId = table.Column<int>(type: "integer", nullable: false),
                    Autor = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Em = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Acao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Observacao = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventosPedidoRecepcao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventosPedidoRecepcao_PedidosRecepcao_PedidoRecepcaoId",
                        column: x => x.PedidoRecepcaoId,
                        principalTable: "PedidosRecepcao",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EventosPedidoRecepcao_PedidoRecepcaoId",
                table: "EventosPedidoRecepcao",
                column: "PedidoRecepcaoId");

            migrationBuilder.CreateIndex(
                name: "IX_PedidosRecepcao_AgendamentoId",
                table: "PedidosRecepcao",
                column: "AgendamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_PedidosRecepcao_CriadoPorId_CriadoEm",
                table: "PedidosRecepcao",
                columns: new[] { "CriadoPorId", "CriadoEm" });

            migrationBuilder.CreateIndex(
                name: "IX_PedidosRecepcao_Idempotencia",
                table: "PedidosRecepcao",
                column: "Idempotencia",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PedidosRecepcao_PacienteId",
                table: "PedidosRecepcao",
                column: "PacienteId");

            migrationBuilder.CreateIndex(
                name: "IX_PedidosRecepcao_Situacao_CriadoEm",
                table: "PedidosRecepcao",
                columns: new[] { "Situacao", "CriadoEm" });

            // Entrega instantânea do pedido: o mesmo aviso do chat (ChatDaEquipe). Só nasce onde a
            // função existe e o papel tem TRIGGER — sem ele a tela segue pelo relógio de fundo.
            if (ActiveProvider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
                migrationBuilder.Sql("""
                    DO $pedido$
                    BEGIN
                        IF EXISTS (SELECT 1 FROM pg_proc WHERE proname = 'clinica_avisar_mudanca'
                                   AND pronamespace = 'public'::regnamespace)
                           AND has_table_privilege(current_user, 'public."PedidosRecepcao"', 'TRIGGER')
                           AND NOT EXISTS (SELECT 1 FROM pg_trigger WHERE tgname = 'clinica_avisar_pedidos_recepcao') THEN
                            CREATE TRIGGER clinica_avisar_pedidos_recepcao AFTER INSERT OR UPDATE ON public."PedidosRecepcao"
                                FOR EACH STATEMENT EXECUTE FUNCTION public.clinica_avisar_mudanca();
                        END IF;
                    END
                    $pedido$;
                    """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
                migrationBuilder.Sql("DROP TRIGGER IF EXISTS clinica_avisar_pedidos_recepcao ON \"PedidosRecepcao\";");

            migrationBuilder.DropTable(
                name: "EventosPedidoRecepcao");

            migrationBuilder.DropTable(
                name: "PedidosRecepcao");

            migrationBuilder.DropColumn(
                name: "Cartao",
                table: "MensagensEquipe");

            migrationBuilder.DropColumn(
                name: "DoSistema",
                table: "MensagensEquipe");
        }
    }
}
