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

public sealed class AgendaCartaoTests : IDisposable
{
    private readonly SqliteConnection _conexao = new("DataSource=:memory:");
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly DateOnly _dia = new(2025, 1, 31);
    public AgendaCartaoTests()
    {
        _conexao.Open();
        _db = new(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conexao).Options);
        _db.Database.EnsureCreated();
        _repo = new(_db);
    }
    public void Dispose() { _db.Dispose(); _conexao.Dispose(); }
    private async Task<Paciente> PacienteAsync()
    {
        var p = new Paciente { Nome = "Teste cartão", Convenio = Convenio.Personalizado };
        _db.Pacientes.Add(p); await _db.SaveChangesAsync(); return p;
    }
    private Task<TaxaCartao> TaxaAsync(bool mensal, string nome = "Contrato A", decimal taxa = 3)
        => new TaxaService(_repo).SalvarAsync(new TaxaCartao { Adquirente = nome,
            Modalidade = ModalidadeCartao.CreditoParcelado, Percentual = taxa,
            DiasParaReceber = 0, LiquidacaoMensal = mensal, Ativa = true });
    private async Task<LancamentoFinanceiro> ReceberAsync(decimal valor = 100, string contrato = "Contrato A")
    {
        var p = await PacienteAsync();
        var l = await new FinanceiroService(_repo).LancarAsync(_dia, TipoLancamento.Entrada, "Pacote", valor,
            StatusLancamento.Previsto, pacienteId: p.Id);
        return await new PagamentosRecepcaoService(_repo, new TaxaService(_repo)).ReceberAsync(p.Id, l.Id,
            valor, _dia, FormaPagamento.CartaoCredito, adquirente: contrato, bandeira: "Visa", parcelas: 3);
    }
    private static string Ofx(DateOnly data, decimal valor, string id = "credito-1")
        => $"<OFX><BANKID>001<BRANCHID>1<ACCTID>42<STMTTRN><DTPOSTED>{data:yyyyMMdd}<TRNAMT>{valor.ToString(System.Globalization.CultureInfo.InvariantCulture)}<FITID>{id}<MEMO>Depósito</STMTTRN></OFX>";

    [Theory]
    [InlineData(true, 3)]
    [InlineData(false, 0)]
    public async Task Contrato_define_agenda_sem_duplicar_receita(bool mensal, int quantidade)
    {
        await TaxaAsync(mensal);
        var l = await ReceberAsync(100.01m);
        var parcelas = await _repo.ParcelasCartaoDoLancamentoAsync(l.Id);
        parcelas.Should().HaveCount(quantidade);
        (await _db.Lancamentos.CountAsync()).Should().Be(1);
        var recebiveis = new RecebiveisService(_repo);
        (await recebiveis.EsperadosAsync(_dia.AddMonths(3))).Sum(d => d.Liquido).Should().Be(97.01m);
        if (mensal)
        {
            parcelas.Select(p => p.Previsao).Should().Equal(_dia, new DateOnly(2025, 2, 28), new DateOnly(2025, 3, 31));
            parcelas.Select(p => p.Bruto).Should().Equal(33.33m, 33.33m, 33.35m);
            parcelas.Sum(p => p.Taxa).Should().Be(3);
            (await recebiveis.EsperadosAsync(_dia)).Single().Liquido.Should().Be(32.33m);
            (await _repo.LancamentosParaConciliacaoAsync(_dia, _dia)).Should().BeEmpty();
        }
    }

    [Fact]
    public async Task Centavos_residuais_nunca_produzem_credito_negativo()
    {
        await TaxaAsync(true, taxa: 83.3333m); var l = await ReceberAsync(.06m);
        var partes = await _repo.ParcelasCartaoDoLancamentoAsync(l.Id);
        partes.Sum(p => p.Bruto).Should().Be(.06m);
        partes.Sum(p => p.Taxa).Should().Be(.05m);
        partes.Should().OnlyContain(p => p.Liquido >= 0);
    }

    [Fact]
    public async Task Duas_estacoes_no_postgres_nao_conciliam_o_mesmo_credito()
    {
        if (!BancoDosTestes.NoPostgres) return;
        await TaxaAsync(true); await ReceberAsync();
        var linha = (await new ConciliacaoBancariaService(_repo).CruzarAsync(Ofx(_dia, 32.33m))).Linhas.Single();
        async Task<bool> Conciliar()
        {
            await using var db = new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conexao).Options);
            try
            {
                await new ConciliacaoBancariaService(new ClinicaRepositorio(db)).ConciliarItensAsync(linha.Candidatos,
                    linha.Extrato, conta: "001|1|42");
                return true;
            }
            catch (InvalidOperationException) { return false; }
        }
        (await Task.WhenAll(Conciliar(), Conciliar())).Count(r => r).Should().Be(1);
        (await _db.Auditoria.CountAsync(e => e.Acao == "LancamentoConciliado")).Should().Be(1);
    }

    [Fact]
    public async Task Renegociar_contrato_nao_reescreve_agenda_e_outro_contrato_pode_ser_integral()
    {
        var taxa = await TaxaAsync(true);
        await TaxaAsync(false, "Contrato B", 6);
        var a = await ReceberAsync();
        var b = await ReceberAsync(100, "Contrato B");
        taxa.LiquidacaoMensal = false; taxa.Percentual = 8;
        await new TaxaService(_repo).SalvarAsync(taxa);
        (await _repo.ParcelasCartaoDoLancamentoAsync(a.Id)).Should().HaveCount(3);
        (await _repo.ParcelasCartaoDoLancamentoAsync(b.Id)).Should().BeEmpty();
        var previsoes = await new RecebiveisService(_repo).EsperadosAsync(_dia.AddMonths(3));
        previsoes.Where(p => p.Adquirente == "Contrato A").Sum(p => p.Liquido).Should().Be(97);
        previsoes.Where(p => p.Adquirente == "Contrato B").Single().Liquido.Should().Be(94);
    }

    [Fact]
    public async Task Confirmar_uma_parcela_mantem_restante_aberto_e_cancelamento_recusado()
    {
        await TaxaAsync(true); var l = await ReceberAsync();
        var partes = await _repo.ParcelasCartaoDoLancamentoAsync(l.Id);
        var svc = new RecebiveisService(_repo);
        (await svc.ConfirmarAsync([], _dia, parcelaIds: [partes[0].Id])).Should().Be(1);
        (await _repo.ObterLancamentoAsync(l.Id))!.RecebimentoConfirmadoEm.Should().BeNull();
        (await svc.EsperadosAsync(_dia.AddMonths(3))).Sum(d => d.Quantidade).Should().Be(2);
        (await svc.ConfirmadosAsync(_dia, _dia)).Single().ParcelaIds.Should().Equal(partes[0].Id);
        await FluentActions.Awaiting(() => new FinanceiroService(_repo).CancelarAsync(l.Id, "Teste"))
            .Should().ThrowAsync<InvalidOperationException>();
        await svc.ConfirmarAsync([], _dia.AddMonths(2), parcelaIds: partes.Skip(1).Select(p => p.Id).ToArray());
        (await _repo.ObterLancamentoAsync(l.Id))!.RecebimentoConfirmadoEm.Should().Be(_dia.AddMonths(2));
        await svc.DesfazerConfirmacaoAsync([], parcelaIds: [partes[0].Id]);
        (await _repo.ObterLancamentoAsync(l.Id))!.RecebimentoConfirmadoEm.Should().BeNull();
        (await svc.EsperadosAsync(_dia.AddMonths(3))).Single().ParcelaIds.Should().Equal(partes[0].Id);
    }

    [Fact]
    public async Task Parcela_conciliada_sai_da_previsao_reimportacao_e_idempotente_e_reversao_restaura()
    {
        await TaxaAsync(true); var l = await ReceberAsync();
        var svc = new ConciliacaoBancariaService(_repo);
        var ofx = Ofx(_dia, 32.33m);
        var linha = (await svc.CruzarAsync(ofx)).Linhas.Single();
        linha.Situacao.Should().Be(SituacaoConciliacao.Casada);
        linha.Unico!.ParcelaId.Should().NotBeNull();
        await svc.ConciliarItensAsync(linha.Candidatos, linha.Extrato, conta: "001|1|42");
        (await svc.CruzarAsync(ofx)).Linhas.Single().Situacao.Should().Be(SituacaoConciliacao.JaConciliada);
        (await new RecebiveisService(_repo).EsperadosAsync(_dia)).Should().BeEmpty();
        await FluentActions.Awaiting(() => new RecebiveisService(_repo).DesfazerConfirmacaoAsync([],
            parcelaIds: [linha.Unico.ParcelaId!.Value])).Should().ThrowAsync<InvalidOperationException>();
        await svc.DesfazerParcelaAsync(linha.Unico.ParcelaId!.Value);
        (await new RecebiveisService(_repo).EsperadosAsync(_dia)).Single().Liquido.Should().Be(32.33m);
    }

    [Fact]
    public async Task Deposito_misto_concilia_integral_e_parcela_e_desfaz_todos_juntos()
    {
        await TaxaAsync(true); var mensal = await ReceberAsync();
        var integral = await new FinanceiroService(_repo).LancarAsync(_dia, TipoLancamento.Entrada, "Cartão integral", 50,
            formaPagamento: FormaPagamento.CartaoCredito, adquirente: "Contrato A",
            modalidadeCartao: ModalidadeCartao.CreditoAVista,
            deducoes: new DeducoesRecebimento(3, 1.5m, null, null, _dia, "teste"));
        var svc = new ConciliacaoBancariaService(_repo);
        var linha = (await svc.CruzarAsync(Ofx(_dia, 80.83m))).Linhas.Single();
        linha.Situacao.Should().Be(SituacaoConciliacao.DepositoCartao);
        linha.Candidatos.Should().HaveCount(2);
        await svc.ConciliarItensAsync(linha.Candidatos, linha.Extrato, conta: "001|1|42");
        (await _repo.ObterLancamentoAsync(integral.Id))!.Conciliado.Should().BeTrue();
        await svc.DesfazerAsync(integral.Id);
        (await _repo.ParcelasCartaoDoLancamentoAsync(mensal.Id)).Should().OnlyContain(p => p.ConciliadoEm == null && p.RecebidoEm == null);
        (await _repo.ObterLancamentoAsync(integral.Id))!.Conciliado.Should().BeFalse();
    }

    [Fact]
    public async Task FITID_na_parcela_impede_duplicata_em_venda_integral_da_mesma_conta()
    {
        await TaxaAsync(true); await ReceberAsync();
        var svc = new ConciliacaoBancariaService(_repo);
        var linha = (await svc.CruzarAsync(Ofx(_dia, 32.33m))).Linhas.Single();
        await svc.ConciliarItensAsync(linha.Candidatos, linha.Extrato, conta: "001|1|42");
        var l = await new FinanceiroService(_repo).LancarAsync(_dia, TipoLancamento.Entrada, "Pix", 32.33m, formaPagamento: FormaPagamento.Pix);
        await FluentActions.Awaiting(() => svc.ConciliarLinhaAsync(l.Id, linha.Extrato, conta: "001|1|42"))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*já foi conciliada*");
        await svc.ConciliarLinhaAsync(l.Id, linha.Extrato, conta: "outra conta");
    }

    [Fact]
    public async Task Agenda_impede_quitar_o_total_e_conciliacao_revalida_valor_do_credito()
    {
        await TaxaAsync(true); var l = await ReceberAsync();
        var svc = new ConciliacaoBancariaService(_repo);
        var linha = (await svc.CruzarAsync(Ofx(_dia, 32.33m))).Linhas.Single();
        await FluentActions.Awaiting(() => svc.ConciliarAsync(l.Id, "venda-inteira")).Should().ThrowAsync<InvalidOperationException>();
        await FluentActions.Awaiting(() => new RecebiveisService(_repo).ConfirmarAsync([l.Id], _dia)).Should().ThrowAsync<InvalidOperationException>();
        var partes = await _repo.ParcelasCartaoDoLancamentoAsync(l.Id);
        partes[0].Taxa = 2; await _db.SaveChangesAsync();
        await FluentActions.Awaiting(() => svc.ConciliarItensAsync(linha.Candidatos, linha.Extrato))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*divergente*");
        (await _repo.ParcelasCartaoDoLancamentoAsync(l.Id))[0].ConciliadoEm.Should().BeNull();
    }

    [Fact]
    public async Task Venda_de_pacote_no_cartao_cria_um_pagamento_e_agenda_do_contrato()
    {
        await TaxaAsync(true); var p = await PacienteAsync();
        var pacote = await new PacoteService(_repo).RegistrarVendaAsync(new PacotePaciente { PacienteId = p.Id,
            Nome = "Três sessões", Tipo = TipoPacote.Sessoes, SessoesContratadas = 3, Valor = 100, DataCompra = _dia },
            pagamento: PagamentoDaVenda.AVista(FormaPagamento.CartaoCredito) with
                { Adquirente = "Contrato A", Bandeira = "Visa", ParcelasCartao = 3 });
        var l = (await _repo.LancamentosDosPacotesAsync([pacote.Id])).Single();
        l.Status.Should().Be(StatusLancamento.Realizado);
        l.ValorTaxa.Should().Be(3);
        (await _repo.ParcelasCartaoDoLancamentoAsync(l.Id)).Should().HaveCount(3);
    }

    [Fact]
    public async Task Pacote_com_cartao_sem_contrato_recusa_toda_a_venda()
    {
        var p = await PacienteAsync();
        await FluentActions.Awaiting(() => new PacoteService(_repo).RegistrarVendaAsync(new PacotePaciente
        { PacienteId = p.Id, Nome = "Pacote", Tipo = TipoPacote.Sessoes, SessoesContratadas = 3, Valor = 100, DataCompra = _dia },
            pagamento: PagamentoDaVenda.AVista(FormaPagamento.CartaoCredito) with
                { Adquirente = "Inexistente", Bandeira = "Visa", ParcelasCartao = 3 }))
            .Should().ThrowAsync<InvalidOperationException>();
        (await _db.PacotesPaciente.CountAsync()).Should().Be(0);
        (await _db.Lancamentos.CountAsync()).Should().Be(0);
    }
}

