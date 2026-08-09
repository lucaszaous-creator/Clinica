using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PublicacaoDocumentoAssinado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "PublicadoAte",
                table: "DocumentosClinicos",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PublicadoEm",
                table: "DocumentosClinicos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TokenPublicacao",
                table: "DocumentosClinicos",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PublicadoAte",
                table: "DocumentosClinicos");

            migrationBuilder.DropColumn(
                name: "PublicadoEm",
                table: "DocumentosClinicos");

            migrationBuilder.DropColumn(
                name: "TokenPublicacao",
                table: "DocumentosClinicos");
        }
    }
}
