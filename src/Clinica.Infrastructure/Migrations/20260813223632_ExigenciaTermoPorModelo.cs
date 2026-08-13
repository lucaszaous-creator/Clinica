using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <summary>
    /// Um procedimento passa a poder exigir VÁRIOS termos (parcela 67).
    ///
    /// A chave era (modalidade, variante), então o BSV só podia exigir um papel — e ele
    /// precisa de dois com validades opostas: o consentimento, que vale a partir da
    /// assinatura, e a declaração de jejum, que não se herda de um dia para o outro. Com a
    /// chave antiga, amarrar o segundo APAGAVA o primeiro em silêncio.
    ///
    /// A troca ALARGA a chave: nada é apagado, e toda linha que passava na chave antiga
    /// passa na nova. O que ela continua impedindo é a duplicata de verdade — a mesma
    /// modalidade exigindo o mesmo modelo duas vezes.
    ///
    /// MIGRATION-NAO-ADITIVA-CONSCIENTE(DropIndex): alarga a chave única de ExigenciasTermo — nenhuma linha se perde.
    ///
    /// O drop é do índice ESTREITO, e toda linha que passava nele passa no novo. A tabela é
    /// da suíte (parcela 66) e o app de faturamento não a lê nem a escreve; versão antiga da
    /// suíte em campo continua funcionando, porque o `ExigirAsync` dela substitui em memória
    /// antes de gravar — ela nunca chega a tentar a segunda linha que o índice novo permite.
    /// </summary>
    public partial class ExigenciaTermoPorModelo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ExigenciasTermo_Modalidade_ModalidadeCodigo",
                table: "ExigenciasTermo");

            migrationBuilder.CreateIndex(
                name: "IX_ExigenciasTermo_Modalidade_ModalidadeCodigo_ModeloDocumento~",
                table: "ExigenciasTermo",
                columns: new[] { "Modalidade", "ModalidadeCodigo", "ModeloDocumentoId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ExigenciasTermo_Modalidade_ModalidadeCodigo_ModeloDocumento~",
                table: "ExigenciasTermo");

            migrationBuilder.CreateIndex(
                name: "IX_ExigenciasTermo_Modalidade_ModalidadeCodigo",
                table: "ExigenciasTermo",
                columns: new[] { "Modalidade", "ModalidadeCodigo" },
                unique: true);
        }
    }
}
