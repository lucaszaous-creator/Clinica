using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// nov/2026, produção: a rodada de confirmação de 25/11 saiu às 13:12; às 14:30 a recepção remarcou quatro horários
/// para 01/12 pelo BALCÃO, e a rodada de amanhã os mostrou como "Não avisado" — o contato continuava em 25/11. A ponte
/// do CRM e a escolha do paciente já moviam a confirmação; o balcão não. Agora a regra mora no AgendaService.RemarcarAsync,
/// por onde as três portas passam. Dados fictícios.
/// </summary>
public class RemarcarMoveConfirmacaoTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly CampanhaService _campanhas;
    private readonly AgendaService _agenda;
    private static readonly DateOnly Dia = DateOnly.FromDateTime(DateTime.Today.AddDays(3));

    public RemarcarMoveConfirmacaoTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:"); _conn.Open();
        _db = new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _campanhas = new CampanhaService(_repo);
        _agenda = new AgendaService(_repo, new AtendimentoService(_repo));
    }

    public void Dispose() { _db.Dispose(); _conn.Dispose(); }

    private async Task<Agendamento> MarcarAsync()
    {
        var p = new Paciente { Nome = "Paciente Fictícia", Telefone = "21999990000", Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p); await _db.SaveChangesAsync();
        var ag = new Agendamento { PacienteId = p.Id, DataHora = Dia.ToDateTime(new TimeOnly(14, 0)),
            ModalidadePrevista = ModalidadeAtendimento.AcupunturaSimples, Status = StatusAgendamento.Agendado };
        _db.Agendamentos.Add(ag); await _db.SaveChangesAsync();
        return ag;
    }

    [Fact]
    public async Task Remarcar_pelo_balcao_tira_o_contato_da_rodada_antiga_e_o_poe_pendente_na_nova()
    {
        var ag = await MarcarAsync();
        await _campanhas.GerarConfirmacoesAsync(Dia);
        var contato = (await _campanhas.ContatosAsync(TipoContato.ConfirmacaoSessao, null, Dia, Dia)).Should().ContainSingle().Subject;
        await _campanhas.RegistrarEnvioAsync(contato.Id, "recepcao");
        _db.ChangeTracker.Clear();

        var novo = Dia.AddDays(6);
        await _agenda.RemarcarAsync(ag.Id, novo.ToDateTime(new TimeOnly(9, 0)), null, operador: "Flávia (balcão)");
        _db.ChangeTracker.Clear();

        (await _campanhas.ContatosAsync(TipoContato.ConfirmacaoSessao, null, Dia, Dia)).Should().BeEmpty("saiu da rodada do dia antigo");
        var movido = (await _campanhas.ContatosAsync(TipoContato.ConfirmacaoSessao, null, novo, novo)).Should().ContainSingle().Subject;
        movido.Id.Should().Be(contato.Id, "é o MESMO contato, não um segundo");
        movido.Status.Should().Be(StatusContato.Pendente);
        movido.EnviadoEm.Should().BeNull(); movido.EnviadoPor.Should().BeNull(); movido.RespondidoEm.Should().BeNull();
        _db.Auditoria.AsNoTracking().Should().ContainSingle(a => a.Acao == "ConfirmacaoReaberta" && a.Operador == "Flávia (balcão)");

        // A rodada do dia novo não cria um segundo contato para o mesmo horário.
        await _campanhas.GerarConfirmacoesAsync(novo);
        _db.ChangeTracker.Clear();
        (await _campanhas.ContatosAsync(TipoContato.ConfirmacaoSessao, null, novo, novo)).Should().ContainSingle();
    }

    [Fact]
    public async Task Editar_sem_mudar_o_horario_nao_mexe_na_confirmacao()
    {
        var ag = await MarcarAsync();
        await _campanhas.GerarConfirmacoesAsync(Dia);
        var contato = (await _campanhas.ContatosAsync(TipoContato.ConfirmacaoSessao, null, Dia, Dia)).Single();
        await _campanhas.RegistrarEnvioAsync(contato.Id, "recepcao");
        _db.ChangeTracker.Clear();

        await _agenda.RemarcarAsync(ag.Id, ag.DataHora, "Trazer exames", operador: "balcao");
        _db.ChangeTracker.Clear();

        var mesmo = (await _campanhas.ContatosAsync(TipoContato.ConfirmacaoSessao, null, Dia, Dia)).Single();
        mesmo.EnviadoEm.Should().NotBeNull("só a observação mudou: o pedido enviado continua valendo");
        _db.Auditoria.AsNoTracking().Should().NotContain(a => a.Acao == "ConfirmacaoReaberta");
    }
}
