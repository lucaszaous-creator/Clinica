using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGlosa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "DataGlosa",
                table: "Codigos",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DataReapresentacao",
                table: "Codigos",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Glosa",
                table: "Codigos",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MotivoGlosa",
                table: "Codigos",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DataGlosa",
                table: "Codigos");

            migrationBuilder.DropColumn(
                name: "DataReapresentacao",
                table: "Codigos");

            migrationBuilder.DropColumn(
                name: "Glosa",
                table: "Codigos");

            migrationBuilder.DropColumn(
                name: "MotivoGlosa",
                table: "Codigos");
        }
    }
}
