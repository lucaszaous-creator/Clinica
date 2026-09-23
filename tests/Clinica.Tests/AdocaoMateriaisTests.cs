using System.Text.Json;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace Clinica.Tests;

public sealed class AdocaoMateriaisTests : IDisposable
{
    private readonly SqliteConnection conexao = new("DataSource=:memory:");
    private readonly ClinicaDbContext db;
    private readonly ClinicaRepositorio repo;
    private readonly PoliticaMateriaisService politica;
    private readonly EstoqueService estoque;
    public AdocaoMateriaisTests()
    {
        conexao.Open(); db = new(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(conexao).Options);
        db.Database.EnsureCreated(); repo = new(db); politica = new(repo); estoque = new(repo);
    }
    public void Dispose() { db.Dispose(); conexao.Dispose(); }
    private async Task<UsuarioSistema> Gerente()
    {
        var u = new UsuarioSistema { Nome = "Gestão fictícia", Login = "gestao.materiais", Perfil = PerfilAcesso.Gerente, Ativo = true, DeveTrocarSenha = false };
        db.Add(u); await db.SaveChangesAsync(); return u;
    }
    private async Task<Agendamento> Concluido(DateTime quando)
    {
        var p = new Paciente { Nome = "Paciente fictício" };
        var a = new Agendamento { Paciente = p, DataHora = quando, Status = StatusAgendamento.Realizado,
            FimAtendimentoEm = quando.AddMinutes(30), Atendimento = new Atendimento { Paciente = p, Data = DateOnly.FromDateTime(quando) } };
        db.Add(a); await db.SaveChangesAsync(); return a;
    }
    [Fact]
    public async Task Padrao_desativado_e_so_gerencia_pode_ativar()
    {
        Assert.Equal(ModoMateriais.Desativado, (await politica.ObterAsync()).Modo);
        var u = await Gerente(); u.Perfil = PerfilAcesso.Recepcao; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => politica.SalvarAsync(ModoMateriais.Equipe, u.Id));
        Assert.Equal(ModoMateriais.Desativado, (await politica.ObterAsync()).Modo);
    }
    [Fact]
    public async Task Ativacao_nao_cria_pendencia_retroativa_e_lista_distingue_nao_informado()
    {
        var u = await Gerente();
        var ontem = await Concluido(PoliticaMateriaisService.Agora.AddDays(-1));
        await politica.SalvarAsync(ModoMateriais.Gestao, u.Id);
        var inicio = (await politica.ObterAsync()).AtivadaEm!.Value;
        var novo = await Concluido(inicio.AddSeconds(1));
        var dia = DateOnly.FromDateTime(inicio);
        var lista = await estoque.MateriaisDosAtendimentosAsync(dia.AddDays(-2), dia.AddDays(1), u.Id);
        Assert.Equal(novo.Id, Assert.Single(lista).AgendamentoId);
        Assert.Equal("Não informado", lista[0].Situacao);
        await Assert.ThrowsAsync<InvalidOperationException>(() => estoque.RegistrarMateriaisAsync(ontem.Id, u.Id, new([], true)));
        await estoque.RegistrarMateriaisAsync(novo.Id, u.Id, new([], true));
        lista = await estoque.MateriaisDosAtendimentosAsync(dia.AddDays(-2), dia.AddDays(1), u.Id);
        Assert.Equal("Sem consumo declarado", Assert.Single(lista).Situacao);
        var registro = Assert.Single(await repo.ConferenciasDosAtendimentosAsync([novo.AtendimentoId!.Value]));
        Assert.True(registro.SemConsumo);
        Assert.NotNull(registro.BaixadoEm);
        Assert.Empty(await repo.ConferenciasDosAtendimentosAsync([]));
        Assert.Equal(novo.AtendimentoId, (await repo.ObterAgendamentoAsync(novo.Id))!.AtendimentoId);
    }
    [Fact]
    public async Task Reativacao_nao_cobra_intervalo_desativado_e_preserva_registro_antigo()
    {
        var u = await Gerente();
        var antigo = await Concluido(PoliticaMateriaisService.Agora.AddDays(-2));
        await repo.SalvarConfiguracaoAsync(PoliticaMateriaisService.Chave,
            JsonSerializer.Serialize(new PoliticaMateriais(ModoMateriais.Gestao, antigo.DataHora.AddMinutes(-1))));
        await repo.SalvarAsync();
        await estoque.RegistrarMateriaisAsync(antigo.Id, u.Id, new([], true));
        await politica.SalvarAsync(ModoMateriais.Desativado, u.Id);
        var intervalo = await Concluido(PoliticaMateriaisService.Agora.AddDays(-1));
        await politica.SalvarAsync(ModoMateriais.Equipe, u.Id);
        var dia = DateOnly.FromDateTime(PoliticaMateriaisService.Agora);
        var lista = await estoque.MateriaisDosAtendimentosAsync(dia.AddDays(-3), dia, u.Id);
        Assert.Equal(antigo.Id, Assert.Single(lista).AgendamentoId);
        Assert.DoesNotContain(lista, a => a.AgendamentoId == intervalo.Id);
    }
    [Fact]
    public async Task Gestao_relê_permissao_e_registro_pendente_sobrevive_a_novo_contexto()
    {
        var u = await Gerente(); await politica.SalvarAsync(ModoMateriais.Gestao, u.Id);
        var a = await Concluido(PoliticaMateriaisService.Agora.AddSeconds(1));
        var material = await estoque.SalvarItemAsync(new ItemEstoque { Nome = "Material sem saldo" });
        await estoque.RegistrarMateriaisAsync(a.Id, u.Id, new([new(material.Id, 1.25m)]));
        var dia = DateOnly.FromDateTime(a.DataHora);
        using (var outro = new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(conexao).Options))
        {
            var r = new ClinicaRepositorio(outro);
            var lista = await new EstoqueService(r).MateriaisDosAtendimentosAsync(dia, dia, u.Id);
            Assert.Equal("Baixa pendente", Assert.Single(lista).Situacao);
            Assert.Equal(1.25m, Assert.Single(EstoqueService.MateriaisRegistrados((await r.ConferenciaConsumoAsync(a.AtendimentoId!.Value))!)).Quantidade);
        }
        u.PermissoesNegadas = Permissao.EditarFinanceiro; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => estoque.RegistrarMateriaisAsync(a.Id, u.Id, new([new(material.Id, 1.25m)])));
    }
}
