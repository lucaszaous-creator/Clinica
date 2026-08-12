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
/// CONCILIAÇÃO BANCÁRIA POR OFX (parcela 63).
///
/// O extrato do banco era conferido A OLHO contra a tela de recebíveis — o internet
/// banking de um lado, o sistema do outro. Era o último ponto do financeiro em que fechar
/// o mês dependia de alguém não se distrair.
///
/// O que estes testes protegem não é "ler arquivo": é a REGRA do casamento. Um par errado
/// que "bate" é pior do que par nenhum, porque a clínica dá o mês por conferido.
/// </summary>
public class ConciliacaoBancariaTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly ConciliacaoBancariaService _conciliacao;

    public ConciliacaoBancariaTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _conciliacao = new ConciliacaoBancariaService(_repo);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }

    // ==================== O leitor ====================

    /// <summary>
    /// ⚠️ O OFX 1.x tem tags que <b>não fecham</b> — é por isso que um parser de XML não
    /// serve: o arquivo não é XML bem formado. Este teste usa o formato como os bancos
    /// brasileiros o emitem.
    /// </summary>
    [Fact]
    public void Le_o_OFX_com_tags_sem_fechamento()
    {
        var extrato = LeitorOfx.Ler(Arquivo());

        extrato.Linhas.Should().HaveCount(3);
        extrato.Conta.Should().Be("123456");
        extrato.Inicio.Should().Be(new DateOnly(2026, 8, 1));
        extrato.Fim.Should().Be(new DateOnly(2026, 8, 31));

        var pix = extrato.Linhas.Single(l => l.Id == "PIX001");
        pix.Data.Should().Be(new DateOnly(2026, 8, 5));
        pix.Valor.Should().Be(150.00m);
        pix.Entrada.Should().BeTrue();
        pix.Descricao.Should().Be("PIX RECEBIDO MARIA SILVA");
    }

    /// <summary>
    /// ⚠️ O valor do OFX é SEMPRE com ponto decimal, e é lido na cultura INVARIANTE. Numa
    /// máquina em pt-BR, "-2500.00" lido na cultura local viraria -250000 — a conciliação
    /// apontaria uma diferença de cem vezes e ninguém entenderia por quê. É a mesma
    /// armadilha que o `ParametrosService` já evita.
    /// </summary>
    [Fact]
    public void O_valor_e_lido_com_ponto_decimal_e_nao_na_cultura_da_maquina()
    {
        var extrato = LeitorOfx.Ler(Arquivo());

        extrato.Linhas.Single(l => l.Id == "ALG001").Valor.Should().Be(-2500.00m);
        extrato.Linhas.Single(l => l.Id == "ALG001").ValorAbsoluto.Should().Be(2500.00m);
    }

    /// <summary>
    /// A data do OFX vem com hora e fuso (<c>20260805120000[-3:BRT]</c>). O que a
    /// conciliação casa é o DIA em que o dinheiro se moveu.
    /// </summary>
    [Fact]
    public void A_data_ignora_a_hora_e_o_fuso()
        => LeitorOfx.Ler(Arquivo()).Linhas.Single(l => l.Id == "PIX001")
            .Data.Should().Be(new DateOnly(2026, 8, 5));

    /// <summary>
    /// Arquivo que não é OFX não explode — devolve zero linhas, e é a contagem zero que
    /// permite a tela dizer "escolhi o arquivo errado" em vez de mostrar um erro técnico.
    /// </summary>
    [Fact]
    public void Arquivo_que_nao_e_extrato_devolve_vazio_em_vez_de_explodir()
    {
        LeitorOfx.Ler("isto não é um OFX").Linhas.Should().BeEmpty();
        LeitorOfx.Ler(string.Empty).Linhas.Should().BeEmpty();
    }

    // ==================== O casamento ====================

    /// <summary>
    /// O caso normal: um lançamento, uma linha do extrato, mesmo valor e mesma data.
    /// </summary>
    [Fact]
    public async Task Lancamento_e_extrato_que_batem_ficam_CASADOS()
    {
        await LancarAsync("Sessão da Maria", 150m, new DateOnly(2026, 8, 5), TipoLancamento.Entrada);

        var r = await _conciliacao.CruzarAsync(Arquivo());

        var pix = r.Linhas.Single(l => l.Extrato.Id == "PIX001");
        pix.Situacao.Should().Be(SituacaoConciliacao.Casada);
        pix.Unico!.Descricao.Should().Be("Sessão da Maria");
    }

    /// <summary>
    /// ⚠️ <b>A direção importa tanto quanto o valor.</b> Uma ENTRADA de R$ 150 no sistema
    /// não pode casar com uma SAÍDA de R$ 150 no banco, por mais que os números
    /// coincidam — seriam dois fatos opostos dados por conferidos.
    /// </summary>
    [Fact]
    public async Task Entrada_no_sistema_nao_casa_com_saida_no_banco()
    {
        // Uma SAÍDA de 2500 no banco (o aluguel) e uma ENTRADA de 2500 no sistema.
        await LancarAsync("Receita alta", 2500m, new DateOnly(2026, 8, 10), TipoLancamento.Entrada);

        var r = await _conciliacao.CruzarAsync(Arquivo());

        r.Linhas.Single(l => l.Extrato.Id == "ALG001")
            .Situacao.Should().Be(SituacaoConciliacao.SoNoBanco);
    }

    /// <summary>
    /// A folga de dias existe porque o boleto e o TED caem no dia seguinte ou depois do
    /// fim de semana. Sem ela, a maior parte do extrato de uma clínica não casaria.
    /// </summary>
    [Fact]
    public async Task A_folga_de_dias_casa_o_que_caiu_no_dia_seguinte()
    {
        // Lançado no dia 3, caiu no banco no dia 5 — dois dias, dentro da folga.
        await LancarAsync("Sessão da Maria", 150m, new DateOnly(2026, 8, 3), TipoLancamento.Entrada);

        var r = await _conciliacao.CruzarAsync(Arquivo());

        r.Linhas.Single(l => l.Extrato.Id == "PIX001")
            .Situacao.Should().Be(SituacaoConciliacao.Casada);
    }

    /// <summary>
    /// E a folga é CURTA de propósito. Um lançamento de trinta dias antes, com o mesmo
    /// valor, não é candidato — folga grande casaria o aluguel de julho com o de agosto, e
    /// a tela passaria a pedir desempate em tudo, que é como se ensina alguém a clicar
    /// sem olhar.
    /// </summary>
    [Fact]
    public async Task Folga_curta_nao_casa_o_mesmo_valor_de_um_mes_atras()
    {
        await LancarAsync("Sessão antiga", 150m, new DateOnly(2026, 7, 5), TipoLancamento.Entrada);

        var r = await _conciliacao.CruzarAsync(Arquivo());

        r.Linhas.Single(l => l.Extrato.Id == "PIX001")
            .Situacao.Should().Be(SituacaoConciliacao.SoNoBanco);
    }

    /// <summary>
    /// Dois lançamentos idênticos no mesmo dia deixam a linha AMBÍGUA — e o sistema não
    /// escolhe. Escolher um dos dois "porque bate" produziria conciliação que fecha sobre
    /// um par errado, que é pior do que não conciliar.
    /// </summary>
    [Fact]
    public async Task Dois_candidatos_iguais_deixam_a_linha_ambigua()
    {
        await LancarAsync("Sessão A", 150m, new DateOnly(2026, 8, 5), TipoLancamento.Entrada);
        await LancarAsync("Sessão B", 150m, new DateOnly(2026, 8, 5), TipoLancamento.Entrada);

        var r = await _conciliacao.CruzarAsync(Arquivo());

        var pix = r.Linhas.Single(l => l.Extrato.Id == "PIX001");
        pix.Situacao.Should().Be(SituacaoConciliacao.Ambigua);
        pix.Candidatos.Should().HaveCount(2);
        pix.Unico.Should().BeNull("ninguém escolheu ainda — a tela pergunta");
    }

    /// <summary>
    /// ⚠️ <b>A metade que o extrato não mostra.</b> Lançamento dado como REALIZADO no
    /// sistema e ausente do banco é onde mora o erro caro: a venda marcada como recebida
    /// que nunca caiu. Nenhuma tela do sistema respondia isso.
    /// </summary>
    [Fact]
    public async Task O_que_esta_no_sistema_e_nao_no_banco_aparece_separado()
    {
        await LancarAsync("Sessão da Maria", 150m, new DateOnly(2026, 8, 5), TipoLancamento.Entrada);
        await LancarAsync("Venda que nunca caiu", 999m, new DateOnly(2026, 8, 6), TipoLancamento.Entrada);

        var r = await _conciliacao.CruzarAsync(Arquivo());

        r.SoNoSistema.Should().ContainSingle()
            .Which.Descricao.Should().Be("Venda que nunca caiu");
    }

    /// <summary>
    /// O PREVISTO não entra: casá-lo com o extrato diria que uma conta a vencer já foi
    /// paga — e a conta a pagar do mês que vem apareceria como conferida.
    /// </summary>
    [Fact]
    public async Task O_previsto_nao_entra_na_conciliacao()
    {
        await LancarAsync("Conta a vencer", 150m, new DateOnly(2026, 8, 5),
            TipoLancamento.Entrada, StatusLancamento.Previsto);

        var r = await _conciliacao.CruzarAsync(Arquivo());

        r.Linhas.Single(l => l.Extrato.Id == "PIX001")
            .Situacao.Should().Be(SituacaoConciliacao.SoNoBanco);
        r.SoNoSistema.Should().BeEmpty();
    }

    // ==================== Confirmar e desfazer ====================

    /// <summary>
    /// Conciliar grava o FITID e <b>não mexe no valor nem no status</b>: "foi pago" e
    /// "apareceu no extrato" são duas perguntas, e fundi-las faria a conciliação reescrever
    /// o caixa que ela existe para conferir.
    /// </summary>
    [Fact]
    public async Task Conciliar_marca_a_linha_sem_tocar_no_dinheiro()
    {
        var id = await LancarAsync(
            "Sessão da Maria", 150m, new DateOnly(2026, 8, 5), TipoLancamento.Entrada);

        await _conciliacao.ConciliarAsync(id, "PIX001", "ana");

        var l = _db.Lancamentos.Single(x => x.Id == id);
        l.Conciliado.Should().BeTrue();
        l.IdBancario.Should().Be("PIX001");
        l.Valor.Should().Be(150m);
        l.Status.Should().Be(StatusLancamento.Realizado);

        _db.Auditoria.Should().ContainSingle(e => e.Acao == "LancamentoConciliado"
                                                  && e.Operador == "ana");
    }

    /// <summary>
    /// <b>Reimportar o mesmo arquivo não concilia nada duas vezes.</b> O extrato do mês é
    /// baixado várias vezes, e sem a idempotência pelo FITID a segunda importação
    /// ofereceria de novo o que já foi conferido — e a clínica casaria o mesmo dinheiro
    /// com um segundo lançamento.
    /// </summary>
    [Fact]
    public async Task Reimportar_o_mesmo_extrato_nao_oferece_o_que_ja_foi_conciliado()
    {
        var id = await LancarAsync(
            "Sessão da Maria", 150m, new DateOnly(2026, 8, 5), TipoLancamento.Entrada);

        await _conciliacao.ConciliarAsync(id, "PIX001", "ana");

        var r = await _conciliacao.CruzarAsync(Arquivo());

        r.Linhas.Single(l => l.Extrato.Id == "PIX001")
            .Situacao.Should().Be(SituacaoConciliacao.JaConciliada);
        r.SoNoSistema.Should().NotContain(l => l.Id == id);
    }

    /// <summary>
    /// Desfazer existe porque conciliar é humano e errar também. Não apaga o lançamento
    /// nem mexe no valor — devolve a linha à lista do que falta conferir.
    /// </summary>
    [Fact]
    public async Task Desfazer_devolve_a_linha_para_a_lista()
    {
        var id = await LancarAsync(
            "Sessão da Maria", 150m, new DateOnly(2026, 8, 5), TipoLancamento.Entrada);

        await _conciliacao.ConciliarAsync(id, "PIX001", "ana");
        await _conciliacao.DesfazerAsync(id, "ana");

        var l = _db.Lancamentos.Single(x => x.Id == id);
        l.Conciliado.Should().BeFalse();
        l.IdBancario.Should().BeNull();
        l.Valor.Should().Be(150m);

        (await _conciliacao.CruzarAsync(Arquivo()))
            .Linhas.Single(x => x.Extrato.Id == "PIX001")
            .Situacao.Should().Be(SituacaoConciliacao.Casada);
    }

    [Fact]
    public async Task Conciliar_duas_vezes_o_mesmo_lancamento_e_recusado()
    {
        var id = await LancarAsync(
            "Sessão da Maria", 150m, new DateOnly(2026, 8, 5), TipoLancamento.Entrada);

        await _conciliacao.ConciliarAsync(id, "PIX001", "ana");

        var denovo = () => _conciliacao.ConciliarAsync(id, "OUTRO", "ana");

        await denovo.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*já foi conciliado*");
    }

    // ==================== Cenário ====================

    private async Task<int> LancarAsync(
        string descricao, decimal valor, DateOnly data, TipoLancamento tipo,
        StatusLancamento status = StatusLancamento.Realizado)
    {
        var l = new LancamentoFinanceiro
        {
            Descricao = descricao,
            Valor = valor,
            Data = data,
            DataPagamento = status == StatusLancamento.Realizado ? data : null,
            Tipo = tipo,
            Status = status
        };
        _db.Lancamentos.Add(l);
        await _db.SaveChangesAsync();
        return l.Id;
    }

    /// <summary>
    /// Um OFX como os bancos brasileiros o emitem: SGML, tags sem fechamento, data com
    /// fuso e valor com ponto decimal.
    /// </summary>
    private static string Arquivo() => """
        OFXHEADER:100
        DATA:OFXSGML
        VERSION:102

        <OFX>
        <BANKMSGSRSV1><STMTTRNRS><STMTRS>
        <CURDEF>BRL
        <BANKACCTFROM><BANKID>001<ACCTID>123456<ACCTTYPE>CHECKING</BANKACCTFROM>
        <BANKTRANLIST>
        <DTSTART>20260801000000[-3:BRT]
        <DTEND>20260831235959[-3:BRT]
        <STMTTRN>
        <TRNTYPE>CREDIT
        <DTPOSTED>20260805120000[-3:BRT]
        <TRNAMT>150.00
        <FITID>PIX001
        <MEMO>PIX RECEBIDO MARIA SILVA
        </STMTTRN>
        <STMTTRN>
        <TRNTYPE>DEBIT
        <DTPOSTED>20260810090000[-3:BRT]
        <TRNAMT>-2500.00
        <FITID>ALG001
        <MEMO>PAGTO ALUGUEL
        </STMTTRN>
        <STMTTRN>
        <TRNTYPE>CREDIT
        <DTPOSTED>20260820140000[-3:BRT]
        <TRNAMT>980.50
        <FITID>ADQ001
        <MEMO>CREDITO ADQUIRENTE
        </STMTTRN>
        </BANKTRANLIST>
        </STMTRS></STMTTRNRS></BANKMSGSRSV1>
        </OFX>
        """;
}
