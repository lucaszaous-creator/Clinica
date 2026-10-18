using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// A mesa da direção (out/2026): a régua do mês, a pauta e as listas. O que a tela afirma
/// mora em <see cref="MesaDaDirecaoNaTela"/>, puro — a tela só desenha.
/// </summary>
public sealed class MesaDaDirecaoTests
{
    private static readonly DateOnly Hoje = new(2026, 10, 19);

    private static PainelDirecao Painel(decimal entradas = 84_320m, IReadOnlyList<MetaApurada>? metas = null,
        IReadOnlyList<AlertaDirecao>? alertas = null, decimal? anterior = 75_000m) =>
        new(Hoje, entradas, 62_840m, anterior,
            new ResumoContas(0, 0, 0, 0, 0), new ResumoRecebiveis(0, 0, 0, null),
            0, 0, 0, 0, 0, 0, 0,
            metas ?? [], 0, 0, 0, 0, 0, 0, 0, 0,
            alertas ?? [], []);

    private static MetaApurada MetaFaturamento(decimal alvo, decimal? realizado, decimal? projecao, int? profissional = null, string? segmento = null) =>
        new(IndicadorMeta.Faturamento, 2026, 10, alvo, realizado, projecao, profissional, profissional is null ? null : "Dra. Ana", segmento);

    [Fact]
    public void Sem_meta_a_regua_nao_inventa_alvo_e_mostra_o_recebido()
    {
        var r = MesaDaDirecaoNaTela.Regua(Painel());
        Assert.False(r.TemMeta);
        Assert.Null(r.Selo);
        Assert.Equal(84_320m, r.Recebido);
        Assert.Equal(19, r.Dia);
        Assert.Equal(31, r.DiasNoMes);
    }

    [Fact]
    public void A_regua_usa_a_meta_de_faturamento_da_clinica_inteira_e_nao_a_de_um_profissional()
    {
        var r = MesaDaDirecaoNaTela.Regua(Painel(metas:
        [
            MetaFaturamento(30_000m, 20_000m, 32_000m, profissional: 7),
            MetaFaturamento(50_000m, 30_000m, 45_000m, segmento: "BSV"),
            MetaFaturamento(140_000m, 84_320m, 131_600m)
        ]));
        Assert.Equal(140_000m, r.Meta);
        Assert.Equal(131_600m, r.Projecao);
        Assert.True(r.SeloEmRisco);
        Assert.Equal("Projeção R$ 8.400 abaixo da meta", r.Selo);
    }

    [Fact]
    public void Meta_batida_e_antes_do_quinto_dia_o_selo_diz_so_o_que_sabe()
    {
        var batida = MesaDaDirecaoNaTela.Regua(Painel(metas: [MetaFaturamento(80_000m, 84_320m, 120_000m)]));
        Assert.Equal("Meta do mês batida", batida.Selo);
        Assert.False(batida.SeloEmRisco);

        // Sem projeção (o serviço não projeta antes do dia 5): nada a afirmar sobre o fim do mês.
        var cedo = MesaDaDirecaoNaTela.Regua(Painel(metas: [MetaFaturamento(140_000m, 9_000m, null)]));
        Assert.Null(cedo.Selo);
        Assert.Null(cedo.FracaoProjecao);
    }

    [Fact]
    public void A_escala_da_regua_e_a_meta_ou_a_projecao_quando_ela_passa_da_meta()
    {
        var abaixo = MesaDaDirecaoNaTela.Regua(Painel(metas: [MetaFaturamento(100_000m, 50_000m, 80_000m)]));
        Assert.Equal(0.5, abaixo.FracaoRecebida, 3);
        Assert.Equal(0.8, abaixo.FracaoProjecao!.Value, 3);
        Assert.Equal(1.0, abaixo.FracaoMeta!.Value, 3);

        var acima = MesaDaDirecaoNaTela.Regua(Painel(metas: [MetaFaturamento(100_000m, 50_000m, 125_000m)]));
        Assert.Equal(1.0, acima.FracaoProjecao!.Value, 3);
        Assert.Equal(0.8, acima.FracaoMeta!.Value, 3);
    }

    [Fact]
    public void A_pauta_poe_o_perigo_primeiro_e_cada_item_leva_a_tela_que_resolve()
    {
        var pauta = MesaDaDirecaoNaTela.Pauta(Painel(alertas:
        [
            new AlertaDirecao(AssuntoDirecao.ProntuarioEmAberto, "9 sessões sem evolução", "", GravidadeDirecao.Aviso),
            new AlertaDirecao(AssuntoDirecao.GlosasAVencer, "3 glosas a vencer", "", GravidadeDirecao.Perigo),
            new AlertaDirecao(AssuntoDirecao.MetaEmRisco, "Meta em risco", "", GravidadeDirecao.Info)
        ]));

        Assert.Equal([AssuntoDirecao.GlosasAVencer, AssuntoDirecao.ProntuarioEmAberto, AssuntoDirecao.MetaEmRisco],
            pauta.Select(i => i.Assunto));
        Assert.Equal("faturamento-gerencial", pauta[0].ChaveDestino);
        Assert.Equal("consultorio-meu-dia", pauta[1].ChaveDestino);
        Assert.Equal("metas", pauta[2].ChaveDestino);
    }

    [Fact]
    public void Todo_assunto_tem_destino_e_acao()
    {
        foreach (var assunto in Enum.GetValues<AssuntoDirecao>())
        {
            var (chave, acao, modulo) = MesaDaDirecaoNaTela.Destino(assunto);
            Assert.False(string.IsNullOrWhiteSpace(chave), assunto.ToString());
            Assert.False(string.IsNullOrWhiteSpace(acao), assunto.ToString());
            Assert.False(string.IsNullOrWhiteSpace(modulo), assunto.ToString());
        }
    }

    [Theory]
    [InlineData(0.12, "12% acima do mesmo trecho do mês passado")]
    [InlineData(-0.3, "30% abaixo do mesmo trecho do mês passado")]
    [InlineData(0.001, "igual ao mesmo trecho do mês passado")]
    public void A_variacao_e_dita_em_palavras(double variacao, string frase) =>
        Assert.Equal(frase, MesaDaDirecaoNaTela.FraseVariacao(variacao));

    [Fact]
    public void Sem_base_de_comparacao_nao_ha_frase_de_variacao() =>
        Assert.Null(MesaDaDirecaoNaTela.FraseVariacao(null));

    [Fact]
    public void Agenda_de_hoje_ordena_pela_ocupacao_e_nao_inventa_zero_para_quem_nao_tem_jornada()
    {
        var lista = MesaDaDirecaoNaTela.AgendaDeHoje(
        [
            new ProdutividadeProfissional(1, "Dra. Marta", 3, 0, 0, 0, 0, 3, 120, 1, 480, 0, null),
            new ProdutividadeProfissional(2, "Dr. Paulo", 4, 2, 0, 0, 0, 6, 360, 1, 480, 2, null),
            new ProdutividadeProfissional(3, "Enf. Joana", 2, 1, 0, 0, 0, 3, 90, 1, 0, 0, null),
            new ProdutividadeProfissional(null, "Sem profissional", 1, 0, 0, 0, 0, 1, 30, 1, 0, 0, null),
            new ProdutividadeProfissional(4, "Dra. Ana", 0, 0, 0, 2, 0, 0, 0, 0, 480, 0, null)
        ]);

        Assert.Equal(["Dr. Paulo", "Dra. Marta", "Enf. Joana"], lista.Select(o => o.Profissional));
        Assert.Equal(0.75, lista[0].Ocupacao!.Value, 3);
        Assert.Null(lista[2].Ocupacao);
    }

    [Fact]
    public void Convenios_vem_pelo_bruto_e_a_fracao_liquida_desconta_a_retencao()
    {
        var lista = MesaDaDirecaoNaTela.Convenios(
        [
            new RentabilidadeConvenio("amil", "Amil", 10, 10, 9_540m, 860m, 1, 30),
            new RentabilidadeConvenio("unimed", "Unimed", 30, 28, 24_400m, 1_400m, 2, 40),
            new RentabilidadeConvenio("nada", "Sem movimento", 0, 0, 0m, 0m, 0, null)
        ]);
        Assert.Equal(["Unimed", "Amil"], lista.Select(c => c.Convenio));
        Assert.Equal(23_000m / 24_400m, (decimal)lista[0].FracaoLiquida!.Value, 3);
    }
}
