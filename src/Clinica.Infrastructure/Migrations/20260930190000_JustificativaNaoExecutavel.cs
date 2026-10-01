using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Clinica.Infrastructure.Migrations;

[DbContext(typeof(ClinicaDbContext))]
[Migration("20260930190000_JustificativaNaoExecutavel")]
public sealed class JustificativaNaoExecutavel : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.AddColumn<bool>("NaoExecutavel", "ChecagensPrescricao",
            type: "boolean", nullable: false, defaultValue: false);

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropColumn("NaoExecutavel", "ChecagensPrescricao");
}
