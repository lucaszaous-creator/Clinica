using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Migrations
{
    /// <summary>
    /// Acerto de inventario no estoque (parcela 30).
    ///
    /// Ate aqui a clinica nao tinha como registrar a diferenca de contagem com
    /// honestidade: so podia lancar `Perda`, e perda e uma AFIRMACAO — alguem quebrou,
    /// venceu ou extraviou. Quando a contagem acha A MAIS, ou quando a diferenca vem de
    /// um erro de digitacao antigo, chamar de perda mente sobre o que aconteceu, e e essa
    /// mentira que faz o custo medio do insumo parar de valer.
    ///
    /// O tipo novo (`Ajuste`) NAO precisa de migration: TipoMovimentoEstoque e gravado
    /// como TEXTO (HasConversion&lt;string&gt;), entao um valor novo do enum entra sem
    /// tocar no schema. O que precisa de coluna e a DIRECAO do acerto.
    ///
    /// AjusteParaCima e um campo separado, e nao uma quantidade negativa, porque
    /// quantidade negativa espalharia Math.Abs por todo calculo de saldo e custo — e um
    /// esquecido daria saldo negativo silencioso.
    ///
    /// PURAMENTE ADITIVA: coluna nova e ANULAVEL (null nos tres tipos antigos, que ja
    /// dizem a direcao pelo proprio nome). Nenhuma linha existente muda, e o faturamento
    /// congelado nao le nem escreve nesta tabela.
    /// </summary>
    public partial class AjusteDeInventario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AjusteParaCima",
                table: "MovimentosEstoque",
                type: "boolean",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AjusteParaCima",
                table: "MovimentosEstoque");
        }
    }
}
