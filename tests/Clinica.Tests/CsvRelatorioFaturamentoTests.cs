using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Regras;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O CSV dos Relatórios de faturamento (subiu do RelatoriosViewModel do WPF quando a tela
/// virou Blazor, out/2026): as mesmas seções, em pt-BR fixo, com o nome do CATÁLOGO.
/// </summary>
public class CsvRelatorioFaturamentoTests
{
    private static RelatorioFaturamento Relatorio(
        IReadOnlyList<ConsultasPorEspecialidade>? consultas = null,
        IReadOnlyList<NaoConformidadeItem>? ncs = null) => new(
        new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31),
        new ResumoFaturamento(30, 20, 10, 66.666, Glosadas: 2, TaxaGlosa: 10, TempoMedioBaixaDias: null, NaoConformidades: 1),
        [new FaturamentoPorConvenio(Convenio.Amil, 3, 2, 1, 66.7, TempoMedioBaixaDias: 2.25) { ConvenioCodigo = "Amil" }],
        [new FaixaEnvelhecimento("Até 7 dias", 4)],
        consultas ?? [],
        ncs ?? []);

    private static IReadOnlyList<ResumoMensal> Comparativo => [new(2026, 10, "out/2026", 30, 20, 10, 66.7)];

    private static IEnumerable<string> Linhas((IReadOnlyList<string>, IReadOnlyList<IReadOnlyList<string>>) csv)
        => csv.Item2.Select(l => string.Join(";", l));

    [Fact]
    public void Cabecalho_diz_o_periodo_em_pt_BR()
    {
        var (cabecalho, _) = CsvRelatorioFaturamento.Montar(Relatorio(), Comparativo);
        cabecalho.Should().Equal("Relatório de faturamento", "01/10/2026 a 31/10/2026");
        CsvRelatorioFaturamento.NomeDoArquivo(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31))
            .Should().Be("Relatorio-Faturamento-2026-10-01-a-2026-10-31.csv");
    }

    [Fact]
    public void Numeros_saem_em_pt_BR_fixo_e_nulo_fica_em_branco()
    {
        var linhas = Linhas(CsvRelatorioFaturamento.Montar(Relatorio(), Comparativo)).ToList();

        linhas.Should().Contain(";30;20;10;1;66,7;2;10;");                       // resumo: tempo médio nulo em branco
        linhas.Should().Contain($"{CatalogoConvenios.Nome("Amil", Convenio.Amil)};3;2;1;0;66,7;0;0;2,3");
        linhas.Should().Contain("out/2026;30;20;10;66,7");
        linhas.Should().Contain("Até 7 dias;4");
        CsvRelatorioFaturamento.Taxa(null).Should().BeEmpty();
        CsvRelatorioFaturamento.Taxa(50).Should().Be("50");
    }

    [Fact]
    public void Secoes_opcionais_so_entram_quando_tem_linha()
    {
        var sem = Linhas(CsvRelatorioFaturamento.Montar(Relatorio(), Comparativo)).ToList();
        sem.Should().NotContain("Consultas por especialidade").And.NotContain("Não conformidades (guias justificadas na rodada)");
        sem.Should().Contain("Faturamento por convênio").And.Contain("Evolução mensal (últimos 6 meses)")
            .And.Contain("Pendências em aberto (envelhecimento)");

        var com = Linhas(CsvRelatorioFaturamento.Montar(Relatorio(consultas: [new("Psiquiatria", 3, 1)]), Comparativo)).ToList();
        com.Should().Contain("Consultas por especialidade").And.Contain("Psiquiatria;3;1");
    }

    [Fact]
    public void Nao_conformidade_sai_com_o_nome_do_catalogo_e_o_rotulo_do_tipo_nunca_o_enum()
    {
        var nc = new NaoConformidadeItem(1, "Maria", Convenio.Amil, TipoCodigo.ConsultaEspecialidade, OrdemCodigo.Primeiro,
            new DateOnly(2026, 10, 5), "Portal fora do ar; paciente não voltou", new DateTime(2026, 10, 9, 14, 30, 0))
        { ConvenioCodigo = "Amil" };

        var linha = CsvRelatorioFaturamento.Montar(Relatorio(ncs: [nc]), Comparativo).Linhas.Last();

        linha.Should().Equal("Maria", CatalogoConvenios.Nome("Amil", Convenio.Amil), RotulosEnum.De(TipoCodigo.ConsultaEspecialidade),
            "05/10/2026", "Portal fora do ar; paciente não voltou", "09/10/2026 14:30");
        linha[2].Should().NotBe("ConsultaEspecialidade");
    }
}
