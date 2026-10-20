using Clinica.Application.Modelos;
using Clinica.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// A seção Histórico da tela do paciente e o Modal de correções (out/2026, migração para Blazor).
/// </summary>
public class HistoricoDoPacienteNaTelaTests
{
    private static Evolucao E(int id, string queixa, string? texto = null) => new()
    {
        Id = id, PacienteId = 1, Data = new DateOnly(2026, 10, id), QueixaPrincipal = queixa, TextoEvolucao = texto
    };

    [Fact]
    public void A_busca_filtra_e_o_resumo_diz_que_esta_filtrado()
    {
        var sessoes = new[] { E(1, "lombalgia"), E(2, "cefaleia"), E(3, "lombalgia crônica") };
        var vazio = new Dictionary<int, int>();

        var linhas = HistoricoClinicoNaTela.Filtrar(sessoes, "lombalgia", vazio, vazio, "Maria");

        linhas.Select(l => l.EvolucaoId).Should().Equal(1, 3);
        HistoricoClinicoNaTela.Resumo(3, linhas.Count, "lombalgia").Should().Be("2 de 3 sessão(ões) contêm “lombalgia”.");
        HistoricoClinicoNaTela.Resumo(3, 3, "  ").Should().Be("3 sessão(ões) no prontuário.");
    }

    [Fact]
    public void Campo_vazio_vira_travessao_e_EVA_nao_medida_e_dito()
    {
        var l = HistoricoClinicoNaTela.LinhaDe(E(4, "dor"), anexos: 2, correcoes: 1, paciente: "Maria");

        l.Conduta.Should().Be("—");
        l.Eva.Should().Be("EVA não medida");
        l.AnexosTexto.Should().Be("2 anexos");
        l.CorrecoesTexto.Should().Be("1 correção");
        l.Retificada.Should().BeTrue();
    }

    [Fact]
    public void A_versao_atual_vem_primeiro_e_as_anteriores_da_mais_nova_para_a_mais_antiga()
    {
        var atual = E(5, "atual");
        var v1 = new VersaoEvolucao { Versao = 1, QueixaPrincipal = "primeira", SubstituidaEm = new DateTime(2026, 10, 5, 9, 0, 0) };
        var v2 = new VersaoEvolucao { Versao = 2, QueixaPrincipal = "segunda", Motivo = "erro de digitação", SubstituidaEm = new DateTime(2026, 10, 5, 10, 0, 0), SubstituidaPor = "ana" };

        var lista = VersaoDaEvolucaoNaTela.Lista(atual, [v1, v2]);

        lista.Select(v => v.Titulo).Should().Equal("Versão atual", "Versão 2", "Versão 1");
        lista[0].Vigente.Should().BeTrue();
        lista[1].Quando.Should().Be("substituída em 05/10/2026 10:00 por ana");
        lista[2].Motivo.Should().Be("Correção sem motivo informado.");
        VersaoDaEvolucaoNaTela.Resumo(0).Should().StartWith("Esta sessão nunca foi corrigida");
    }
}
