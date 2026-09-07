using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// A COBRANÇA NO BALCÃO e o dono do lançamento manual (set/2026, item 3 da lista "o que
/// falta para ficar profissional").
///
/// Dois buracos da mesma família, e nenhum falhava: o <c>ElegibilidadeService</c> avisa
/// desde a parcela 27 que o paciente deve — com a pessoa na frente, que é a hora barata
/// de resolver — e a única porta para RECEBER ficava no Financeiro, outro app, de outra
/// pessoa; e o lançamento manual do caixa era a única porta de dinheiro sem paciente, de
/// modo que a receita entrava órfã e a sessão continuava na aba Particulares da
/// Conciliação, cobrando um pagamento que já estava na gaveta.
///
/// O que os testes prendem é o CIRCUITO, não a tela: receber pela porta do balcão tem de
/// APAGAR o alerta, e amarrar o recebimento manual à sessão tem de TIRÁ-LA da lista de
/// sessões sem receita. Elo partido aqui não vira erro — vira alerta que nunca some e
/// linha que nunca sai, que é indistinguível de "ainda não pagaram".
/// </summary>
public class CobrancaNoBalcaoTests : IDisposable
{
    private const string CodigoParticular = "Particular";

    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly FinanceiroService _financeiro;
    private readonly ContasService _contas;
    private readonly InadimplenciaService _inadimplencia;
    private readonly ElegibilidadeService _elegibilidade;
    private readonly AgendaService _agenda;
    private readonly FechamentoSessaoService _fechamento;
    private readonly int _profPadrao;
    private int _marcados;

    private static readonly DateTime Sessao = new(2026, 8, 20, 14, 0, 0);
    private static DateOnly Hoje => DateOnly.FromDateTime(Sessao);

    public CobrancaNoBalcaoTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        var prof = new Profissional { Nome = "Dra. Padrão" };
        _db.Profissionais.Add(prof);
        _db.SaveChanges();
        _profPadrao = prof.Id;

        _repo = new ClinicaRepositorio(_db);
        _financeiro = new FinanceiroService(_repo);
        _contas = new ContasService(_repo);
        _inadimplencia = new InadimplenciaService(_repo, _financeiro);
        var pacotes = new PacoteService(_repo);
        var precos = new PrecoParticularService(_repo);
        _agenda = new AgendaService(_repo, new AtendimentoService(_repo));
        _fechamento = new FechamentoSessaoService(
            _repo, _agenda, pacotes, new EstoqueService(_repo), _financeiro, precos, _contas);
        _elegibilidade = new ElegibilidadeService(
            _repo, new AutorizacaoService(_repo), new ConsentimentoService(_repo),
            new ConsultaService(_repo), _inadimplencia, pacotes);

        CatalogoConvenios.Atualizar(
        [
            new EntradaConvenio(CodigoParticular, "Particular", Convenio.Personalizado, true,
                new ConfiguracaoRegraGenerica { FazEletro = true, TemSegundoCodigo = true },
                FormatoNumeroGuia.SemValidacao, GeraGuia: false)
        ]);
    }

    public void Dispose()
    {
        CatalogoConvenios.Atualizar([]);
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<int> ParticularAsync(string nome = "Maria")
    {
        var p = new Paciente
        {
            Nome = nome, Convenio = Convenio.Personalizado, ConvenioCodigo = CodigoParticular,
            Sexo = Sexo.Feminino, Telefone = "21999990000"
        };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    private async Task<Atendimento> RealizarAsync(int pacienteId)
    {
        var ag = await _agenda.AgendarAsync(
            pacienteId, Sessao.AddHours(_marcados++), ModalidadeAtendimento.AcupunturaComEletro,
            null, profissionalId: _profPadrao);
        var registro = await _fechamento.RegistrarAtendimentoAsync(ag.Id, "recepcao");
        return registro.Atendimento;
    }

    // ==================== A dívida, e a porta que a apaga ====================

    [Fact]
    public async Task Receber_no_balcao_apaga_o_alerta_de_divida()
    {
        var pacienteId = await ParticularAsync();
        var conta = await _contas.LancarContaAsync(
            TipoLancamento.Entrada, "Sessão avulsa", 120m,
            vencimento: Hoje.AddDays(-20), pacienteId: pacienteId);

        // Antes: o balcão vê o aviso — é ele que traz o botão "Receber…".
        var antes = await _elegibilidade.ConferirAsync(pacienteId, Hoje);
        antes.Alertas.Should().ContainSingle(a => a.Motivo == ImpedimentoElegibilidade.PacienteEmDebito);

        // A janela do balcão passa por aqui — quem grava dinheiro continua sendo um só.
        await _inadimplencia.ReceberAsync(
            conta.Id, Hoje, FormaPagamento.Dinheiro, operador: "recepcao");

        // Depois: o alerta SOME. Ele sai por a conta ter sido paga, nunca por alguém marcá-la.
        var depois = await _elegibilidade.ConferirAsync(pacienteId, Hoje);
        depois.Alertas.Should().NotContain(a => a.Motivo == ImpedimentoElegibilidade.PacienteEmDebito);
    }

    [Fact]
    public async Task Receber_no_balcao_grava_a_forma_de_pagamento_escolhida_na_linha()
    {
        // A forma é escolhida NA LINHA, com o paciente na frente: presumir a última usada
        // gravaria PIX no dinheiro que ele acabou de pôr no balcão, e o caixa não bate.
        var pacienteId = await ParticularAsync();
        var conta = await _contas.LancarContaAsync(
            TipoLancamento.Entrada, "Sessão avulsa", 120m,
            vencimento: Hoje.AddDays(-30), pacienteId: pacienteId);

        await _inadimplencia.ReceberAsync(conta.Id, Hoje, FormaPagamento.Pix, operador: "recepcao");

        var gravado = await _db.Lancamentos.AsNoTracking().SingleAsync(l => l.Id == conta.Id);
        gravado.Status.Should().Be(StatusLancamento.Realizado);
        gravado.FormaPagamento.Should().Be(FormaPagamento.Pix);
        gravado.DataPagamento.Should().Be(Hoje);
    }

    [Fact]
    public async Task A_divida_do_paciente_traz_o_detalhe_que_a_janela_lista()
    {
        // A janela do balcão lista conta a conta: "todas" não é número, e receber às cegas
        // é o que a lista existe para impedir.
        var pacienteId = await ParticularAsync();
        await _contas.LancarContaAsync(
            TipoLancamento.Entrada, "Sessão de 05/07", 120m,
            vencimento: Hoje.AddDays(-40), pacienteId: pacienteId);
        await _contas.LancarContaAsync(
            TipoLancamento.Entrada, "Sessão de 12/07", 80m,
            vencimento: Hoje.AddDays(-15), pacienteId: pacienteId);

        var devedor = await _inadimplencia.DoPacienteAsync(pacienteId, Hoje);

        devedor.Should().NotBeNull();
        devedor!.Contas.Should().Be(2);
        devedor.Total.Should().Be(200m);
        devedor.Detalhe.Should().HaveCount(2);
        devedor.Detalhe.Should().Contain(c => c.Descricao == "Sessão de 05/07" && c.DiasEmAtraso == 40);
    }

    // ==================== O dono do lançamento manual ====================

    [Fact]
    public async Task Sessao_particular_sem_receita_aparece_para_o_balcao()
    {
        var pacienteId = await ParticularAsync();
        var atendimento = await RealizarAsync(pacienteId);

        var sessoes = await _repo.SessoesParticularesSemReceitaDoPacienteAsync(
            pacienteId, Hoje.AddDays(1));

        sessoes.Should().ContainSingle(s => s.AtendimentoId == atendimento.Id);
    }

    [Fact]
    public async Task Lancamento_manual_amarrado_a_sessao_a_tira_da_lista()
    {
        // O elo é a coluna: a sessão sai por passar a TER lançamento, nunca porque alguém
        // a marcou. Sem o campo de paciente/sessão na tela do caixa, a receita entrava
        // órfã e a linha ficava aberta para sempre.
        var pacienteId = await ParticularAsync();
        var atendimento = await RealizarAsync(pacienteId);

        await _financeiro.LancarAsync(
            data: Hoje,
            tipo: TipoLancamento.Entrada,
            descricao: "Sessão particular",
            valor: 150m,
            formaPagamento: FormaPagamento.Dinheiro,
            pacienteId: pacienteId,
            atendimentoId: atendimento.Id,
            operador: "recepcao");

        var sessoes = await _repo.SessoesParticularesSemReceitaDoPacienteAsync(
            pacienteId, Hoje.AddDays(1));

        sessoes.Should().NotContain(s => s.AtendimentoId == atendimento.Id);
    }

    [Fact]
    public async Task Lancamento_manual_SEM_sessao_nao_tira_nenhuma_da_lista()
    {
        // "(não é de uma sessão)" é a primeira opção do combo, e é o caso comum: venda de
        // produto, acerto, troco. Amarrar por padrão daria baixa numa sessão que ninguém
        // pagou — e ela sumiria da conciliação em silêncio.
        var pacienteId = await ParticularAsync();
        var atendimento = await RealizarAsync(pacienteId);

        await _financeiro.LancarAsync(
            data: Hoje,
            tipo: TipoLancamento.Entrada,
            descricao: "Venda de pomada",
            valor: 30m,
            formaPagamento: FormaPagamento.Dinheiro,
            pacienteId: pacienteId,
            operador: "recepcao");

        var sessoes = await _repo.SessoesParticularesSemReceitaDoPacienteAsync(
            pacienteId, Hoje.AddDays(1));

        sessoes.Should().ContainSingle(s => s.AtendimentoId == atendimento.Id);
    }

    [Fact]
    public async Task A_sessao_de_HOJE_nao_entra_na_lista_do_balcao()
    {
        // A de hoje está sendo fechada agora, pelo Finalizar: oferecê-la no combo do caixa
        // daria dois caminhos para o mesmo dinheiro, no mesmo minuto.
        var pacienteId = await ParticularAsync();
        await RealizarAsync(pacienteId);

        var sessoes = await _repo.SessoesParticularesSemReceitaDoPacienteAsync(pacienteId, Hoje);

        sessoes.Should().BeEmpty();
    }
}
