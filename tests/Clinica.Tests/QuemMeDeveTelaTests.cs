using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O que as telas Blazor "Quem me deve" e "Plano de contas" AFIRMAM (out/2026). Estas
/// decisões moravam no ViewModel WPF, onde o `dotnet test` não alcança; subiram para a
/// Application com a migração, e é aqui que elas ficam presas.
/// </summary>
public class QuemMeDeveTelaTests
{
    private static PacienteInadimplente Devedor(string nome, int diasMaiorAtraso, decimal total,
        int contas = 1, string? telefone = "(21) 99876-5432")
    {
        var hoje = new DateOnly(2026, 10, 15);
        var detalhe = Enumerable.Range(0, contas)
            .Select(i => new ContaEmAtraso(i + 1, $"Conta {i + 1}", hoje.AddDays(-(diasMaiorAtraso - i)), diasMaiorAtraso - i, total / contas))
            .ToList();
        return new PacienteInadimplente(nome.GetHashCode(), nome, telefone, total, contas, diasMaiorAtraso,
            detalhe[0].Vencimento, detalhe);
    }

    [Theory]
    [InlineData(90, false)]
    [InlineData(91, true)]
    [InlineData(200, true)]
    public void Critico_e_mais_de_90_dias_o_mesmo_corte_da_faixa(int dias, bool critico)
    {
        var d = Devedor("Ana", dias, 100m);
        QuemMeDeveTela.EhCritico(d).Should().Be(critico);
        (d.Faixa == FaixasInadimplencia.Acima90).Should().Be(critico);
    }

    [Fact]
    public void A_frase_do_devedor_distingue_uma_conta_de_varias()
    {
        QuemMeDeveTela.ResumoDoDevedor(Devedor("Ana", 20, 100m)).Should().Be("1 conta · vencida faz 20 dia(s)");
        QuemMeDeveTela.ResumoDoDevedor(Devedor("Ana", 80, 300m, contas: 3)).Should().Be("3 contas · a mais antiga faz 80 dia(s)");
    }

    [Fact]
    public void Filtro_casa_sem_acento_e_so_criticos_corta_ate_90()
    {
        var lista = new[] { Devedor("João Pedro", 20, 100m), Devedor("Mariana", 200, 900m), Devedor("Joana", 120, 50m) };

        QuemMeDeveTela.Filtrar(lista, "joao", soCriticos: false).Select(d => d.Nome).Should().Equal("João Pedro");
        QuemMeDeveTela.Filtrar(lista, null, soCriticos: true).Select(d => d.Nome).Should().Equal("Mariana", "Joana");
        QuemMeDeveTela.Filtrar(lista, "jo", soCriticos: true).Select(d => d.Nome).Should().Equal("Joana");
        QuemMeDeveTela.Filtrar(lista, "  ", soCriticos: false).Should().HaveCount(3);
    }

    [Fact]
    public void O_resumo_diz_que_esta_filtrado_e_o_vazio_muda_de_frase()
    {
        var resumo = InadimplenciaService.Resumir([Devedor("Ana", 20, 100m), Devedor("Bia", 40, 200m, contas: 2)]);

        QuemMeDeveTela.ResumoDaLista(resumo, 2, filtroAtivo: false).Should().Be("2 paciente(s) com 3 conta(s) vencida(s).");
        QuemMeDeveTela.ResumoDaLista(resumo, 1, filtroAtivo: true).Should().Be("1 de 2 devedor(es) no filtro.");
        QuemMeDeveTela.ResumoDaLista(InadimplenciaService.Resumir([]), 0, false)
            .Should().Be("Nenhuma conta de paciente vencida. Nada a cobrar hoje.");

        QuemMeDeveTela.TituloDoVazio(true).Should().NotBe(QuemMeDeveTela.TituloDoVazio(false));
        QuemMeDeveTela.DescricaoDoVazio(true).Should().Contain("filtro");
        QuemMeDeveTela.DescricaoDoVazio(false).Should().Contain("PACIENTE");
    }

    [Fact]
    public void As_quatro_faixas_aparecem_sempre_inclusive_sem_ninguem_devendo()
    {
        var vazio = QuemMeDeveTela.FaixasDoEnvelhecimento(InadimplenciaService.Resumir([]));
        vazio.Select(f => f.Faixa).Should().Equal(FaixasInadimplencia.Todas);
        vazio.Should().OnlyContain(f => f.Valor == 0m && f.Pacientes == 0);

        var cheio = QuemMeDeveTela.FaixasDoEnvelhecimento(InadimplenciaService.Resumir([Devedor("Ana", 200, 300m)]));
        cheio.Select(f => f.Faixa).Should().Equal(FaixasInadimplencia.Todas);
        cheio.Single(f => f.Faixa == FaixasInadimplencia.Acima90).Valor.Should().Be(300m);
        cheio.Single(f => f.Faixa == FaixasInadimplencia.Ate30).Valor.Should().Be(0m);
    }

    [Fact]
    public void O_valor_da_faixa_sai_em_pt_BR_fixo_com_a_contagem_so_quando_ha_paciente()
    {
        QuemMeDeveTela.ValorDaFaixa(new FaixaInadimplencia(FaixasInadimplencia.Ate30, 3, 1200m))
            .Should().Be("R$ 1.200,00 · 3");
        QuemMeDeveTela.ValorDaFaixa(new FaixaInadimplencia(FaixasInadimplencia.Ate30, 0, 0m))
            .Should().Be("R$ 0,00");
    }

    [Theory]
    [InlineData("(21) 99876-5432", "5521998765432")]
    [InlineData("2133334444", "552133334444")]
    [InlineData("+55 21 99876-5432", "5521998765432")]
    [InlineData("9876-5432", null)]
    [InlineData(null, null)]
    [InlineData("", null)]
    public void Telefone_do_whatsapp_segue_a_regra_do_shell(string? telefone, string? esperado)
        => QuemMeDeveTela.TelefoneParaWhatsApp(telefone).Should().Be(esperado);

    [Fact]
    public void O_link_do_whatsapp_leva_a_mensagem_codificada_e_some_sem_telefone()
    {
        QuemMeDeveTela.LinkWhatsApp("(21) 99876-5432", "Olá, Ana! R$ 10,00")
            .Should().Be("https://wa.me/5521998765432?text=Ol%C3%A1%2C%20Ana%21%20R%24%2010%2C00");
        QuemMeDeveTela.LinkWhatsApp(null, "x").Should().BeNull();
    }

    [Fact]
    public void Resumo_do_plano_de_contas_conta_entradas_e_saidas()
    {
        var categorias = new List<CategoriaFinanceira>
        {
            new() { Codigo = "E1", Nome = "Convênios", Tipo = TipoLancamento.Entrada },
            new() { Codigo = "S1", Nome = "Aluguel", Tipo = TipoLancamento.Saida },
            new() { Codigo = "S2", Nome = "Folha", Tipo = TipoLancamento.Saida, Ativa = false }
        };
        PlanoDeContasTela.Resumo(categorias).Should().Be("3 categoria(s) · 1 de entrada · 2 de saída");
        PlanoDeContasTela.Resumo([]).Should().Be("0 categoria(s) · 0 de entrada · 0 de saída");
    }
}
