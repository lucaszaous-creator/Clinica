using Clinica.Application.Modelos;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>A seção Exames e anexos da tela do paciente (out/2026, migração para Blazor).</summary>
public class ExamesDoPacienteNaTelaTests
{
    private static PedidoDeExameLinha Pedido(int id, int resultados = 0, bool cancelado = false)
        => new(id, $"2026/{id:0000}", 1, "Maria", new DateOnly(2026, 10, 1), "Hemograma", 1, resultados, cancelado, "Dra. Helena");

    [Fact]
    public void Abre_no_que_pede_acao()
    {
        ExamesDoPacienteNaTela.SecaoInicial(2).Should().Be(ExamesDoPacienteNaTela.Aguardando);
        ExamesDoPacienteNaTela.SecaoInicial(0).Should().Be(ExamesDoPacienteNaTela.Resultados);
    }

    [Fact]
    public void O_resumo_so_fala_do_que_existe()
    {
        ExamesDoPacienteNaTela.Resumo(1, 0, 2, 0).Should().Be("1 pedido(s) aguardando resultado · 2 arquivo(s) da ficha");
        ExamesDoPacienteNaTela.Resumo(0, 0, 0, 0).Should().Be("Nenhum exame nem arquivo deste paciente ainda.");
    }

    [Fact]
    public void O_combo_tem_o_avulso_primeiro_esconde_o_cancelado_e_diz_quem_ja_tem_resultado()
    {
        var opcoes = ExamesDoPacienteNaTela.OpcoesDePedido([Pedido(1), Pedido(2, resultados: 2), Pedido(3, cancelado: true)]);

        opcoes.Select(o => o.DocumentoId).Should().Equal(null, 1, 2);
        opcoes[2].Rotulo.Should().EndWith("(já tem 2)");
    }

    [Theory]
    [InlineData("laudo.PDF", "application/pdf")]
    [InlineData("foto.jpeg", "image/jpeg")]
    [InlineData("x.png", "image/png")]
    [InlineData("x.docx", null)]
    public void O_tipo_do_laudo_sai_da_extensao(string nome, string? tipo)
        => ExamesDoPacienteNaTela.TipoDoLaudo(nome).Should().Be(tipo);

    [Fact]
    public void A_data_do_documento_em_branco_e_hoje_e_formato_errado_e_recusado()
    {
        var hoje = new DateOnly(2026, 10, 19);
        ExamesDoPacienteNaTela.LerData("  ", hoje).Should().Be(hoje);
        ExamesDoPacienteNaTela.LerData("05/03/2025", hoje).Should().Be(new DateOnly(2025, 3, 5));
        ExamesDoPacienteNaTela.LerData("2025-03-05", hoje).Should().BeNull();
    }
}
