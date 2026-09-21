using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

public sealed partial class AtendimentoTabletTests
{
    [Fact]
    public async Task Lista_enfermagem_inclui_BSV_concluido_pendente_e_registrado_hoje_com_filtros()
    {
        await PrepararBSV();
        horario.DataHora = svc.Hoje.AddDays(-4).ToDateTime(new(8,0));
        horario.FimAtendimentoEm = horario.DataHora.AddHours(1);
        horario.Status = StatusAgendamento.Realizado;
        outro.DataHora = svc.Hoje.ToDateTime(new(9,0));
        outro.ModalidadePrevista = ModalidadeAtendimento.BsvComAcupuntura;
        await db.SaveChangesAsync();
        await Enfermagem(outro.Id);
        await Enfermeira();
        var lista = new SessoesEnfermagemService(db);
        var ambos = await lista.ListarAsync(usuario.Id, new(), svc.Hoje);
        Assert.Equal(2, ambos.Itens.Count);
        Assert.Contains(ambos.Itens, a => a.Id == horario.Id && a.Concluida && !a.Registrada);
        Assert.Contains(ambos.Itens, a => a.Id == outro.Id && a.Registrada);
        var pendentes = await lista.ListarAsync(usuario.Id, new(Situacao: "Pendentes"), svc.Hoje);
        Assert.Equal(horario.Id, Assert.Single(pendentes.Itens).Id);
        var dia = await lista.ListarAsync(usuario.Id, new(Inicio: svc.Hoje, Fim: svc.Hoje, Situacao: "Todas"), svc.Hoje);
        Assert.Equal(outro.Id, Assert.Single(dia.Itens).Id);
        Assert.Empty((await lista.ListarAsync(usuario.Id, new(Paciente: "nome inexistente"), svc.Hoje)).Itens);
        Assert.Empty((await lista.ListarAsync(usuario.Id, new(Medico: "medico inexistente"), svc.Hoje)).Itens);
        await Enfermagem(horario.Id);
        Assert.DoesNotContain((await lista.ListarAsync(usuario.Id, new(), svc.Hoje)).Itens, a => a.Id == horario.Id);
    }

    [Fact]
    public async Task Lista_pagina_sem_duplicar_e_respeita_revogacao_no_tablet()
    {
        await PrepararBSV(); await Enfermeira();
        for (var i = 0; i < 51; i++) db.Agendamentos.Add(new Agendamento {
            PacienteId = horario.PacienteId, ProfissionalId = horario.ProfissionalId,
            DataHora = svc.Hoje.ToDateTime(new(8,0)), ModalidadePrevista = ModalidadeAtendimento.BsvApenas });
        await db.SaveChangesAsync();
        var primeira = await Posto.SessoesEnfermagemAsync(sessao, new(), default);
        var segunda = await Posto.SessoesEnfermagemAsync(sessao, new(Pagina: 1), default);
        Assert.Equal(50, primeira.Itens.Count); Assert.True(primeira.Mais);
        Assert.Equal(2, segunda.Itens.Count); Assert.False(segunda.Mais);
        Assert.Empty(primeira.Itens.Select(a => a.Id).Intersect(segunda.Itens.Select(a => a.Id)));
        usuario.PermissoesNegadas = Permissao.RegistrarEvolucaoEnfermagem;
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Posto.SessoesEnfermagemAsync(sessao, new(), default));
    }
    [Fact]
    public async Task Lista_enfermagem_exclui_outras_modalidades_cancelados_e_restringe_perfil()
    {
        await PrepararBSV();
        var lista = new SessoesEnfermagemService(db);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => lista.ListarAsync(usuario.Id, new(), svc.Hoje));
        usuario.Perfil = PerfilAcesso.Gerente; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => lista.ListarAsync(usuario.Id, new(), svc.Hoje));
        await Enfermeira();
        horario.Status = StatusAgendamento.Cancelado;
        outro.ModalidadePrevista = ModalidadeAtendimento.Consulta;
        await db.SaveChangesAsync();
        Assert.Empty((await lista.ListarAsync(usuario.Id, new(Situacao: "Todas"), svc.Hoje)).Itens);
        usuario.Ativo = false; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => lista.ListarAsync(usuario.Id, new(), svc.Hoje));
    }
}
