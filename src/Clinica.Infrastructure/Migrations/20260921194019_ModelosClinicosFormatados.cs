using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ModelosClinicosFormatados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IndicacaoFormatada",
                table: "PrescricoesInternas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ObservacoesFormatadas",
                table: "PrescricoesInternas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConfiguracaoInfusao",
                table: "ModelosDocumento",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CorpoFormatado",
                table: "ModelosDocumento",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ParaInfusao",
                table: "ModelosDocumento",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DescricaoFormatada",
                table: "ItensPrescricaoInterna",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ObservacoesFormatadas",
                table: "ItensPrescricaoInterna",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DescricaoFormatada",
                table: "ItensModelo",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DetalheFormatado",
                table: "ItensModelo",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DescricaoFormatada",
                table: "ItensDocumento",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DetalheFormatado",
                table: "ItensDocumento",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CorpoFormatado",
                table: "DocumentosClinicos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ObservacoesFormatadas",
                table: "DocumentosClinicos",
                type: "text",
                nullable: true);
            migrationBuilder.Sql("""
                CREATE FUNCTION public.proteger_formato_documento() RETURNS trigger LANGUAGE plpgsql AS $f$
                BEGIN
                  IF (OLD."AssinadoEm" IS NOT NULL OR OLD."PacienteAssinadoEm" IS NOT NULL)
                    AND ROW(NEW."CorpoFormatado",NEW."ObservacoesFormatadas") IS DISTINCT FROM ROW(OLD."CorpoFormatado",OLD."ObservacoesFormatadas") THEN
                    RAISE EXCEPTION 'A formatação do documento assinado deve ser preservada.' USING ERRCODE='23514';
                  END IF;
                  RETURN NEW;
                END $f$;
                CREATE TRIGGER proteger_formato_documento BEFORE UPDATE ON public."DocumentosClinicos"
                  FOR EACH ROW EXECUTE FUNCTION public.proteger_formato_documento();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new InvalidOperationException("Reverta a aplicação preservando o texto e a formatação salvos. Não remova o schema.");
        }
    }
}
