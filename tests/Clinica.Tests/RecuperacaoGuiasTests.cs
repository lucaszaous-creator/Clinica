using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

public sealed class RecuperacaoGuiasTests : IDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly ClinicaDbContext db;
    private readonly ClinicaRepositorio repo;
    private readonly AgendaService agenda;
    private readonly RecuperacaoGuiasService svc;
    private readonly UsuarioSistema gerente;
    private readonly DateOnly dia = new(2026, 9, 1);
    public RecuperacaoGuiasTests()
    {
        connection.Open();
        db = new(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(connection).Options);
        db.Database.EnsureCreated(); repo = new(db);
        var atendimento = new AtendimentoService(repo);
        agenda = new(repo, atendimento); svc = new(db, repo, agenda, atendimento);
        gerente = new() { Login = "gerente-teste", Nome = "Gerente de teste", Perfil = PerfilAcesso.Gerente };
        db.Usuarios.Add(gerente); db.SaveChanges();
    }
    private async Task<(Agendamento A, Evolucao E)> Preparar(bool vinculada = true)
    {
        var a = new Agendamento { Paciente = new() { Nome = "Paciente fictício", Convenio = Convenio.UnimedIntercambio },
            Profissional = new() { Nome = "Médico fictício" }, DataHora = dia.ToDateTime(new(9, 0)),
            ModalidadePrevista = ModalidadeAtendimento.BsvComAcupuntura };
        db.Agendamentos.Add(a); await db.SaveChangesAsync();
        var e = new Evolucao { PacienteId = a.PacienteId, ProfissionalId = a.ProfissionalId,
            AgendamentoId = vinculada ? a.Id : null, Data = dia, TextoEvolucao = "Registro médico original",
            CriadoEm = dia.ToDateTime(new(10, 0)), CriadoPor = "medico-original" };
        db.Evolucoes.Add(e); await db.SaveChangesAsync();
        return (a, e);
    }
    private async Task<ItemRecuperacaoGuias> Previa() => Assert.Single((await svc.PreverAsync(gerente.Id, new(dia, dia))).Itens);

    [Fact] public async Task Historica_conclui_gera_guias_sem_enfermagem_preservando_autoria_e_idempotencia()
    {
        var (a, e) = await Preparar(); var data = e.CriadoEm;
        var p = await Previa(); Assert.True(p.PodeRecuperar, p.Situacao);
        var r = await svc.RecuperarAsync(gerente.Id, new(p.Id, p.Versao));
        Assert.True(r.Sucesso); Assert.True(r.Guias > 0);
        db.ChangeTracker.Clear();
        var salvo = await db.Agendamentos.SingleAsync(x => x.Id == a.Id);
        Assert.Equal(StatusAgendamento.Realizado, salvo.Status); Assert.NotNull(salvo.FimAtendimentoEm);
        Assert.Null(salvo.InicioAtendimentoEm); Assert.Null(salvo.EnfermagemConferidaPorUsuarioId);
        var evo = await db.Evolucoes.SingleAsync(); Assert.Equal("medico-original", evo.CriadoPor);
        Assert.Equal(data, evo.CriadoEm); Assert.Null(evo.AtualizadoEm); Assert.Equal(e.TextoEvolucao, evo.TextoEvolucao);
        Assert.Equal(salvo.AtendimentoId, evo.AtendimentoId);
        var ids = await db.Codigos.OrderBy(c => c.Id).Select(c => c.Id).ToArrayAsync();
        Assert.Equal(0, (await svc.RecuperarAsync(gerente.Id, new(p.Id, p.Versao))).Guias);
        Assert.Equal(ids, await db.Codigos.OrderBy(c => c.Id).Select(c => c.Id).ToArrayAsync());
        Assert.Single(await db.Atendimentos.ToListAsync());
        Assert.Empty((await svc.PreverAsync(gerente.Id, new(dia, dia))).Itens);
    }

    [Fact] public async Task Atendimento_ja_encerrado_sem_codigos_reutiliza_capa_e_data()
    {
        var (a, _) = await Preparar();
        var primeiro = await agenda.ConcluirAtendimentoClinicoAsync(a.Id, gerente.Login, usuarioId: gerente.Id, permitirEnfermagemPosterior: true);
        var id = primeiro.Atendimento.Id; var fim = a.FimAtendimentoEm;
        db.Codigos.RemoveRange(await db.Codigos.ToListAsync()); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var p = await Previa(); Assert.True(p.PodeRecuperar, p.Situacao);
        Assert.True((await svc.RecuperarAsync(gerente.Id, new(p.Id, p.Versao))).Guias > 0);
        Assert.Equal(id, (await db.Atendimentos.SingleAsync()).Id);
        Assert.Equal(fim, (await db.Agendamentos.SingleAsync()).FimAtendimentoEm);
    }

    [Fact] public async Task Escrita_com_guia_antecipada_conclui_sem_duplicar_nem_baixar_codigos()
    {
        var (a, _) = await Preparar();
        await agenda.ConfirmarPresencaAsync(a.Id);
        var ids = await db.Codigos.OrderBy(c => c.Id).Select(c => c.Id).ToArrayAsync();
        var estados = await db.Codigos.OrderBy(c => c.Id).Select(c => c.Status).ToArrayAsync();
        var p = await Previa(); Assert.True(p.PodeRecuperar, p.Situacao);
        var r = await svc.RecuperarAsync(gerente.Id, new(p.Id, p.Versao));
        Assert.True(r.Sucesso); Assert.Equal(0, r.Guias);
        Assert.NotNull((await db.Agendamentos.SingleAsync()).FimAtendimentoEm);
        Assert.Equal(ids, await db.Codigos.OrderBy(c => c.Id).Select(c => c.Id).ToArrayAsync());
        Assert.Equal(estados, await db.Codigos.OrderBy(c => c.Id).Select(c => c.Status).ToArrayAsync());
    }

    [Fact] public async Task Particular_conclui_sem_inventar_guia_de_convenio()
    {
        var (a, _) = await Preparar(); a.Paciente!.ConvenioCodigo = ConvenioCadastro.CodigoParticular;
        await db.SaveChangesAsync(); await new ConvenioCatalogoService(repo).RecarregarCacheAsync();
        var p = await Previa(); Assert.True(p.PodeRecuperar, p.Situacao);
        var r = await svc.RecuperarAsync(gerente.Id, new(p.Id, p.Versao));
        Assert.True(r.Sucesso); Assert.Equal(0, r.Guias);
        Assert.NotNull((await db.Agendamentos.SingleAsync()).FimAtendimentoEm);
        Assert.DoesNotContain(await db.Codigos.ToListAsync(), c => c.Status != StatusCodigo.NaoAplicavel);
    }

    [Theory]
    [InlineData("sem-medico")][InlineData("sem-texto")][InlineData("convenio")]
    [InlineData("duas-evolucoes")][InlineData("outro-atendimento")][InlineData("outro-dia")]
    public async Task Inconsistencias_nao_geram_guias(string caso)
    {
        var (a, e) = await Preparar();
        if (caso == "sem-medico") e.ProfissionalId = null;
        if (caso == "sem-texto") e.TextoEvolucao = " ";
        if (caso == "convenio") a.Paciente!.ConvenioCodigo = ConvenioCadastro.CodigoADefinir;
        if (caso == "outro-dia") e.Data = dia.AddDays(-1);
        if (caso == "duas-evolucoes") db.Evolucoes.Add(new() { PacienteId = a.PacienteId, AgendamentoId = a.Id, Data = dia, TextoEvolucao = "Outra" });
        if (caso == "outro-atendimento") db.Atendimentos.Add(new() { PacienteId = a.PacienteId, Data = dia });
        await db.SaveChangesAsync();
        var p = await Previa(); Assert.False(p.PodeRecuperar);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.RecuperarAsync(gerente.Id, new(p.Id, p.Versao)));
        Assert.Empty(await db.Codigos.ToListAsync());
        db.ChangeTracker.Clear(); Assert.Null((await db.Agendamentos.SingleAsync()).FimAtendimentoEm);
    }

    [Fact] public async Task Evolucao_avulsa_unica_pode_vincular_mas_varios_horarios_exigem_revisao()
    {
        var (a, _) = await Preparar(false); var p = await Previa(); Assert.True(p.PodeRecuperar, p.Situacao);
        db.Agendamentos.Add(new() { PacienteId = a.PacienteId, DataHora = a.DataHora.AddHours(1), ProfissionalId = a.ProfissionalId });
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.RecuperarAsync(gerente.Id, new(p.Id, p.Versao)));
        Assert.Empty(await db.Codigos.ToListAsync());
    }

    [Fact] public async Task Alteracao_apos_previa_exige_revisao_e_lote_isola_falhas()
    {
        var (_, e) = await Preparar(); var primeira = await Previa();
        await Preparar(); e.TextoEvolucao = "Alterada após prévia"; await db.SaveChangesAsync();
        var outra = (await svc.PreverAsync(gerente.Id, new(dia, dia))).Itens.Single(x => x.Id != primeira.Id);
        var r = await svc.RecuperarLoteAsync(gerente.Id, [new(primeira.Id, primeira.Versao), new(outra.Id, outra.Versao)]);
        Assert.False(r[0].Sucesso); Assert.True(r[1].Sucesso); Assert.Single(await db.Atendimentos.ToListAsync());
    }

    [Fact] public async Task Agenda_legada_sem_medico_usa_autoria_da_evolucao_sem_inventar_responsavel()
    {
        var (a, e) = await Preparar(); var autor = e.ProfissionalId;
        a.ProfissionalId = null; await db.SaveChangesAsync();
        var p = await Previa(); Assert.True(p.PodeRecuperar, p.Situacao); Assert.True(p.Escrita);
        Assert.True((await svc.RecuperarAsync(gerente.Id, new(p.Id, p.Versao))).Sucesso);
        Assert.Equal(autor, (await db.Evolucoes.SingleAsync()).ProfissionalId);
        Assert.Null((await db.Agendamentos.SingleAsync()).ProfissionalId);
        Assert.NotNull((await db.Agendamentos.SingleAsync()).FimAtendimentoEm);
    }

    [Fact] public async Task Enfermagem_sem_evolucao_medica_nao_entra_no_lote_nem_pode_concluir()
    {
        var (a, e) = await Preparar(); db.Evolucoes.Remove(e);
        db.EvolucoesEnfermagem.Add(new() { PacienteId = a.PacienteId, AgendamentoId = a.Id,
            Data = dia, Hora = new(10, 0), Texto = "Registro exclusivo de enfermagem" });
        await db.SaveChangesAsync();
        Assert.Empty((await svc.PreverAsync(gerente.Id, new(dia, dia))).Itens);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.RecuperarAsync(gerente.Id, new(a.Id, "qualquer")));
        db.ChangeTracker.Clear(); Assert.Null((await db.Agendamentos.SingleAsync()).FimAtendimentoEm);
        Assert.Empty(await db.Codigos.ToListAsync());
    }

    [Theory][InlineData(false)][InlineData(true)]
    public async Task Gerente_desativado_ou_sem_permissao_nao_pode_executar(bool semPermissao)
    {
        await Preparar(); var p = await Previa();
        if (semPermissao) gerente.PermissoesNegadas = Permissao.LancarAtendimento; else gerente.Ativo = false;
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.RecuperarAsync(gerente.Id, new(p.Id, p.Versao)));
        Assert.Empty(await db.Codigos.ToListAsync());
    }
    public void Dispose() { db.Dispose(); connection.Dispose(); }
}
