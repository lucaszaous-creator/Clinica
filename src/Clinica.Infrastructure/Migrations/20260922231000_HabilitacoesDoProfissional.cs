using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations;

[DbContext(typeof(ClinicaDbContext))]
[Migration("20260922231000_HabilitacoesDoProfissional")]
public sealed class HabilitacoesDoProfissional : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.AddColumn<string>(
            name: "HabilitacoesAtendimentoJson", table: "Profissionais", type: "text", nullable: true);

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropColumn(name: "HabilitacoesAtendimentoJson", table: "Profissionais");
}
