using Clinica.Application.Tablet;
using Clinica.Domain.Entities;
using Clinica.Infrastructure.Tablet;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

public sealed partial class AtendimentoTabletTests
{
    [Fact]
    public async Task Modelo_de_evolucao_e_gerido_sem_paciente_e_copia_nao_muda_registro()
    {
        await Preparar();
        var pedido = new SalvarModeloEvolucaoTablet(Guid.NewGuid(), 0, null, "Roteiro de retorno",
            false, true, TextoEvolucao: "Texto reutilizável");
        var criado = await Posto.SalvarModeloEvolucaoAsync(sessao, pedido, default);
        Assert.Equal(criado, await Posto.SalvarModeloEvolucaoAsync(sessao, pedido, default));
        db.ChangeTracker.Clear(); // Cada chamada HTTP usa outro contexto e relê o timestamp persistido.
        Assert.False(criado.Compartilhado);
        Assert.Equal(usuario.ProfissionalId, (await db.ModelosEvolucao.SingleAsync()).ProfissionalId);
        var listado = Assert.Single(await Posto.ModelosEvolucaoAsync(sessao, default));
        Assert.Equal(criado.Id, listado.Id);
        Assert.Equal(criado.Versao, listado.Versao);
        var editado = await Posto.SalvarModeloEvolucaoAsync(sessao,
            pedido with { Id = criado.Id, Versao = criado.Versao, Idempotencia = Guid.NewGuid(), TextoEvolucao = "Texto alterado" }, default);
        Assert.NotEqual(criado.Versao, editado.Versao);
        db.ChangeTracker.Clear();
        Assert.Equal(editado.Versao, Assert.Single(await Posto.ModelosEvolucaoAsync(sessao, default)).Versao);
        await Posto.SalvarModeloEvolucaoAsync(sessao,
            pedido with { Id = editado.Id, Versao = editado.Versao, Idempotencia = Guid.NewGuid(), Ativo = false }, default);
        Assert.Empty(await Posto.ModelosEvolucaoAsync(sessao, default));
        Assert.True(await db.ModelosEvolucao.AnyAsync(m => m.Id == criado.Id && !m.Ativo));
        await Assert.ThrowsAsync<ConflitoClinicoTablet>(() => Posto.SalvarModeloEvolucaoAsync(sessao,
            pedido with { Id = criado.Id, Idempotencia = Guid.NewGuid(), TextoEvolucao = "Texto antigo" }, default));
    }

    [Fact]
    public async Task Modelo_de_documento_global_grava_sem_paciente_e_rejeita_versao_antiga()
    {
        await Preparar();
        var pedido = new NovoModeloDocumentoTablet(Guid.NewGuid(), "Receita reutilizável",
            TipoDocumentoClinico.Receita, "Texto de teste");
        var criado = await Posto.SalvarModeloDocumentoGlobalAsync(sessao, pedido, default);
        Assert.Equal(criado, await Posto.SalvarModeloDocumentoGlobalAsync(sessao, pedido, default));
        Assert.Null(Assert.Single(await db.Set<OperacaoClinicaTablet>().ToListAsync()).PacienteId);
        var listado = Assert.Single(Json(await Posto.ModelosDocumentoAsync(sessao, default)).EnumerateArray());
        Assert.Equal(criado.Id, listado.GetProperty("id").GetInt32());
        await Assert.ThrowsAsync<ConflitoClinicoTablet>(() => Posto.SalvarModeloDocumentoGlobalAsync(sessao,
            pedido with { Id = criado.Id, Idempotencia = Guid.NewGuid(), Texto = "Outro texto" }, default));
    }
}
