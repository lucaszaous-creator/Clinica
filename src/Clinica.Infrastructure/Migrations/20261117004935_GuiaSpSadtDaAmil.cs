using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class GuiaSpSadtDaAmil : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AusenciaCodigoValidacao",
                table: "GuiasTissConsulta",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CaraterAtendimento",
                table: "GuiasTissConsulta",
                type: "character varying(1)",
                maxLength: 1,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CodigoValidacao",
                table: "GuiasTissConsulta",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DataAutorizacao",
                table: "GuiasTissConsulta",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DescricaoProcedimento",
                table: "GuiasTissConsulta",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModeloGuia",
                table: "GuiasTissConsulta",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Senha",
                table: "GuiasTissConsulta",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TipoAtendimento",
                table: "GuiasTissConsulta",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ValidadeSenha",
                table: "GuiasTissConsulta",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AusenciaCodigoValidacao",
                table: "GuiasTissConsulta");

            migrationBuilder.DropColumn(
                name: "CaraterAtendimento",
                table: "GuiasTissConsulta");

            migrationBuilder.DropColumn(
                name: "CodigoValidacao",
                table: "GuiasTissConsulta");

            migrationBuilder.DropColumn(
                name: "DataAutorizacao",
                table: "GuiasTissConsulta");

            migrationBuilder.DropColumn(
                name: "DescricaoProcedimento",
                table: "GuiasTissConsulta");

            migrationBuilder.DropColumn(
                name: "ModeloGuia",
                table: "GuiasTissConsulta");

            migrationBuilder.DropColumn(
                name: "Senha",
                table: "GuiasTissConsulta");

            migrationBuilder.DropColumn(
                name: "TipoAtendimento",
                table: "GuiasTissConsulta");

            migrationBuilder.DropColumn(
                name: "ValidadeSenha",
                table: "GuiasTissConsulta");
        }
    }
}
