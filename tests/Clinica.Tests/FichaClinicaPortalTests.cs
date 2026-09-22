using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Application.Tablet;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

public sealed partial class AtendimentoTabletTests
{
    [Fact]
    public async Task Ficha_desktop_le_evolucao_e_conclusao_do_portal_sem_duplicar_guias()
    {
        await Preparar();
        var pedido = await Pedido("Registro feito no portal");
        await svc.SalvarAsync(sessao, horario.Id, pedido, default);

        using var leitura = new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(connection).Options);
        var leitor = new ClinicaRepositorio(leitura);
        var salva = Assert.Single(await leitor.SessoesNaFichaAsync(horario.PacienteId, 0, 26));
        Assert.NotNull(salva.EvolucaoId);
        Assert.Equal("Evolução salva", salva.EvolucaoTexto);
        Assert.NotEqual("Sessão concluída", salva.Situacao);

        var finalizar = (await Pedido("Registro feito no portal")) with { Finalizar = true, HouveEnfermagem = false, Consumo = new([], true) };
        await svc.SalvarAsync(sessao, horario.Id, finalizar, default);
        await svc.SalvarAsync(sessao, horario.Id, finalizar, default);
        var concluida = Assert.Single(await leitor.SessoesNaFichaAsync(horario.PacienteId, 0, 26));
        Assert.Equal("Sessão concluída", concluida.Situacao);
        Assert.NotNull(concluida.AtendimentoId);
        Assert.NotNull(concluida.EvolucaoId);
        Assert.True(concluida.Guias > 0);
        Assert.Equal(await db.Codigos.CountAsync(c => c.AtendimentoId == concluida.AtendimentoId), concluida.Guias);
        Assert.Single(await new ProntuarioService(leitor).DoPacienteAsync(horario.PacienteId));
        Assert.Single(await db.Atendimentos.Where(a => a.PacienteId == horario.PacienteId).ToListAsync());
    }

    [Fact]
    public async Task Ficha_nao_vincula_evolucao_avulsa_por_dia_nem_mistura_pacientes()
    {
        await Preparar();
        db.Evolucoes.Add(new Evolucao { PacienteId = horario.PacienteId, Data = svc.Hoje,
            TextoEvolucao = "Registro sem vínculo", CriadoPor = usuario.Login });
        await db.SaveChangesAsync();
        var linha = Assert.Single(await repo.SessoesNaFichaAsync(horario.PacienteId, 0, 26));
        Assert.Equal(horario.Id, linha.AgendamentoId);
        Assert.Null(linha.EvolucaoId);
        Assert.Equal("Sem evolução vinculada", linha.EvolucaoTexto);
        Assert.Empty(await repo.SessoesNaFichaAsync(int.MaxValue, 0, 26));
    }

    [Fact]
    public async Task Ficha_pagina_agenda_sem_repetir_linhas_e_preserva_cancelamentos()
    {
        await Preparar();
        for (var i = 1; i <= 30; i++)
            db.Agendamentos.Add(new Agendamento { PacienteId = horario.PacienteId,
                ProfissionalId = horario.ProfissionalId, DataHora = horario.DataHora.AddDays(-i),
                Status = i == 1 ? StatusAgendamento.Cancelado : StatusAgendamento.Agendado });
        await db.SaveChangesAsync();
        var primeira = await repo.SessoesNaFichaAsync(horario.PacienteId, 0, 25);
        var segunda = await repo.SessoesNaFichaAsync(horario.PacienteId, 25, 25);
        Assert.Equal(25, primeira.Count);
        Assert.Equal(6, segunda.Count);
        Assert.Equal(31, primeira.Concat(segunda).Select(s => s.AgendamentoId).Distinct().Count());
        Assert.Equal("Cancelada", primeira[1].Situacao);
        Assert.True(primeira[^1].DataHora > segunda[0].DataHora);
    }

    [Fact]
    public async Task Documentos_e_enfermagem_do_portal_estao_nos_leitores_desktop()
    {
        await PrepararBSV();
        var documento = await Posto.EmitirAsync(sessao, horario.PacienteId,
            new(Guid.NewGuid(), "receita", "Texto fictício para integração"), default);
        await Enfermeira();
        var observado = DateTime.Now.AddMinutes(-1);
        var enfermagem = await Posto.RegistrarEnfermagemAsync(sessao, horario.PacienteId,
            new RegistroEnfermagemTablet(Guid.NewGuid(), DateOnly.FromDateTime(observado), TimeOnly.FromDateTime(observado),
                "Observação fictícia da enfermagem", false, new(120, 80), null, AgendamentoId: horario.Id), default);

        using var leitura = new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(connection).Options);
        var leitor = new ClinicaRepositorio(leitura);
        var documentos = await new DocumentoClinicoService(leitor, new(leitor), new(leitor)).DoPacienteAsync(horario.PacienteId);
        Assert.Contains(documentos, d => d.Id == documento.Id && d.Corpo == "Texto fictício para integração");
        var passagens = await new EvolucaoEnfermagemService(leitor).DoPacienteAsync(horario.PacienteId);
        Assert.Contains(passagens, e => e.Id == enfermagem.Id && e.PressaoSistolica == 120);
        Assert.Empty(await new EvolucaoEnfermagemService(leitor).DoPacienteAsync(outro.PacienteId));
    }
}

public sealed class SituacaoSessaoNaFichaTests
{
    [Fact]
    public void Guias_antecipadas_nao_significam_sessao_concluida()
    {
        var linha = new SessaoNaFichaPaciente(1, new DateTime(2026, 9, 17), "Profissional", default,
            null, null, StatusAgendamento.Agendado, null, null, 10, "AT-10", null, null, null, 2);
        Assert.Equal("Agendada", linha.Situacao);
        Assert.Equal("2 guia(s) gerada(s)", linha.GuiasTexto);
        Assert.Equal("Sem evolução vinculada", linha.EvolucaoTexto);
        Assert.Equal("Conclusão pendente", (linha with { Fim = linha.DataHora }).Situacao);
        Assert.Equal("Atendimento estornado", (linha with { EstornadoEm = linha.DataHora }).Situacao);
    }
}
