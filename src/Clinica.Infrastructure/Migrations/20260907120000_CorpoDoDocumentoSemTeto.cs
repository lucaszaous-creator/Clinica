using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <summary>
    /// O CORPO DO DOCUMENTO NÃO TEM TETO (set/2026 — a primeira rodada da suíte contra um
    /// Postgres de verdade).
    ///
    /// O texto do TCLE do BSV aprovado pelo advogado da clínica (<c>ModelosTermoBsv</c>,
    /// parcela 84) passa de 4.000 caracteres, e <c>ModelosDocumento.Corpo</c> era
    /// varchar(4000): o botão "Criar os termos do BSV" levaria um 22001 na clínica. Os
    /// testes não viam porque o SQLite ignora o tamanho declarado de uma coluna de texto —
    /// a rede de CI contra o Postgres (<c>testes-postgres</c> no verificar.yml) é quem pegou.
    ///
    /// <c>DocumentosClinicos.Corpo</c> vai junto porque é a CÓPIA: a emissão copia o corpo
    /// do modelo para o documento (a segunda via tem de sair idêntica), e alargar a origem
    /// sem alargar o destino só mudaria o lugar da falha — a lição da
    /// <c>FichaDaSessaoCabeNaColuna</c>, duas migrations atrás.
    ///
    /// MIGRATION-NAO-ADITIVA-CONSCIENTE(AlterColumn): alarga <c>Corpo</c> de varchar(4000)
    /// para text em <c>ModelosDocumento</c> e em <c>DocumentosClinicos</c>. Alargar nunca
    /// perde linha; o <c>Down</c> só é seguro enquanto nenhum texto acima de 4.000 tiver
    /// sido gravado.
    ///
    /// Escrita à mão (não há dotnet ef neste ambiente): carimbo MAIOR que todas as
    /// migrations existentes; os nomes das tabelas são os dos DbSets (checagem 41).
    /// </summary>
    public partial class CorpoDoDocumentoSemTeto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Corpo",
                table: "ModelosDocumento",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(4000)",
                oldMaxLength: 4000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Corpo",
                table: "DocumentosClinicos",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(4000)",
                oldMaxLength: 4000,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Encolher RECUSA no Postgres se alguma linha passar do teto — e é o certo:
            // um texto de consentimento cortado no meio é o que não pode existir.
            migrationBuilder.AlterColumn<string>(
                name: "Corpo",
                table: "ModelosDocumento",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Corpo",
                table: "DocumentosClinicos",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
