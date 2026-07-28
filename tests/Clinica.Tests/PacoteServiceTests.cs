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
/// Pacotes, planos e vouchers (parcela 4).
///
/// A regra que carrega a suíte: consumo é FATO datado. Desfazer é cancelar com motivo,
/// nunca apagar — sem isso, "o paciente diz que sobrou uma sessão" viraria a palavra de
/// um contra a do outro.
///
/// E a situação do pacote é CALCULADA, não guardada: um pacote gravado como "Ativo"
/// viraria mentira à meia-noite do vencimento, e ninguém roda tarefa noturna aqui.
/// </summary>
public class PacoteServiceTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly PacoteService _pacotes;

    private static readonly DateOnly Hoje = new(2026, 8, 15);

    public PacoteServiceTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _pacotes = new PacoteService(_repo);
    }

    private async Task<int> CriarPacienteAsync(string nome = "Maria")
    {
        var p = new Paciente { Nome = nome, Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    private async Task<int> CriarAtendimentoAsync(int pacienteId, DateOnly data)
    {
        var a = new Atendimento { PacienteId = pacienteId, Data = data };
        _db.Atendimentos.Add(a);
        await _db.SaveChangesAsync();
        return a.Id;
    }

    private Task<PacoteCatalogo> CriarNoCatalogoAsync(
        string nome = "Pacote 10 sessões", int? sessoes = 10, decimal valor = 1000m, int? validadeDias = 90)
        => _pacotes.SalvarCatalogoAsync(new PacoteCatalogo
        {
            Nome = nome,
            Tipo = sessoes is null ? TipoPacote.Plano : TipoPacote.Sessoes,
            SessoesIncluidas = sessoes,
            Valor = valor,
            ValidadeDias = validadeDias,
            Ativo = true
        }, "secretaria");

    // ==================== Venda ====================

    [Fact]
    public async Task Venda_copia_os_dados_do_catalogo_e_calcula_a_validade()
    {
        var pacienteId = await CriarPacienteAsync();
        var catalogo = await CriarNoCatalogoAsync();

        var vendido = await _pacotes.VenderAsync(pacienteId, catalogo.Id, Hoje, operador: "secretaria");

        vendido.Nome.Should().Be("Pacote 10 sessões");
        vendido.SessoesContratadas.Should().Be(10);
        vendido.Valor.Should().Be(1000m);
        vendido.ValidoAte.Should().Be(Hoje.AddDays(90));
        vendido.PacoteCatalogoId.Should().Be(catalogo.Id);
    }

    [Fact]
    public async Task Mudar_o_preco_de_tabela_nao_reescreve_o_que_ja_foi_vendido()
    {
        var pacienteId = await CriarPacienteAsync();
        var catalogo = await CriarNoCatalogoAsync(valor: 1000m);
        var vendido = await _pacotes.VenderAsync(pacienteId, catalogo.Id, Hoje);

        catalogo.Valor = 1500m;
        await _pacotes.SalvarCatalogoAsync(catalogo);

        var recarregado = await _pacotes.ObterAsync(vendido.Id);
        recarregado!.Valor.Should().Be(1000m);
    }

    [Fact]
    public async Task Pacote_de_sessoes_sem_numero_de_sessoes_e_recusado()
    {
        var pacienteId = await CriarPacienteAsync();

        var vender = () => _pacotes.RegistrarVendaAsync(new PacotePaciente
        {
            PacienteId = pacienteId,
            Nome = "Combinado na hora",
            Tipo = TipoPacote.Sessoes,
            Valor = 500m,
            DataCompra = Hoje
        });

        await vender.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*sem número de sessões*");
    }

    // ==================== Saldo e consumo ====================

    [Fact]
    public async Task Consumir_debita_do_saldo_e_o_cancelamento_devolve()
    {
        var pacienteId = await CriarPacienteAsync();
        var catalogo = await CriarNoCatalogoAsync(sessoes: 3);
        var vendido = await _pacotes.VenderAsync(pacienteId, catalogo.Id, Hoje);

        var consumo = await _pacotes.ConsumirAsync(vendido.Id, Hoje, operador: "secretaria");

        var saldo = (await _pacotes.DoPacienteAsync(pacienteId, Hoje)).Single();
        saldo.SessoesUsadas.Should().Be(1);
        saldo.SaldoSessoes.Should().Be(2);

        await _pacotes.CancelarConsumoAsync(consumo.Id, "sessão não aconteceu", "secretaria");

        var depois = (await _pacotes.DoPacienteAsync(pacienteId, Hoje)).Single();
        depois.SaldoSessoes.Should().Be(3);
        // O consumo continua no histórico, cancelado — não some.
        _db.ConsumosPacote.Count(c => c.PacotePacienteId == vendido.Id).Should().Be(1);
    }

    [Fact]
    public async Task Pacote_esgotado_recusa_nova_sessao()
    {
        var pacienteId = await CriarPacienteAsync();
        var catalogo = await CriarNoCatalogoAsync(sessoes: 1);
        var vendido = await _pacotes.VenderAsync(pacienteId, catalogo.Id, Hoje);

        await _pacotes.ConsumirAsync(vendido.Id, Hoje);

        var consumir = () => _pacotes.ConsumirAsync(vendido.Id, Hoje);
        await consumir.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*não tem mais sessões*");

        (await _pacotes.DoPacienteAsync(pacienteId, Hoje)).Single()
            .Situacao.Should().Be(StatusPacote.Esgotado);
    }

    [Fact]
    public async Task Pacote_vencido_recusa_sessao_mesmo_com_saldo()
    {
        var pacienteId = await CriarPacienteAsync();
        var catalogo = await CriarNoCatalogoAsync(sessoes: 10, validadeDias: 30);
        var vendido = await _pacotes.VenderAsync(pacienteId, catalogo.Id, Hoje);

        var depoisDoPrazo = Hoje.AddDays(31);

        var consumir = () => _pacotes.ConsumirAsync(vendido.Id, depoisDoPrazo);
        await consumir.Should().ThrowAsync<InvalidOperationException>().WithMessage("*venceu*");

        // A situação muda sozinha com o calendário, sem ninguém gravar nada.
        (await _pacotes.DoPacienteAsync(pacienteId, Hoje)).Single()
            .Situacao.Should().Be(StatusPacote.Ativo);
        (await _pacotes.DoPacienteAsync(pacienteId, depoisDoPrazo)).Single()
            .Situacao.Should().Be(StatusPacote.Expirado);
    }

    [Fact]
    public async Task Pacote_cancelado_sai_do_uso_e_guarda_o_motivo()
    {
        var pacienteId = await CriarPacienteAsync();
        var catalogo = await CriarNoCatalogoAsync();
        var vendido = await _pacotes.VenderAsync(pacienteId, catalogo.Id, Hoje);

        await _pacotes.CancelarAsync(vendido.Id, "desistiu do tratamento", "secretaria");

        var saldo = (await _pacotes.DoPacienteAsync(pacienteId, Hoje)).Single();
        saldo.Situacao.Should().Be(StatusPacote.Cancelado);

        var consumir = () => _pacotes.ConsumirAsync(vendido.Id, Hoje);
        await consumir.Should().ThrowAsync<InvalidOperationException>().WithMessage("*cancelado*");

        (await _pacotes.ObterAsync(vendido.Id))!.MotivoCancelamento.Should().Be("desistiu do tratamento");
    }

    // ==================== Baixa automática ====================

    [Fact]
    public async Task Baixa_automatica_debita_o_pacote_que_vence_primeiro()
    {
        var pacienteId = await CriarPacienteAsync();
        var catalogo = await CriarNoCatalogoAsync();

        var vencePrimeiro = await _pacotes.VenderAsync(pacienteId, catalogo.Id, Hoje.AddDays(-60));
        var venceDepois = await _pacotes.VenderAsync(pacienteId, catalogo.Id, Hoje);

        var atendimentoId = await CriarAtendimentoAsync(pacienteId, Hoje);
        var consumo = await _pacotes.ConsumirPorAtendimentoAsync(
            pacienteId, atendimentoId, Hoje, operador: "secretaria");

        consumo.Should().NotBeNull();
        consumo!.PacotePacienteId.Should().Be(vencePrimeiro.Id);

        var saldos = await _pacotes.DoPacienteAsync(pacienteId, Hoje);
        saldos.Single(s => s.PacoteId == vencePrimeiro.Id).SaldoSessoes.Should().Be(9);
        saldos.Single(s => s.PacoteId == venceDepois.Id).SaldoSessoes.Should().Be(10);
    }

    [Fact]
    public async Task Baixa_automatica_nao_debita_duas_vezes_o_mesmo_atendimento()
    {
        var pacienteId = await CriarPacienteAsync();
        var catalogo = await CriarNoCatalogoAsync();
        var vendido = await _pacotes.VenderAsync(pacienteId, catalogo.Id, Hoje);
        var atendimentoId = await CriarAtendimentoAsync(pacienteId, Hoje);

        (await _pacotes.ConsumirPorAtendimentoAsync(pacienteId, atendimentoId, Hoje)).Should().NotBeNull();
        // Concluir de novo (ou reabrir) a mesma sessão não pode comer outra do paciente.
        (await _pacotes.ConsumirPorAtendimentoAsync(pacienteId, atendimentoId, Hoje)).Should().BeNull();

        (await _pacotes.DoPacienteAsync(pacienteId, Hoje)).Single(s => s.PacoteId == vendido.Id)
            .SaldoSessoes.Should().Be(9);
    }

    [Fact]
    public async Task Sem_pacote_utilizavel_a_baixa_automatica_nao_e_erro()
    {
        var pacienteId = await CriarPacienteAsync();
        var atendimentoId = await CriarAtendimentoAsync(pacienteId, Hoje);

        // Atender sem pacote é o caso NORMAL (particular avulso, convênio) — devolver
        // null é a resposta certa, não uma exceção para a Recepção tratar.
        (await _pacotes.ConsumirPorAtendimentoAsync(pacienteId, atendimentoId, Hoje)).Should().BeNull();
    }

    [Fact]
    public async Task Plano_sem_numero_de_sessoes_e_ilimitado_dentro_da_validade()
    {
        var pacienteId = await CriarPacienteAsync();
        var catalogo = await CriarNoCatalogoAsync("Plano mensal", sessoes: null, valor: 400m, validadeDias: 30);
        var vendido = await _pacotes.VenderAsync(pacienteId, catalogo.Id, Hoje);

        for (var i = 0; i < 12; i++)
            await _pacotes.ConsumirAsync(vendido.Id, Hoje);

        var saldo = (await _pacotes.DoPacienteAsync(pacienteId, Hoje)).Single();
        saldo.SaldoSessoes.Should().BeNull();
        saldo.SessoesUsadas.Should().Be(12);
        saldo.Situacao.Should().Be(StatusPacote.Ativo);
        saldo.SaldoRotulo.Should().Contain("livre até");
    }

    [Fact]
    public async Task Venda_deixa_rastro_na_auditoria()
    {
        var pacienteId = await CriarPacienteAsync();
        var catalogo = await CriarNoCatalogoAsync();
        await _pacotes.VenderAsync(pacienteId, catalogo.Id, Hoje, operador: "secretaria");

        var eventos = await _repo.EventosAuditoriaAsync();
        eventos.Should().Contain(e => e.Acao == "PacoteVendido" && e.PacienteId == pacienteId);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }
}
