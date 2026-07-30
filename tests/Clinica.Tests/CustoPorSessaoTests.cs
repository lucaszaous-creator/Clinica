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
/// Custo de insumo por sessão, visto pelo PERÍODO (parcela 25).
///
/// `CustoDoAtendimentoAsync` existia desde a parcela 4 e nenhuma tela o lia. A parcela 6
/// ligou a baixa de insumo por sessão justamente para este número deixar de responder
/// zero — e ele continuou sem porta. Estes testes protegem a leitura que a tela usa:
///
/// 1. **Só entra saída COM atendimento.** A baixa digitada à mão não pertence a sessão
///    nenhuma, e rateá-la daria a cada sessão um custo que ela não teve.
/// 2. **Média sem base é NULA, nunca zero.** Período sem baixa por sessão não tem média
///    zero, tem média nenhuma — dizer "R$ 0,00" faria a sessão parecer de graça.
/// 3. **O preço sai do custo do movimento, e do médio do item quando ele falta** — a
///    mesma regra do custo de uma sessão só, porque é o mesmo número.
/// </summary>
public class CustoPorSessaoTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly EstoqueService _estoque;

    private static readonly DateOnly Inicio = new(2026, 8, 1);
    private static readonly DateOnly Fim = new(2026, 8, 31);

    public CustoPorSessaoTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _estoque = new EstoqueService(_repo);
    }

    private async Task<int> CriarPacienteAsync(string nome = "Maria")
    {
        var p = new Paciente { Nome = nome, Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    private async Task<int> CriarAtendimentoAsync(int pacienteId, DateOnly dia)
    {
        var atendimento = new Atendimento { PacienteId = pacienteId, Data = dia };
        _db.Atendimentos.Add(atendimento);
        await _db.SaveChangesAsync();
        return atendimento.Id;
    }

    private Task<ItemEstoque> CriarItemAsync(string nome = "Agulha 0,25")
        => _estoque.SalvarItemAsync(new ItemEstoque
        {
            Nome = nome,
            Unidade = "un",
            EstoqueMinimo = 0m,
            Ativo = true
        }, "gerente");

    [Fact]
    public async Task Custo_por_sessao_agrupa_por_atendimento_e_soma_os_itens()
    {
        var pacienteId = await CriarPacienteAsync();
        var primeira = await CriarAtendimentoAsync(pacienteId, Inicio.AddDays(3));
        var segunda = await CriarAtendimentoAsync(pacienteId, Inicio.AddDays(10));

        var agulha = await CriarItemAsync("Agulha");
        var moxa = await CriarItemAsync("Moxa");

        await _estoque.EntrarAsync(agulha.Id, 1000m, custoUnitario: 0.20m, data: Inicio);
        await _estoque.EntrarAsync(moxa.Id, 100m, custoUnitario: 3m, data: Inicio);

        await _estoque.BaixarAsync(agulha.Id, 12m, primeira, pacienteId, Inicio.AddDays(3));
        await _estoque.BaixarAsync(moxa.Id, 2m, primeira, pacienteId, Inicio.AddDays(3));
        await _estoque.BaixarAsync(agulha.Id, 10m, segunda, pacienteId, Inicio.AddDays(10));

        var sessoes = await _estoque.CustosDeSessaoAsync(Inicio, Fim);

        sessoes.Should().HaveCount(2);

        // A mais recente na frente — é a que a clínica acabou de fazer.
        sessoes[0].AtendimentoId.Should().Be(segunda);
        sessoes[0].Custo.Should().Be(2.00m);
        sessoes[1].AtendimentoId.Should().Be(primeira);
        sessoes[1].Custo.Should().Be(8.40m);
        sessoes[1].Itens.Should().HaveCount(2);

        // O mesmo número que a leitura de uma sessão só devolve: é o mesmo fato.
        var umaSo = await _estoque.CustoDoAtendimentoAsync(primeira);
        umaSo.Custo.Should().Be(sessoes[1].Custo);
    }

    [Fact]
    public async Task Baixa_sem_atendimento_nao_entra_no_custo_por_sessao()
    {
        var pacienteId = await CriarPacienteAsync();
        var atendimentoId = await CriarAtendimentoAsync(pacienteId, Inicio.AddDays(2));

        var agulha = await CriarItemAsync("Agulha");
        await _estoque.EntrarAsync(agulha.Id, 500m, custoUnitario: 0.20m, data: Inicio);

        await _estoque.BaixarAsync(agulha.Id, 10m, atendimentoId, pacienteId, Inicio.AddDays(2));
        // Saída digitada à mão na tela de movimento: não pertence a sessão nenhuma.
        await _estoque.BaixarAsync(agulha.Id, 50m, data: Inicio.AddDays(2));
        await _estoque.PerderAsync(agulha.Id, 5m, "caixa molhada", Inicio.AddDays(2));

        var sessoes = await _estoque.CustosDeSessaoAsync(Inicio, Fim);

        sessoes.Should().ContainSingle();
        sessoes.Single().Custo.Should().Be(2.00m);
    }

    [Fact]
    public async Task Periodo_sem_baixa_por_sessao_devolve_media_nula_e_nao_zero()
    {
        var agulha = await CriarItemAsync("Agulha");
        await _estoque.EntrarAsync(agulha.Id, 100m, custoUnitario: 0.20m, data: Inicio);

        var resumo = await _estoque.ResumoCustoSessoesAsync(Inicio, Fim);

        resumo.Sessoes.Should().Be(0);
        resumo.Total.Should().Be(0m);
        // 0% e "não deu para medir" são coisas diferentes — a tela mostra "—".
        resumo.MedioPorSessao.Should().BeNull();
        resumo.MaisCara.Should().BeNull();
    }

    [Fact]
    public async Task Resumo_traz_media_e_a_sessao_mais_cara()
    {
        var pacienteId = await CriarPacienteAsync();
        var barata = await CriarAtendimentoAsync(pacienteId, Inicio.AddDays(1));
        var cara = await CriarAtendimentoAsync(pacienteId, Inicio.AddDays(2));

        var agulha = await CriarItemAsync("Agulha");
        await _estoque.EntrarAsync(agulha.Id, 1000m, custoUnitario: 1m, data: Inicio);

        await _estoque.BaixarAsync(agulha.Id, 10m, barata, pacienteId, Inicio.AddDays(1));
        await _estoque.BaixarAsync(agulha.Id, 30m, cara, pacienteId, Inicio.AddDays(2));

        var resumo = await _estoque.ResumoCustoSessoesAsync(Inicio, Fim);

        resumo.Sessoes.Should().Be(2);
        resumo.Total.Should().Be(40m);
        resumo.MedioPorSessao.Should().Be(20m);
        resumo.MaisCara!.AtendimentoId.Should().Be(cara);
    }

    [Fact]
    public async Task Item_sem_preco_entra_com_zero_em_vez_de_sumir()
    {
        var pacienteId = await CriarPacienteAsync();
        var atendimentoId = await CriarAtendimentoAsync(pacienteId, Inicio.AddDays(4));

        // Item que entrou SEM custo cadastrado: não há médio para consultar.
        var semPreco = await CriarItemAsync("Algodão");
        await _estoque.EntrarAsync(semPreco.Id, 100m, data: Inicio);
        await _estoque.BaixarAsync(semPreco.Id, 3m, atendimentoId, pacienteId, Inicio.AddDays(4));

        var sessoes = await _estoque.CustosDeSessaoAsync(Inicio, Fim);

        // A falta de cadastro fica VISÍVEL: a sessão aparece, com o item listado e custo
        // zero. Sumir com ela baratearia o custo médio da clínica sem avisar ninguém.
        sessoes.Should().ContainSingle();
        sessoes.Single().Custo.Should().Be(0m);
        sessoes.Single().Itens.Should().ContainSingle();
    }

    [Fact]
    public async Task Fora_do_periodo_nao_entra()
    {
        var pacienteId = await CriarPacienteAsync();
        var dentro = await CriarAtendimentoAsync(pacienteId, Inicio.AddDays(5));
        var fora = await CriarAtendimentoAsync(pacienteId, Fim.AddDays(10));

        var agulha = await CriarItemAsync("Agulha");
        await _estoque.EntrarAsync(agulha.Id, 1000m, custoUnitario: 1m, data: Inicio);

        await _estoque.BaixarAsync(agulha.Id, 5m, dentro, pacienteId, Inicio.AddDays(5));
        await _estoque.BaixarAsync(agulha.Id, 5m, fora, pacienteId, Fim.AddDays(10));

        var sessoes = await _estoque.CustosDeSessaoAsync(Inicio, Fim);

        sessoes.Should().ContainSingle();
        sessoes.Single().AtendimentoId.Should().Be(dentro);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }
}
