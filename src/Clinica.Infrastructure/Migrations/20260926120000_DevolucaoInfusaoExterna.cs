using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Clinica.Infrastructure.Migrations;

[DbContext(typeof(ClinicaDbContext))]
[Migration("20260926120000_DevolucaoInfusaoExterna")]
public sealed class DevolucaoInfusaoExterna : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>("DevolvidaEm", "PrescricoesInternas",
            type: "timestamp without time zone", nullable: true);
        migrationBuilder.AddColumn<string>("MotivoDevolucao", "PrescricoesInternas",
            type: "character varying(500)", maxLength: 500, nullable: true);
        migrationBuilder.AddColumn<int>("DevolvidaPorUsuarioId", "PrescricoesInternas",
            type: "integer", nullable: true);
        migrationBuilder.AddColumn<int>("RetificaPrescricaoId", "PrescricoesInternas",
            type: "integer", nullable: true);
        migrationBuilder.CreateIndex("IX_PrescricoesInternas_RetificaPrescricaoId",
            "PrescricoesInternas", "RetificaPrescricaoId", unique: true);
        migrationBuilder.AddForeignKey("FK_PrescricoesInternas_PrescricoesInternas_RetificaPrescricaoId",
            "PrescricoesInternas", "RetificaPrescricaoId", "PrescricoesInternas", "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey("FK_PrescricoesInternas_PrescricoesInternas_RetificaPrescricaoId", "PrescricoesInternas");
        migrationBuilder.DropIndex("IX_PrescricoesInternas_RetificaPrescricaoId", "PrescricoesInternas");
        migrationBuilder.DropColumn("DevolvidaEm", "PrescricoesInternas");
        migrationBuilder.DropColumn("MotivoDevolucao", "PrescricoesInternas");
        migrationBuilder.DropColumn("DevolvidaPorUsuarioId", "PrescricoesInternas");
        migrationBuilder.DropColumn("RetificaPrescricaoId", "PrescricoesInternas");
    }
}
