using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace Clinica.Tests;

public sealed class AcompanhamentoPacienteTests : IDisposable
{
    private readonly SqliteConnection conexao = new("Data Source=:memory:");
    private readonly ClinicaDbContext db;
    private readonly AcompanhamentoPacienteService svc;
    private readonly UsuarioSistema gestora = new() { Nome = "Gestora teste", Login = "gestora", Perfil = PerfilAcesso.Gerente };
    private readonly UsuarioSistema medico = new() { Nome = "Médico teste", Login = "medico", Perfil = PerfilAcesso.Profissional };
    private readonly UsuarioSistema recepcao = new() { Nome = "Recepção teste", Login = "recepcao", Perfil = PerfilAcesso.Recepcao };
    private readonly Paciente paciente = new() { Nome = "Paciente fictício", Telefone = "21999999999", Convenio = Convenio.UnimedPadrao };
    private readonly Agendamento consulta;
    public AcompanhamentoPacienteTests()
    {
        conexao.Open(); db = new(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(conexao).Options);
        db.Database.EnsureCreated(); var repo = new ClinicaRepositorio(db); svc = new(db, repo, new(repo));
        medico.Profissional = new() { Nome = "Médico autorizado", Ativo = true };
        db.Usuarios.AddRange(gestora, medico, recepcao); db.Pacientes.Add(paciente); db.SaveChanges();
        consulta = new() { PacienteId = paciente.Id, ProfissionalId = medico.ProfissionalId, DataHora = DateTime.Today, ModalidadePrevista = ModalidadeAtendimento.Consulta };
        db.Agendamentos.Add(consulta);
        db.Configuracoes.AddRange(new ConfiguracaoGlobal { Chave = AcompanhamentoPacienteService.ChaveProfissional, Valor = medico.ProfissionalId.ToString()! },
            new ConfiguracaoGlobal { Chave = AcompanhamentoPacienteService.ChaveResponsavel, Valor = recepcao.Id.ToString() });
        db.SaveChanges();
    }
    public void Dispose() { db.Dispose(); conexao.Dispose(); }
    private async Task<LinhaAcompanhamento> Linha() => Assert.Single(await svc.ListarAsync(gestora.Id));
    private async Task<Agendamento> Agendar(ModalidadeAtendimento modalidade = ModalidadeAtendimento.BsvApenas)
    {
        var a = new Agendamento { PacienteId = paciente.Id, DataHora = DateTime.Today.AddDays(2), ModalidadePrevista = modalidade };
        db.Agendamentos.Add(a); await db.SaveChangesAsync(); return a;
    }
    private void Atendimento(ModalidadeAtendimento modalidade, int dias)
    {
        db.Atendimentos.Add(new() { PacienteId = paciente.Id, Modalidade = modalidade, Data = DateOnly.FromDateTime(DateTime.Today.AddDays(dias)), RealizadoEm = DateTime.Now.AddDays(dias) });
        db.SaveChanges();
    }
    private async Task Consentir()
    {
        db.Consentimentos.Add(new() { PacienteId = paciente.Id, Finalidade = FinalidadeConsentimento.ComunicacaoEMarketing, Concedido = true }); await db.SaveChangesAsync();
    }

    [Fact] public async Task Indicacao_repetida_nao_duplica_e_preserva_responsavel()
    {
        await svc.IndicarBsvAsync(medico.Id, consulta.Id); await svc.IndicarBsvAsync(medico.Id, consulta.Id);
        var linha = await Linha(); Assert.True(linha.Pendente); Assert.Equal(recepcao.Id, linha.ResponsavelId);
        Assert.Single(await svc.HistoricoAsync(gestora.Id, linha.Id));
    }
    [Theory] [InlineData(ModalidadeAtendimento.BsvApenas)] [InlineData(ModalidadeAtendimento.BsvComAcupuntura)]
    public async Task Agendar_retira_cancelar_devolve_sem_exigir_evolucao(ModalidadeAtendimento modalidade)
    {
        await svc.IndicarBsvAsync(medico.Id, consulta.Id); var agenda = await Agendar(modalidade);
        Assert.Equal("Agendado", (await Linha()).Situacao); Assert.False((await Linha()).Pendente);
        agenda.Status = StatusAgendamento.Cancelado; await db.SaveChangesAsync();
        Assert.True((await Linha()).Pendente); Assert.Equal("Cancelou", (await Linha()).Situacao);
    }
    [Fact] public async Task Remarcacao_valida_impede_reabertura_e_consulta_nao_resolve_bsv()
    {
        await svc.IndicarBsvAsync(medico.Id, consulta.Id); await Agendar(ModalidadeAtendimento.Consulta);
        Assert.True((await Linha()).Pendente);
        var a = await Agendar(); await Agendar(ModalidadeAtendimento.BsvComAcupuntura);
        a.Status = StatusAgendamento.Cancelado; await db.SaveChangesAsync(); Assert.False((await Linha()).Pendente);
    }
    [Fact] public async Task Falta_reabre_e_sessao_realizada_estornada_volta_a_pender()
    {
        await svc.IndicarBsvAsync(medico.Id, consulta.Id); var a = await Agendar();
        a.Status = StatusAgendamento.Faltou; await db.SaveChangesAsync(); Assert.Equal("Faltou", (await Linha()).Situacao);
        Atendimento(ModalidadeAtendimento.BsvApenas, 0); Assert.Equal("Sessão realizada", (await Linha()).Situacao);
        var atendimento = await db.Atendimentos.SingleAsync(); atendimento.EstornadoEm = DateTime.Now; await db.SaveChangesAsync();
        Assert.True((await Linha()).Pendente);
    }
    [Fact] public async Task Recall_usa_ultima_sessao_da_modalidade_e_nao_a_consulta_recente()
    {
        Atendimento(ModalidadeAtendimento.BsvApenas, -90); Atendimento(ModalidadeAtendimento.Consulta, -1);
        await Agendar(ModalidadeAtendimento.Consulta);
        Assert.Equal(1, await svc.GerarRecallAsync(gestora.Id, 60));
        Assert.Equal(ModalidadeAtendimento.BsvApenas, (await Linha()).Modalidade);
        Assert.Equal(0, await svc.GerarRecallAsync(gestora.Id, 60));
    }
    [Fact] public async Task Sem_sessao_realizada_nao_entra_no_recall()
    {
        db.Atendimentos.Add(new() { PacienteId = paciente.Id, Modalidade = ModalidadeAtendimento.BsvApenas, Data = DateOnly.FromDateTime(DateTime.Today.AddDays(-100)) });
        await db.SaveChangesAsync(); Assert.Equal(0, await svc.GerarRecallAsync(gestora.Id, 60));
    }
    [Fact] public async Task Outro_profissional_e_recepcao_nao_indicam()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.IndicarBsvAsync(recepcao.Id, consulta.Id));
        db.Configuracoes.Single(x => x.Chave == AcompanhamentoPacienteService.ChaveProfissional).Valor = "999"; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.IndicarBsvAsync(medico.Id, consulta.Id));
    }
    [Fact] public async Task Contato_exige_consentimento_resultado_e_prazo_e_reenvio_e_idempotente()
    {
        await svc.IndicarBsvAsync(medico.Id, consulta.Id); var linha = await Linha();
        var pedido = new AtualizarAcompanhamento(Guid.NewGuid(), linha.Versao, recepcao.Id, DateOnly.FromDateTime(DateTime.Today.AddDays(2)), EtapaAcompanhamento.SemResposta, CanalContato.WhatsApp, "Mensagem enviada, aguardando resposta.");
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.AtualizarAsync(recepcao.Id, linha.Id, pedido));
        await Consentir();
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.AtualizarAsync(recepcao.Id, linha.Id, pedido with { ProximoContato = null }));
        await svc.AtualizarAsync(recepcao.Id, linha.Id, pedido); await svc.AtualizarAsync(recepcao.Id, linha.Id, pedido);
        Assert.Equal(1, (await Linha()).Tentativas); Assert.True((await Linha()).Pendente);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.AtualizarAsync(recepcao.Id, linha.Id, pedido with { Idempotencia = Guid.NewGuid() }));
    }
    [Fact] public async Task Nao_conformidade_exige_motivo_e_tentativa_e_pode_reabrir_com_historico()
    {
        await svc.IndicarBsvAsync(medico.Id, consulta.Id); var linha = await Linha();
        await svc.AdicionarMotivoAsync(gestora.Id, "Paciente desistiu"); var motivo = await db.MotivosAcompanhamento.SingleAsync(m => m.Nome == "Paciente desistiu");
        var pedido = new AtualizarAcompanhamento(Guid.NewGuid(), linha.Versao, recepcao.Id, null, EtapaAcompanhamento.AContatar, null, "Paciente informou desistência.", true, motivo.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.AtualizarAsync(recepcao.Id, linha.Id, pedido));
        await svc.AtualizarAsync(gestora.Id, linha.Id, pedido); Assert.False((await Linha()).Pendente);
        linha = await Linha();
        await svc.AtualizarAsync(gestora.Id, linha.Id, pedido with { Idempotencia = Guid.NewGuid(), Versao = linha.Versao, Encerrar = false, Reabrir = true, ProximoContato = DateOnly.FromDateTime(DateTime.Today) });
        Assert.True((await Linha()).Pendente); Assert.Equal(3, (await svc.HistoricoAsync(gestora.Id, linha.Id)).Count);
    }
    [Fact] public async Task Permissao_revogada_e_relida_antes_de_operar()
    {
        recepcao.PermissoesNegadas = Permissao.GerenciarCampanhas; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.ListarAsync(recepcao.Id));
    }
    [Fact] public async Task Fila_compartilhada_sem_responsavel_e_assumida_pelo_faturamento()
    {
        await svc.ConfigurarAsync(gestora.Id, medico.ProfissionalId!.Value, 0);
        await svc.IndicarBsvAsync(medico.Id, consulta.Id);
        var faturista = new UsuarioSistema { Nome = "Faturamento teste", Login = "faturista", Perfil = PerfilAcesso.Faturista };
        db.Usuarios.Add(faturista); await db.SaveChangesAsync();
        var linha = Assert.Single(await svc.ListarAsync(faturista.Id));
        Assert.Null(linha.ResponsavelId); Assert.Equal("A assumir", linha.Responsavel);
        await svc.AtualizarAsync(faturista.Id, linha.Id, new(Guid.NewGuid(), linha.Versao, faturista.Id,
            DateOnly.FromDateTime(DateTime.Today), EtapaAcompanhamento.AguardandoPlano, null, "Assumiu acompanhamento da autorização."));
        Assert.Equal(faturista.Id, Assert.Single(await svc.ListarAsync(recepcao.Id)).ResponsavelId);
        Assert.Single(await svc.ListarAsync(gestora.Id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.ListarAsync(medico.Id));
    }
    [Fact] public async Task Novo_ciclo_de_recall_preserva_historico_sem_duplicar_paciente()
    {
        Atendimento(ModalidadeAtendimento.BsvApenas, -180);
        await svc.GerarRecallAsync(recepcao.Id, 60);
        var anterior = await Linha();
        Atendimento(ModalidadeAtendimento.BsvApenas, -90);
        Assert.False((await Linha()).Pendente);
        Assert.Equal(1, await svc.GerarRecallAsync(recepcao.Id, 60));
        var atual = await Linha();
        Assert.Equal(anterior.Id, atual.Id); Assert.True(atual.Pendente);
        Assert.Equal(2, (await svc.HistoricoAsync(recepcao.Id, atual.Id)).Count);
    }

    [Fact] public async Task Duas_recepcoes_geram_recall_ao_mesmo_tempo_sem_duplicar_o_ciclo()
    {
        if (!BancoDosTestes.NoPostgres) return; // PostgreSQL: two connections and the real unique index.
        Atendimento(ModalidadeAtendimento.BsvApenas, -90);
        var pausa = new DuasGravacoes();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(conexao).AddInterceptors(pausa).Options;
        await using var primeira = new ClinicaDbContext(options);
        await using var segunda = new ClinicaDbContext(options);
        var r1 = new ClinicaRepositorio(primeira); var r2 = new ClinicaRepositorio(segunda);
        var s1 = new AcompanhamentoPacienteService(primeira, r1, new(r1));
        var s2 = new AcompanhamentoPacienteService(segunda, r2, new(r2));
        var resultados = await Task.WhenAll(s1.GerarRecallAsync(recepcao.Id, 60), s2.GerarRecallAsync(recepcao.Id, 60));
        Assert.Equal(1, resultados.Sum());
        Assert.Single(await db.Acompanhamentos.AsNoTracking().ToListAsync());
        Assert.Single(await db.ContatosAcompanhamento.AsNoTracking().ToListAsync());
    }

    private sealed class DuasGravacoes : SaveChangesInterceptor
    {
        private int _chegadas;
        private readonly TaskCompletionSource _ambas = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<AcompanhamentoPaciente>().Any(e => e.State == EntityState.Added))
            {
                if (Interlocked.Increment(ref _chegadas) == 2) _ambas.TrySetResult();
                await _ambas.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            }
            return result;
        }
    }
}
