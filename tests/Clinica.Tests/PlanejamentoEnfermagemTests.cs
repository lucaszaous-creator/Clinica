using Clinica.Application.Abstracoes;
using Clinica.Application.Tablet;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Clinica.Infrastructure.Planejamento;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

public sealed class PlanejamentoEnfermagemTests : IDisposable
{
    private readonly SqliteConnection conexao = new("Data Source=:memory:");
    private readonly ClinicaDbContext db;
    private readonly PlanejamentoEnfermagemService svc;
    private readonly Relogio tempo = new();
    private readonly UsuarioSistema tecnica = new() { Nome = "Técnica fictícia", Login = "tecnica.planejamento", Perfil = PerfilAcesso.Enfermagem, Profissional = new() { Nome = "Enfermagem", Ativo = true } };
    private readonly UsuarioSistema outra = new() { Nome = "Outra técnica", Login = "outra.planejamento", Perfil = PerfilAcesso.Enfermagem, Profissional = new() { Nome = "Outra enfermagem", Ativo = true } };
    private readonly UsuarioSistema recepcao = new() { Nome = "Recepção fictícia", Login = "recepcao.planejamento", Perfil = PerfilAcesso.Recepcao };
    private readonly Paciente paciente = new() { Nome = "Paciente fictício" };
    private static readonly DateTime Inicio = new(2026, 10, 16, 14, 0, 0, DateTimeKind.Unspecified);
    private static readonly DateTime Fim = Inicio.AddHours(2);
    public PlanejamentoEnfermagemTests()
    {
        conexao.Open(); db = NovoContexto(); db.Database.EnsureCreated();
        db.Usuarios.AddRange(tecnica, outra, recepcao); db.Pacientes.Add(paciente); db.SaveChanges();
        svc = new(db, tempo);
    }
    private ClinicaDbContext NovoContexto() => new(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(conexao).Options);
    public void Dispose() { db.Dispose(); conexao.Dispose(); }
    private sealed class Relogio : TimeProvider
    {
        public DateTimeOffset Agora = new(2026, 10, 14, 15, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Agora;
    }
    private async Task<CriarSolicitacaoEnfermagem> Pedido()
    {
        var r = await svc.PeriodoAsync(tecnica.Id, new(Inicio, Fim));
        return new(Guid.NewGuid(), Inicio, Fim, Inicio, ModalidadeAtendimento.BsvApenas, 4, "Distribuir ao longo do período.", r.Referencia);
    }
    private async Task<int> Criar() => await svc.CriarAsync(tecnica.Id, await Pedido());
    private async Task<Agendamento> Agendar(DateTime? quando = null, ModalidadeAtendimento modalidade = ModalidadeAtendimento.BsvApenas)
    {
        var a = new Agendamento { PacienteId = paciente.Id, DataHora = quando ?? Inicio, ModalidadePrevista = modalidade };
        db.Agendamentos.Add(a); await db.SaveChangesAsync(); return a;
    }

    [Fact] public async Task Contador_global_independe_do_dia_e_distingue_lido_expirado_e_acesso()
    {
        var id = await Criar();
        Assert.Equal((1, 1), await svc.ContarAsync(recepcao.Id));
        var detalhe = await svc.DetalheAsync(recepcao.Id, id);
        await svc.AtualizarAsync(recepcao.Id, id, new(Guid.NewGuid(), detalhe.Pedido.Versao, "visto"));
        Assert.Equal((0, 1), await svc.ContarAsync(recepcao.Id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.ContarAsync(tecnica.Id));
        tempo.Agora = tempo.Agora.AddDays(4);
        Assert.Equal((0, 0), await svc.ContarAsync(recepcao.Id));
    }

    [Fact] public async Task Aviso_e_reenvio_nao_modificam_agenda_nem_permissoes()
    {
        var a = await Agendar(); var p = await Pedido();
        var id = await svc.CriarAsync(tecnica.Id, p);
        Assert.Equal(id, await svc.CriarAsync(tecnica.Id, p));
        Assert.Single(await db.Set<SolicitacaoEnfermagem>().ToListAsync());
        Assert.Single(await db.Set<EventoSolicitacaoEnfermagem>().ToListAsync());
        Assert.Equal(a.Id, (await db.Agendamentos.SingleAsync()).Id);
        Assert.False(tecnica.Pode(Permissao.EditarAgenda));
        Assert.Equal(1, (await svc.DetalheAsync(recepcao.Id, id)).Pedido.TotalReferencia);
        await Assert.ThrowsAsync<ConflitoClinicoTablet>(() => svc.CriarAsync(tecnica.Id, p with { Quantidade = 5 }));
    }

    [Fact] public async Task Semana_mostra_sete_dias_inclusive_ocupacao_que_cruza_meia_noite()
    {
        await Agendar(Inicio.Date.AddMinutes(-15));
        await Agendar(Inicio.AddDays(1), ModalidadeAtendimento.BsvComAcupuntura);
        db.BloqueiosAgenda.Add(new() { Inicio = Inicio, Fim = Inicio.AddDays(1), Motivo = "Fictício" });
        await db.SaveChangesAsync();
        var semana = await svc.SemanaAsync(tecnica.Id, new(DateOnly.FromDateTime(Inicio)));
        Assert.Equal(7, semana.Count); Assert.Equal(1, semana[0].Total); Assert.Equal(1, semana[0].Bsv);
        Assert.Equal(1, semana[1].BsvAcupuntura); Assert.Equal(1, semana[0].Bloqueios); Assert.Equal(1, semana[1].Bloqueios);
        Assert.Equal(0, semana[2].Total); Assert.Equal(0, semana[2].Bloqueios);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.SemanaAsync(999999, new(DateOnly.FromDateTime(Inicio))));
    }

    [Fact] public async Task Total_completo_paginacao_outros_medicos_e_bloqueios()
    {
        var medico1 = new Profissional { Nome = "Médico um", Ativo = true, DuracaoPadraoMinutos = 45 };
        var medico2 = new Profissional { Nome = "Médico dois", Ativo = true };
        db.Profissionais.AddRange(medico1, medico2); await db.SaveChangesAsync();
        for (var i = 0; i < 65; i++) db.Agendamentos.Add(new() { PacienteId = paciente.Id, DataHora = Inicio,
            ProfissionalId = i % 2 == 0 ? medico1.Id : medico2.Id, ModalidadePrevista = ModalidadeAtendimento.BsvComAcupuntura });
        // A sessão iniciada antes da faixa continua ocupando o período pela duração do médico.
        db.Agendamentos.Add(new() { PacienteId = paciente.Id, DataHora = Inicio.AddMinutes(-40), ProfissionalId = medico1.Id,
            ModalidadePrevista = ModalidadeAtendimento.Consulta });
        db.BloqueiosAgenda.Add(new() { Inicio = Inicio, Fim = Fim, Motivo = "Manutenção" });
        await db.SaveChangesAsync();
        var r = await svc.PeriodoAsync(tecnica.Id, new(Inicio, Fim));
        Assert.Equal(66, r.Total); Assert.Equal(65, r.BsvAcupuntura); Assert.Equal(1, r.OutrasModalidades);
        Assert.Equal(50, r.Horarios.Count); Assert.True(r.Mais); Assert.Single(r.Bloqueios);
        var segunda = await svc.PeriodoAsync(tecnica.Id, new(Inicio, Fim, 1));
        Assert.Equal(16, segunda.Horarios.Count); Assert.Equal(r.Referencia, segunda.Referencia);
        Assert.False(segunda.Mais);
    }

    [Fact] public async Task Mudanca_de_agenda_recusa_envio_desatualizado_e_avisa_no_detalhe()
    {
        var p = await Pedido(); await Agendar();
        await Assert.ThrowsAsync<ConflitoClinicoTablet>(() => svc.CriarAsync(tecnica.Id, p));
        var id = await Criar(); Assert.False((await svc.DetalheAsync(recepcao.Id, id)).AgendaAlterada);
        var a = await db.Agendamentos.SingleAsync(); a.ModalidadePrevista = ModalidadeAtendimento.BsvComAcupuntura;
        await db.SaveChangesAsync(); Assert.True((await svc.DetalheAsync(recepcao.Id, id)).AgendaAlterada);
        a.Status = StatusAgendamento.Cancelado; await db.SaveChangesAsync();
        var d = await svc.DetalheAsync(recepcao.Id, id); Assert.Equal(0, d.Periodo.Total); Assert.True(d.AgendaAlterada);
    }

    [Fact] public async Task Recepcao_ve_todos_os_dias_e_leitura_e_compartilhada_sem_assumir()
    {
        var id = await Criar();
        Assert.Single(await svc.ListarAsync(recepcao.Id));
        Assert.Empty(await svc.ListarAsync(recepcao.Id, new DateOnly(2026, 10, 15)));
        var d = await svc.DetalheAsync(recepcao.Id, id);
        var acao = new AcaoSolicitacaoEnfermagem(Guid.NewGuid(), d.Pedido.Versao, "visto");
        await svc.AtualizarAsync(recepcao.Id, id, acao);
        await svc.AtualizarAsync(recepcao.Id, id, acao);
        // Segunda recepcionista, em outro contexto, abre a versão anterior: a leitura continua idempotente.
        using var outroDb = NovoContexto();
        await new PlanejamentoEnfermagemService(outroDb, tempo).AtualizarAsync(recepcao.Id, id, acao with { Idempotencia = Guid.NewGuid() });
        var visto = await svc.DetalheAsync(tecnica.Id, id);
        Assert.Equal("Visto", visto.Pedido.Situacao); Assert.Equal(recepcao.Nome, visto.Pedido.VistaPor);
        Assert.Equal(2, visto.Historico.Count); Assert.Empty(await db.Agendamentos.ToListAsync());
    }

    [Fact] public async Task Encerramento_tem_retorno_e_preserva_historico_sem_confirmar_agendamento()
    {
        var a = await Agendar(); var id = await Criar();
        var d = await svc.DetalheAsync(recepcao.Id, id);
        var fechar = new AcaoSolicitacaoEnfermagem(Guid.NewGuid(), d.Pedido.Versao, "encerrar", "Pacientes consultados.");
        await svc.AtualizarAsync(recepcao.Id, id, fechar);
        await svc.AtualizarAsync(recepcao.Id, id, fechar);
        a.Status = StatusAgendamento.Cancelado; await db.SaveChangesAsync();
        var final = await svc.DetalheAsync(tecnica.Id, id);
        Assert.Equal("Encerrado", final.Pedido.Situacao); Assert.False(final.Pedido.Ativa);
        Assert.Equal("Pacientes consultados.", final.Historico.Last().Observacao);
        Assert.Single(await db.Agendamentos.ToListAsync()); Assert.Equal(2, final.Historico.Count);
    }

    [Fact] public async Task Retirada_so_da_autora_e_concorrencia_nao_sobrescreve()
    {
        var id = await Criar(); var d = await svc.DetalheAsync(tecnica.Id, id);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.AtualizarAsync(outra.Id, id,
            new(Guid.NewGuid(), d.Pedido.Versao, "retirar")));
        using (var segundo = NovoContexto()) await new PlanejamentoEnfermagemService(segundo, tempo).AtualizarAsync(recepcao.Id, id,
            new(Guid.NewGuid(), d.Pedido.Versao, "visto"));
        db.ChangeTracker.Clear();
        await Assert.ThrowsAsync<ConflitoClinicoTablet>(() => svc.AtualizarAsync(tecnica.Id, id,
            new(Guid.NewGuid(), d.Pedido.Versao, "retirar")));
        d = await svc.DetalheAsync(tecnica.Id, id);
        await svc.AtualizarAsync(tecnica.Id, id, new(Guid.NewGuid(), d.Pedido.Versao, "retirar"));
        Assert.Equal("Retirado", (await svc.DetalheAsync(recepcao.Id, id)).Pedido.Situacao);
    }

    [Fact] public async Task Perfis_e_revogacao_sao_revalidados_no_servidor()
    {
        var p = await Pedido(); var id = await svc.CriarAsync(tecnica.Id, p);
        var d = await svc.DetalheAsync(tecnica.Id, id);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.CriarAsync(recepcao.Id, p));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.AtualizarAsync(tecnica.Id, id, new(Guid.NewGuid(), d.Pedido.Versao, "visto")));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.AtualizarAsync(tecnica.Id, id, new(Guid.NewGuid(), d.Pedido.Versao, "encerrar")));
        tecnica.PermissoesExtras |= Permissao.EditarAgenda;
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.AtualizarAsync(tecnica.Id, id, new(Guid.NewGuid(), d.Pedido.Versao, "encerrar")));
        tecnica.PermissoesNegadas |= Permissao.VerAgenda; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.PeriodoAsync(tecnica.Id, new(Inicio, Fim)));
        recepcao.PermissoesNegadas |= Permissao.EditarAgenda; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.ListarAsync(recepcao.Id));
    }

    [Fact] public async Task Expiracao_e_validacoes_nao_criam_vagas()
    {
        var p = await Pedido();
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CriarAsync(tecnica.Id, p with { Quantidade = 0 }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CriarAsync(tecnica.Id, p with { Validade = Fim }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CriarAsync(tecnica.Id, p with { Modalidade = ModalidadeAtendimento.Consulta }));
        var id = await svc.CriarAsync(tecnica.Id, p);
        tempo.Agora = new(2026, 10, 16, 17, 0, 0, TimeSpan.Zero);
        var d = await svc.DetalheAsync(recepcao.Id, id);
        Assert.Equal("Expirado", d.Pedido.Situacao); Assert.False(d.Pedido.Ativa);
        Assert.False(d.Pedido.PodeTratar); Assert.Empty(await db.Agendamentos.ToListAsync());
    }
}
