using Clinica.Application.Modelos;
using Clinica.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// A WEB DE LEITURA (set/2026) — o que ela deixa ver.
///
/// O que estes testes prendem é a régua de ACESSO, que é a parte da web que não pode
/// divergir do desktop: uma segunda regra ("na web todo mundo vê o resumo") seria a
/// permissão granular da parcela 49 desfeita por uma porta nova — exatamente o que a
/// parcela 60 achou nas cópias do faturamento.
/// </summary>
public class WebDeLeituraTests
{
    [Fact]
    public void Cada_pagina_exige_o_MESMO_bit_do_desktop()
    {
        AcessoWeb.Paginas.Should().Contain(p => p.Rota == "/dia" && p.Exige == Permissao.VerAgenda);
        AcessoWeb.Paginas.Should().Contain(p => p.Rota == "/painel" && p.Exige == Permissao.VerIndicadores);
        AcessoWeb.Paginas.Should().Contain(p => p.Rota == "/pacientes" && p.Exige == Permissao.VerFichaPaciente);
    }

    [Fact]
    public void O_menu_mostra_SO_o_que_a_pessoa_alcanca()
    {
        // O balcão vê a agenda e as fichas; os números do mês são da direção.
        var recepcao = PerfisAcesso.Padrao(PerfilAcesso.Recepcao);

        var paginas = AcessoWeb.Do(recepcao).Select(p => p.Rota).ToList();

        paginas.Should().Contain("/dia");
        paginas.Should().Contain("/pacientes");
        paginas.Should().NotContain("/painel");
    }

    [Fact]
    public void Quem_nao_alcanca_NENHUMA_pagina_nao_entra()
    {
        // Deixar entrar e mostrar um menu vazio faz a pessoa ligar para o suporte em vez de
        // falar com a direção (a regra da parcela 45).
        AcessoWeb.AberturaDe(Permissao.Nenhuma).Should().BeNull();
    }

    [Fact]
    public void A_abertura_e_a_primeira_pagina_que_a_pessoa_alcanca()
    {
        AcessoWeb.AberturaDe(Permissao.VerIndicadores).Should().Be("/painel");
        AcessoWeb.AberturaDe(Permissao.VerAgenda | Permissao.VerIndicadores).Should().Be("/dia");
    }

    [Fact]
    public void O_PRONTUARIO_exige_o_bit_dele_mesmo_para_quem_abre_a_ficha()
    {
        // O corte da parcela 49 — dado de contato de um lado, dado de saúde do art. 5º, II
        // do outro — não afrouxa por a tela ser web.
        AcessoWeb.MostraProntuario(Permissao.VerFichaPaciente).Should().BeFalse();
        AcessoWeb.MostraProntuario(Permissao.VerFichaPaciente | Permissao.VerProntuario)
            .Should().BeTrue();
    }

    [Fact]
    public void O_faturista_nao_alcanca_o_prontuario_pela_web()
    {
        // É o mesmo perfil que a parcela 49 separou no desktop: ele abre a ficha (contato)
        // e não lê a evolução.
        var faturista = PerfisAcesso.Padrao(PerfilAcesso.Faturista);

        AcessoWeb.MostraProntuario(faturista).Should().BeFalse();
    }

    [Fact]
    public void Toda_pagina_declara_a_pergunta_que_responde()
    {
        // A frase não é enfeite: ela é o que o menu e a documentação usam para dizer o que
        // a web faz — e uma página sem resposta declarada é uma página que ninguém sabe
        // por que existe.
        AcessoWeb.Paginas.Should().OnlyContain(
            p => !string.IsNullOrWhiteSpace(p.Resposta) && !string.IsNullOrWhiteSpace(p.Titulo));
    }
}
