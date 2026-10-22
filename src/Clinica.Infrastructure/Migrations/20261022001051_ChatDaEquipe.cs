using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChatDaEquipe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LeiturasEquipe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UsuarioId = table.Column<int>(type: "integer", nullable: false),
                    Conversa = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    UltimaLidaId = table.Column<int>(type: "integer", nullable: false),
                    LidaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeiturasEquipe", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MensagensEquipe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Conversa = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    AutorId = table.Column<int>(type: "integer", nullable: false),
                    AutorNome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Texto = table.Column<string>(type: "text", nullable: false),
                    TextoBusca = table.Column<string>(type: "text", nullable: false),
                    EnviadaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EditadaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Mencoes = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    PacienteId = table.Column<int>(type: "integer", nullable: true),
                    PacienteNome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DocumentoClinicoId = table.Column<int>(type: "integer", nullable: true),
                    DocumentoRotulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ArquivoNome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ArquivoTamanho = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MensagensEquipe", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MensagensEquipe_DocumentosClinicos_DocumentoClinicoId",
                        column: x => x.DocumentoClinicoId,
                        principalTable: "DocumentosClinicos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MensagensEquipe_Pacientes_PacienteId",
                        column: x => x.PacienteId,
                        principalTable: "Pacientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MensagensEquipe_Usuarios_AutorId",
                        column: x => x.AutorId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PresencasEquipe",
                columns: table => new
                {
                    UsuarioId = table.Column<int>(type: "integer", nullable: false),
                    VistoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EscrevendoEm = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    EscrevendoAte = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PresencasEquipe", x => x.UsuarioId);
                });

            migrationBuilder.CreateTable(
                name: "ArquivosMensagemEquipe",
                columns: table => new
                {
                    MensagemId = table.Column<int>(type: "integer", nullable: false),
                    Conteudo = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArquivosMensagemEquipe", x => x.MensagemId);
                    table.ForeignKey(
                        name: "FK_ArquivosMensagemEquipe_MensagensEquipe_MensagemId",
                        column: x => x.MensagemId,
                        principalTable: "MensagensEquipe",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConversasEquipe",
                columns: table => new
                {
                    Conversa = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ParticipanteA = table.Column<int>(type: "integer", nullable: true),
                    ParticipanteB = table.Column<int>(type: "integer", nullable: true),
                    UltimaMensagemId = table.Column<int>(type: "integer", nullable: false),
                    UltimaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UltimoAutorId = table.Column<int>(type: "integer", nullable: false),
                    UltimoAutorNome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    UltimaPrevia = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversasEquipe", x => x.Conversa);
                    table.ForeignKey(
                        name: "FK_ConversasEquipe_MensagensEquipe_UltimaMensagemId",
                        column: x => x.UltimaMensagemId,
                        principalTable: "MensagensEquipe",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VersoesMensagemEquipe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MensagemId = table.Column<int>(type: "integer", nullable: false),
                    Texto = table.Column<string>(type: "text", nullable: false),
                    SubstituidaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VersoesMensagemEquipe", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VersoesMensagemEquipe_MensagensEquipe_MensagemId",
                        column: x => x.MensagemId,
                        principalTable: "MensagensEquipe",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConversasEquipe_ParticipanteA",
                table: "ConversasEquipe",
                column: "ParticipanteA");

            migrationBuilder.CreateIndex(
                name: "IX_ConversasEquipe_ParticipanteB",
                table: "ConversasEquipe",
                column: "ParticipanteB");

            migrationBuilder.CreateIndex(
                name: "IX_ConversasEquipe_UltimaMensagemId",
                table: "ConversasEquipe",
                column: "UltimaMensagemId");

            migrationBuilder.CreateIndex(
                name: "IX_LeiturasEquipe_Conversa",
                table: "LeiturasEquipe",
                column: "Conversa");

            migrationBuilder.CreateIndex(
                name: "IX_LeiturasEquipe_UsuarioId_Conversa",
                table: "LeiturasEquipe",
                columns: new[] { "UsuarioId", "Conversa" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MensagensEquipe_AutorId",
                table: "MensagensEquipe",
                column: "AutorId");

            migrationBuilder.CreateIndex(
                name: "IX_MensagensEquipe_Conversa_EditadaEm",
                table: "MensagensEquipe",
                columns: new[] { "Conversa", "EditadaEm" });

            migrationBuilder.CreateIndex(
                name: "IX_MensagensEquipe_Conversa_Id",
                table: "MensagensEquipe",
                columns: new[] { "Conversa", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_MensagensEquipe_DocumentoClinicoId",
                table: "MensagensEquipe",
                column: "DocumentoClinicoId");

            migrationBuilder.CreateIndex(
                name: "IX_MensagensEquipe_PacienteId",
                table: "MensagensEquipe",
                column: "PacienteId");

            migrationBuilder.CreateIndex(
                name: "IX_VersoesMensagemEquipe_MensagemId",
                table: "VersoesMensagemEquipe",
                column: "MensagemId");

            // A BUSCA (índice de texto): "procurar" num histórico de anos não lê as mensagens
            // uma a uma — o Postgres vai ao índice GIN e devolve só as que têm as palavras. A
            // coluna já vem minúscula e sem acento (ChatEquipeRegras.TextoDeBusca), então o
            // dicionário é o 'simple': sem radical, a palavra digitada casa como foi escrita
            // (com prefixo, ":*", na consulta). A expressão é a MESMA da consulta do serviço —
            // índice de expressão só é usado quando ela casa exatamente.
            if (ActiveProvider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
                migrationBuilder.Sql("""
                    CREATE INDEX "IX_MensagensEquipe_Busca" ON "MensagensEquipe"
                        USING GIN (to_tsvector('simple', "TextoBusca"));
                    """);

            // Entrega instantânea: o mesmo aviso dos agendamentos (AvisosInstantaneos). Só nasce
            // onde a função existe e o papel tem TRIGGER — sem ele o chat segue pelo relógio de
            // fundo, como as outras telas (o app nunca deixa de abrir por causa disto).
            if (ActiveProvider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
                migrationBuilder.Sql("""
                    DO $chat$
                    BEGIN
                        IF EXISTS (SELECT 1 FROM pg_proc WHERE proname = 'clinica_avisar_mudanca'
                                   AND pronamespace = 'public'::regnamespace)
                           AND has_table_privilege(current_user, 'public."MensagensEquipe"', 'TRIGGER')
                           AND NOT EXISTS (SELECT 1 FROM pg_trigger WHERE tgname = 'clinica_avisar_mensagens_equipe') THEN
                            CREATE TRIGGER clinica_avisar_mensagens_equipe AFTER INSERT OR UPDATE ON public."MensagensEquipe"
                                FOR EACH STATEMENT EXECUTE FUNCTION public.clinica_avisar_mudanca();
                        END IF;
                    END
                    $chat$;
                    """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
                migrationBuilder.Sql("DROP TRIGGER IF EXISTS clinica_avisar_mensagens_equipe ON \"MensagensEquipe\";");

            migrationBuilder.DropTable(
                name: "ArquivosMensagemEquipe");

            migrationBuilder.DropTable(
                name: "ConversasEquipe");

            migrationBuilder.DropTable(
                name: "LeiturasEquipe");

            migrationBuilder.DropTable(
                name: "PresencasEquipe");

            migrationBuilder.DropTable(
                name: "VersoesMensagemEquipe");

            migrationBuilder.DropTable(
                name: "MensagensEquipe");
        }
    }
}
