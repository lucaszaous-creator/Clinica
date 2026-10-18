using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O que a tela TABELA DE PREÇO POR CONVÊNIO afirma (Blazor desde out/2026, etapa 6) — antes no
/// <c>PrecosConvenioViewModel</c> e no <c>PrecoEdicaoViewModel</c> do WPF.
/// </summary>
public class PrecosConvenioNaTelaTests
{
    private static readonly DateOnly Hoje = new(2026, 10, 18);

    private static LinhaDePrecoConvenio Linha(string convenio, bool valendo, int id = 1)
        => new(id, convenio, "Acupuntura", "R$ 145,00", "sem prazo", valendo, valendo ? "valendo agora" : "inativo");

    [Fact]
    public void Linha_escreve_o_rotulo_do_enum_e_o_valor_em_pt_BR()
    {
        var p = new PrecoConvenio
        {
            Id = 7, ConvenioCodigo = "Amil", Tipo = TipoCodigo.ConsultaEspecialidade,
            Especialidade = Especialidade.ClinicaDaDor, Valor = 1250.5m, VigenteDe = new DateOnly(2026, 1, 1)
        };
        var l = PrecosConvenioNaTela.LinhaDe(p, Hoje);
        l.PrecoId.Should().Be(7);
        l.Convenio.Should().Be(CatalogoConvenios.Nome("Amil"));
        l.Descricao.Should().NotContain("ConsultaEspecialidade").And.NotContain("ClinicaDaDor");
        l.Descricao.Should().Contain("(Clínica da dor)");
        l.Valor.Should().Be("R$ 1.250,50");
        l.Vigencia.Should().Be("desde 01/01/2026");
        l.ValendoAgora.Should().BeTrue();
        l.Situacao.Should().Be("valendo agora");
    }

    [Fact]
    public void Situacao_diz_por_que_o_preco_esta_fora_de_vigencia()
    {
        PrecosConvenioNaTela.Situacao(new PrecoConvenio { Ativo = false }, Hoje).Should().Be("inativo");
        PrecosConvenioNaTela.Situacao(new PrecoConvenio { VigenteDe = Hoje.AddDays(1) }, Hoje).Should().Be("ainda não vale");
        PrecosConvenioNaTela.Situacao(new PrecoConvenio { VigenteAte = Hoje.AddDays(-1) }, Hoje).Should().Be("vigência encerrada");
        PrecosConvenioNaTela.Situacao(new PrecoConvenio { VigenteDe = Hoje, VigenteAte = Hoje }, Hoje).Should().Be("valendo agora");
        // Inativo vence a data: o preço desligado não "ainda vai valer".
        PrecosConvenioNaTela.Situacao(new PrecoConvenio { Ativo = false, VigenteDe = Hoje.AddDays(9) }, Hoje).Should().Be("inativo");
    }

    [Fact]
    public void Opcoes_so_trazem_operadoras_com_preco_em_ordem_e_comecam_por_todos()
    {
        var opcoes = PrecosConvenioNaTela.OpcoesDeConvenio([Linha("Unimed", true), Linha("Amil", false), Linha("Unimed", false)]);
        opcoes.Should().Equal(PrecosConvenioNaTela.TodosConvenios, "Amil", "Unimed");
    }

    [Fact]
    public void Atualizar_preserva_a_escolha_que_ainda_existe()
    {
        var opcoes = new[] { PrecosConvenioNaTela.TodosConvenios, "Amil" };
        PrecosConvenioNaTela.ManterEscolha("Amil", opcoes).Should().Be("Amil");
        PrecosConvenioNaTela.ManterEscolha("Unimed", opcoes).Should().Be(PrecosConvenioNaTela.TodosConvenios);
        PrecosConvenioNaTela.ManterEscolha(null, opcoes).Should().Be(PrecosConvenioNaTela.TodosConvenios);
    }

    [Fact]
    public void Filtro_por_convenio_e_por_vigencia()
    {
        var todas = new[] { Linha("Amil", true, 1), Linha("Amil", false, 2), Linha("Unimed", true, 3) };
        PrecosConvenioNaTela.Filtrar(todas, PrecosConvenioNaTela.TodosConvenios, false).Should().HaveCount(3);
        PrecosConvenioNaTela.Filtrar(todas, "Amil", false).Select(l => l.PrecoId).Should().Equal(1, 2);
        PrecosConvenioNaTela.Filtrar(todas, null, true).Select(l => l.PrecoId).Should().Equal(1, 3);
        PrecosConvenioNaTela.Filtrar(todas, "Amil", true).Select(l => l.PrecoId).Should().Equal(1);
        PrecosConvenioNaTela.FiltroAtivo(PrecosConvenioNaTela.TodosConvenios, false).Should().BeFalse();
        PrecosConvenioNaTela.FiltroAtivo("Amil", false).Should().BeTrue();
        PrecosConvenioNaTela.FiltroAtivo(null, true).Should().BeTrue();
    }

    [Fact]
    public void Resumo_diz_que_esta_filtrado_e_conta_o_valendo_hoje_do_total()
    {
        var todas = new[] { Linha("Amil", true, 1), Linha("Amil", false, 2), Linha("Unimed", true, 3) };
        PrecosConvenioNaTela.Resumo(todas, 3, false).Should().Be("3 preço(s) cadastrado(s) · 2 valendo hoje.");
        PrecosConvenioNaTela.Resumo(todas, 1, true).Should().Be("1 de 3 preço(s) no filtro · 2 valendo hoje no total.");
        PrecosConvenioNaTela.Resumo([], 0, false).Should().StartWith("Nenhum preço cadastrado");
    }

    [Fact]
    public void Vazio_separa_sem_tabela_de_nada_bate_com_o_filtro()
    {
        PrecosConvenioNaTela.Vazio(false).Should().Be(PrecosConvenioNaTela.VazioSemTabela);
        PrecosConvenioNaTela.Vazio(true).Should().Be(PrecosConvenioNaTela.VazioComFiltro);
        PrecosConvenioNaTela.Concluido(true).Should().Be(PrecosConvenioNaTela.Cadastrado);
        PrecosConvenioNaTela.Concluido(false).Should().Be(PrecosConvenioNaTela.Atualizado);
    }

    [Fact]
    public void Pergunta_de_excluir_nomeia_a_linha_e_manda_encerrar_pela_vigencia()
    {
        var p = PrecosConvenioNaTela.PerguntaDeExcluir(Linha("Amil", true));
        p.Should().StartWith("Apagar o preço de Amil (Acupuntura)?");
        p.Should().Contain("ENCERRAR pela vigência");
    }

    [Fact]
    public void Formulario_confere_na_ordem_do_WPF_e_le_o_valor_pela_mesma_leitura()
    {
        PrecosConvenioNaTela.Conferir(null, "145", out _, out var e1).Should().BeFalse();
        e1.Should().Be("Escolha o convênio.");
        PrecosConvenioNaTela.Conferir("Amil", "", out _, out var e2).Should().BeFalse();
        e2.Should().Contain("145,00");
        PrecosConvenioNaTela.Conferir("Amil", "0", out _, out _).Should().BeFalse("preço zero é não ter tabela");
        PrecosConvenioNaTela.Conferir("Amil", "abc", out _, out _).Should().BeFalse();

        PrecosConvenioNaTela.Conferir("Amil", "1.250,00", out var v1, out var ok).Should().BeTrue();
        v1.Should().Be(1250m);
        ok.Should().BeNull();
        PrecosConvenioNaTela.Conferir("Amil", "R$ 62,5", out var v2, out _).Should().BeTrue();
        v2.Should().Be(62.5m);
    }

    [Fact]
    public void Valor_volta_ao_campo_em_pt_BR()
    {
        PrecosConvenioNaTela.ParaCampo(145m).Should().Be("145");
        PrecosConvenioNaTela.ParaCampo(62.5m).Should().Be("62,5");
    }

    [Fact]
    public void Formulario_oferece_os_ativos_e_o_convenio_desligado_do_proprio_preco()
    {
        var catalogo = new[]
        {
            new ConvenioCadastro { Codigo = "Amil", Nome = "Amil", Ativo = true },
            new ConvenioCadastro { Codigo = "Velha", Nome = "Velha Saúde", Ativo = false },
        };
        PrecosConvenioNaTela.ConveniosDoFormulario(catalogo, null).Select(c => c.Codigo).Should().Equal("Amil");
        PrecosConvenioNaTela.ConveniosDoFormulario(catalogo, "velha").Select(c => c.Codigo).Should().Equal("Amil", "Velha");
    }
}
