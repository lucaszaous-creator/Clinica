using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <summary>
    /// Modalidades e especialidades dinâmicas: cria os catálogos (Modalidades, Especialidades),
    /// semeia as embutidas e adiciona as colunas de código nas tabelas que as referenciam
    /// (preenchidas com o enum atual), no mesmo padrão de Paciente.ConvenioCodigo.
    /// </summary>
    [DbContext(typeof(ClinicaDbContext))]
    [Migration("20260721120000_ModalidadesEspecialidadesDinamicas")]
    public partial class ModalidadesEspecialidadesDinamicas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Modalidades",
                columns: table => new
                {
                    Codigo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Nome = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Base = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Modalidades", x => x.Codigo);
                });

            migrationBuilder.CreateTable(
                name: "Especialidades",
                columns: table => new
                {
                    Codigo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Nome = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Especialidades", x => x.Codigo);
                });

            // Semeia as embutidas (código = nome do enum). Tipos explícitos: migração manual
            // sem BuildTargetModel, o EF não infere os tipos das colunas no InsertData.
            migrationBuilder.InsertData(
                table: "Modalidades",
                columns: new[] { "Codigo", "Nome", "Base", "Ativo" },
                columnTypes: new[] { "character varying(40)", "character varying(80)", "character varying(40)", "boolean" },
                values: new object[,]
                {
                    { "AcupunturaSimples", "Acupuntura (apenas)", "AcupunturaSimples", true },
                    { "AcupunturaComEletro", "Acupuntura + eletroacupuntura", "AcupunturaComEletro", true },
                    { "BsvComAcupuntura", "BSV + acupuntura", "BsvComAcupuntura", true },
                    { "BsvApenas", "BSV (apenas)", "BsvApenas", true },
                    { "Consulta", "Consulta", "Consulta", true }
                });

            migrationBuilder.InsertData(
                table: "Especialidades",
                columns: new[] { "Codigo", "Nome", "Ativo" },
                columnTypes: new[] { "character varying(40)", "character varying(80)", "boolean" },
                values: new object[,]
                {
                    { "Psiquiatria", "Psiquiatria", true },
                    { "Geriatria", "Geriatria", true },
                    { "Ginecologia", "Ginecologia", true },
                    { "Acupuntura", "Acupuntura", true },
                    { "ClinicaDaDor", "Clínica da Dor", true },
                    { "Endocrinologia", "Endocrinologia", true }
                });

            migrationBuilder.AddColumn<string>(
                name: "ModalidadeCodigo", table: "Atendimentos",
                type: "character varying(40)", maxLength: 40, nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "EspecialidadeConsultaCodigo", table: "Atendimentos",
                type: "character varying(40)", maxLength: 40, nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "EspecialidadeCodigo", table: "Codigos",
                type: "character varying(40)", maxLength: 40, nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "ModalidadeCodigo", table: "Agendamentos",
                type: "character varying(40)", maxLength: 40, nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "EspecialidadeConsultaCodigo", table: "Agendamentos",
                type: "character varying(40)", maxLength: 40, nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "ModalidadePreferidaCodigo", table: "Pacientes",
                type: "character varying(40)", maxLength: 40, nullable: true);

            // Registros existentes: código = o enum atual (modalidade/especialidade embutida).
            migrationBuilder.Sql(@"UPDATE ""Atendimentos"" SET ""ModalidadeCodigo"" = ""Modalidade"" WHERE ""ModalidadeCodigo"" IS NULL;");
            migrationBuilder.Sql(@"UPDATE ""Atendimentos"" SET ""EspecialidadeConsultaCodigo"" = ""EspecialidadeConsulta"" WHERE ""EspecialidadeConsultaCodigo"" IS NULL AND ""EspecialidadeConsulta"" IS NOT NULL;");
            migrationBuilder.Sql(@"UPDATE ""Codigos"" SET ""EspecialidadeCodigo"" = ""Especialidade"" WHERE ""EspecialidadeCodigo"" IS NULL AND ""Especialidade"" IS NOT NULL;");
            migrationBuilder.Sql(@"UPDATE ""Agendamentos"" SET ""ModalidadeCodigo"" = ""ModalidadePrevista"" WHERE ""ModalidadeCodigo"" IS NULL;");
            migrationBuilder.Sql(@"UPDATE ""Agendamentos"" SET ""EspecialidadeConsultaCodigo"" = ""EspecialidadeConsulta"" WHERE ""EspecialidadeConsultaCodigo"" IS NULL AND ""EspecialidadeConsulta"" IS NOT NULL;");
            migrationBuilder.Sql(@"UPDATE ""Pacientes"" SET ""ModalidadePreferidaCodigo"" = ""ModalidadePreferida"" WHERE ""ModalidadePreferidaCodigo"" IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ModalidadeCodigo", table: "Atendimentos");
            migrationBuilder.DropColumn(name: "EspecialidadeConsultaCodigo", table: "Atendimentos");
            migrationBuilder.DropColumn(name: "EspecialidadeCodigo", table: "Codigos");
            migrationBuilder.DropColumn(name: "ModalidadeCodigo", table: "Agendamentos");
            migrationBuilder.DropColumn(name: "EspecialidadeConsultaCodigo", table: "Agendamentos");
            migrationBuilder.DropColumn(name: "ModalidadePreferidaCodigo", table: "Pacientes");
            migrationBuilder.DropTable(name: "Modalidades");
            migrationBuilder.DropTable(name: "Especialidades");
        }
    }
}
