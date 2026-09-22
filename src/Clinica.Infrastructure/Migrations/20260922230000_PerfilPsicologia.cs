using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations;

/// <summary>Inclui Psicologia no catálogo configurável sem tocar no enum de especialidades faturáveis.</summary>
[DbContext(typeof(ClinicaDbContext))]
[Migration("20260922230000_PerfilPsicologia")]
public sealed class PerfilPsicologia : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (ActiveProvider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
            migrationBuilder.Sql("INSERT OR IGNORE INTO \"Especialidades\" (\"Codigo\", \"Nome\", \"Ativo\") VALUES ('Psicologia', 'Psicologia', 1)");
        else
            migrationBuilder.Sql("INSERT INTO \"Especialidades\" (\"Codigo\", \"Nome\", \"Ativo\") VALUES ('Psicologia', 'Psicologia', TRUE) ON CONFLICT (\"Codigo\") DO NOTHING");
    }

    // Uma especialidade já usada por profissionais ou consultas permanece no catálogo no rollback.
    protected override void Down(MigrationBuilder migrationBuilder) { }
}
