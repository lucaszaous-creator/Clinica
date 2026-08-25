using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <summary>
    /// Os itens novos das duas fichas de atendimento (parcela 77).
    ///
    /// MÉDICO — o retorno sugerido e o encaminhamento. O `PlanoTerapeutico` já dizia
    /// "reavaliar em 4 semanas" em texto livre e nada podia agir sobre a frase; e o
    /// encaminhamento entre as cinco especialidades da casa era conversa de corredor, fora
    /// do prontuário.
    ///
    /// ENFERMAGEM — o acesso venoso. A clínica tem sala de infusão e o acesso morava em
    /// texto corrido, de onde não se conta há quantos dias ele está no paciente.
    ///
    /// ADITIVA: sete colunas anuláveis. Registro anterior a esta versão fica com NULL, que é
    /// a verdade — não havia onde escrever nada disso quando ele foi feito.
    ///
    /// ⚠️ As três do médico entram TAMBÉM em VersoesEvolucao, no mesmo commit (lugar 4 da
    /// auditoria de linha): sem elas, corrigir a sessão apagaria o retorno anterior sem
    /// rastro — contra o art. 3º da Lei 13.787/2018.
    /// </summary>
    public partial class RetornoEncaminhamentoEAcessoVenoso : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var tabela in new[] { "Evolucoes", "VersoesEvolucao" })
            {
                migrationBuilder.AddColumn<DateOnly>(
                    name: "RetornoSugeridoEm", table: tabela,
                    type: "date", nullable: true);

                migrationBuilder.AddColumn<string>(
                    name: "RetornoSugeridoNota", table: tabela,
                    type: "character varying(300)", maxLength: 300, nullable: true);

                migrationBuilder.AddColumn<string>(
                    name: "Encaminhamento", table: tabela,
                    type: "character varying(600)", maxLength: 600, nullable: true);
            }

            migrationBuilder.AddColumn<string>(
                name: "AcessoLocal", table: "EvolucoesEnfermagem",
                type: "character varying(120)", maxLength: 120, nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AcessoCalibre", table: "EvolucoesEnfermagem",
                type: "character varying(20)", maxLength: 20, nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "AcessoPuncionadoEm", table: "EvolucoesEnfermagem",
                type: "date", nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var tabela in new[] { "Evolucoes", "VersoesEvolucao" })
            {
                migrationBuilder.DropColumn(name: "RetornoSugeridoEm", table: tabela);
                migrationBuilder.DropColumn(name: "RetornoSugeridoNota", table: tabela);
                migrationBuilder.DropColumn(name: "Encaminhamento", table: tabela);
            }

            migrationBuilder.DropColumn(name: "AcessoLocal", table: "EvolucoesEnfermagem");
            migrationBuilder.DropColumn(name: "AcessoCalibre", table: "EvolucoesEnfermagem");
            migrationBuilder.DropColumn(name: "AcessoPuncionadoEm", table: "EvolucoesEnfermagem");
        }
    }
}
