using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class WhatsAppDaEquipe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TelefoneWhatsApp",
                table: "Usuarios",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WhatsAppLigadoEm",
                table: "Usuarios",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AvisoWhatsAppEm",
                table: "PedidosRecepcao",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_TelefoneWhatsApp",
                table: "Usuarios",
                column: "TelefoneWhatsApp",
                unique: true,
                filter: "\"TelefoneWhatsApp\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Usuarios_TelefoneWhatsApp",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "TelefoneWhatsApp",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "WhatsAppLigadoEm",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "AvisoWhatsAppEm",
                table: "PedidosRecepcao");
        }
    }
}
