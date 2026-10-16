using System.Globalization;
using System.Text;
using Clinica.Application.Formatacao;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O que as telas Blazor "Taxas e impostos" e "Extrato do banco" AFIRMAM (out/2026).
///
/// A prova que carrega o arquivo é a da ALÍQUOTA ÚNICA: a faixa dela aparece exatamente
/// quando o serviço cai nela — e o serviço cai nela quando não há tributo GERAL vigente,
/// retenção de convênio NÃO conta. O teste compara a tela com o serviço, não a tela com ela
/// mesma (a lição da parcela 64: o mesmo ato em dois lugares se testa comparando os dois).
/// </summary>
public class TaxasEExtratoNaTelaTests : IDisposable
{
    private static readonly DateOnly Hoje = new(2026, 9, 15);

    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly ParametrosService _parametros;
    private readonly TributoService _tributos;

    public TaxasEExtratoNaTelaTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _parametros = new ParametrosService(_repo);
        _tributos = new TributoService(_repo, _parametros);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }

    private static Tributo T(string sigla, decimal p, string? convenio = null, DateOnly? de = null,
        DateOnly? ate = null, bool ativo = true, decimal b = 100m)
        => new() { Sigla = sigla, Nome = sigla, Percentual = p, BasePercentual = b, ConvenioCodigo = convenio,
                   VigenteDe = de, VigenteAte = ate, Ativo = ativo };

    // ==================== Alíquota única ====================

    public static TheoryData<string, Tributo[], bool> Cenarios => new()
    {
        { "sem tributo", [], true },
        { "ISS vigente", [T("ISS", 3m)], false },
        { "só retenção da Unimed", [T("IRRF", 1.5m, "UnimedPadrao")], true },
        { "geral com vigência futura", [T("ISS", 3m, de: Hoje.AddDays(1))], true },
        { "geral expirado", [T("ISS", 3m, ate: Hoje.AddDays(-1))], true },
        { "geral inativo", [T("ISS", 3m, ativo: false)], true },
        { "geral + retenção", [T("ISS", 3m), T("IRRF", 1.5m, "UnimedPadrao")], false }
    };

    [Theory]
    [MemberData(nameof(Cenarios))]
    public async Task A_faixa_da_aliquota_unica_segue_a_MESMA_condicao_do_servico(
        string cenario, Tributo[] tributos, bool esperado)
    {
        await _parametros.SalvarAliquotaImpostoAsync(6m);
        foreach (var t in tributos) await _tributos.SalvarAsync(t);

        var catalogo = await _tributos.CatalogoAsync();
        var tela = TaxasEImpostosNaTela.AliquotaUnicaValendo(catalogo, Hoje);

        // O serviço: num recebimento SEM convênio, o detalhe diz "Alíquota única" exatamente
        // quando o fallback armou.
        var apurado = await _tributos.ApurarAsync(100m, Hoje);
        var servicoUsaAliquotaUnica = apurado.Detalhe?.StartsWith("Alíquota única") == true;

        tela.Should().Be(esperado, cenario);
        tela.Should().Be(servicoUsaAliquotaUnica, $"a tela tem de concordar com o serviço ({cenario})");
    }

    [Fact]
    public void So_retencao_vigente_diz_que_a_aliquota_unica_CONTINUA_valendo_e_por_que()
    {
        var frase = TaxasEImpostosNaTela.OrigemDaCarga([T("IRRF", 1.5m, "UnimedPadrao")], 6m, Hoje);

        frase.Should().Contain("vale a alíquota única");
        frase.Should().Contain("retenção(ões) de convênio");
        frase.Should().NotContain("desligada");
    }

    [Fact]
    public void A_frase_do_mecanismo_cobre_os_quatro_casos_do_WPF()
    {
        TaxasEImpostosNaTela.OrigemDaCarga([T("ISS", 3m)], 6m, Hoje).Should().Contain("A alíquota única está desligada");
        TaxasEImpostosNaTela.OrigemDaCarga([T("ISS", 3m, ativo: false)], 0m, Hoje).Should().Contain("não se retém imposto");
        TaxasEImpostosNaTela.OrigemDaCarga([], 6m, Hoje).Should().Be("Nenhum tributo cadastrado — vale a alíquota única abaixo.");
        TaxasEImpostosNaTela.OrigemDaCarga([], 0m, Hoje).Should().Be("Nenhum tributo e nenhuma alíquota: não se retém imposto.");
    }

    // ==================== Leitura de percentual ====================

    [Theory]
    [InlineData("0", 0)]
    [InlineData("3,2", 3.2)]
    [InlineData("3.2", 3.2)]
    [InlineData(" 15% ", 15)]
    [InlineData("100", 100)]
    public void Percentual_aceita_o_que_a_clinica_digita_INCLUSIVE_zero(string texto, double esperado)
    {
        TaxasEImpostosNaTela.TentarLerPercentual(texto, out var p).Should().BeTrue();
        p.Should().Be((decimal)esperado);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("-1")]
    [InlineData("100,5")]
    public void Percentual_recusa_vazio_texto_e_fora_de_zero_a_cem(string? texto)
        => TaxasEImpostosNaTela.TentarLerPercentual(texto, out _).Should().BeFalse();

    // ==================== Taxas ====================

    [Fact]
    public void A_descricao_da_taxa_sai_em_pt_BR_mesmo_com_a_cultura_invariante()
    {
        var anterior = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var t = new TaxaCartao { Adquirente = "Stone", Modalidade = ModalidadeCartao.CreditoParcelado,
                                     ParcelasDe = 2, ParcelasAte = 6, Percentual = 4.2m };
            TaxasEImpostosNaTela.DescreverTaxa(t).Should().Be("Stone · todas as bandeiras · crédito parcelado 2–6x — 4,2%");
        }
        finally { CultureInfo.CurrentCulture = anterior; }
    }

    [Fact]
    public void A_situacao_da_taxa_distingue_valendo_encerrada_futura_e_inativa()
    {
        TaxasEImpostosNaTela.SituacaoTaxa(new TaxaCartao(), Hoje).Rotulo.Should().Be("Valendo agora");
        TaxasEImpostosNaTela.SituacaoTaxa(new TaxaCartao { VigenteAte = Hoje.AddDays(-1) }, Hoje).Rotulo.Should().Be("Vigência encerrada");
        TaxasEImpostosNaTela.SituacaoTaxa(new TaxaCartao { VigenteDe = Hoje.AddDays(1) }, Hoje).Rotulo.Should().Be("Começa depois");
        TaxasEImpostosNaTela.SituacaoTaxa(new TaxaCartao { Ativa = false }, Hoje).Rotulo.Should().Be("Inativa");
    }

    [Fact]
    public void Quem_retem_aparece_pelo_NOME_da_operadora_e_codigo_desconhecido_fica_como_esta()
    {
        var nomes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["UnimedPadrao"] = "Unimed Costa do Sol" };
        TaxasEImpostosNaTela.Origem(T("IRRF", 1.5m, "unimedpadrao"), nomes).Should().Be("retido na fonte por Unimed Costa do Sol");
        TaxasEImpostosNaTela.Origem(T("IRRF", 1.5m, "Bradesco"), nomes).Should().Be("retido na fonte por Bradesco");
        TaxasEImpostosNaTela.Origem(T("ISS", 3m), nomes).Should().Be("recolhido pela clínica");
    }

    [Fact]
    public void Simulador_sem_taxa_no_cartao_DIZ_que_nao_achou_e_o_liquido_e_o_bruto()
    {
        var frase = TaxasEImpostosNaTela.Simulacao(200m, DeducoesRecebimento.Nenhuma, ehCartao: true, out var semTaxa);

        semTaxa.Should().BeTrue();
        frase.Should().Contain("sem taxa cadastrada para esta combinação");
        frase.Should().Contain("líquido R$ 200,00");
    }

    [Fact]
    public void Simulador_com_taxa_e_imposto_escreve_cada_parte_e_quando_cai()
    {
        var d = new DeducoesRecebimento(3.15m, 6.30m, 3m, 6m, new DateOnly(2026, 10, 15), "Stone")
            { DetalheImposto = "ISS 3%" };
        var frase = TaxasEImpostosNaTela.Simulacao(200m, d, ehCartao: true, out var semTaxa);

        semTaxa.Should().BeFalse();
        frase.Should().Be("Bruto R$ 200,00 · taxa R$ 6,30 (3,15%) · imposto R$ 6,00 (ISS 3%) · → líquido R$ 187,70 · cai em 15/10/2026");
    }

    [Fact]
    public void Pix_sem_taxa_nao_e_aviso_de_taxa_faltando()
    {
        TaxasEImpostosNaTela.Simulacao(200m, DeducoesRecebimento.Nenhuma, ehCartao: false, out var semTaxa);
        semTaxa.Should().BeFalse();
    }

    // ==================== Extrato do banco ====================

    private static ItemConciliacao Item(int id, decimal valor, string descricao = "PIX")
        => new(new LancamentoFinanceiro { Id = id, Valor = valor, Descricao = descricao, Tipo = TipoLancamento.Entrada,
                                          Status = StatusLancamento.Realizado, Data = Hoje, DataPagamento = Hoje });

    private static LinhaExtrato Extrato(string id = "FIT1", decimal valor = 150m, bool comFitid = true)
        => new(id, Hoje, valor, "PIX RECEBIDO") { IdentidadeBancariaInformada = comFitid };

    [Fact]
    public void Casada_vem_pre_escolhida_e_ambigua_vem_em_branco()
    {
        var casada = new LinhaConciliada(Extrato(), SituacaoConciliacao.Casada, [Item(1, 150m)]);
        var ambigua = new LinhaConciliada(Extrato(), SituacaoConciliacao.Ambigua, [Item(1, 150m), Item(2, 150m)]);

        ExtratoDoBancoNaTela.Proposta(casada).Should().NotBeNull();
        ExtratoDoBancoNaTela.PedeEscolha(casada).Should().BeFalse();
        ExtratoDoBancoNaTela.Proposta(ambigua).Should().BeNull("escolher o primeiro seria decidir por quem não sabe");
        ExtratoDoBancoNaTela.PedeEscolha(ambigua).Should().BeTrue();
        ExtratoDoBancoNaTela.ItensAConferir(ambigua, null).Should().BeNull("a tela diz que falta escolher");
        ExtratoDoBancoNaTela.Estado(ambigua).Should().Be(EstadoLinhaExtrato.EscolhaQual);
    }

    [Fact]
    public void A_frase_ao_lado_do_selo_nao_repete_o_selo()
    {
        var casada = new LinhaConciliada(Extrato(), SituacaoConciliacao.Casada, [Item(1, 150m)]);
        var lote = new LinhaConciliada(Extrato(valor: 400m), SituacaoConciliacao.DepositoCartao, [Item(1, 200m), Item(2, 200m)]);

        ExtratoDoBancoNaTela.Detalhe(casada).Should().NotContain("Casada");
        ExtratoDoBancoNaTela.Situacao(casada).Should().Be("Casada — confirme", "a frase do WPF continua disponível");
        ExtratoDoBancoNaTela.Detalhe(lote).Should().Be(ExtratoDoBancoNaTela.Situacao(lote));
    }

    [Fact]
    public void O_lote_da_maquininha_vai_INTEIRO_no_clique_e_nao_pede_escolha()
    {
        var lote = new LinhaConciliada(Extrato(valor: 700m), SituacaoConciliacao.DepositoCartao,
            [Item(1, 200m), Item(2, 250m), Item(3, 250m)]);

        ExtratoDoBancoNaTela.PedeEscolha(lote).Should().BeFalse();
        ExtratoDoBancoNaTela.ItensAConferir(lote, null).Should().HaveCount(3);
        ExtratoDoBancoNaTela.PodeConferir(lote).Should().BeTrue();
    }

    [Fact]
    public void Ja_conferida_so_desfaz_e_sem_FITID_nao_concilia_nem_desfaz()
    {
        var ja = new LinhaConciliada(Extrato(), SituacaoConciliacao.JaConciliada, [Item(1, 150m)]);
        var semId = new LinhaConciliada(Extrato(comFitid: false), SituacaoConciliacao.SoNoBanco, []);

        ExtratoDoBancoNaTela.PodeConferir(ja).Should().BeFalse();
        ExtratoDoBancoNaTela.PodeDesfazer(ja).Should().BeTrue();
        ExtratoDoBancoNaTela.Estado(semId).Should().Be(EstadoLinhaExtrato.SemIdentificador);
        ExtratoDoBancoNaTela.Situacao(semId).Should().Contain("FITID");
        ExtratoDoBancoNaTela.Tom(EstadoLinhaExtrato.SemIdentificador).Should().Be("perigo");
        ExtratoDoBancoNaTela.PodeConferir(semId).Should().BeFalse();
        ExtratoDoBancoNaTela.PodeDesfazer(semId).Should().BeFalse();
    }

    [Fact]
    public void O_resumo_conta_e_diz_o_periodo_em_pt_BR()
    {
        var r = new ResultadoConciliacao(
            [new LinhaConciliada(Extrato(), SituacaoConciliacao.Casada, [Item(1, 150m)]),
             new LinhaConciliada(Extrato("FIT2"), SituacaoConciliacao.SoNoBanco, [])],
            [Item(9, 80m)], new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 14), "123");

        ExtratoDoBancoNaTela.Resumo(r).Should().Be(
            "2 linha(s) no extrato · 1 já casam · 1 pedem atenção · 1 no sistema sem correspondência no banco · período de 01/09/2026 a 14/09/2026");
        ExtratoDoBancoNaTela.Resumo(new ResultadoConciliacao([], [], null, null, null))
            .Should().Contain("não tem nenhuma transação");
    }

    [Fact]
    public void O_arquivo_e_Latin1_por_padrao_e_UTF8_so_quando_DIZ_que_e()
    {
        const string memo = "<MEMO>PAGTO SERVIÇOS";
        ExtratoDoBancoNaTela.Decodificar(Encoding.Latin1.GetBytes("CHARSET:1252\n" + memo)).Should().EndWith("SERVIÇOS");
        ExtratoDoBancoNaTela.Decodificar(Encoding.UTF8.GetBytes("CHARSET:UTF-8\n" + memo)).Should().EndWith("SERVIÇOS");
        ExtratoDoBancoNaTela.Decodificar([0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(memo)]).Should().Be(memo);
    }
}
