using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O painel da DIREÇÃO sobre os papéis emitidos (set/2026, tela 4 do mockup aprovado
/// "documentos nas quatro telas").
///
/// Os números daqui são os que a direção lê — "Receituário, 31%", "6 cancelados" —, e por
/// isso a conta mora na Application e não na ViewModel do WPF.
/// </summary>
public class PainelDeDocumentosTests
{
    private static readonly DateOnly Hoje = new(2026, 9, 10);

    private static FolhaEmitida Folha(
        string rotulo, string? profissional = null, bool cancelada = false,
        bool assinada = false, DateOnly? publicadoAte = null, int id = 1)
        => new(id, NaturezaFolha.Clinico, $"2026/{id:0000}", "ABCD-1234", rotulo,
            "Maria Aparecida Silva", profissional, Hoje, DateTime.Now, "ana",
            cancelada, cancelada ? "dose errada" : null)
        {
            Assinado = assinada,
            PublicadoAte = publicadoAte,
            Chave = "receita"
        };

    /// <summary>
    /// "Emitidos" conta os CANCELADOS também: o número da folha foi gasto, a numeração por
    /// ano não se reaproveita, e esconder o cancelado daqui faria a contagem não bater com
    /// a sequência dos números.
    /// </summary>
    [Fact]
    public void O_cancelado_conta_como_emitido_e_tambem_no_proprio_numero()
    {
        var painel = PainelDeDocumentos.Montar(
            [Folha("Receituário", id: 1), Folha("Receituário", cancelada: true, id: 2)], Hoje);

        painel.Emitidos.Should().Be(2);
        painel.Cancelados.Should().Be(1);
        painel.Canceladas.Should().HaveCount(1);
        painel.Canceladas[0].MotivoCancelamento.Should().Be("dose errada");
    }

    /// <summary>
    /// O link no ar conta o dia do vencimento INTEIRO: quem publicou com prazo até 10/09
    /// espera que a receita abra no dia 10, não que ela caia na virada.
    /// </summary>
    [Fact]
    public void No_ar_conta_o_dia_do_vencimento_inteiro()
    {
        var painel = PainelDeDocumentos.Montar(
        [
            Folha("Receituário", publicadoAte: Hoje, id: 1),
            Folha("Receituário", publicadoAte: Hoje.AddDays(-1), id: 2),
            Folha("Receituário", id: 3)
        ], Hoje);

        painel.NoAr.Should().Be(1);
    }

    [Fact]
    public void Assinados_conta_so_o_que_foi_selado_com_e_CPF()
        => PainelDeDocumentos.Montar(
                [Folha("Receituário", assinada: true, id: 1), Folha("Atestado", id: 2)], Hoje)
            .Assinados.Should().Be(1);

    /// <summary>
    /// O desempate é pelo NOME. Sem ele, duas folhas com a mesma contagem trocariam de
    /// lugar entre duas leituras do MESMO período, e a direção veria o "mais emitido"
    /// mudar sem nada ter mudado.
    /// </summary>
    [Fact]
    public void O_mais_emitido_traz_a_fatia_e_desempata_pelo_nome()
    {
        var tresReceitas = PainelDeDocumentos.Montar(
        [
            Folha("Receituário", id: 1), Folha("Receituário", id: 2),
            Folha("Receituário", id: 3), Folha("Atestado", id: 4)
        ], Hoje);

        tresReceitas.MaisEmitido.Should().Be("Receituário — 75%");

        var empate = PainelDeDocumentos.Montar(
            [Folha("Receituário", id: 1), Folha("Atestado", id: 2)], Hoje);

        empate.MaisEmitido.Should().StartWith("Atestado");
    }

    /// <summary>
    /// Sem emissão nenhuma, o cartão fica VAZIO em vez de escrever "— 0%": um número onde
    /// não há pergunta a responder é pior do que o travessão.
    /// </summary>
    [Fact]
    public void Periodo_sem_emissao_nao_inventa_numero()
    {
        var painel = PainelDeDocumentos.Montar([], Hoje);

        painel.Vazio.Should().BeTrue();
        painel.MaisEmitido.Should().BeEmpty();
        painel.PorQuemAssina.Should().BeEmpty();
    }

    /// <summary>
    /// Quem não assinou é o BALCÃO, nomeado — e não uma linha em branco. A fração é
    /// normalizada pela MAIOR barra, porque o que se compara é um profissional com o
    /// outro; sobre o total, a maior barra de uma clínica com cinco profissionais nunca
    /// passaria de um quinto da largura.
    /// </summary>
    [Fact]
    public void Por_quem_assina_nomeia_o_balcao_e_normaliza_pela_maior_barra()
    {
        var painel = PainelDeDocumentos.Montar(
        [
            Folha("Receituário", "Dra. Ana", id: 1),
            Folha("Receituário", "Dra. Ana", id: 2),
            Folha("Declaração de comparecimento", id: 3)
        ], Hoje);

        painel.PorQuemAssina.Should().HaveCount(2);
        painel.PorQuemAssina[0].Nome.Should().Be("Dra. Ana");
        painel.PorQuemAssina[0].Quantidade.Should().Be(2);
        painel.PorQuemAssina[0].Fracao.Should().Be(1);

        painel.PorQuemAssina[1].Nome.Should().Be(PainelDeDocumentos.SemAssinatura);
        painel.PorQuemAssina[1].Fracao.Should().Be(0.5);
    }

    /// <summary>
    /// A barra é desenhada pelo <c>ItemBarraRotulada</c> do design system, que espera
    /// <c>Rotulo</c>, <c>ValorRotulo</c> e <c>Fracao</c>. Sem esses dois nomes a linha sai
    /// em branco — binding morto não quebra build nem teste.
    /// </summary>
    [Fact]
    public void A_linha_da_barra_fala_a_lingua_do_design_system()
    {
        var linha = new QuemAssina("Dra. Ana", 102, 1);

        linha.Rotulo.Should().Be("Dra. Ana");
        linha.ValorRotulo.Should().Be("102");
    }
}
