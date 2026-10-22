using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PedidoDoPacientePeloWhatsApp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EscolhidoEm",
                table: "PedidosRecepcao",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "HorarioEscolhido",
                table: "PedidosRecepcao",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HorariosOferecidos",
                table: "PedidosRecepcao",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "OferecidoEm",
                table: "PedidosRecepcao",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OfertaFalha",
                table: "PedidosRecepcao",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OfertaToken",
                table: "PedidosRecepcao",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OfertaTratadaEm",
                table: "PedidosRecepcao",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Origem",
                table: "PedidosRecepcao",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "equipe");

            migrationBuilder.AddColumn<string>(
                name: "RespostaAoPaciente",
                table: "PedidosRecepcao",
                type: "character varying(600)",
                maxLength: 600,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "RespostaTratadaEm",
                table: "PedidosRecepcao",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TelefonePaciente",
                table: "PedidosRecepcao",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_PedidosRecepcao_OfertaToken",
                table: "PedidosRecepcao",
                column: "OfertaToken",
                unique: true,
                filter: "\"OfertaToken\" IS NOT NULL");

            // O usuário técnico do SemDor (UsuarioTecnico): o autor do pedido que vem do PACIENTE e do
            // cartão dele no # Recepção — o chat e o pedido pedem um usuário, e o paciente não é um.
            // INATIVO e com senha impossível: não entra no sistema, não conta como "já há usuário" na
            // primeira abertura e não aparece em Acessos (ClinicaRepositorio.UsuariosAsync o tira).
            // Só no Postgres: o SQLite dos testes cria o banco pelo modelo, e o serviço o cria na falta.
            if (ActiveProvider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
                migrationBuilder.Sql("""
                    INSERT INTO "Usuarios" ("Nome", "Login", "SenhaHash", "SenhaSalt", "Perfil", "PermissoesExtras", "PermissoesNegadas",
                                            "Ativo", "DeveTrocarSenha", "CriadoEm", "TentativasFalhas", "Observacoes")
                    SELECT 'SemDor', 'semdor.sistema', 'AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=', 'AAAAAAAAAAAAAAAAAAAAAA==', 'Recepcao', 0, 0,
                           false, false, clock_timestamp() AT TIME ZONE 'America/Sao_Paulo', 0,
                           'Usuário técnico do @SemDor (pedidos do paciente pelo WhatsApp). Não entra no sistema.'
                    WHERE NOT EXISTS (SELECT 1 FROM "Usuarios" WHERE "Login" = 'semdor.sistema');
                    """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PedidosRecepcao_OfertaToken",
                table: "PedidosRecepcao");

            migrationBuilder.DropColumn(
                name: "EscolhidoEm",
                table: "PedidosRecepcao");

            migrationBuilder.DropColumn(
                name: "HorarioEscolhido",
                table: "PedidosRecepcao");

            migrationBuilder.DropColumn(
                name: "HorariosOferecidos",
                table: "PedidosRecepcao");

            migrationBuilder.DropColumn(
                name: "OferecidoEm",
                table: "PedidosRecepcao");

            migrationBuilder.DropColumn(
                name: "OfertaFalha",
                table: "PedidosRecepcao");

            migrationBuilder.DropColumn(
                name: "OfertaToken",
                table: "PedidosRecepcao");

            migrationBuilder.DropColumn(
                name: "OfertaTratadaEm",
                table: "PedidosRecepcao");

            migrationBuilder.DropColumn(
                name: "Origem",
                table: "PedidosRecepcao");

            migrationBuilder.DropColumn(
                name: "RespostaAoPaciente",
                table: "PedidosRecepcao");

            migrationBuilder.DropColumn(
                name: "RespostaTratadaEm",
                table: "PedidosRecepcao");

            migrationBuilder.DropColumn(
                name: "TelefonePaciente",
                table: "PedidosRecepcao");
        }
    }
}
