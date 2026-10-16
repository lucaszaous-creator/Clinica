using Clinica.Application.Formatacao;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O que a tela de Estoque afirma (out/2026, migração do EstoqueViewModel para Blazor).
/// As decisões que moravam no ViewModel do WPF subiram para <see cref="TelaDoEstoque"/>,
/// e são conferidas aqui — o Razor só desenha.
/// </summary>
public class TelaDoEstoqueTests
{
    private static SaldoItemEstoque Saldo(decimal saldo, decimal minimo, decimal? maximo = null)
        => new(1, "Agulha 0,25 × 30", "un", saldo, minimo, true, 0.35m) { EstoqueMaximo = maximo };

    [Fact]
    public void Resumo_sem_alerta_diz_quantos_itens_e_que_nada_falta()
    {
        var (texto, sem) = TelaDoEstoque.Resumo(12, 0, 0);
        sem.Should().BeTrue();
        texto.Should().Be("12 item(ns) no estoque — nada abaixo do mínimo nem vencendo.");
    }

    [Fact]
    public void Resumo_com_alerta_conta_o_que_repor_e_os_lotes()
    {
        var (texto, sem) = TelaDoEstoque.Resumo(12, 2, 3);
        sem.Should().BeFalse();
        texto.Should().Be("2 item(ns) para repor · 3 lote(s) vencendo ou vencidos.");
    }

    [Fact]
    public void Minimo_zero_e_sem_minimo_e_nao_zero_unidade()
    {
        TelaDoEstoque.Minimo(Saldo(10, 0)).Should().Be("—");
        TelaDoEstoque.Minimo(Saldo(10, 2.5m)).Should().Be("2,5 un");
    }

    [Theory]
    [InlineData(3, 10, null, 17)]   // sem máximo: o dobro do mínimo menos o saldo
    [InlineData(3, 10, 50, 47)]     // com máximo: até o máximo
    [InlineData(30, 10, null, 0)]   // acima do dobro: nunca negativo
    public void Sugestao_de_compra_repoe_acima_do_minimo(decimal saldo, decimal minimo, int? maximo, decimal esperado)
        => TelaDoEstoque.SugestaoDeCompra(Saldo(saldo, minimo, maximo)).Should().Be(esperado);

    [Fact]
    public void Lote_vencido_e_lote_a_vencer()
    {
        var hoje = new DateOnly(2026, 10, 15);
        TelaDoEstoque.SituacaoDoLote(new ValidadeProxima(1, "Álcool", hoje.AddDays(-1), 3, "L1"), hoje).Should().Be("VENCIDO");
        TelaDoEstoque.SituacaoDoLote(new ValidadeProxima(1, "Álcool", hoje.AddDays(9), 3, "L1"), hoje).Should().Be("vence em 9 dia(s)");
    }

    [Fact]
    public void Inventario_pede_lote_e_validade_conforme_o_cadastro()
    {
        var comum = new ItemEstoque { Grupo = GrupoEstoque.MaterialAssistencial };
        TelaDoEstoque.InventarioPedeLote(comum).Should().BeFalse();
        TelaDoEstoque.InventarioPedeValidade(comum).Should().BeFalse();

        var soLote = new ItemEstoque { ExigirLote = true };
        TelaDoEstoque.InventarioPedeLote(soLote).Should().BeTrue();
        TelaDoEstoque.InventarioPedeValidade(soLote).Should().BeFalse();

        var validade = new ItemEstoque { ExigirValidade = true };
        TelaDoEstoque.InventarioPedeLote(validade).Should().BeTrue("quem exige validade identifica o lote dela");
        TelaDoEstoque.InventarioPedeValidade(validade).Should().BeTrue();

        var remedio = new ItemEstoque { Grupo = GrupoEstoque.Medicamento };
        TelaDoEstoque.InventarioPedeLote(remedio).Should().BeTrue();
        TelaDoEstoque.InventarioPedeValidade(remedio).Should().BeTrue();
    }

    [Fact]
    public void Resultado_do_inventario_diz_a_direcao_ou_que_bateu()
    {
        TelaDoEstoque.ResultadoDoInventario(null).Should().Contain("bateu");
        TelaDoEstoque.ResultadoDoInventario(new MovimentoEstoque { Tipo = TipoMovimentoEstoque.Ajuste, Quantidade = 4, AjusteParaCima = true })
            .Should().Be("Ajuste para cima: 4 a mais no saldo.");
        TelaDoEstoque.ResultadoDoInventario(new MovimentoEstoque { Tipo = TipoMovimentoEstoque.Ajuste, Quantidade = 1.5m, AjusteParaCima = false })
            .Should().Be("Ajuste para baixo: 1,5 a menos no saldo.");
    }

    /// <summary>
    /// O saldo depois de cada movimento usa a MESMA conta do saldo da lista (Delta): o
    /// acerto PARA BAIXO subtrai, a perda subtrai, a entrada soma — e a primeira linha
    /// (a mais recente) é o saldo atual.
    /// </summary>
    [Fact]
    public void Extrato_acumula_na_ordem_cronologica_e_mostra_do_mais_recente()
    {
        var d = new DateOnly(2026, 10, 1);
        var cronologica = new[]
        {
            new MovimentoEstoque { Data = d, Tipo = TipoMovimentoEstoque.Entrada, Quantidade = 100, Lote = "A1", Validade = d.AddMonths(6), CriadoPor = "ana" },
            new MovimentoEstoque { Data = d.AddDays(1), Tipo = TipoMovimentoEstoque.Saida, Quantidade = 10, AtendimentoId = 7 },
            new MovimentoEstoque { Data = d.AddDays(2), Tipo = TipoMovimentoEstoque.Perda, Quantidade = 5, Observacao = "caixa molhada" },
            new MovimentoEstoque { Data = d.AddDays(3), Tipo = TipoMovimentoEstoque.Ajuste, Quantidade = 2, AjusteParaCima = false, Observacao = "inventário" },
        };
        var extrato = TelaDoEstoque.Extrato(cronologica.Reverse().ToList());

        extrato.SaldoFinal.Should().Be(83);
        extrato.Linhas.Select(l => l.SaldoApos).Should().Equal("83", "85", "90", "100");
        extrato.Linhas.Select(l => l.Movimento).Should().Equal("−2", "−5", "−10", "+100");
        extrato.Linhas[0].Detalhe.Should().Contain("contagem achou a menos");
        extrato.Linhas[1].EhPerda.Should().BeTrue();
        extrato.Linhas[1].Detalhe.Should().StartWith("caixa molhada", "o motivo da perda é o que a clínica exige e precisa ser lido");
        extrato.Linhas[2].Detalhe.Should().Contain("consumo do atendimento #7");
        extrato.Linhas[3].Detalhe.Should().Contain("lote A1").And.Contain("validade 01/04/2027");
        extrato.Linhas[3].Quem.Should().Be("ana");
        extrato.Linhas[2].Quem.Should().Be("—");
        extrato.Resumo.Should().Be("4 movimento(s) · saldo atual 83");
    }

    [Fact]
    public void Extrato_vazio_diz_que_nao_ha_movimento()
        => TelaDoEstoque.Extrato([]).Resumo.Should().Be("Nenhum movimento registrado.");

    [Fact]
    public void Custo_sem_sessao_tem_media_nenhuma_e_nao_zero()
    {
        var r = TelaDoEstoque.ResumoDeCusto(new ResumoCustoSessoes(0, 0m, null, null));
        r.SemCusto.Should().BeTrue();
        r.Medio.Should().Be("—");
        r.Frase.Should().Contain("Nenhuma sessão baixou insumo");
    }

    [Fact]
    public void Custo_incompleto_marca_o_total_como_parcial()
    {
        var r = TelaDoEstoque.ResumoDeCusto(new ResumoCustoSessoes(3, 12.5m, null, null) { SessoesComCustoIncompleto = 1 });
        r.Total.Should().EndWith("(parcial)");
        r.Medio.Should().Be("—");
        r.Frase.Should().StartWith("1 sessão(ões) com custo incompleto");
    }

    [Fact]
    public void Custo_completo_nomeia_a_sessao_mais_cara()
    {
        var cara = new CustoDeSessao(9, new DateOnly(2026, 10, 3), "Ana", 8.4m, ["Agulha × 12"]);
        var r = TelaDoEstoque.ResumoDeCusto(new ResumoCustoSessoes(2, 12m, 6m, cara));
        r.Sessoes.Should().Be("2");
        r.Frase.Should().Contain("mais cara").And.Contain("03/10/2026");
        TelaDoEstoque.CustoDaSessao(cara with { Completo = false }).Should().EndWith("(parcial)");
        TelaDoEstoque.PacienteDaSessao(cara with { Paciente = null }).Should().Be("(paciente não informado)");
    }

    [Fact]
    public void Linhas_dos_materiais_tiram_a_rotina_e_trazem_o_registrado_preenchido()
    {
        var saldos = new[]
        {
            new SaldoItemEstoque(1, "Agulha", "un", 100, 0, true, null) { Uso = UsoEstoque.Procedimentos, CodigoInterno = "AG25" },
            new SaldoItemEstoque(2, "Detergente", "fr", 3, 0, true, null) { Uso = UsoEstoque.Rotina },
        };
        var registrados = new[]
        {
            new MaterialConsumidoProcedimento(1, 6, "L1"),
            new MaterialConsumidoProcedimento(1, 4, "L1"),
            new MaterialConsumidoProcedimento(5, 1, null), // item que saiu da lista
        };
        var linhas = TelaDoEstoque.LinhasDosMateriais(saldos, registrados, new Dictionary<int, string> { [5] = "Eletrodo" });

        linhas.Should().HaveCount(2);
        linhas[0].Nome.Should().Be("AG25 · Agulha");
        linhas[0].Quantidade.Should().Be("10");
        linhas[0].Lote.Should().Be("L1");
        linhas[1].Nome.Should().Be("Eletrodo");
        linhas[1].Saldo.Should().Be("Já registrado nesta sessão");
    }

    [Fact]
    public void Pedido_de_materiais_exige_material_ou_a_declaracao_de_que_nao_houve()
    {
        var linha = new LinhaMaterialDaSessao { ItemId = 1, Nome = "Agulha" };
        var nada = () => TelaDoEstoque.PedidoDosMateriais([linha], semConsumo: false);
        nada.Should().Throw<InvalidOperationException>();

        TelaDoEstoque.PedidoDosMateriais([linha], semConsumo: true).SemConsumo.Should().BeTrue();

        linha.Quantidade = "2,5";
        var ambos = () => TelaDoEstoque.PedidoDosMateriais([linha], semConsumo: true);
        ambos.Should().Throw<InvalidOperationException>();
        TelaDoEstoque.PedidoDosMateriais([linha], semConsumo: false).Materiais.Single().Quantidade.Should().Be(2.5m);

        linha.Quantidade = "0,0005";
        var casas = () => TelaDoEstoque.PedidoDosMateriais([linha], semConsumo: false);
        casas.Should().Throw<InvalidOperationException>().WithMessage("Agulha:*");
    }

    [Fact]
    public void Lista_de_compras_sai_em_pt_BR()
    {
        var linhas = TelaDoEstoque.ListaDeCompras([Saldo(1.5m, 10)]);
        linhas.Single().Should().Equal("Agulha 0,25 × 30", "un", "1,5", "10", "18,5", "R$ 0,35");
    }

    [Fact]
    public void Quantidade_digitada_com_virgula_e_lida_como_fracao()
    {
        LeituraDeValores.TentarLerQuantidade("2,5", out var q).Should().BeTrue();
        q.Should().Be(2.5m);
        LeituraDeValores.TentarLerQuantidade("-1", out _).Should().BeFalse("quem tira do estoque é o tipo do movimento, não o sinal");
    }
}
