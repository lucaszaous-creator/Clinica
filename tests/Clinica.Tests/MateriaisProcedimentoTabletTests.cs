using System.Text.Json;
using Clinica.Application.Servicos;
using Clinica.Application.Tablet;
using Clinica.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace Clinica.Tests;

public sealed partial class AtendimentoTabletTests
{
    private async Task HabilitarMateriais(ModoMateriais modo = ModoMateriais.Equipe)
    {
        await repo.SalvarConfiguracaoAsync(PoliticaMateriaisService.Chave,
            JsonSerializer.Serialize(new PoliticaMateriais(modo, horario.DataHora.AddMinutes(-1))));
        await repo.SalvarAsync();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Conclusao_independe_de_estoque_e_declaracao(bool habilitado)
    {
        await Preparar();
        if (habilitado) await HabilitarMateriais();
        var estoque = new EstoqueService(repo);
        await estoque.SalvarItemAsync(new ItemEstoque { Nome = "Material sem saldo" });
        var pedido = (await Pedido()) with { ConcluirAoSalvar = true, Consumo = null };
        var salvo = await svc.SalvarAsync(sessao, horario.Id, pedido, default);
        var repetido = await svc.SalvarAsync(sessao, horario.Id, pedido, default);
        Assert.True(salvo.Finalizado);
        Assert.True(salvo.Guias > 0);
        Assert.Equal(salvo.AtendimentoId, repetido.AtendimentoId);
        Assert.Single(await db.Evolucoes.ToListAsync());
        Assert.Null(await estoque.ConferenciaDoProcedimentoAsync(salvo.AtendimentoId!.Value));
    }

    [Fact]
    public async Task Consumo_do_portal_antigo_nao_bloqueia_nem_vira_baixa_silenciosa()
    {
        await Preparar();
        var p = (await Pedido()) with { ConcluirAoSalvar = true, Consumo = new([new(999, 1)]) };
        var salvo = await svc.SalvarAsync(sessao, horario.Id, p, default);
        Assert.True(salvo.Finalizado);
        Assert.Contains(salvo.Avisos, a => a.Contains("não foram registrados"));
        Assert.Empty(await db.Set<ConferenciaConsumoProcedimento>().ToListAsync());
    }

    [Theory]
    [InlineData(ModoMateriais.Desativado)] [InlineData(ModoMateriais.Gestao)]
    public async Task Portal_nao_expoe_estoque_antes_da_liberacao_para_equipe(ModoMateriais modo)
    {
        await Preparar(); await HabilitarMateriais(modo);
        var salvo = await svc.SalvarAsync(sessao, horario.Id, (await Pedido()) with { ConcluirAoSalvar = true }, default);
        await Assert.ThrowsAsync<RecursoClinicoIndisponivel>(() => svc.MateriaisAsync(sessao, horario.Id, default));
        await Assert.ThrowsAsync<RecursoClinicoIndisponivel>(() => svc.RegistrarMateriaisAsync(sessao, horario.Id,
            new(Guid.NewGuid(), new([], true)), default));
        Assert.Null(await repo.ConferenciaConsumoAsync(salvo.AtendimentoId!.Value));
    }

    [Fact]
    public async Task Registro_posterior_pendente_recarrega_e_reprocessa_sem_reabrir_nem_duplicar_guias()
    {
        await Preparar(); await HabilitarMateriais();
        var estoque = new EstoqueService(repo);
        var material = await estoque.SalvarItemAsync(new ItemEstoque { Nome = "Material clínico" });
        var rotina = await estoque.SalvarItemAsync(new ItemEstoque { Nome = "Limpeza", Uso = UsoEstoque.Rotina });
        var salvo = await svc.SalvarAsync(sessao, horario.Id, (await Pedido()) with { ConcluirAoSalvar = true }, default);
        var antes = (await repo.ObterAtendimentoAsync(salvo.AtendimentoId!.Value))!.Codigos.Select(c => c.Id).ToArray();
        var catalogo = await svc.MateriaisAsync(sessao, horario.Id, default);
        Assert.Contains(catalogo.Itens, i => i.ItemId == material.Id);
        Assert.DoesNotContain(catalogo.Itens, i => i.ItemId == rotina.Id);
        Assert.False(catalogo.Registrado); Assert.False(catalogo.SemConsumo);
        var pedido = new RegistrarMateriaisTablet(Guid.NewGuid(), new([new(material.Id, 2)]));
        var resposta = await svc.RegistrarMateriaisAsync(sessao, horario.Id, pedido, default);
        Assert.False(resposta.Baixado);
        Assert.Equal(resposta, await svc.RegistrarMateriaisAsync(sessao, horario.Id, pedido, default));
        var pendente = await svc.MateriaisAsync(sessao, horario.Id, default);
        Assert.True(pendente.Registrado); Assert.False(pendente.Conferido);
        Assert.Equal(2m, pendente.Itens.Single(i => i.ItemId == material.Id).QuantidadeUtilizada);
        Assert.NotNull((await db.Agendamentos.SingleAsync(a => a.Id == horario.Id)).FimAtendimentoEm);
        await estoque.EntrarAsync(material.Id, 10, data: DateOnly.FromDateTime(PoliticaMateriaisService.Agora));
        pedido = pedido with { Idempotencia = Guid.NewGuid() };
        Assert.True((await svc.RegistrarMateriaisAsync(sessao, horario.Id, pedido, default)).Baixado);
        Assert.True((await svc.RegistrarMateriaisAsync(sessao, horario.Id, pedido with { Idempotencia = Guid.NewGuid() }, default)).Baixado);
        Assert.Equal(8, (await estoque.SaldosAsync()).Single(i => i.ItemId == material.Id).Saldo);
        Assert.Equal(antes, (await repo.ObterAtendimentoAsync(salvo.AtendimentoId.Value))!.Codigos.Select(c => c.Id).ToArray());
        Assert.Single(await db.Evolucoes.ToListAsync());
        Assert.Single(await db.Set<ConferenciaConsumoProcedimento>().ToListAsync());
    }

    [Fact]
    public async Task Materiais_recusam_outro_profissional_e_permissao_revogada()
    {
        await Preparar(); await HabilitarMateriais();
        await svc.SalvarAsync(sessao, horario.Id, (await Pedido()) with { ConcluirAoSalvar = true }, default);
        await Assert.ThrowsAsync<RecursoClinicoIndisponivel>(() => svc.MateriaisAsync(sessao, outro.Id, default));
        usuario.PermissoesNegadas = Permissao.EditarProntuario; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.RegistrarMateriaisAsync(sessao, horario.Id,
            new(Guid.NewGuid(), new([], true)), default));
    }
}
