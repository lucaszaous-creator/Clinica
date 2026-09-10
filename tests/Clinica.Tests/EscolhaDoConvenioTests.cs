using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// A escolha "convênio ou particular?" — a ORDEM, a FRASE e o que nunca é padrão de ninguém
/// (set/2026).
///
/// Ela era feita em DOIS lugares com DUAS regras, e a do lugar mais importante era a errada:
/// a janela de vínculo ordenava por "gera guia", explicava cada opção e não pré-selecionava
/// nada; o CADASTRO do paciente — por onde todo paciente novo recebe um convênio — listava em
/// ordem alfabética e pré-selecionava o primeiro. Numa base que importou a carteira do
/// sistema anterior, o primeiro alfabético é "A definir (importado sem convênio)".
///
/// Estes testes existem porque a regra mudou de casa: enquanto ela vivia dentro de uma
/// ViewModel do WPF, nenhum teste a alcançava.
/// </summary>
public class EscolhaDoConvenioTests
{
    private static ConvenioCadastro Cadastro(
        string codigo, string nome, bool geraGuia = true, bool ativo = true)
        => new()
        {
            Codigo = codigo,
            Nome = nome,
            Familia = Convenio.Personalizado,
            Ativo = ativo,
            GeraGuia = geraGuia
        };

    /// <summary>O catálogo como a clínica o tem hoje: quatro operadoras, o particular e o "a definir".</summary>
    private static IReadOnlyList<ConvenioCadastro> Catalogo() =>
    [
        Cadastro("Amil", "Amil"),
        Cadastro("UnimedPadrao", "Unimed Padrão"),
        Cadastro("Petrobras", "Petrobras"),
        ConvenioCadastro.Particular(),
        ConvenioCadastro.ADefinir()
    ];

    /// <summary>
    /// A ORDEM: operadoras (que geram guia) → Particular → "a definir".
    ///
    /// Alfabética, "A definir" encabeça a lista e "Particular" cai entre a Petrobras e a
    /// Unimed com o mesmo peso — e esta lista é justamente onde a recepcionista descobre que o
    /// particular existe.
    /// </summary>
    [Fact]
    public void As_operadoras_vem_primeiro_o_particular_depois_e_o_a_definir_por_ultimo()
    {
        var opcoes = OpcoesDeConvenio.Montar(Catalogo(), incluirADefinir: true);

        opcoes.Select(o => o.Codigo).Should().Equal(
            "Amil", "Petrobras", "UnimedPadrao",
            ConvenioCadastro.CodigoParticular,
            ConvenioCadastro.CodigoADefinir);
    }

    /// <summary>
    /// A janela de VÍNCULO não oferece o "a definir": ele é a PERGUNTA. Oferecê-lo como
    /// resposta devolveria uma tela de sucesso e um lançamento recusado em seguida
    /// (parcela 92).
    /// </summary>
    [Fact]
    public void O_a_definir_nao_e_oferecido_quando_a_tela_pede_uma_RESPOSTA()
    {
        var opcoes = OpcoesDeConvenio.Montar(Catalogo(), incluirADefinir: false);

        opcoes.Should().NotContain(o => o.EhADefinir);
        opcoes.Should().Contain(o => o.Codigo == ConvenioCadastro.CodigoParticular);
    }

    /// <summary>
    /// Inativo sai das escolhas NOVAS, como em todo cadastro — o histórico de quem já está
    /// nele é preservado pelo código gravado na ficha.
    /// </summary>
    [Fact]
    public void Convenio_inativo_sai_da_lista()
    {
        var opcoes = OpcoesDeConvenio.Montar(
            [Cadastro("Amil", "Amil"), Cadastro("Velho", "Operadora que saiu", ativo: false)],
            incluirADefinir: true);

        opcoes.Select(o => o.Codigo).Should().Equal("Amil");
    }

    /// <summary>
    /// A FRASE diz o que a escolha significa — e, no particular, ONDE o dinheiro entra. Sem a
    /// segunda metade a tela informa "não gera guia" e cala sobre o que fazer em seguida, que
    /// é exatamente a dúvida de quem nunca lançou um particular.
    /// </summary>
    [Fact]
    public void Cada_opcao_diz_o_que_a_escolha_significa()
    {
        var opcoes = OpcoesDeConvenio.Montar(Catalogo(), incluirADefinir: true);

        opcoes.Single(o => o.Codigo == "Amil").Explicacao
            .Should().Contain("guia");

        var particular = opcoes.Single(o => o.Codigo == ConvenioCadastro.CodigoParticular);
        particular.Explicacao.Should().Contain("paga a sessão");
        particular.Explicacao.Should().Contain("Finalizar");

        // O "a definir" AVISA o que acontece com ele: alerta vermelho no balcão e lançamento
        // recusado. É o que separa "ainda não sei" de "escolhi isto".
        var aDefinir = opcoes.Single(o => o.EhADefinir);
        aDefinir.Explicacao.Should().Contain("NÃO pode ser lançado");
    }

    /// <summary>
    /// O PARTICULAR não gera guia e o "a definir" também não — e eles não são a mesma coisa.
    /// "A definir" é a pergunta (acende alerta vermelho até alguém responder); "Particular" é
    /// a resposta, e não alerta nada. Tratá-los igual faria a clínica inteira de particulares
    /// aparecer como pendência para sempre.
    /// </summary>
    [Fact]
    public void Particular_e_a_definir_nao_geram_guia_e_nao_sao_a_mesma_coisa()
    {
        var opcoes = OpcoesDeConvenio.Montar(Catalogo(), incluirADefinir: true);

        var particular = opcoes.Single(o => o.Codigo == ConvenioCadastro.CodigoParticular);
        var aDefinir = opcoes.Single(o => o.Codigo == ConvenioCadastro.CodigoADefinir);

        particular.GeraGuia.Should().BeFalse();
        aDefinir.GeraGuia.Should().BeFalse();

        particular.EhADefinir.Should().BeFalse();
        aDefinir.EhADefinir.Should().BeTrue();
        particular.Explicacao.Should().NotBe(aDefinir.Explicacao);
    }

    /// <summary>
    /// A FAMÍLIA viaja na opção porque é o outro campo que a ficha grava
    /// (<c>Paciente.Convenio</c>): sem ela a tela teria de voltar ao catálogo para escrever, e
    /// uma segunda resolução é uma segunda chance de gravar a família errada.
    /// </summary>
    [Fact]
    public void A_opcao_carrega_a_familia_que_a_ficha_grava()
    {
        var amil = new ConvenioCadastro
        {
            Codigo = "Amil", Nome = "Amil", Familia = Convenio.Amil, Ativo = true, GeraGuia = true
        };

        OpcoesDeConvenio.Montar([amil], incluirADefinir: false)
            .Single().Familia.Should().Be(Convenio.Amil);
    }

    /// <summary>
    /// A MESMA lista a partir do cache em memória (<c>CatalogoConvenios.Ativos</c>), que é o
    /// que as telas de cadastro têm à mão. Duas fontes, uma ordem: se elas divergissem, o
    /// cadastro e a janela de vínculo voltariam a oferecer o particular em posições
    /// diferentes.
    /// </summary>
    [Fact]
    public void A_ordem_e_a_mesma_lendo_do_cache_em_memoria()
    {
        var doBanco = OpcoesDeConvenio.Montar(Catalogo(), incluirADefinir: true);

        var doCache = OpcoesDeConvenio.Montar(
            Catalogo().Select(c => new EntradaConvenio(
                c.Codigo, c.Nome, c.Familia, c.Ativo, GeraGuia: c.GeraGuia)).ToList(),
            incluirADefinir: true);

        doCache.Select(o => o.Codigo).Should().Equal(doBanco.Select(o => o.Codigo));
        doCache.Select(o => o.Explicacao).Should().Equal(doBanco.Select(o => o.Explicacao));
    }
}
