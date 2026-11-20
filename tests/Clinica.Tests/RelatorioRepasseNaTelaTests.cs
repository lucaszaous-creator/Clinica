using Clinica.Application.Modelos;
using Clinica.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O que a vista "Relatório para os médicos" da tela de Repasses AFIRMA (nov/2026): os atalhos de
/// período, a recusa antes de ir ao banco, os rótulos da situação, o detalhe agrupado e as frases.
/// </summary>
public class RelatorioRepasseNaTelaTests
{
    private static readonly DateOnly Hoje = new(2026, 11, 20);
    private static DateOnly M(int ano, int mes) => new(ano, mes, 1);

    [Fact]
    public void Atalhos_de_periodo_sao_competencias_inteiras()
    {
        RelatorioRepasseNaTela.EsteMes(Hoje).Should().Be((M(2026, 11), M(2026, 11)));
        RelatorioRepasseNaTela.MesPassado(Hoje).Should().Be((M(2026, 10), M(2026, 10)));
        RelatorioRepasseNaTela.UltimosTresMeses(Hoje).Should().Be((M(2026, 9), M(2026, 11)));
        RelatorioRepasseNaTela.EsteAno(Hoje).Should().Be((M(2026, 1), M(2026, 11)));
        RelatorioRepasseNaTela.MesPassado(new DateOnly(2027, 1, 5)).Should().Be((M(2026, 12), M(2026, 12)), "janeiro volta para dezembro do ano anterior");
        RelatorioRepasseNaTela.Inicial(Hoje).Should().Be(new FiltroRelatorioRepasse(M(2026, 10), M(2026, 10)));
    }

    [Fact]
    public void Recusa_periodo_invertido_e_mais_de_24_competencias()
    {
        RelatorioRepasseNaTela.Recusa(new(M(2026, 11), M(2026, 9))).Should().Contain("depois da final");
        RelatorioRepasseNaTela.Recusa(new(M(2024, 12), M(2026, 11))).Should().BeNull("são exatamente 24 competências");
        RelatorioRepasseNaTela.Recusa(new(M(2024, 11), M(2026, 11))).Should().Contain("No máximo 24");
        RelatorioRepasseNaTela.Periodo(new(M(2026, 9), M(2026, 9))).Should().Be("09/2026");
        RelatorioRepasseNaTela.Periodo(new(M(2026, 9), M(2026, 11))).Should().Be("09/2026 a 11/2026");
    }

    [Fact]
    public void Situacoes_tem_rotulos_claros_e_o_padrao_nao_conta_como_filtro()
    {
        RelatorioRepasseNaTela.Situacoes.Select(RelatorioRepasseNaTela.Rotulo).Should()
            .Equal("Conferidos e pagos", "Só pagos", "Só a pagar", "Incluir cancelados");
        var f = new FiltroRelatorioRepasse(M(2026, 10), M(2026, 10));
        RelatorioRepasseNaTela.FiltroAtivo(f).Should().BeFalse();
        RelatorioRepasseNaTela.FiltroAtivo(f with { Situacao = SituacaoNoRelatorioRepasse.SoAPagar }).Should().BeTrue();
        RelatorioRepasseNaTela.FiltroAtivo(f with { Paciente = "ana" }).Should().BeTrue();
        RelatorioRepasseNaTela.SemFiltros(f with { ProfissionalId = 3, ConvenioCodigo = "Amil" }).Should().Be(f);
        RelatorioRepasseNaTela.DoMedico(f with { ConvenioCodigo = "Amil" }, 7).Should().Be(f with { ConvenioCodigo = "Amil", ProfissionalId = 7 });
        RelatorioRepasseNaTela.Paciente("   ").Should().BeNull();
        RelatorioRepasseNaTela.Paciente(" Ana ").Should().Be("Ana");
    }

    private static LinhaRelatorioRepasse Linha(int prof, DateOnly comp, string convenio, string? referencia, decimal valor,
        bool cancelado = false, NaturezaItemRepasse natureza = NaturezaItemRepasse.Credito, DateOnly? pagoEm = null)
        => new(1, prof, "Dra. Helena", null, comp, cancelado ? "Cancelado" : pagoEm is null ? "Conferido" : "Pago", cancelado,
            new DateTime(2026, 10, 2), "Ana", pagoEm, OrigemItemRepasse.Convenio, natureza, "Paciente", comp.AddDays(5),
            convenio, convenio, "Consulta", "1", referencia, comp.AddDays(9), 210m, "regra", valor);

    [Fact]
    public void Detalhe_agrupa_por_competencia_e_pagamento_e_cancelado_nao_soma()
    {
        var detalhe = RelatorioRepasseNaTela.Detalhe([
            Linha(1, M(2026, 10), "Petrobras", "Conta 1", 80m),
            Linha(1, M(2026, 9), "Amil", "GRD 2", 65m),
            Linha(1, M(2026, 9), "Amil", "GRD 2", 65m, cancelado: true),
            Linha(1, M(2026, 9), "Amil", "GRD 2", -65m, natureza: NaturezaItemRepasse.Estorno),
            Linha(1, M(2026, 9), "Amil", null, 65m),
            Linha(2, M(2026, 9), "Amil", "GRD 2", 65m)], profissionalId: 1);

        detalhe.Select(c => c.Competencia).Should().Equal(M(2026, 9), M(2026, 10));
        detalhe[0].Blocos.Select(b => b.ReferenciaPagamento).Should().Equal("GRD 2", null);
        detalhe[0].Blocos[0].Linhas.Should().HaveCount(3, "o cancelado aparece, marcado");
        detalhe[0].Blocos[0].Total.Should().Be(0m, "65 − 65 do estorno; o cancelado não soma");
        detalhe[0].Total.Should().Be(65m);
        detalhe[1].Total.Should().Be(80m);
    }

    [Fact]
    public void Tom_da_linha_e_frases()
    {
        RelatorioRepasseNaTela.TomDaLinha(Linha(1, M(2026, 9), "Amil", null, 65m, cancelado: true)).Should().Be("neutro");
        RelatorioRepasseNaTela.TomDaLinha(Linha(1, M(2026, 9), "Amil", null, 65m, pagoEm: M(2026, 10))).Should().Be("sucesso");
        RelatorioRepasseNaTela.TomDaLinha(Linha(1, M(2026, 9), "Amil", null, 65m)).Should().Be("info");
        RelatorioRepasseNaTela.PdfsGravados(3, "/pasta").Should().Be("3 PDFs gravados em /pasta — um por médico.");
        RelatorioRepasseNaTela.DescricaoDoVazio(new(M(2026, 10), M(2026, 10))).Should()
            .Contain("fechamentos CONFERIDOS").And.Contain("10/2026");

        var relatorio = new RelatorioRepasse(new(M(2026, 9), M(2026, 10)), "Clínica", [],
            [new(1, "Dra. Helena", null, 2, 5, 325m, 195m, 130m), new(2, "Dr. Marcos", null, 1, 2, 160m, 0m, 160m)],
            [], 7, 485m, 195m, 290m);
        RelatorioRepasseNaTela.Resumo(relatorio).Should().Be("2 médicos · 3 fechamentos · 7 guias · competência 09/2026 a 10/2026");
    }
}
