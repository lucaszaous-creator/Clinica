using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations;

/// <summary>
/// A MÍDIA DO PRONTUÁRIO (set/2026): onde o arquivo mora quando ele é grande demais para
/// o banco.
///
/// ADITIVA — duas colunas anuláveis, uma em cada tabela de anexo. Nasce vazia em toda
/// linha já gravada, e vazio quer dizer "está no banco", que é a verdade de todas elas.
///
/// ⚠️ O valor NOVO do enum <c>TipoAnexo</c> (Video/Audio) não aparece aqui porque ele é
/// gravado como TEXTO na coluna que já existe — o que ele pede está escrito na entidade:
/// a clínica atualiza os cinco apps antes de anexar o primeiro vídeo, senão um binário
/// anterior perde a consulta inteira ao ler a linha (a lição da parcela 67).
/// </summary>
public partial class MidiaDoProntuarioNoArmazenamento : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "CaminhoRemoto",
            table: "AnexosProntuario",
            type: "character varying(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "CaminhoRemoto",
            table: "AnexosPaciente",
            type: "character varying(200)",
            maxLength: 200,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "CaminhoRemoto", table: "AnexosProntuario");
        migrationBuilder.DropColumn(name: "CaminhoRemoto", table: "AnexosPaciente");
    }
}
