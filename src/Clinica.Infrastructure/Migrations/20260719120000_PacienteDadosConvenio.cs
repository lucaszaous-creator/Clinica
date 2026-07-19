using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <summary>
    /// Dados do paciente exigidos pelas operadoras nas guias: data de nascimento,
    /// número da carteirinha e validade da carteirinha (vencida = guia recusada).
    /// </summary>
    [DbContext(typeof(ClinicaDbContext))]
    [Migration("20260719120000_PacienteDadosConvenio")]
    public partial class PacienteDadosConvenio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>("DataNascimento", "Pacientes", "date", nullable: true);
            migrationBuilder.AddColumn<string>("Carteirinha", "Pacientes", "character varying(40)", maxLength: 40, nullable: true);
            migrationBuilder.AddColumn<DateOnly>("ValidadeCarteirinha", "Pacientes", "date", nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn("DataNascimento", "Pacientes");
            migrationBuilder.DropColumn("Carteirinha", "Pacientes");
            migrationBuilder.DropColumn("ValidadeCarteirinha", "Pacientes");
        }
    }
}
