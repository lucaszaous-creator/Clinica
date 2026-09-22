using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace Clinica.Tests;

public sealed partial class AtendimentoTabletTests
{
    [Fact]
    public async Task Tablet_exige_conferencia_antes_de_concluir_e_preserva_registro_em_caso_de_falta()
    {
        await Preparar();
        var estoque = new EstoqueService(repo);
        var material = await estoque.SalvarItemAsync(new ItemEstoque { Nome = "Material clínico" });
        var rotina = await estoque.SalvarItemAsync(new ItemEstoque { Nome = "Limpeza", Uso = UsoEstoque.Rotina });
        var catalogo = await svc.MateriaisAsync(sessao, horario.Id, default);
        Assert.Contains(catalogo.Itens, i => i.ItemId == material.Id);
        Assert.DoesNotContain(catalogo.Itens, i => i.ItemId == rotina.Id);
        var pedido = (await Pedido()) with { ConcluirAoSalvar = true, Consumo = null };
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.SalvarAsync(sessao, horario.Id, pedido, default));
        db.ChangeTracker.Clear();
        sessao = await db.SessoesTablet.SingleAsync(s => s.Id == sessao.Id);
        Assert.Null((await db.Agendamentos.SingleAsync(a => a.Id == horario.Id)).FimAtendimentoEm);
        pedido = pedido with { Consumo = new([new(material.Id, 1)]) };
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.SalvarAsync(sessao, horario.Id, pedido, default));
        db.ChangeTracker.Clear();
        sessao = await db.SessoesTablet.SingleAsync(s => s.Id == sessao.Id);
        Assert.Empty(await db.Evolucoes.ToListAsync());
        pedido = pedido with { Consumo = new([], true) };
        var salvo = await svc.SalvarAsync(sessao, horario.Id, pedido, default);
        Assert.True(salvo.Finalizado);
        Assert.True((await estoque.ConferenciaDoProcedimentoAsync(salvo.AtendimentoId!.Value))!.SemConsumo);
        var repetido = await svc.SalvarAsync(sessao, horario.Id, pedido, default);
        Assert.Equal(salvo.EvolucaoId, repetido.EvolucaoId);
        Assert.Equal(salvo.AtendimentoId, repetido.AtendimentoId);
        Assert.Equal(salvo.Versao, repetido.Versao);
    }
}
