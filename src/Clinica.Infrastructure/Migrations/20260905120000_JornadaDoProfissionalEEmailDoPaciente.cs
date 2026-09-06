using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <summary>
    /// Duas colunas de agenda, ADITIVAS (set/2026):
    ///
    /// 1. A JORNADA do profissional (<c>DiasDeAtendimento</c>, <c>AtendeDas</c>, <c>AtendeAte</c>,
    ///    todas anuláveis). Nulo = não declarado, e é o que toda linha já gravada vale: a
    ///    grade e a marcação continuam como sempre para quem não preencher. Não há
    ///    <c>defaultValue</c> de propósito — um padrão "seg a sex, 8h às 18h" inventaria
    ///    uma jornada para todo profissional cadastrado e a agenda passaria a recusar
    ///    horários que hoje aceita.
    /// 2. O E-MAIL do paciente (<c>Email</c>, anulável), para o lembrete automático.
    ///
    /// Escrita à mão (sem <c>dotnet ef</c> neste ambiente). O nome das tabelas vem do
    /// <c>DbSet</c> — <c>Profissionais</c> e <c>Pacientes</c> —, nunca da classe (parcela 79).
    /// </summary>
    public partial class JornadaDoProfissionalEEmailDoPaciente : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeOnly>(
                name: "AtendeAte",
                table: "Profissionais",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "AtendeDas",
                table: "Profissionais",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DiasDeAtendimento",
                table: "Profissionais",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Pacientes",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AtendeAte",
                table: "Profissionais");

            migrationBuilder.DropColumn(
                name: "AtendeDas",
                table: "Profissionais");

            migrationBuilder.DropColumn(
                name: "DiasDeAtendimento",
                table: "Profissionais");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Pacientes");
        }
    }
}
