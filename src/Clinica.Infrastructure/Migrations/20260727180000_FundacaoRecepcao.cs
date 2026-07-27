using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <summary>
    /// Fundação da recepção (parcela 1): profissionais, salas e lista de espera, mais
    /// os campos que a agenda multiprofissional e a fila em kanban precisam no
    /// agendamento.
    ///
    /// PURAMENTE ADITIVA: só tabelas novas e colunas novas anuláveis (ou com default).
    /// O faturamento em produção continua lendo e gravando Agendamentos sem saber que
    /// estas colunas existem — regra que vale enquanto houver mais de um app instalado.
    /// </summary>
    [DbContext(typeof(ClinicaDbContext))]
    [Migration("20260727180000_FundacaoRecepcao")]
    public partial class FundacaoRecepcao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Profissionais",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NomeCurto = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    RegistroConselho = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    EspecialidadeCodigo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Telefone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Email = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Cor = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: true),
                    DuracaoPadraoMinutos = table.Column<int>(type: "integer", nullable: true),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    Ordem = table.Column<int>(type: "integer", nullable: false),
                    Observacoes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Profissionais", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Salas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Capacidade = table.Column<int>(type: "integer", nullable: false),
                    Ativa = table.Column<bool>(type: "boolean", nullable: false),
                    Ordem = table.Column<int>(type: "integer", nullable: false),
                    Observacoes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Salas", x => x.Id);
                });

            // ---- Colunas novas do agendamento (todas anuláveis / com default) ----
            migrationBuilder.AddColumn<int>(
                name: "ProfissionalId", table: "Agendamentos", type: "integer", nullable: true);
            migrationBuilder.AddColumn<int>(
                name: "SalaId", table: "Agendamentos", type: "integer", nullable: true);
            migrationBuilder.AddColumn<int>(
                name: "DuracaoMinutos", table: "Agendamentos", type: "integer", nullable: true);
            migrationBuilder.AddColumn<bool>(
                name: "Encaixe", table: "Agendamentos", type: "boolean",
                nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<DateTime>(
                name: "ChegadaEm", table: "Agendamentos",
                type: "timestamp without time zone", nullable: true);
            migrationBuilder.AddColumn<DateTime>(
                name: "InicioAtendimentoEm", table: "Agendamentos",
                type: "timestamp without time zone", nullable: true);

            migrationBuilder.CreateTable(
                name: "ListaEspera",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PacienteId = table.Column<int>(type: "integer", nullable: false),
                    ProfissionalId = table.Column<int>(type: "integer", nullable: true),
                    ModalidadeCodigo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Periodo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DisponivelDe = table.Column<DateOnly>(type: "date", nullable: true),
                    DisponivelAte = table.Column<DateOnly>(type: "date", nullable: true),
                    Prioritario = table.Column<bool>(type: "boolean", nullable: false),
                    Observacoes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ResolvidoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    AgendamentoId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListaEspera", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ListaEspera_Pacientes_PacienteId",
                        column: x => x.PacienteId,
                        principalTable: "Pacientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ListaEspera_Profissionais_ProfissionalId",
                        column: x => x.ProfissionalId,
                        principalTable: "Profissionais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ListaEspera_Agendamentos_AgendamentoId",
                        column: x => x.AgendamentoId,
                        principalTable: "Agendamentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex("IX_Profissionais_Nome", "Profissionais", "Nome");
            migrationBuilder.CreateIndex("IX_Salas_Nome", "Salas", "Nome", unique: true);
            migrationBuilder.CreateIndex("IX_ListaEspera_PacienteId", "ListaEspera", "PacienteId");
            migrationBuilder.CreateIndex("IX_ListaEspera_ProfissionalId", "ListaEspera", "ProfissionalId");
            migrationBuilder.CreateIndex("IX_ListaEspera_AgendamentoId", "ListaEspera", "AgendamentoId");
            migrationBuilder.CreateIndex("IX_ListaEspera_Status", "ListaEspera", "Status");

            migrationBuilder.CreateIndex(
                "IX_Agendamentos_ProfissionalId_DataHora", "Agendamentos",
                new[] { "ProfissionalId", "DataHora" });
            migrationBuilder.CreateIndex("IX_Agendamentos_SalaId", "Agendamentos", "SalaId");

            migrationBuilder.AddForeignKey(
                name: "FK_Agendamentos_Profissionais_ProfissionalId",
                table: "Agendamentos", column: "ProfissionalId",
                principalTable: "Profissionais", principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
            migrationBuilder.AddForeignKey(
                name: "FK_Agendamentos_Salas_SalaId",
                table: "Agendamentos", column: "SalaId",
                principalTable: "Salas", principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey("FK_Agendamentos_Profissionais_ProfissionalId", "Agendamentos");
            migrationBuilder.DropForeignKey("FK_Agendamentos_Salas_SalaId", "Agendamentos");
            migrationBuilder.DropIndex("IX_Agendamentos_ProfissionalId_DataHora", "Agendamentos");
            migrationBuilder.DropIndex("IX_Agendamentos_SalaId", "Agendamentos");

            migrationBuilder.DropTable("ListaEspera");
            migrationBuilder.DropTable("Profissionais");
            migrationBuilder.DropTable("Salas");

            migrationBuilder.DropColumn("ProfissionalId", "Agendamentos");
            migrationBuilder.DropColumn("SalaId", "Agendamentos");
            migrationBuilder.DropColumn("DuracaoMinutos", "Agendamentos");
            migrationBuilder.DropColumn("Encaixe", "Agendamentos");
            migrationBuilder.DropColumn("ChegadaEm", "Agendamentos");
            migrationBuilder.DropColumn("InicioAtendimentoEm", "Agendamentos");
        }
    }
}
