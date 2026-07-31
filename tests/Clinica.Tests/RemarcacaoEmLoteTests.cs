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
/// Empurrar em lote as sessões que caíram dentro de um bloqueio (parcela 29).
///
/// A medição de cobertura mostrou este método com ZERO execução: ele foi escrito,
/// revisado, aprovado no CI e nunca rodou uma vez. É o botão que a recepção usa num mês
/// de férias, quando são trinta sessões — exatamente a situação em que ninguém tem tempo
/// de descobrir que ele não funciona.
///
/// As duas regras que os testes prendem:
///
/// 1. **Bloquear não desmarca ninguém** — a remarcação é um segundo ato, deliberado. Uma
///    sessão que some sem o paciente ser avisado é pior que o choque de agenda.
/// 2. **Data com choque é PULADA e dita**, nunca aborta o lote: abortar por causa de uma
///    devolveria a recepção ao trabalho manual que o botão existe para evitar.
/// </summary>
public class RemarcacaoEmLoteTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly AgendaService _agenda;
    private readonly BloqueioAgendaService _bloqueios;

    /// <summary>Segunda-feira; o bloqueio cobre a semana inteira.</summary>
    private static readonly DateTime Segunda = new(2026, 8, 10, 9, 0, 0);

    public RemarcacaoEmLoteTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _agenda = new AgendaService(_repo, new AtendimentoService(_repo));
        _bloqueios = new BloqueioAgendaService(_repo, _agenda);
    }

    private async Task<int> CriarPacienteAsync(string nome = "Maria")
    {
        var p = new Paciente { Nome = nome, Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    private async Task<int> CriarProfissionalAsync()
    {
        var p = new Profissional { Nome = "Dra. Ana", RegistroConselho = "CRM-SP 1234" };
        _db.Profissionais.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    private Task<Agendamento> MarcarAsync(int pacienteId, DateTime quando, int profissionalId)
        => _agenda.AgendarAsync(pacienteId, quando, ModalidadeAtendimento.AcupunturaComEletro,
            null, profissionalId: profissionalId);

    /// <summary>
    /// O caso do mês de férias: três sessões dentro da semana bloqueada, empurradas sete
    /// dias de uma vez. O HORÁRIO é mantido — é o motivo de o paciente ter escolhido
    /// aquele encaixe na semana.
    /// </summary>
    [Fact]
    public async Task Empurra_as_sessoes_do_periodo_mantendo_o_horario()
    {
        var pacienteId = await CriarPacienteAsync();
        var profissionalId = await CriarProfissionalAsync();

        await MarcarAsync(pacienteId, Segunda, profissionalId);
        await MarcarAsync(pacienteId, Segunda.AddDays(1), profissionalId);
        await MarcarAsync(pacienteId, Segunda.AddDays(2), profissionalId);

        var bloqueio = await _bloqueios.CriarAsync(
            Segunda.Date, Segunda.Date.AddDays(5), "Férias", profissionalId: profissionalId);

        var r = await _bloqueios.RemarcarEmLoteAsync(bloqueio.Bloqueio.Id, 7, "recepcao");

        r.Remarcados.Should().HaveCount(3);
        r.TudoRemarcado.Should().BeTrue();
        r.Vazio.Should().BeFalse();

        r.Remarcados.Select(a => a.DataHora).Should().BeEquivalentTo(new[]
        {
            Segunda.AddDays(7), Segunda.AddDays(8), Segunda.AddDays(9)
        }, "o horário do dia é mantido — é por ele que o paciente escolheu o encaixe");
    }

    /// <summary>
    /// A data de destino já ocupada é PULADA e volta dita, com o motivo. O resto do lote
    /// segue: é isso que separa o botão do trabalho manual.
    /// </summary>
    [Fact]
    public async Task Data_de_destino_ocupada_e_pulada_e_dita()
    {
        var pacienteId = await CriarPacienteAsync();
        var outro = await CriarPacienteAsync("João");
        var profissionalId = await CriarProfissionalAsync();

        await MarcarAsync(pacienteId, Segunda, profissionalId);
        await MarcarAsync(pacienteId, Segunda.AddDays(1), profissionalId);

        // Alguém já ocupa o destino da primeira sessão.
        await MarcarAsync(outro, Segunda.AddDays(7), profissionalId);

        var bloqueio = await _bloqueios.CriarAsync(
            Segunda.Date, Segunda.Date.AddDays(5), "Congresso", profissionalId: profissionalId);

        var r = await _bloqueios.RemarcarEmLoteAsync(bloqueio.Bloqueio.Id, 7, "recepcao");

        r.Remarcados.Should().ContainSingle("a segunda sessão cabe no destino");
        r.Recusados.Should().ContainSingle("a primeira esbarra em horário ocupado");
        r.TudoRemarcado.Should().BeFalse();
        r.Recusados[0].Motivo.Should().NotBeNullOrWhiteSpace(
            "a recepção precisa saber POR QUE aquela ficou para trás");
    }

    /// <summary>
    /// Bloqueio sem ninguém marcado dentro: o resultado é `Vazio`, não erro. É o caso do
    /// feriado cadastrado com antecedência, e a tela precisa dizer "não havia nada" em vez
    /// de "falhou".
    /// </summary>
    [Fact]
    public async Task Bloqueio_sem_sessoes_dentro_devolve_vazio()
    {
        var profissionalId = await CriarProfissionalAsync();

        var bloqueio = await _bloqueios.CriarAsync(
            Segunda.Date, Segunda.Date.AddDays(2), "Feriado", profissionalId: profissionalId);

        var r = await _bloqueios.RemarcarEmLoteAsync(bloqueio.Bloqueio.Id, 7);

        r.Vazio.Should().BeTrue();
        r.TudoRemarcado.Should().BeTrue("não há recusa quando não há o que remarcar");
    }

    /// <summary>
    /// Sessão JÁ REALIZADA dentro do período não se remarca — ela aconteceu. É o caso do
    /// bloqueio cadastrado depois do fato (a clínica fechou e alguém registrou na semana
    /// seguinte).
    ///
    /// O que anda junto é o **retorno sugerido do 2º código**: confirmar a presença cria
    /// um agendamento +24h para buscar a segunda guia, e esse ainda não aconteceu. Ele
    /// cai dentro do bloqueio e TEM de ser empurrado — se ficasse para trás, a clínica
    /// perderia o 2º código numa data em que não abre, que é justamente o defeito que o
    /// produto inteiro existe para evitar.
    /// </summary>
    [Fact]
    public async Task Sessao_ja_realizada_nao_e_empurrada_mas_o_retorno_sugerido_e()
    {
        var pacienteId = await CriarPacienteAsync();
        var profissionalId = await CriarProfissionalAsync();

        var realizada = await MarcarAsync(pacienteId, Segunda, profissionalId);
        await _agenda.ConfirmarPresencaAsync(realizada.Id);

        var bloqueio = await _bloqueios.CriarAsync(
            Segunda.Date, Segunda.Date.AddDays(2), "Fechamento", profissionalId: profissionalId);

        var r = await _bloqueios.RemarcarEmLoteAsync(bloqueio.Bloqueio.Id, 7);

        r.Remarcados.Should().NotContain(a => a.Id == realizada.Id,
            "sessão que já aconteceu não se empurra");
        r.Recusados.Should().NotContain(t => t.Sessao.Id == realizada.Id,
            "realizada não é recusa — ela simplesmente não entra no lote");

        var depois = await _agenda.ObterAsync(realizada.Id);
        depois!.DataHora.Should().Be(Segunda, "a sessão realizada fica onde estava");

        r.Remarcados.Should().ContainSingle(a => a.Origem == OrigemAgendamento.RetornoSugerido,
            "o retorno do 2º código ainda não aconteceu e cai dentro do bloqueio");
    }

    /// <summary>
    /// Deslocamento zero é recusado. Sem isto o botão rodaria o lote inteiro para deixar
    /// tudo onde estava, gravaria auditoria e diria "3 remarcadas" — mentindo sobre um
    /// trabalho que não fez.
    /// </summary>
    [Fact]
    public async Task Deslocamento_zero_e_recusado()
    {
        var profissionalId = await CriarProfissionalAsync();
        var bloqueio = await _bloqueios.CriarAsync(
            Segunda.Date, Segunda.Date.AddDays(2), "Férias", profissionalId: profissionalId);

        var acao = () => _bloqueios.RemarcarEmLoteAsync(bloqueio.Bloqueio.Id, 0);

        await acao.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Bloqueio_inexistente_falha_com_mensagem()
    {
        var acao = () => _bloqueios.RemarcarEmLoteAsync(9999, 7);

        await acao.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>
    /// O lote grava UM evento de auditoria, não um por sessão: trinta linhas iguais
    /// esconderiam a decisão que de fato foi tomada — a de empurrar a semana.
    /// </summary>
    [Fact]
    public async Task Lote_grava_um_unico_evento_de_auditoria()
    {
        var pacienteId = await CriarPacienteAsync();
        var profissionalId = await CriarProfissionalAsync();

        await MarcarAsync(pacienteId, Segunda, profissionalId);
        await MarcarAsync(pacienteId, Segunda.AddDays(1), profissionalId);

        var bloqueio = await _bloqueios.CriarAsync(
            Segunda.Date, Segunda.Date.AddDays(5), "Férias", profissionalId: profissionalId);

        await _bloqueios.RemarcarEmLoteAsync(bloqueio.Bloqueio.Id, 7, "recepcao");

        var eventos = await _db.Auditoria
            .Where(e => e.Acao == "AgendaRemarcadaEmLote")
            .ToListAsync();

        eventos.Should().ContainSingle();
        eventos[0].Operador.Should().Be("recepcao");
        eventos[0].Detalhe.Should().Contain("Férias",
            "o motivo do bloqueio é o que explica a remarcação meses depois");
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }
}
