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

public sealed class ConsolidacaoGestaoTests : IDisposable
{
    private readonly SqliteConnection _conexao;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly FinanceiroService _financeiro;
    private readonly EstoqueService _estoque;
    private readonly DateOnly _dia = new(2026, 8, 10);

    public ConsolidacaoGestaoTests()
    {
        _conexao = new SqliteConnection("DataSource=:memory:");
        _conexao.Open();
        _db = new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conexao).Options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _financeiro = new FinanceiroService(_repo);
        _estoque = new EstoqueService(_repo);
    }

    public void Dispose() { _db.Dispose(); _conexao.Dispose(); }

    private async Task<Paciente> PacienteAsync()
    {
        var p = new Paciente { Nome = "Paciente de teste", Convenio = Convenio.Personalizado, ConvenioCodigo = "particular" };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p;
    }

    private Task<ItemEstoque> ItemAsync() => _estoque.SalvarItemAsync(new ItemEstoque { Nome = "Agulha", Unidade = "un" });

    [Fact]
    public async Task Insumo_sem_custo_nao_produz_media_zero_nem_sessao_mais_barata()
    {
        var item = await ItemAsync();
        var p = await PacienteAsync();
        var atendimento = new Atendimento { PacienteId = p.Id, Data = _dia };
        _db.Atendimentos.Add(atendimento); await _db.SaveChangesAsync();
        await _estoque.EntrarAsync(item.Id, 2, data: _dia);
        await _estoque.BaixarAsync(item.Id, 1, atendimento.Id, p.Id, _dia);
        (await _estoque.CustoDoAtendimentoAsync(atendimento.Id)).Completo.Should().BeFalse();
        var resumo = await _estoque.ResumoCustoSessoesAsync(_dia, _dia);
        resumo.SessoesComCustoIncompleto.Should().Be(1);
        resumo.MedioPorSessao.Should().BeNull();
        resumo.MaisCara.Should().BeNull();
    }

    [Fact]
    public async Task Caixa_e_resultado_contam_pagamento_no_mes_em_que_foi_recebido()
    {
        await _financeiro.LancarAsync(_dia.AddMonths(-1), TipoLancamento.Entrada, "Sessão anterior", 100,
            formaPagamento: FormaPagamento.Pix, dataPagamento: _dia,
            deducoes: new DeducoesRecebimento(null, null, 5, 5, null, null));
        var resumo = await _financeiro.ResumoAsync(_dia, _dia);
        resumo.EntradasRealizadas.Should().Be(100);
        resumo.SaldoLiquido.Should().Be(95);
        (await _financeiro.DoPeriodoAsync(_dia, _dia)).Should().ContainSingle();
        (await _financeiro.ResumoAsync(_dia.AddMonths(-1), _dia.AddMonths(-1))).EntradasRealizadas.Should().Be(0);
        (await new ResultadoMensalService(_repo).DoMesAsync(2026, 8)).Resultado.Should().Be(95);
    }

    [Fact]
    public async Task Recepcao_recebe_cobranca_existente_e_segunda_tentativa_nao_duplica()
    {
        var p = await PacienteAsync();
        var conta = await _financeiro.LancarAsync(_dia, TipoLancamento.Entrada, "Sessão", 150,
            StatusLancamento.Previsto, pacienteId: p.Id, dataVencimento: _dia);
        var servico = new PagamentosRecepcaoService(_repo, new TaxaService(_repo));
        var recebido = await servico.ReceberAsync(p.Id, conta.Id, 150, _dia, FormaPagamento.Pix, "balcão");
        recebido.Id.Should().Be(conta.Id);
        recebido.DataPagamento.Should().Be(_dia);
        recebido.Status.Should().Be(StatusLancamento.Realizado);
        await FluentActions.Awaiting(() => servico.ReceberAsync(p.Id, conta.Id, 150, _dia, FormaPagamento.Pix))
            .Should().ThrowAsync<InvalidOperationException>();
        (await _db.Lancamentos.CountAsync()).Should().Be(1);
        (await servico.DoPacienteAsync(p.Id)).Should().ContainSingle();
        (await new FechamentoCaixaService(_repo).PrepararAsync(_dia)).Esperado.Should().Be(0);
    }

    [Fact]
    public async Task Recepcao_recusa_valor_alterado_e_cobranca_de_outro_paciente()
    {
        var p = await PacienteAsync();
        var outro = await PacienteAsync();
        var conta = await _financeiro.LancarAsync(_dia, TipoLancamento.Entrada, "Sessão", 150,
            StatusLancamento.Previsto, pacienteId: p.Id);
        var servico = new PagamentosRecepcaoService(_repo, new TaxaService(_repo));
        await FluentActions.Awaiting(() => servico.ReceberAsync(outro.Id, conta.Id, 150, _dia, FormaPagamento.Pix))
            .Should().ThrowAsync<InvalidOperationException>();
        await FluentActions.Awaiting(() => servico.ReceberAsync(p.Id, conta.Id, 100, _dia, FormaPagamento.Pix))
            .Should().ThrowAsync<InvalidOperationException>();
        (await _repo.ObterLancamentoAsync(conta.Id))!.Status.Should().Be(StatusLancamento.Previsto);
    }

    [Fact]
    public async Task Recepcao_nao_expoe_despesas_nem_cobrancas_da_operadora()
    {
        var p = await PacienteAsync();
        await _financeiro.LancarAsync(_dia, TipoLancamento.Saida, "Despesa", 40, pacienteId: p.Id);
        await _financeiro.LancarAsync(_dia, TipoLancamento.Entrada, "Operadora", 40, pacienteId: p.Id,
            formaPagamento: FormaPagamento.Convenio);
        (await _repo.CobrancasDoPacienteAsync(p.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task Cartao_do_balcao_guarda_taxa_prazo_e_vira_recebivel_liquido()
    {
        var p = await PacienteAsync();
        var taxas = new TaxaService(_repo);
        await taxas.SalvarAsync(new TaxaCartao { Adquirente = "Maquininha", Modalidade = ModalidadeCartao.CreditoAVista,
            Percentual = 3, DiasParaReceber = 30, Ativa = true });
        var conta = await _financeiro.LancarAsync(_dia, TipoLancamento.Entrada, "Sessão", 100,
            StatusLancamento.Previsto, pacienteId: p.Id);
        var recebido = await new PagamentosRecepcaoService(_repo, taxas).ReceberAsync(p.Id, conta.Id, 100,
            _dia, FormaPagamento.CartaoCredito, adquirente: "Maquininha", bandeira: "Visa");
        recebido.ValorTaxa.Should().Be(3);
        recebido.PrevisaoRecebimento.Should().Be(_dia.AddDays(30));
        (await new RecebiveisService(_repo).ResumoAsync(_dia, _dia.AddDays(31))).Total.Should().Be(97);
    }

    [Fact]
    public async Task Cartao_sem_taxa_cadastrada_nao_vira_recebimento_invisivel()
    {
        var p = await PacienteAsync();
        var conta = await _financeiro.LancarAsync(_dia, TipoLancamento.Entrada, "Sessão", 100,
            StatusLancamento.Previsto, pacienteId: p.Id);
        await FluentActions.Awaiting(() => new PagamentosRecepcaoService(_repo, new TaxaService(_repo))
            .ReceberAsync(p.Id, conta.Id, 100, _dia, FormaPagamento.CartaoCredito, adquirente: "Ausente", bandeira: "Visa"))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*taxa*");
        (await _repo.ObterLancamentoAsync(conta.Id))!.Status.Should().Be(StatusLancamento.Previsto);
    }

    [Fact]
    public async Task Recibo_nao_comprova_cobranca_ainda_nao_recebida()
    {
        var p = await PacienteAsync();
        var conta = await _financeiro.LancarAsync(_dia, TipoLancamento.Entrada, "Sessão", 100,
            StatusLancamento.Previsto, pacienteId: p.Id);
        await FluentActions.Awaiting(() => new DocumentoFinanceiroService(_repo).EmitirReciboDoLancamentoAsync(conta.Id))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*recebimento*");
    }

    private string Ofx(decimal valor, string id = "TX1", string conta = "123") =>
        $"<OFX><BANKID>001<BRANCHID>0001<ACCTID>{conta}<DTSTART>{_dia:yyyyMMdd}<DTEND>{_dia:yyyyMMdd}"
        + $"<STMTTRN><DTPOSTED>{_dia:yyyyMMdd}<TRNAMT>{valor.ToString(System.Globalization.CultureInfo.InvariantCulture)}<FITID>{id}</STMTTRN></OFX>";

    [Fact]
    public async Task Conciliacao_encontra_venda_antiga_pelo_liquido_e_data_do_deposito()
    {
        var l = await _financeiro.LancarAsync(_dia.AddMonths(-1), TipoLancamento.Entrada, "Cartão", 100,
            formaPagamento: FormaPagamento.CartaoCredito,
            deducoes: new DeducoesRecebimento(3, 3, 2, 2, _dia, "teste"));
        var servico = new ConciliacaoBancariaService(_repo);
        var r = await servico.CruzarAsync(Ofx(97));
        r.Linhas.Single().Unico!.Id.Should().Be(l.Id);
        await servico.ConciliarLinhaAsync(l.Id, r.Linhas.Single().Extrato, conta: "001|0001|123");
        l.RecebimentoConfirmadoEm.Should().Be(_dia);
        (await new RecebiveisService(_repo).EsperadosAsync(_dia)).Should().BeEmpty();
        (await servico.CruzarAsync(Ofx(97))).Linhas.Single().Situacao.Should().Be(SituacaoConciliacao.JaConciliada);
        await servico.DesfazerAsync(l.Id);
        l.RecebimentoConfirmadoEm.Should().BeNull();
        (await new RecebiveisService(_repo).EsperadosAsync(_dia)).Should().ContainSingle();
    }

    [Fact]
    public async Task Conciliacao_recusa_mesma_transacao_duas_vezes_e_aceita_fitid_de_outra_conta()
    {
        var a = await _financeiro.LancarAsync(_dia, TipoLancamento.Entrada, "A", 100, formaPagamento: FormaPagamento.Pix);
        var b = await _financeiro.LancarAsync(_dia, TipoLancamento.Entrada, "B", 100, formaPagamento: FormaPagamento.Pix);
        var servico = new ConciliacaoBancariaService(_repo);
        var linha = new LinhaExtrato("TX1", _dia, 100, "Pix");
        await servico.ConciliarLinhaAsync(a.Id, linha, conta: "001|1|A");
        await FluentActions.Awaiting(() => servico.ConciliarLinhaAsync(b.Id, linha, conta: "001|1|A"))
            .Should().ThrowAsync<InvalidOperationException>();
        await servico.ConciliarLinhaAsync(b.Id, linha, conta: "001|1|B");
        (await _db.Lancamentos.CountAsync(l => l.ConciliadoEm != null)).Should().Be(2);
    }

    [Fact]
    public async Task Concilia_so_realizado_com_valor_e_direcao_corretos_sem_dinheiro_da_gaveta()
    {
        var a = await _financeiro.LancarAsync(_dia, TipoLancamento.Entrada, "Pix", 100, formaPagamento: FormaPagamento.Pix);
        var servico = new ConciliacaoBancariaService(_repo);
        await FluentActions.Awaiting(() => servico.ConciliarLinhaAsync(a.Id, new LinhaExtrato("x", _dia, 99, "Pix")))
            .Should().ThrowAsync<InvalidOperationException>();
        await FluentActions.Awaiting(() => servico.ConciliarLinhaAsync(a.Id, new LinhaExtrato("x", _dia, -100, "Pix")))
            .Should().ThrowAsync<InvalidOperationException>();
        var dinheiro = await _financeiro.LancarAsync(_dia, TipoLancamento.Entrada, "Espécie", 100, formaPagamento: FormaPagamento.Dinheiro);
        await FluentActions.Awaiting(() => servico.ConciliarAsync(dinheiro.Id, "x"))
            .Should().ThrowAsync<InvalidOperationException>();
        (await _repo.LancamentosParaConciliacaoAsync(_dia, _dia)).Should().ContainSingle();
    }

    [Fact]
    public async Task Consumo_guarda_custo_que_nao_muda_quando_chega_compra_mais_cara()
    {
        var item = await ItemAsync();
        var p = await PacienteAsync();
        var atendimento = new Atendimento { PacienteId = p.Id, Data = _dia };
        _db.Atendimentos.Add(atendimento); await _db.SaveChangesAsync();
        await _estoque.EntrarAsync(item.Id, 10, 2, _dia);
        var baixa = await _estoque.BaixarAsync(item.Id, 5, atendimento.Id, p.Id, _dia);
        baixa.CustoUnitario.Should().Be(2);
        await _estoque.EntrarAsync(item.Id, 10, 8, _dia.AddDays(1));
        (await _estoque.CustoDoAtendimentoAsync(atendimento.Id)).Custo.Should().Be(10);
        (await _estoque.CustosMediosAsync())[item.Id].Should().Be(6);
    }

    [Fact]
    public async Task Validade_mostra_somente_saldo_restante_do_lote()
    {
        var item = await ItemAsync();
        await _estoque.EntrarAsync(item.Id, 10, 2, _dia, _dia.AddDays(2), "A");
        await _estoque.EntrarAsync(item.Id, 20, 3, _dia, _dia.AddDays(60), "B");
        await _estoque.BaixarAsync(item.Id, 10, data: _dia);
        var validades = await _estoque.ValidadesAsync(_dia, 90);
        validades.Should().ContainSingle().Which.Lote.Should().Be("B");
        validades.Single().Quantidade.Should().Be(20);
    }

    [Fact]
    public async Task Estoque_recusa_consumo_vencido_e_preserva_historico_contra_exclusao()
    {
        var item = await ItemAsync();
        await _estoque.EntrarAsync(item.Id, 10, 2, _dia, _dia, "Vencido");
        await FluentActions.Awaiting(() => _estoque.BaixarAsync(item.Id, 1, data: _dia.AddDays(1)))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*vencido*");
        await FluentActions.Awaiting(() => _estoque.ExcluirItemAsync(item.Id))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*inativado*");
        (await _estoque.MovimentosAsync(item.Id)).Should().ContainSingle();
    }

    [Fact]
    public async Task Compra_vincula_estoque_conta_e_auditoria_sem_dobrar_a_despesa()
    {
        var item = await ItemAsync();
        var movimento = await _estoque.ComprarAsync(new MovimentoEstoque { ItemEstoqueId = item.Id,
            Tipo = TipoMovimentoEstoque.Entrada, Data = _dia, Quantidade = 10, CustoUnitario = 2 },
            "Fornecedor", _dia.AddDays(30));
        var conta = await _repo.ObterLancamentoAsync(movimento.LancamentoFinanceiroId!.Value);
        conta!.Tipo.Should().Be(TipoLancamento.Saida);
        conta.Status.Should().Be(StatusLancamento.Previsto);
        conta.Valor.Should().Be(20);
        (await _estoque.SaldosAsync()).Single().Saldo.Should().Be(10);
        await _financeiro.RealizarAsync(conta.Id, _dia.AddDays(30), FormaPagamento.Pix);
        (await _db.Lancamentos.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Falha_financeira_da_compra_desfaz_tambem_entrada_e_auditoria_do_estoque()
    {
        var item = await ItemAsync();
        await FluentActions.Awaiting(() => _estoque.ComprarAsync(new MovimentoEstoque { ItemEstoqueId = item.Id,
            Tipo = TipoMovimentoEstoque.Entrada, Data = _dia, Quantidade = .001m, CustoUnitario = .0001m },
            "Fornecedor", _dia)).Should().ThrowAsync<ArgumentException>();
        (await _estoque.MovimentosAsync(item.Id)).Should().BeEmpty();
        (await _db.Lancamentos.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Recibo_repetido_reutiliza_documento_vigente_e_conserva_numero()
    {
        var p = await PacienteAsync();
        var l = await _financeiro.LancarAsync(_dia, TipoLancamento.Entrada, "Consulta", 100, pacienteId: p.Id);
        var servico = new DocumentoFinanceiroService(_repo);
        var primeiro = await servico.EmitirReciboDoLancamentoAsync(l.Id);
        var segundaVia = await servico.EmitirReciboDoLancamentoAsync(l.Id);
        segundaVia.Id.Should().Be(primeiro.Id);
        (await _db.DocumentosFinanceiros.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Deposito_agrupa_vendas_da_mesma_adquirente_e_desfaz_o_lote_inteiro()
    {
        var ids = new List<int>();
        foreach (var valor in new[] { 100m, 200m })
            ids.Add((await _financeiro.LancarAsync(_dia.AddDays(-30), TipoLancamento.Entrada, "Cartão", valor,
                formaPagamento: FormaPagamento.CartaoCredito, adquirente: "Maquininha",
                modalidadeCartao: ModalidadeCartao.CreditoAVista,
                deducoes: new DeducoesRecebimento(3, valor * .03m, 2, valor * .02m, _dia, "teste"))).Id);
        var servico = new ConciliacaoBancariaService(_repo);
        var proposta = (await servico.CruzarAsync(Ofx(291))).Linhas.Single();
        proposta.Situacao.Should().Be(SituacaoConciliacao.DepositoCartao);
        proposta.Candidatos.Select(l => l.Id).Should().BeEquivalentTo(ids);
        await servico.ConciliarDepositoAsync(ids, proposta.Extrato, conta: "001|0001|123");
        (await servico.CruzarAsync(Ofx(291))).Linhas.Single().Situacao.Should().Be(SituacaoConciliacao.JaConciliada);
        (await new RecebiveisService(_repo).EsperadosAsync(_dia)).Should().BeEmpty();
        await servico.DesfazerAsync(ids[0]);
        (await _db.Lancamentos.CountAsync(l => l.ConciliadoEm != null)).Should().Be(0);
        (await new RecebiveisService(_repo).EsperadosAsync(_dia)).Single().Quantidade.Should().Be(2);
    }

    [Fact]
    public async Task Lote_divergente_nao_concilia_nenhuma_venda()
    {
        var a = await _financeiro.LancarAsync(_dia, TipoLancamento.Entrada, "A", 100,
            formaPagamento: FormaPagamento.CartaoCredito, adquirente: "A", modalidadeCartao: ModalidadeCartao.CreditoAVista);
        var b = await _financeiro.LancarAsync(_dia, TipoLancamento.Entrada, "B", 100,
            formaPagamento: FormaPagamento.CartaoCredito, adquirente: "B", modalidadeCartao: ModalidadeCartao.CreditoAVista);
        await FluentActions.Awaiting(() => new ConciliacaoBancariaService(_repo)
            .ConciliarDepositoAsync(new[] { a.Id, b.Id }, new LinhaExtrato("LOTE", _dia, 200, "Crédito")))
            .Should().ThrowAsync<InvalidOperationException>();
        (await _db.Lancamentos.CountAsync(l => l.ConciliadoEm != null)).Should().Be(0);
    }

    [Fact]
    public async Task Caixa_conferido_bloqueia_recebimento_e_cancelamento_ate_reabertura()
    {
        var p = await PacienteAsync();
        var recebido = await _financeiro.LancarAsync(_dia, TipoLancamento.Entrada, "Recebido", 50,
            formaPagamento: FormaPagamento.Dinheiro, pacienteId: p.Id);
        var conta = await _financeiro.LancarAsync(_dia, TipoLancamento.Entrada, "Pendente", 100,
            StatusLancamento.Previsto, pacienteId: p.Id);
        var caixa = new FechamentoCaixaService(_repo);
        await caixa.ConferirAsync(_dia, 50);
        var pagamentos = new PagamentosRecepcaoService(_repo, new TaxaService(_repo));
        await FluentActions.Awaiting(() => pagamentos.ReceberAsync(p.Id, conta.Id, 100, _dia, FormaPagamento.Dinheiro))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*Reabra*");
        await FluentActions.Awaiting(() => _financeiro.CancelarAsync(recebido.Id, "Correção"))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*Reabra*");
        await caixa.ReabrirAsync(_dia, "Incluir pagamento atrasado", "gerente");
        await pagamentos.ReceberAsync(p.Id, conta.Id, 100, _dia, FormaPagamento.Dinheiro);
        (await caixa.ConferirAsync(_dia, 150)).Bateu.Should().BeTrue();
    }

    [Fact]
    public async Task Duas_estacoes_no_postgres_nao_recebem_a_mesma_cobranca_duas_vezes()
    {
        if (!BancoDosTestes.NoPostgres) return;
        var p = await PacienteAsync();
        var l = await _financeiro.LancarAsync(_dia, TipoLancamento.Entrada, "Consulta", 100,
            StatusLancamento.Previsto, pacienteId: p.Id);
        async Task<bool> Receber()
        {
            await using var db = new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conexao).Options);
            var repo = new ClinicaRepositorio(db);
            try { await new PagamentosRecepcaoService(repo, new TaxaService(repo)).ReceberAsync(p.Id, l.Id, 100, _dia, FormaPagamento.Pix); return true; }
            catch (InvalidOperationException) { return false; }
        }
        (await Task.WhenAll(Receber(), Receber())).Count(r => r).Should().Be(1);
        (await _db.Auditoria.CountAsync(e => e.Acao == "PagamentoRecebidoNaRecepcao")).Should().Be(1);
    }

    [Fact]
    public async Task Duas_estacoes_no_postgres_nao_consumem_o_mesmo_saldo_de_estoque()
    {
        if (!BancoDosTestes.NoPostgres) return;
        var item = await ItemAsync();
        await _estoque.EntrarAsync(item.Id, 1, data: _dia, custoUnitario: 10);
        async Task<bool> Baixar()
        {
            await using var db = new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conexao).Options);
            try { await new EstoqueService(new ClinicaRepositorio(db)).MovimentarAsync(new MovimentoEstoque
                { ItemEstoqueId = item.Id, Tipo = TipoMovimentoEstoque.Saida, Quantidade = 1, Data = _dia }); return true; }
            catch (InvalidOperationException) { return false; }
        }
        (await Task.WhenAll(Baixar(), Baixar())).Count(r => r).Should().Be(1);
        (await _estoque.SaldosAsync()).Single().Saldo.Should().Be(0);
    }

    [Fact]
    public async Task Recebimento_futuro_nao_e_confirmado_no_caixa_ou_na_adquirente()
    {
        var amanha = DateOnly.FromDateTime(DateTime.Today).AddDays(1);
        var l = await _financeiro.LancarAsync(_dia, TipoLancamento.Entrada, "Consulta", 100, StatusLancamento.Previsto);
        await FluentActions.Awaiting(() => _financeiro.RealizarAsync(l.Id, amanha))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*futuro*");
        await FluentActions.Awaiting(() => new RecebiveisService(_repo).ConfirmarAsync(new[] { l.Id }, amanha))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*futuro*");
    }

    [Fact]
    public async Task Convenio_concilia_valor_transferido_apos_retencoes()
    {
        var l = await _financeiro.LancarAsync(_dia, TipoLancamento.Entrada, "Operadora", 100,
            formaPagamento: FormaPagamento.Convenio, deducoes: new DeducoesRecebimento(null, null, 6, 6, null, null));
        var r = await new ConciliacaoBancariaService(_repo).CruzarAsync(Ofx(94));
        r.Linhas.Single().Unico!.Id.Should().Be(l.Id);
    }

    [Fact]
    public async Task Ofx_sem_identidade_bancaria_nao_pode_ser_conciliado_por_id_sintetico()
    {
        var l = await _financeiro.LancarAsync(_dia, TipoLancamento.Entrada, "Pix", 100, formaPagamento: FormaPagamento.Pix);
        var servico = new ConciliacaoBancariaService(_repo);
        var r = await servico.CruzarAsync(Ofx(100).Replace("<FITID>TX1", ""));
        r.Linhas.Single().Situacao.Should().Be(SituacaoConciliacao.SoNoBanco);
        await FluentActions.Awaiting(() => servico.ConciliarLinhaAsync(l.Id, r.Linhas.Single().Extrato))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*FITID*");
    }
}
