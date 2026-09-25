using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Clinica.Infrastructure.Migrations;

[DbContext(typeof(ClinicaDbContext))]
[Migration("20260925170000_DataRealizacaoInfusao")]
public sealed class DataRealizacaoInfusao : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.AddColumn<DateOnly>(
            name: "DataRealizacao", table: "ChecagensPrescricao", type: "date", nullable: true);

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropColumn(name: "DataRealizacao", table: "ChecagensPrescricao");
}
