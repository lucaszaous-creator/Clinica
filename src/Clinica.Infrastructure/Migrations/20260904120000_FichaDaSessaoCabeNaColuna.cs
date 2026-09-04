using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <summary>
    /// A FICHA DA SESSÃO CABE NA COLUNA QUE A RECEBE (set/2026 — a clínica clicou em
    /// "Imprimir esta sessão" e levou <i>"Não foi possível gravar. O banco respondeu:
    /// 22001: value too long for type character varying(1000)"</i>).
    ///
    /// A CAUSA é a cópia que ficou para trás. A importação do Smart Clinic alargou os
    /// quatro textos longos de `Evolucoes` para `text` — o prontuário antigo tem 88
    /// registros acima de 4.000 caracteres —, e `ItensDocumento.Detalhe`, que recebe NOVE
    /// campos da sessão concatenados, continuou em varchar(1000). O mesmo item leva em
    /// `Quantidade` QUEM ASSINA a linha (nome do profissional, até 120; na enfermagem,
    /// nome + COREN, até 183) numa coluna dimensionada para "1 caixa".
    ///
    /// MIGRATION-NAO-ADITIVA-CONSCIENTE(AlterColumn): alarga `Detalhe` de varchar(1000)
    /// para text e `Quantidade` de 60 para 200, em `ItensDocumento`. Alargar nunca perde
    /// linha — tudo o que cabia no teto antigo cabe no novo. Encolher, sim, e é por isso
    /// que o `Down` só é seguro enquanto nenhuma ficha tiver sido emitida por esta versão.
    ///
    /// ⚠️ Recortar o texto no serviço seria pior do que o erro: é o argumento do `Desenho`
    /// (que já nasce sem teto) e o da própria importação — cortar registro clínico em
    /// silêncio entrega ao paciente uma ficha com a história da doença picada no meio de
    /// uma frase, sem ninguém saber o que ficou de fora.
    ///
    /// Escrita à mão (não há dotnet ef neste ambiente): carimbo MAIOR que todas as
    /// migrations existentes; o nome da tabela é o do DbSet ("ItensDocumento" —
    /// checagem 41).
    /// </summary>
    public partial class FichaDaSessaoCabeNaColuna : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Detalhe",
                table: "ItensDocumento",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Quantidade",
                table: "ItensDocumento",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(60)",
                oldMaxLength: 60,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // O caminho de volta ENCOLHE: só é seguro enquanto nenhuma ficha emitida tiver
            // detalhe acima de 1.000 caracteres ou assinatura acima de 60 — depois da
            // primeira sessão longa impressa, não é.
            migrationBuilder.AlterColumn<string>(
                name: "Detalhe",
                table: "ItensDocumento",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Quantidade",
                table: "ItensDocumento",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);
        }
    }
}
