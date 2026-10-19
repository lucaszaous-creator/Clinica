using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O que a tela PREÇOS DO PARTICULAR afirma (Blazor desde out/2026) — antes no
/// <c>PrecosParticularViewModel</c> e no <c>PrecoParticularEdicaoViewModel</c> do WPF.
/// </summary>
public class PrecosParticularNaTelaTests
{
    private static readonly DateOnly Hoje = new(2026, 10, 18);

    private static LinhaDePrecoParticular Linha(bool valendo, int id = 1)
        => new(id, "Acupuntura", PrecosParticularNaTela.QualquerEspecialidade, "R$ 180,00", "sem prazo",
            valendo, valendo ? "valendo agora" : "inativo");

    [Fact]
    public void Linha_escreve_qualquer_especialidade_por_extenso_e_o_valor_em_pt_BR()
    {
        var p = new PrecoParticular { Id = 3, ModalidadeCodigo = "Acupuntura", Valor = 1180.5m, VigenteDe = new DateOnly(2026, 1, 1) };
        var l = PrecosParticularNaTela.LinhaDe(p, Hoje);
        l.PrecoId.Should().Be(3);
        l.Especialidade.Should().Be("qualquer especialidade", "vazio se lê como 'não preenchido'");
        l.Valor.Should().Be("R$ 1.180,50");
        l.Vigencia.Should().Be("desde 01/01/2026");
        l.ValendoAgora.Should().BeTrue();
        l.Situacao.Should().Be("valendo agora");
    }

    [Fact]
    public void Situacao_diz_por_que_o_preco_esta_fora_de_vigencia()
    {
        PrecosParticularNaTela.Situacao(new PrecoParticular { Ativo = false }, Hoje).Should().Be("inativo");
        PrecosParticularNaTela.Situacao(new PrecoParticular { VigenteDe = Hoje.AddDays(1) }, Hoje).Should().Be("ainda não vale");
        PrecosParticularNaTela.Situacao(new PrecoParticular { VigenteAte = Hoje.AddDays(-1) }, Hoje).Should().Be("vigência encerrada");
        PrecosParticularNaTela.Situacao(new PrecoParticular { VigenteAte = Hoje }, Hoje).Should().Be("valendo agora");
    }

    [Fact]
    public void Filtro_so_o_que_vale_hoje_e_o_resumo_que_diz_que_esta_filtrado()
    {
        var todas = new[] { Linha(true, 1), Linha(false, 2), Linha(true, 3) };
        PrecosParticularNaTela.Filtrar(todas, true).Select(l => l.PrecoId).Should().Equal(1, 3);
        PrecosParticularNaTela.Filtrar(todas, false).Should().HaveCount(3);
        PrecosParticularNaTela.Resumo(todas, 3, false).Should().Be("3 preço(s) cadastrado(s) · 2 valendo hoje.");
        PrecosParticularNaTela.Resumo(todas, 2, true).Should().Be("2 de 3 preço(s) valendo hoje.");
        PrecosParticularNaTela.Resumo([], 0, false).Should().Contain("Nenhum preço cadastrado");
    }

    [Fact]
    public void Vazio_separa_sem_tabela_de_nada_vale_hoje()
    {
        PrecosParticularNaTela.Vazio(false).Should().Be(PrecosParticularNaTela.VazioSemTabela);
        PrecosParticularNaTela.Vazio(true).Should().Be(PrecosParticularNaTela.VazioComFiltro);
        PrecosParticularNaTela.Concluido(true).Should().Be(PrecosParticularNaTela.Cadastrado);
        PrecosParticularNaTela.Concluido(false).Should().Be(PrecosParticularNaTela.Atualizado);
    }

    [Fact]
    public void Pergunta_de_excluir_nomeia_a_linha_e_manda_encerrar_pela_vigencia()
        => PrecosParticularNaTela.PerguntaDeExcluir(Linha(true))
            .Should().Contain("Acupuntura (qualquer especialidade)").And.Contain("ENCERRAR pela vigência");

    [Fact]
    public void Formulario_oferece_os_ativos_e_a_opcao_desligada_do_proprio_preco()
    {
        var catalogo = new[] { ("Acupuntura", "Acupuntura", true), ("Bsv", "BSV", false), ("Consulta", "Consulta", true) };
        PrecosParticularNaTela.Opcoes(catalogo, null).Select(o => o.Codigo).Should().Equal("Acupuntura", "Consulta");
        var naEdicao = PrecosParticularNaTela.Opcoes(catalogo, "bsv");
        naEdicao.Select(o => o.Codigo).Should().Equal("Acupuntura", "Bsv", "Consulta");
        naEdicao[1].Nome.Should().Be("BSV (desativada)");
        PrecosParticularNaTela.Casar(naEdicao, "BSV").Should().Be("Bsv");
        PrecosParticularNaTela.Casar(naEdicao, null).Should().BeNull();
    }

    [Fact]
    public void Formulario_confere_na_ordem_do_WPF_e_le_o_valor_pela_mesma_leitura()
    {
        PrecosParticularNaTela.Conferir(null, "180", out _, out var e1).Should().BeFalse();
        e1.Should().Contain("modalidade");
        PrecosParticularNaTela.Conferir("Acupuntura", "0", out _, out var e2).Should().BeFalse();
        e2.Should().Contain("Informe quanto");
        PrecosParticularNaTela.Conferir("Acupuntura", "1.250,00", out var v, out _).Should().BeTrue();
        v.Should().Be(1250m);
        PrecosParticularNaTela.ParaCampo(62.5m).Should().Be("62,5");
    }

    [Fact]
    public void Permissao_e_a_mesma_da_venda_de_pacote()
        => PrecosParticularNaTela.QuemEdita.Should().Be(PacotesNaTela.QuemVende);
}
