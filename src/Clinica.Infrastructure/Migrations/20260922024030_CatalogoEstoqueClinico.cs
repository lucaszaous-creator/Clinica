using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CatalogoEstoqueClinico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CustoInformado",
                table: "MovimentosEstoque",
                type: "numeric(14,4)",
                precision: 14,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DestinoConsumo",
                table: "MovimentosEstoque",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "NaoInformado");

            migrationBuilder.AddColumn<string>(
                name: "DocumentoEntrada",
                table: "MovimentosEstoque",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FatorConversao",
                table: "MovimentosEstoque",
                type: "numeric(14,3)",
                precision: 14,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Fornecedor",
                table: "MovimentosEstoque",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "QuantidadeInformada",
                table: "MovimentosEstoque",
                type: "numeric(14,3)",
                precision: 14,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SetorDestino",
                table: "MovimentosEstoque",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnidadeInformada",
                table: "MovimentosEstoque",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Apresentacao",
                table: "ItensEstoque",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CodigoBarras",
                table: "ItensEstoque",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CodigoInterno",
                table: "ItensEstoque",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EstoqueMaximo",
                table: "ItensEstoque",
                type: "numeric(14,3)",
                precision: 14,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ExigirLote",
                table: "ItensEstoque",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ExigirValidade",
                table: "ItensEstoque",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Fabricante",
                table: "ItensEstoque",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FatorCompra",
                table: "ItensEstoque",
                type: "numeric(14,3)",
                precision: 14,
                scale: 3,
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddColumn<string>(
                name: "Grupo",
                table: "ItensEstoque",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "MaterialAssistencial");

            migrationBuilder.AddColumn<string>(
                name: "LocalArmazenamento",
                table: "ItensEstoque",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnidadeCompra",
                table: "ItensEstoque",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Uso",
                table: "ItensEstoque",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Procedimentos");

            migrationBuilder.CreateIndex(
                name: "IX_ItensEstoque_CodigoInterno",
                table: "ItensEstoque",
                column: "CodigoInterno",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ItensEstoque_CodigoInterno",
                table: "ItensEstoque");

            migrationBuilder.DropColumn(
                name: "CustoInformado",
                table: "MovimentosEstoque");

            migrationBuilder.DropColumn(
                name: "DestinoConsumo",
                table: "MovimentosEstoque");

            migrationBuilder.DropColumn(
                name: "DocumentoEntrada",
                table: "MovimentosEstoque");

            migrationBuilder.DropColumn(
                name: "FatorConversao",
                table: "MovimentosEstoque");

            migrationBuilder.DropColumn(
                name: "Fornecedor",
                table: "MovimentosEstoque");

            migrationBuilder.DropColumn(
                name: "QuantidadeInformada",
                table: "MovimentosEstoque");

            migrationBuilder.DropColumn(
                name: "SetorDestino",
                table: "MovimentosEstoque");

            migrationBuilder.DropColumn(
                name: "UnidadeInformada",
                table: "MovimentosEstoque");

            migrationBuilder.DropColumn(
                name: "Apresentacao",
                table: "ItensEstoque");

            migrationBuilder.DropColumn(
                name: "CodigoBarras",
                table: "ItensEstoque");

            migrationBuilder.DropColumn(
                name: "CodigoInterno",
                table: "ItensEstoque");

            migrationBuilder.DropColumn(
                name: "EstoqueMaximo",
                table: "ItensEstoque");

            migrationBuilder.DropColumn(
                name: "ExigirLote",
                table: "ItensEstoque");

            migrationBuilder.DropColumn(
                name: "ExigirValidade",
                table: "ItensEstoque");

            migrationBuilder.DropColumn(
                name: "Fabricante",
                table: "ItensEstoque");

            migrationBuilder.DropColumn(
                name: "FatorCompra",
                table: "ItensEstoque");

            migrationBuilder.DropColumn(
                name: "Grupo",
                table: "ItensEstoque");

            migrationBuilder.DropColumn(
                name: "LocalArmazenamento",
                table: "ItensEstoque");

            migrationBuilder.DropColumn(
                name: "UnidadeCompra",
                table: "ItensEstoque");

            migrationBuilder.DropColumn(
                name: "Uso",
                table: "ItensEstoque");
        }
    }
}
