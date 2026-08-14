using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <summary>
    /// As seis colunas de data que nasceram COM fuso — e que por isso nunca puderam ser gravadas.
    ///
    /// O sistema grava hora de PAREDE (<c>DateTime.Now</c>, <c>Kind=Local</c>), e o Npgsql
    /// RECUSA isso numa coluna <c>timestamp with time zone</c> ("only UTC is supported"). O
    /// padrão do provedor para <c>DateTime</c> é justamente COM fuso, então esquecer o
    /// <c>HasColumnType</c> numa propriedade nova não quebra nada até alguém tentar gravá-la —
    /// em produção.
    ///
    /// Foi o que a clínica levou em 14/08/2026: um documento assinado com SUCESSO e, na
    /// publicação, "An error occurred while saving the entity changes". As outras cinco são da
    /// parcela 52 (cancelar evolução, anexo, avaliação e medida; versionar evolução) — as
    /// garantias de prontuário imutável que a auditoria da cliente exigiu, todas quebradas no
    /// Postgres, e nenhuma detectada pelos 1580 testes: eles rodam em SQLite, que guarda data
    /// como texto e não liga para o <c>Kind</c>. <c>DatasSemFusoTests</c> é a rede que faltava.
    ///
    /// MIGRATION-NAO-ADITIVA-CONSCIENTE(AlterColumn): troca o TIPO de seis colunas de data, de
    /// "timestamp with time zone" para "timestamp without time zone". Nenhuma linha se perde (o
    /// Postgres converte o valor existente) e, na prática, estas colunas estão VAZIAS — nunca
    /// foi possível escrever nelas, que é precisamente o defeito sendo corrigido.
    /// </summary>
    public partial class DatasDeCancelamentoSemFuso : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "SubstituidaEm",
                table: "VersoesEvolucao",
                type: "timestamp without time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CanceladaEm",
                table: "MedidasClinicas",
                type: "timestamp without time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CanceladaEm",
                table: "Evolucoes",
                type: "timestamp without time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "PublicadoEm",
                table: "DocumentosClinicos",
                type: "timestamp without time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CanceladaEm",
                table: "AvaliacoesClinicas",
                type: "timestamp without time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CanceladoEm",
                table: "AnexosProntuario",
                type: "timestamp without time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "SubstituidaEm",
                table: "VersoesEvolucao",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CanceladaEm",
                table: "MedidasClinicas",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CanceladaEm",
                table: "Evolucoes",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "PublicadoEm",
                table: "DocumentosClinicos",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CanceladaEm",
                table: "AvaliacoesClinicas",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CanceladoEm",
                table: "AnexosProntuario",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone",
                oldNullable: true);
        }
    }
}
