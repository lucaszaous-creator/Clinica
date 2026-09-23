using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace Clinica.Tests;

public sealed partial class ParcelamentoContasProfissionalTests
{
    [Theory]
    [InlineData(TipoLancamento.Entrada)]
    [InlineData(TipoLancamento.Saida)]
    public async Task Duas_baixas_parciais_e_estorno_preservam_total_da_obrigacao(TipoLancamento tipo)
    {
        var financeiro = new FinanceiroService(new ClinicaRepositorio(_db));
        var conta = await _contas.LancarContaAsync(tipo, "Obrigação", 100m, new(2026,8,10), contraparte:"Contraparte", documentoReferencia:"DOC");
        await financeiro.RealizarAsync(conta.Id, formaPagamento:FormaPagamento.Pix, valorConferido:100m, valorPago:30.33m);
        var historico = await _contas.HistoricoObrigacaoAsync(conta.Id);
        historico.Should().HaveCount(2);
        var saldo = historico.Single(c => c.Status == StatusLancamento.Previsto);
        saldo.Valor.Should().Be(69.67m);
        saldo.Contraparte.Should().Be("Contraparte"); saldo.DocumentoReferencia.Should().Be("DOC");
        saldo.ValorOriginalObrigacao.Should().Be(100m); saldo.OrigemDesdobramentoId.Should().Be(conta.Id);
        await financeiro.RealizarAsync(saldo.Id, formaPagamento:FormaPagamento.Pix, valorPago:20m);
        await financeiro.CancelarAsync(conta.Id, "Baixa informada incorretamente");
        historico = await _contas.HistoricoObrigacaoAsync(saldo.Id);
        historico.Where(c => c.Status == StatusLancamento.Realizado).Sum(c => c.Valor).Should().Be(20m);
        historico.Where(c => c.Status == StatusLancamento.Previsto).Sum(c => c.Valor).Should().Be(80m);
        historico.Should().OnlyContain(c => c.ValorOriginalObrigacao == 100m);
        var repetir = () => financeiro.CancelarAsync(conta.Id, "Repetir");
        await repetir.Should().ThrowAsync<InvalidOperationException>();
        (await _contas.HistoricoObrigacaoAsync(conta.Id)).Should().HaveCount(4);
    }

    [Fact]
    public async Task Recepcao_baixa_parte_emite_recibo_do_pago_e_estorno_cancela_o_recibo()
    {
        var repo = new ClinicaRepositorio(_db);
        var paciente = new Paciente { Nome = "Paciente de teste" };
        _db.Add(paciente); await _db.SaveChangesAsync();
        var conta = await _contas.LancarContaAsync(TipoLancamento.Entrada,"Sessão",100m,new(2026,8,10),pacienteId:paciente.Id);
        var pagamentos = new PagamentosRecepcaoService(repo,new TaxaService(repo));
        var pago = await pagamentos.ReceberAsync(paciente.Id,conta.Id,100m,new(2026,8,10),FormaPagamento.Pix,valorRecebido:25m);
        pago.Valor.Should().Be(25m);
        var recibo = await new DocumentoFinanceiroService(repo).EmitirReciboDoLancamentoAsync(pago.Id,"Paciente de teste");
        recibo.ValorTotal.Should().Be(25m);
        var repetir = () => pagamentos.ReceberAsync(paciente.Id,conta.Id,100m,new(2026,8,10),FormaPagamento.Pix,valorRecebido:25m);
        await repetir.Should().ThrowAsync<InvalidOperationException>();
        (await _contas.HistoricoObrigacaoAsync(conta.Id)).Single(l => l.Status == StatusLancamento.Previsto).Valor.Should().Be(75m);
        await new FinanceiroService(repo).CancelarAsync(pago.Id,"Pagamento incorreto");
        _db.ChangeTracker.Clear();
        (await repo.ObterDocumentoFinanceiroAsync(recibo.Id))!.Cancelado.Should().BeTrue();
        (await _contas.HistoricoObrigacaoAsync(conta.Id)).Where(c => c.Status == StatusLancamento.Previsto).Sum(c => c.Valor).Should().Be(100m);
    }

    [Fact]
    public async Task Falha_na_taxa_de_cartao_desfaz_desdobramento_e_valor_alterado()
    {
        var conta = await _contas.LancarContaAsync(TipoLancamento.Entrada,"Sessão",100m,new(2026,8,10));
        var pagar = () => new FinanceiroService(new ClinicaRepositorio(_db)).RealizarAsync(conta.Id,
            formaPagamento:FormaPagamento.CartaoCredito, adquirente:"Contrato não cadastrado",bandeira:"Visa",parcelas:2,valorPago:30m);
        await pagar.Should().ThrowAsync<InvalidOperationException>();
        _db.ChangeTracker.Clear();
        var salvo = await _db.Set<LancamentoFinanceiro>().SingleAsync();
        salvo.Valor.Should().Be(100m); salvo.Status.Should().Be(StatusLancamento.Previsto);
        salvo.GrupoObrigacao.Should().BeNull();
    }

    [Fact]
    public async Task Baixa_de_parcela_nao_recria_parcelamento_nem_perde_centavos_de_retencao()
    {
        var grupo = Guid.NewGuid();
        var parcelas = await _contas.LancarParcelamentoAsync(grupo,TipoLancamento.Entrada,"Contrato",300.03m,3,new(2026,8,10),new(2026,8,1));
        var primeira = parcelas[0]; primeira.ValorImposto = 3m; await _db.SaveChangesAsync();
        await new FinanceiroService(new ClinicaRepositorio(_db)).RealizarAsync(primeira.Id, formaPagamento:FormaPagamento.Convenio,valorPago:33.34m);
        var historico = await _contas.HistoricoObrigacaoAsync(primeira.Id);
        historico.Sum(l => l.ValorImposto ?? 0m).Should().Be(3m);
        historico.Sum(l => l.Valor).Should().Be(100.01m);
        var repetir = await _contas.LancarParcelamentoAsync(grupo,TipoLancamento.Entrada,"Contrato",300.03m,3,new(2026,8,10),new(2026,8,1));
        repetir.Select(l => l.Id).Should().Equal(parcelas.Select(l => l.Id));
        (await _db.Set<LancamentoFinanceiro>().CountAsync()).Should().Be(4);
    }

    [Fact]
    public async Task Cartao_parcial_gera_depositos_somente_do_valor_pago_e_preserva_saldo_do_paciente()
    {
        var repo = new ClinicaRepositorio(_db);
        var paciente = new Paciente { Nome = "Paciente de cartão" };
        _db.Add(paciente); await _db.SaveChangesAsync();
        var taxas = new TaxaService(repo);
        await taxas.SalvarAsync(new TaxaCartao { Adquirente = "Contrato mensal", Modalidade = ModalidadeCartao.CreditoParcelado,
            Percentual = 3, DiasParaReceber = 0, LiquidacaoMensal = true, Ativa = true });
        var conta = await _contas.LancarContaAsync(TipoLancamento.Entrada,"Tratamento",100m,new(2026,8,10),pacienteId:paciente.Id);
        var pago = await new PagamentosRecepcaoService(repo,taxas).ReceberAsync(paciente.Id,conta.Id,100m,new(2026,8,10),
            FormaPagamento.CartaoCredito,adquirente:"Contrato mensal",bandeira:"Visa",parcelas:3,valorRecebido:30m);
        pago.ValorTaxa.Should().Be(0.90m);
        var depositos = await repo.ParcelasCartaoDoLancamentoAsync(pago.Id);
        depositos.Should().HaveCount(3);
        depositos.Sum(p => p.Bruto).Should().Be(30m);
        depositos.Sum(p => p.Taxa).Should().Be(0.90m);
        (await new RecebiveisService(repo).EsperadosAsync(new(2026,12,31))).Sum(p => p.Liquido).Should().Be(29.10m);
        (await _contas.HistoricoObrigacaoAsync(conta.Id)).Single(c => c.Status == StatusLancamento.Previsto).Valor.Should().Be(70m);
    }
}
