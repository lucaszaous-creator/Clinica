using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Application.Tablet;
using Clinica.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

public sealed partial class AtendimentoTabletTests
{
    [Fact]
    public async Task Filtros_da_agenda_sao_validados_no_servidor()
    {
        await PrepararBSV(); await Enfermeira();
        var lista = new SessoesEnfermagemService(db);
        foreach (var filtro in new[] {
            new FiltroSessoesEnfermagem(Paciente: new string('P', 121)),
            new FiltroSessoesEnfermagem(Medico: new string('M', 121)),
            new FiltroSessoesEnfermagem(Fim: DateOnly.MaxValue),
            new FiltroSessoesEnfermagem(Situacao: new string('S', 21)) })
        {
            var erro = await Assert.ThrowsAsync<InvalidOperationException>(() => lista.ListarAsync(usuario.Id, filtro, svc.Hoje));
            Assert.True(ErroFormularioTablet.EhPublico(erro));
        }
        Assert.Empty((await lista.ListarAsync(usuario.Id,
            new(Paciente: new string('P', 120)), svc.Hoje)).Itens);
    }

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
        var hoje = await lista.ListarAsync(usuario.Id, new(), svc.Hoje);
        Assert.Equal(outro.Id, Assert.Single(hoje.Itens).Id);
        var ambos = await lista.ListarAsync(usuario.Id, new(Situacao: "DiaEPendentes"), svc.Hoje);
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
        Assert.DoesNotContain((await lista.ListarAsync(usuario.Id, new(Situacao: "DiaEPendentes"), svc.Hoje)).Itens, a => a.Id == horario.Id);
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
    public async Task Chegada_nao_esconde_pos_aplicacao_pendente_da_agenda_compartilhada()
    {
        await PrepararBSV(); await Enfermeira();
        horario.DataHora = svc.Hoje.AddDays(-1).ToDateTime(new(9, 0));
        horario.Status = StatusAgendamento.Realizado;
        await db.SaveChangesAsync();
        var lista = new SessoesEnfermagemService(db);
        Assert.Empty((await lista.ListarAsync(usuario.Id, new(), svc.Hoje)).Itens);
        Assert.Single((await lista.ListarAsync(usuario.Id,
            new(Situacao: "Todas", Fim: svc.Hoje.AddDays(-1)), svc.Hoje)).Itens);
        var chegada = await Enfermagem(horario.Id);
        chegada.FaseAtendimento = "Chegada";
        await db.SaveChangesAsync();
        // Complementos sem fase não são evoluções antigas nem concluem a saída.
        await Enfermagem(horario.Id);
        var pendente = Assert.Single((await lista.ListarAsync(usuario.Id,
            new(Situacao: "Pendentes"), svc.Hoje)).Itens);
        Assert.True(pendente.ChegadaRegistrada);
        Assert.False(pendente.AposAplicacaoRegistrada);
        Assert.False(pendente.Registrada);
        Assert.Contains("após aplicação pendente", pendente.Situacao);
        db.EvolucoesEnfermagem.Add(new EvolucaoEnfermagem {
            PacienteId = horario.PacienteId, AgendamentoId = horario.Id, Data = svc.Hoje,
            Hora = new(10, 0), Texto = "Após aplicação fictício", FaseAtendimento = "AposAplicacao",
            AutorNome = "Outra enfermeira", AutorConselho = "COREN teste"
        });
        await db.SaveChangesAsync();
        Assert.Empty((await lista.ListarAsync(usuario.Id, new(), svc.Hoje)).Itens);
        var completo = Assert.Single((await lista.ListarAsync(usuario.Id, new(Situacao: "Registradas"), svc.Hoje)).Itens);
        Assert.True(completo.Registrada);
    }
    [Fact]
    public async Task Duas_enfermeiras_veem_a_mesma_fila_sem_agendamento_proprio()
    {
        await PrepararBSV(); await Enfermeira();
        horario.DataHora = svc.Hoje.ToDateTime(new(9, 0));
        outro.DataHora = svc.Hoje.ToDateTime(new(10, 0));
        outro.ModalidadePrevista = ModalidadeAtendimento.BsvComAcupuntura;
        var segunda = await new Clinica.Application.Servicos.AcessoService(repo)
            .CriarAsync("Enfermeira B", "enfermeira.b", "TabletTeste#2026", PerfilAcesso.Enfermagem);
        await db.SaveChangesAsync();

        var fila = new SessoesEnfermagemService(db);
        var primeiraIds = (await fila.ListarAsync(usuario.Id, new(), svc.Hoje)).Itens.Select(x => x.Id).ToArray();
        var segundaIds = (await fila.ListarAsync(segunda.Id, new(), svc.Hoje)).Itens.Select(x => x.Id).ToArray();
        Assert.Equal(new[] { horario.Id, outro.Id }.Order(), primeiraIds.Order());
        Assert.Equal(primeiraIds.Order(), segundaIds.Order());
        Assert.Null(segunda.ProfissionalId);

        var chegada = await Enfermagem(horario.Id);
        chegada.FaseAtendimento = "Chegada";
        await db.SaveChangesAsync();
        var pendenteParaSegunda = Assert.Single((await fila.ListarAsync(segunda.Id,
            new(Situacao: "Pendentes", Inicio: svc.Hoje, Fim: svc.Hoje), svc.Hoje)).Itens,
            x => x.Id == horario.Id);
        Assert.True(pendenteParaSegunda.ChegadaRegistrada);
        Assert.False(pendenteParaSegunda.AposAplicacaoRegistrada);
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
