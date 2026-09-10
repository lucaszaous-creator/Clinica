using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// COMO ESTA SESSÃO VAI SER PAGA — a frase que o balcão lê antes de lançar (set/2026).
///
/// O defeito que estes testes prendem: a prévia do Novo atendimento dizia, para TODO
/// particular, "R$ 180,00 (tabela do particular) — o pagamento é registrado no Finalizar",
/// inclusive para quem tem um pacote de dez sessões com sete pelo uso. Nesse caso a sessão
/// não é cobrada: ela DEBITA do pacote, e é isso que o Finalizar propõe
/// (<c>FechamentoSessaoService.PrepararAsync</c> não sugere lançamento quando há pacote).
///
/// A tela afirmava uma cobrança que não ia acontecer, com um valor ao lado — e nada falhava.
/// </summary>
public class CobrancaDaSessaoTests
{
    private static SaldoPacote Pacote(string nome, int contratadas, int usadas) => new(
        PacoteId: 7, PacienteId: 1, PacienteNome: "Maria", Nome: nome,
        Tipo: TipoPacote.Sessoes, Situacao: StatusPacote.Ativo,
        SessoesContratadas: contratadas, SessoesUsadas: usadas,
        SaldoSessoes: contratadas - usadas, Valor: 1_000m,
        DataCompra: new DateOnly(2026, 8, 1), ValidoAte: new DateOnly(2026, 12, 31));

    private static readonly PrecoProposto Tabela =
        new(180m, "tabela do particular: Acupuntura (a partir de 01/08/2026)");

    /// <summary>
    /// Quem tem convênio não tem esta linha: o dinheiro dele vem pela conciliação da guia, e
    /// escrever qualquer coisa sobre pagamento no balcão seria inventar um passo.
    /// </summary>
    [Fact]
    public void Convenio_nao_tem_o_que_dizer_sobre_pagamento()
    {
        var c = CobrancaDaSessao.Montar(ehParticular: false, Tabela, pacote: null);

        c.Frase.Should().BeEmpty();
        c.DebitaDePacote.Should().BeFalse();
    }

    /// <summary>Particular sem pacote: o preço da tabela e ONDE o pagamento é registrado.</summary>
    [Fact]
    public void Particular_sem_pacote_mostra_o_preco_e_onde_registrar()
    {
        var c = CobrancaDaSessao.Montar(ehParticular: true, Tabela, pacote: null);

        c.Frase.Should().Contain("180,00");
        c.Frase.Should().Contain("Finalizar");
        c.DebitaDePacote.Should().BeFalse();
        c.SemPreco.Should().BeFalse();
    }

    /// <summary>
    /// O CASO QUE ESTAVA ERRADO: com pacote, a sessão debita e NÃO é cobrada. A frase diz o
    /// saldo, porque é ele que responde a pergunta seguinte do balcão ("quantas faltam?").
    ///
    /// ⚠️ E o valor da tabela NÃO aparece: dizer "R$ 180,00" ao lado de "debita do pacote" é
    /// oferecer dois números para a mesma sessão.
    /// </summary>
    [Fact]
    public void Particular_com_pacote_debita_e_nao_cobra()
    {
        var c = CobrancaDaSessao.Montar(
            ehParticular: true, Tabela, Pacote("10 sessões", contratadas: 10, usadas: 3));

        c.DebitaDePacote.Should().BeTrue();
        c.Frase.Should().Contain("DEBITA");
        c.Frase.Should().Contain("7 de 10 sessões");
        c.Frase.Should().NotContain("180");
        c.SemPreco.Should().BeFalse();
    }

    /// <summary>
    /// Sem preço cadastrado não se inventa valor — e a frase aponta a porta do BALCÃO. Ela
    /// mandava a recepcionista ao "Gerente → Tabela de preço", que é outro app e outra
    /// pessoa; instrução que manda procurar no lugar errado é pior que nenhuma.
    /// </summary>
    [Fact]
    public void Particular_sem_preco_diz_onde_cadastrar_sem_inventar_valor()
    {
        var c = CobrancaDaSessao.Montar(ehParticular: true, PrecoProposto.Nenhum, pacote: null);

        c.SemPreco.Should().BeTrue();
        c.Frase.Should().Contain("Particular e pacotes");
        c.Frase.Should().Contain("Preço da sessão");
        c.Frase.Should().NotContain("R$ 0,00");
    }

    /// <summary>
    /// O PACOTE vence o preço: tendo sessão comprada não há valor a combinar, mesmo com
    /// tabela cadastrada. É a mesma ordem do <c>FechamentoSessaoService</c> — sem ela as duas
    /// telas discordariam sobre a mesma sessão.
    /// </summary>
    [Fact]
    public void O_pacote_vence_o_preco_de_tabela()
    {
        var comTabela = CobrancaDaSessao.Montar(
            ehParticular: true, Tabela, Pacote("Mensal", contratadas: 8, usadas: 1));
        var semTabela = CobrancaDaSessao.Montar(
            ehParticular: true, PrecoProposto.Nenhum, Pacote("Mensal", contratadas: 8, usadas: 1));

        comTabela.Should().Be(semTabela);
        comTabela.SemPreco.Should().BeFalse("com pacote não falta preço — não há o que cobrar");
    }
}
