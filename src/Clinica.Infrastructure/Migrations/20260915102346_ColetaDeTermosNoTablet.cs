using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ColetaDeTermosNoTablet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SessoesTablet",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UsuarioId = table.Column<int>(type: "integer", nullable: false),
                    CredencialVersao = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Dispositivo = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Modo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ExpiraEm = table.Column<long>(type: "bigint", nullable: false),
                    Versao = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessoesTablet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessoesTablet_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ColetasTablet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessaoId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DocumentoId = table.Column<int>(type: "integer", nullable: false),
                    PacienteId = table.Column<int>(type: "integer", nullable: false),
                    Operadora = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IdentidadeConferida = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ChaveAtiva = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Estado = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ConteudoJson = table.Column<string>(type: "text", nullable: false),
                    ConteudoHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SubmissaoJson = table.Column<string>(type: "text", nullable: true),
                    SubmissaoHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    TracoPng = table.Column<byte[]>(type: "bytea", nullable: true),
                    Idempotencia = table.Column<Guid>(type: "uuid", nullable: true),
                    PreparadoEm = table.Column<long>(type: "bigint", nullable: false),
                    ExpiraEm = table.Column<long>(type: "bigint", nullable: false),
                    RecebidoEm = table.Column<long>(type: "bigint", nullable: true),
                    FinalizadoEm = table.Column<long>(type: "bigint", nullable: true),
                    Tentativas = table.Column<int>(type: "integer", nullable: false),
                    Falha = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Versao = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ColetasTablet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ColetasTablet_DocumentosClinicos_DocumentoId",
                        column: x => x.DocumentoId,
                        principalTable: "DocumentosClinicos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ColetasTablet_Pacientes_PacienteId",
                        column: x => x.PacienteId,
                        principalTable: "Pacientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ColetasTablet_SessoesTablet_SessaoId",
                        column: x => x.SessaoId,
                        principalTable: "SessoesTablet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ViasAssinadasPaciente",
                columns: table => new
                {
                    DocumentoId = table.Column<int>(type: "integer", nullable: false),
                    ColetaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Conteudo = table.Column<byte[]>(type: "bytea", nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EvidenciaJson = table.Column<string>(type: "text", nullable: false),
                    EvidenciaSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ArquivadoEm = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ViasAssinadasPaciente", x => x.DocumentoId);
                    table.ForeignKey(
                        name: "FK_ViasAssinadasPaciente_ColetasTablet_ColetaId",
                        column: x => x.ColetaId,
                        principalTable: "ColetasTablet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ViasAssinadasPaciente_DocumentosClinicos_DocumentoId",
                        column: x => x.DocumentoId,
                        principalTable: "DocumentosClinicos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ColetasTablet_ChaveAtiva",
                table: "ColetasTablet",
                column: "ChaveAtiva",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ColetasTablet_DocumentoId",
                table: "ColetasTablet",
                column: "DocumentoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ColetasTablet_Estado_ExpiraEm",
                table: "ColetasTablet",
                columns: new[] { "Estado", "ExpiraEm" });

            migrationBuilder.CreateIndex(
                name: "IX_ColetasTablet_PacienteId",
                table: "ColetasTablet",
                column: "PacienteId");

            migrationBuilder.CreateIndex(
                name: "IX_ColetasTablet_SessaoId",
                table: "ColetasTablet",
                column: "SessaoId");

            migrationBuilder.CreateIndex(
                name: "IX_SessoesTablet_ExpiraEm",
                table: "SessoesTablet",
                column: "ExpiraEm");

            migrationBuilder.CreateIndex(
                name: "IX_SessoesTablet_UsuarioId",
                table: "SessoesTablet",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_ViasAssinadasPaciente_ColetaId",
                table: "ViasAssinadasPaciente",
                column: "ColetaId",
                unique: true);
            // Proteção no banco também para versões anteriores dos desktops.
            // Cancelamento e vínculos assistenciais continuam possíveis; o original não muda.
            migrationBuilder.Sql("""
                CREATE FUNCTION public.tablet_via_imutavel() RETURNS trigger
                LANGUAGE plpgsql AS $corpo$
                BEGIN
                    RAISE EXCEPTION 'Via assinada imutável. Preserve o original.' USING ERRCODE = '23514';
                END $corpo$;
                CREATE TRIGGER tablet_proteger_via BEFORE UPDATE OR DELETE ON "ViasAssinadasPaciente"
                    FOR EACH ROW EXECUTE FUNCTION public.tablet_via_imutavel();

                CREATE FUNCTION public.tablet_documento_imutavel() RETURNS trigger
                LANGUAGE plpgsql AS $corpo$
                BEGIN
                    IF EXISTS (SELECT 1 FROM public."ViasAssinadasPaciente" WHERE "DocumentoId"=OLD."Id") THEN
                        IF TG_OP='DELETE' THEN
                            RAISE EXCEPTION 'Documento com via arquivada não pode ser excluído.' USING ERRCODE = '23514';
                        END IF;
                        IF ROW(NEW."PacienteId",NEW."Tipo",NEW."Numero",NEW."Data",NEW."Titulo",NEW."Corpo",
                               NEW."Observacoes",NEW."ModeloOrigemId",NEW."TracoAssinaturaId",NEW."PacienteAssinadoEm",
                               NEW."PacienteAssinaturaHash",NEW."PacienteDocumentoConferido",NEW."PacienteAssinaturaTestemunha",
                               NEW."PacienteAssinaturaMeio") IS DISTINCT FROM
                           ROW(OLD."PacienteId",OLD."Tipo",OLD."Numero",OLD."Data",OLD."Titulo",OLD."Corpo",
                               OLD."Observacoes",OLD."ModeloOrigemId",OLD."TracoAssinaturaId",OLD."PacienteAssinadoEm",
                               OLD."PacienteAssinaturaHash",OLD."PacienteDocumentoConferido",OLD."PacienteAssinaturaTestemunha",
                               OLD."PacienteAssinaturaMeio") THEN
                            RAISE EXCEPTION 'Conteúdo assinado imutável. Emita nova versão.' USING ERRCODE = '23514';
                        END IF;
                    END IF;
                    IF TG_OP='DELETE' THEN RETURN OLD; END IF;
                    RETURN NEW;
                END $corpo$;
                CREATE TRIGGER tablet_proteger_documento BEFORE UPDATE OR DELETE ON "DocumentosClinicos"
                    FOR EACH ROW EXECUTE FUNCTION public.tablet_documento_imutavel();

                CREATE FUNCTION public.tablet_item_imutavel() RETURNS trigger
                LANGUAGE plpgsql AS $corpo$
                BEGIN
                    IF (TG_OP <> 'INSERT' AND EXISTS (SELECT 1 FROM public."ViasAssinadasPaciente" WHERE "DocumentoId"=OLD."DocumentoClinicoId"))
                       OR (TG_OP <> 'DELETE' AND EXISTS (SELECT 1 FROM public."ViasAssinadasPaciente" WHERE "DocumentoId"=NEW."DocumentoClinicoId")) THEN
                        RAISE EXCEPTION 'Declarações assinadas imutáveis.' USING ERRCODE = '23514';
                    END IF;
                    IF TG_OP='DELETE' THEN RETURN OLD; END IF;
                    RETURN NEW;
                END $corpo$;
                CREATE TRIGGER tablet_proteger_itens BEFORE INSERT OR UPDATE OR DELETE ON "ItensDocumento"
                    FOR EACH ROW EXECUTE FUNCTION public.tablet_item_imutavel();

                CREATE FUNCTION public.tablet_traco_imutavel() RETURNS trigger
                LANGUAGE plpgsql AS $corpo$
                BEGIN
                    IF EXISTS (SELECT 1 FROM public."DocumentosClinicos" d
                        JOIN public."ViasAssinadasPaciente" v ON v."DocumentoId"=d."Id"
                        WHERE d."TracoAssinaturaId"=OLD."Id") THEN
                        RAISE EXCEPTION 'Rubrica arquivada imutável.' USING ERRCODE = '23514';
                    END IF;
                    IF TG_OP='DELETE' THEN RETURN OLD; END IF;
                    RETURN NEW;
                END $corpo$;
                CREATE TRIGGER tablet_proteger_traco BEFORE UPDATE OR DELETE ON "TracosAssinatura"
                    FOR EACH ROW EXECUTE FUNCTION public.tablet_traco_imutavel();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new InvalidOperationException("A reversão do portal preserva as vias assinadas. Reverta a aplicação, sem remover o schema.");
        }
    }
}
