using Clinica.Application.Tablet;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

public sealed partial class AtendimentoTabletTests
{
    [Fact]
    public async Task Modelo_enfermagem_salva_lista_edita_sem_alterar_evolucao_ou_sessao()
    {
        await PrepararBSV(); await Enfermeira();
        var pedido = new SalvarModeloEnfermagemTablet(Guid.NewGuid(), 0, null, "Admissão para infusão", "Texto reutilizável fictício");
        var primeiro = await Posto.SalvarModeloEnfermagemAsync(sessao, pedido, default);
        Assert.Equal(primeiro, await Posto.SalvarModeloEnfermagemAsync(sessao, pedido, default));
        Assert.Single(await db.ModelosEvolucaoEnfermagem.ToListAsync());
        Assert.Equal(primeiro, Assert.Single(await Posto.ModelosEnfermagemAsync(sessao, default)));
        var editado = await Posto.SalvarModeloEnfermagemAsync(sessao,
            new(Guid.NewGuid(), primeiro.Id, primeiro.Versao, "Admissão revisada", "Novo texto fictício"), default);
        Assert.Equal("Novo texto fictício", editado.Texto);
        Assert.NotEqual(primeiro.Versao, editado.Versao);
        Assert.Empty(await db.EvolucoesEnfermagem.ToListAsync());
        Assert.Empty(await db.Codigos.ToListAsync());
        Assert.Null(horario.FimAtendimentoEm);
        Assert.Equal(2, await db.Set<OperacaoClinicaTablet>().CountAsync());
    }

    [Fact]
    public async Task Modelo_enfermagem_recusa_nome_repetido_versao_antiga_e_permissao_revogada()
    {
        await PrepararBSV(); await Enfermeira();
        var original = await Posto.SalvarModeloEnfermagemAsync(sessao,
            new(Guid.NewGuid(), 0, null, "Admissão", "Texto fictício"), default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Posto.SalvarModeloEnfermagemAsync(sessao,
            new(Guid.NewGuid(), 0, null, "Admissao", "Outro texto"), default));
        // Cada chamada HTTP usa outro DbContext; o teste descarta entidades da transação revertida.
        db.ChangeTracker.Clear(); sessao = await db.SessoesTablet.SingleAsync(s => s.Id == sessao.Id);
        await Posto.SalvarModeloEnfermagemAsync(sessao,
            new(Guid.NewGuid(), original.Id, original.Versao, original.Nome, "Texto revisado"), default);
        await Assert.ThrowsAsync<ConflitoClinicoTablet>(() => Posto.SalvarModeloEnfermagemAsync(sessao,
            new(Guid.NewGuid(), original.Id, original.Versao, original.Nome, "Texto obsoleto"), default));
        db.ChangeTracker.Clear(); sessao = await db.SessoesTablet.SingleAsync(s => s.Id == sessao.Id);
        usuario = await db.Usuarios.SingleAsync(u => u.Id == usuario.Id);
        usuario.PermissoesNegadas = Permissao.RegistrarEvolucaoEnfermagem; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Posto.ModelosEnfermagemAsync(sessao, default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Posto.SalvarModeloEnfermagemAsync(sessao,
            new(Guid.NewGuid(), 0, null, "Bloqueado", "Texto"), default));
    }
}
