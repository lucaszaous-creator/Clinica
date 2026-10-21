using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Clinica.Infrastructure.Planejamento;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

public sealed class OcupacaoAgendaTests : IDisposable
{
    private readonly SqliteConnection conexao = new("Data Source=:memory:");
    private readonly ClinicaDbContext db;
    private readonly OcupacaoAgendaService svc;
    private readonly UsuarioSistema recepcao = new() { Nome = "Recepção teste", Login = "ocupacao", Perfil = PerfilAcesso.Recepcao };
    private readonly Profissional profissional = new() { Nome = "Profissional teste", Ativo = true,
        DiasDeAtendimento = Profissional.BitDe(DayOfWeek.Monday), AtendeDas = new(8, 0), AtendeAte = new(12, 0) };
    private readonly Paciente paciente = new() { Nome = "Paciente teste" };
    private static readonly DateOnly Segunda = new(2026, 10, 12);
    public OcupacaoAgendaTests()
    {
        conexao.Open(); db = new(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(conexao).Options);
        db.Database.EnsureCreated(); db.AddRange(recepcao, profissional, paciente); db.SaveChanges(); svc = new(db);
    }
    public void Dispose() { db.Dispose(); conexao.Dispose(); }
    private Agendamento Horario(int hora, int minutos = 60, StatusAgendamento status = StatusAgendamento.Agendado, int dias = 0)
        => new() { PacienteId = paciente.Id, ProfissionalId = profissional.Id, DataHora = Segunda.AddDays(dias).ToDateTime(new(hora, 0)), DuracaoMinutos = minutos, Status = status };

    [Fact] public async Task Sobreposicoes_e_bloqueios_nao_duplicam_tempo_e_cancelados_nao_ocupam()
    {
        db.Agendamentos.AddRange(Horario(8, 120), Horario(9, 120), Horario(11, 60, StatusAgendamento.Cancelado), Horario(11, 60, StatusAgendamento.Faltou));
        db.BloqueiosAgenda.AddRange(new BloqueioAgenda { Inicio = Segunda.ToDateTime(new(10, 0)), Fim = Segunda.ToDateTime(new(11, 0)), Motivo = "Teste" },
            new BloqueioAgenda { ProfissionalId = profissional.Id, Inicio = Segunda.ToDateTime(new(10, 30)), Fim = Segunda.ToDateTime(new(11, 0)), Motivo = "Teste" });
        await db.SaveChangesAsync();
        var r = await svc.ConsultarAsync(recepcao.Id, Segunda, Segunda);
        Assert.Equal(2, r.Resumo.Atendimentos); Assert.Equal(1, r.Resumo.Cancelados); Assert.Equal(1, r.Resumo.Faltas);
        Assert.Equal(120, r.Resumo.MinutosOcupados); Assert.Equal(180, r.Resumo.MinutosDisponiveis);
        Assert.Equal(60, r.Resumo.MinutosBloqueados); Assert.Equal(60, r.Resumo.MinutosLivres);
        Assert.Equal(66.7m, r.Resumo.Percentual); Assert.Equal(1, r.Resumo.ForaDaJornada);
        Assert.Equal(r.Resumo, Assert.Single(r.Dias).Resumo); Assert.Equal(r.Resumo, Assert.Single(r.Profissionais).Resumo);
    }

    [Fact] public async Task Movimento_preserva_contagens_filtros_e_autorizacao_sem_consultar_bloqueios()
    {
        db.Agendamentos.AddRange(Horario(8), Horario(9, status: StatusAgendamento.Cancelado), Horario(10, status: StatusAgendamento.Faltou));
        profissional.AtendeDas = null; profissional.AtendeAte = null;
        await db.SaveChangesAsync();
        var tabela = db.Model.FindEntityType(typeof(BloqueioAgenda))!.GetTableName();
        // O nome vem do MODELO do EF, não de fora — e DDL não aceita parâmetro.
        await db.Database.ExecuteSqlRawAsync("DROP TABLE \"" + tabela + "\"");
        var r = await svc.ConsultarMovimentoAsync(recepcao.Id, Segunda, Segunda, profissional.Id);
        Assert.Equal(1, r.Resumo.Atendimentos); Assert.Equal(1, r.Resumo.Cancelados); Assert.Equal(1, r.Resumo.Faltas);
        Assert.Equal(3, r.Horarios.Count); Assert.Single(r.Profissionais); Assert.Single(r.Dias);
        Assert.Empty(r.Profissionais[0].Jornada);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.ConsultarMovimentoAsync(-1, Segunda, Segunda));
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.ConsultarMovimentoAsync(recepcao.Id, Segunda, Segunda.AddDays(42)));
    }

    [Fact] public async Task Dias_sem_jornada_e_cruzamento_da_meia_noite_nao_inventam_capacidade()
    {
        db.Agendamentos.AddRange(Horario(23, 120, dias: -1), Horario(8, dias: 1)); await db.SaveChangesAsync();
        var r = await svc.ConsultarAsync(recepcao.Id, Segunda, Segunda.AddDays(1));
        Assert.Equal(2, r.Resumo.Atendimentos); Assert.Equal(2, r.Resumo.ForaDaJornada);
        Assert.Equal(240, r.Resumo.MinutosDisponiveis); Assert.Equal(0, r.Resumo.MinutosOcupados);
        Assert.Null(r.Dias[1].Resumo.Percentual); Assert.Equal(1, r.Dias[0].Resumo.Atendimentos);
        Assert.Equal(1, r.Dias[1].Resumo.Atendimentos);
    }

    [Fact] public async Task Profissional_sem_jornada_nao_vira_zero_porcento_e_filtro_preserva_total()
    {
        var outro = new Profissional { Nome = "Sem jornada", Ativo = true };
        db.Profissionais.Add(outro); db.Agendamentos.Add(Horario(8)); await db.SaveChangesAsync();
        var todos = await svc.ConsultarAsync(recepcao.Id, Segunda, Segunda.AddDays(6));
        Assert.Null(todos.Resumo.Percentual); Assert.Equal(1, todos.Resumo.SemJornada);
        var filtrado = await svc.ConsultarAsync(recepcao.Id, Segunda, Segunda.AddDays(6), profissional.Id);
        Assert.Equal(25m, filtrado.Resumo.Percentual); Assert.Single(filtrado.Profissionais);
        Assert.Equal(todos.Profissionais.Single(p => p.Id == profissional.Id).Resumo, filtrado.Resumo);
    }

    [Fact] public async Task Cada_profissional_vem_dia_a_dia_com_faixas_fechamento_e_quem_nao_tem_horario()
    {
        var outro = new Profissional { Nome = "Sem horário", Ativo = true };
        db.Profissionais.Add(outro);
        db.Agendamentos.AddRange(Horario(8), Horario(9, dias: 2));
        db.BloqueiosAgenda.Add(new BloqueioAgenda { ProfissionalId = profissional.Id, Inicio = Segunda.AddDays(7).ToDateTime(new(0, 0)),
            Fim = Segunda.AddDays(7).ToDateTime(new(23, 59)), Motivo = "Folga" });
        await db.SaveChangesAsync();
        var r = await svc.ConsultarAsync(recepcao.Id, Segunda, Segunda.AddDays(7));
        var dele = r.Profissionais.Single(p => p.Id == profissional.Id);
        Assert.False(dele.SemHorario);
        Assert.Equal(8, dele.Dias.Count);
        Assert.Equal(1, dele.Dias[0].Resumo.Atendimentos);
        Assert.Equal(25m, dele.Dias[0].Resumo.Percentual);
        Assert.Single(dele.Dias[0].Faixas!);
        Assert.Empty(dele.Dias[1].Faixas!);                 // terça: não atende
        Assert.Equal(1, dele.Dias[2].Resumo.ForaDaJornada); // quarta, marcado fora do horário
        Assert.Contains("Folga", dele.Dias[7].Fechado);      // segunda seguinte, fechada
        var sem = r.Profissionais.Single(p => p.Id == outro.Id);
        Assert.True(sem.SemHorario);
        Assert.Null(sem.Dias[0].Faixas);
        // A consulta de MOVIMENTO não calcula horários: sem dia a dia.
        var mov = await svc.ConsultarMovimentoAsync(recepcao.Id, Segunda, Segunda);
        Assert.All(mov.Profissionais, p => Assert.Empty(p.Dias));
    }

    [Fact] public async Task Horario_sem_profissional_afeta_apenas_o_dia_correspondente()
    {
        var a = Horario(8, dias: 1); a.ProfissionalId = null; db.Agendamentos.Add(a); await db.SaveChangesAsync();
        var r = await svc.ConsultarAsync(recepcao.Id, Segunda, Segunda.AddDays(1));
        Assert.Equal(0, r.Dias[0].Resumo.SemJornada); Assert.Equal(0m, r.Dias[0].Resumo.Percentual);
        Assert.Equal(1, r.Dias[1].Resumo.SemJornada); Assert.Null(r.Resumo.Percentual);
    }

    [Fact] public async Task Mes_inclui_profissional_inativo_com_horario_e_respeita_limites()
    {
        profissional.Ativo = false; db.Agendamentos.AddRange(Horario(8), Horario(9, dias: 7)); await db.SaveChangesAsync();
        var r = await svc.ConsultarAsync(recepcao.Id, new(2026, 10, 1), new(2026, 10, 31));
        Assert.Equal(31, r.Dias.Count); Assert.Equal(2, r.Resumo.Atendimentos); Assert.Single(r.Profissionais);
        Assert.Equal(r.Resumo.MinutosOcupados, r.Dias.Sum(d => d.Resumo.MinutosOcupados));
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.ConsultarAsync(recepcao.Id, Segunda, Segunda.AddDays(42)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.ConsultarAsync(recepcao.Id, Segunda, Segunda.AddDays(-1)));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.ConsultarAsync(-1, Segunda, Segunda));
        recepcao.Ativo = false; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.ConsultarAsync(recepcao.Id, Segunda, Segunda));
    }

    // ==================== o "Chegou" da Visão geral (out/2026) ====================

    [Fact] public async Task A_visao_geral_sabe_quem_ja_chegou_com_a_mesma_etapa_da_agenda_do_dia()
    {
        var chegou = Horario(8); chegou.ChegadaEm = Segunda.ToDateTime(new(7, 52));
        var marcado = Horario(9);
        var cancelado = Horario(10, status: StatusAgendamento.Cancelado);
        db.Agendamentos.AddRange(chegou, marcado, cancelado);
        await db.SaveChangesAsync();

        var r = await svc.ConsultarAsync(recepcao.Id, Segunda, Segunda);
        var linhas = Clinica.Application.Formatacao.VisaoGeralDaAgendaNaTela.HorariosDoDia(r);
        var lChegou = linhas.Single(l => l.Horario.Id == chegou.Id);
        var lMarcado = linhas.Single(l => l.Horario.Id == marcado.Id);
        var lCancelado = linhas.Single(l => l.Horario.Id == cancelado.Id);

        Assert.Equal(EtapaFila.Chegou, lChegou.Horario.Etapa);
        Assert.Equal("No local · chegou às 07:52", lChegou.Situacao);
        Assert.False(lChegou.PodeChegar(Segunda));          // já chegou: não carimba de novo

        Assert.Equal("Marcado", lMarcado.Situacao);
        Assert.True(lMarcado.PodeChegar(Segunda));
        Assert.False(lMarcado.PodeChegar(Segunda.AddDays(-1))); // só no dia da sessão

        Assert.Equal("Cancelado", lCancelado.Situacao);
        Assert.False(lCancelado.PodeChegar(Segunda));        // cancelado não tem check-in

        // A contagem por status (cancelados/faltas) continua lendo a situação de sempre.
        Assert.Equal(1, r.Resumo.Cancelados);
    }

    [Fact] public async Task O_bloco_Hoje_da_semana_e_do_mes_so_traz_quem_comeca_hoje()
    {
        db.Agendamentos.AddRange(Horario(8), Horario(9, dias: 1), Horario(10, dias: 2));
        await db.SaveChangesAsync();
        var r = await svc.ConsultarAsync(recepcao.Id, Segunda, Segunda.AddDays(6));
        var hoje = Clinica.Application.Formatacao.VisaoGeralDaAgendaNaTela.HorariosDeHoje(r, Segunda.AddDays(1));
        var unico = Assert.Single(hoje);
        Assert.Equal(9, unico.Horario.Inicio.Hour);
        Assert.True(unico.PodeChegar(Segunda.AddDays(1)));
        Assert.True(Clinica.Application.Formatacao.VisaoGeralDaAgendaNaTela.IncluiHoje(Segunda, Segunda.AddDays(6), Segunda.AddDays(3)));
        Assert.False(Clinica.Application.Formatacao.VisaoGeralDaAgendaNaTela.IncluiHoje(Segunda, Segunda.AddDays(6), Segunda.AddDays(9)));
    }
}
