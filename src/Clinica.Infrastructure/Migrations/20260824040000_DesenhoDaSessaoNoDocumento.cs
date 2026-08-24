using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <summary>
    /// O DESENHO DA SESSÃO dentro do documento emitido (parcela 79).
    ///
    /// A ficha do atendimento passou a sair com o MAPA CORPORAL e a curva da dor. Os dois
    /// são copiados para dentro do documento no instante da emissão, e não lidos do
    /// prontuário na hora de imprimir: a regra mais antiga do `DocumentoClinico` é que a
    /// segunda via saia idêntica à que o paciente levou, e o mapa de uma sessão pode ser
    /// corrigido depois.
    ///
    /// ADITIVA: uma coluna anulável. Documento emitido antes desta versão fica com NULL, que
    /// é a verdade — ele foi impresso sem desenho nenhum, e a reimpressão dele continua
    /// saindo exatamente como saiu.
    /// </summary>
    public partial class DesenhoDaSessaoNoDocumento : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Desenho", table: "ItensDocumento",
                type: "text", nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Desenho", table: "ItensDocumento");
        }
    }
}
