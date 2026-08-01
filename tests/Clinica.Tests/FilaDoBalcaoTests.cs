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
/// O que o quadro do balcão precisa responder, e não respondia (parcela 36).
///
/// A fila em kanban media UMA coisa — minutos desde a chegada — e a usava para tudo:
/// para pintar de vermelho quem esperava demais, e para nada na coluna de quem ainda não
/// tinha chegado. Disso saíam dois erros com o mesmo formato: alarme onde não havia
/// problema (quem chega quarenta minutos antes) e silêncio onde havia (quem tinha hora às
/// 9h e não veio). Os dois se resolvem separando três relógios que não são o mesmo:
/// o do paciente que ainda não chegou, o da clínica que está fazendo esperar, e o da
/// sessão que já começou.
///
/// A quarta regra é o desfazer: falta marcada por engano não tinha volta no balcão.
/// </summary>
public class FilaDoBalcaoTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly AgendaService _agenda;

    /// <summary>Hora marcada de referência das sessões destes testes.</summary>
    private static readonly DateTime Marcado = new(2026, 8, 3, 14, 0, 0);

    public FilaDoBalcaoTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _agenda = new AgendaService(_repo, new AtendimentoService(_repo));
    }

    private async Task<int> CriarPacienteAsync(string nome = "Paciente")
    {
        var p = new Paciente { Nome = nome, Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    private Task<Agendamento> AgendarAsync(int pacienteId, DateTime? quando = null)
        => _agenda.AgendarAsync(
            pacienteId, quando ?? Marcado, ModalidadeAtendimento.AcupunturaSimples, null);

    // ===== O relógio do paciente que ainda não chegou =====

    [Fact]
    public void Paciente_que_nao_chegou_e_cuja_hora_passou_esta_atrasado()
    {
        var ag = new Agendamento { DataHora = Marcado };

        ag.AtrasoDoPacienteMinutos(Marcado.AddMinutes(20)).Should().Be(20);
    }

    [Fact]
    public void Antes_da_hora_marcada_nao_ha_atraso_nenhum()
    {
        var ag = new Agendamento { DataHora = Marcado };

        // Quem tem hora às 17h não pode aparecer igual a quem tinha hora às 9h e não veio:
        // era exatamente essa a coluna "Aguardando" antes desta parcela.
        ag.AtrasoDoPacienteMinutos(Marcado.AddMinutes(-30)).Should().BeNull();
    }

    [Fact]
    public void Quem_ja_fez_check_in_nao_esta_atrasado()
    {
        var ag = new Agendamento { DataHora = Marcado, ChegadaEm = Marcado.AddMinutes(10) };

        ag.AtrasoDoPacienteMinutos(Marcado.AddMinutes(30)).Should().BeNull();
    }

    [Fact]
    public void Horario_cancelado_nao_acumula_atraso()
    {
        var ag = new Agendamento { DataHora = Marcado, Status = StatusAgendamento.Cancelado };

        // Cancelado não está atrasado: ele não vem, e cobrar presença dele encheria o
        // quadro de alarme que ninguém pode resolver.
        ag.AtrasoDoPacienteMinutos(Marcado.AddMinutes(60)).Should().BeNull();
    }

    // ===== O relógio da clínica =====

    [Fact]
    public void Chegar_adiantado_nao_conta_como_espera_da_clinica()
    {
        var ag = new Agendamento { DataHora = Marcado, ChegadaEm = Marcado.AddMinutes(-40) };

        // Dez minutos ANTES da hora marcada: ele está no balcão há trinta minutos por
        // escolha dele, e o relógio da clínica ainda nem começou.
        var agora = Marcado.AddMinutes(-10);

        ag.EsperaMinutos(agora).Should().Be(30, "ele está sentado ali há meia hora");
        ag.EsperaDaClinicaMinutos(agora).Should().Be(0, "mas a clínica não o fez esperar nada");
        ag.ChegouAdiantado.Should().BeTrue();
    }

    [Fact]
    public void Depois_da_hora_marcada_a_espera_passa_a_ser_da_clinica()
    {
        var ag = new Agendamento { DataHora = Marcado, ChegadaEm = Marcado.AddMinutes(-40) };

        ag.EsperaDaClinicaMinutos(Marcado.AddMinutes(15)).Should().Be(15);
    }

    [Fact]
    public void Quem_chega_atrasado_espera_a_partir_da_chegada()
    {
        var ag = new Agendamento { DataHora = Marcado, ChegadaEm = Marcado.AddMinutes(10) };

        // Marcado 14h, chegou 14h10, são 14h25: a clínica o fez esperar quinze minutos,
        // não vinte e cinco — o atraso dele não é conta da clínica.
        ag.EsperaDaClinicaMinutos(Marcado.AddMinutes(25)).Should().Be(15);
    }

    [Fact]
    public void A_espera_para_de_correr_quando_o_paciente_e_chamado()
    {
        var ag = new Agendamento
        {
            DataHora = Marcado,
            ChegadaEm = Marcado.AddMinutes(-5),
            InicioAtendimentoEm = Marcado.AddMinutes(20)
        };

        ag.EsperaDaClinicaMinutos(Marcado.AddHours(3)).Should().Be(20);
    }

    [Fact]
    public void Sem_check_in_nao_ha_espera_para_medir()
    {
        var ag = new Agendamento { DataHora = Marcado };

        ag.InicioDaEsperaDevida.Should().BeNull();
        ag.EsperaDaClinicaMinutos(Marcado.AddMinutes(30)).Should().BeNull();
    }

    // ===== O relógio da sessão =====

    [Fact]
    public void A_sessao_e_medida_do_inicio_REAL_e_nao_da_hora_marcada()
    {
        var ag = new Agendamento
        {
            DataHora = Marcado,
            DuracaoMinutos = 30,
            ChegadaEm = Marcado,
            // Começou vinte minutos atrasada.
            InicioAtendimentoEm = Marcado.AddMinutes(20)
        };

        // Às 14h40 ela está em curso há vinte minutos e cumprindo a duração à risca.
        // Medida pela hora marcada, apareceria como estourada — e o atraso, que já está
        // dito na espera, seria cobrado duas vezes.
        ag.DuracaoAtendimentoMinutos(Marcado.AddMinutes(40)).Should().Be(20);
        ag.SessaoPassouDoPrevisto(Marcado.AddMinutes(40)).Should().BeFalse();
    }

    [Fact]
    public void A_sessao_que_passa_da_duracao_prevista_e_acusada()
    {
        var ag = new Agendamento
        {
            DataHora = Marcado,
            DuracaoMinutos = 30,
            ChegadaEm = Marcado,
            InicioAtendimentoEm = Marcado
        };

        ag.SessaoPassouDoPrevisto(Marcado.AddMinutes(31)).Should().BeTrue();
    }

    [Fact]
    public void Sessao_que_nao_comecou_nao_estoura_duracao()
    {
        var ag = new Agendamento { DataHora = Marcado, ChegadaEm = Marcado };

        ag.FimPrevistoDaSessao.Should().BeNull();
        ag.SessaoPassouDoPrevisto(Marcado.AddHours(5)).Should().BeFalse();
    }

    // ===== O desfazer do balcão =====

    [Fact]
    public async Task Reabrir_devolve_a_falta_para_a_agenda()
    {
        var paciente = await CriarPacienteAsync();
        var ag = await AgendarAsync(paciente);

        await _agenda.MarcarFaltaAsync(ag.Id, "recepcao");
        var reaberto = await _agenda.ReabrirAsync(ag.Id, "recepcao");

        reaberto.Status.Should().Be(StatusAgendamento.Agendado);
        reaberto.Etapa.Should().Be(EtapaFila.Aguardando);
    }

    [Fact]
    public async Task Reabrir_preserva_os_carimbos_e_devolve_o_cartao_a_coluna_em_que_estava()
    {
        var paciente = await CriarPacienteAsync();
        var ag = await AgendarAsync(paciente);

        await _agenda.RegistrarChegadaAsync(ag.Id);
        await _agenda.MarcarFaltaAsync(ag.Id, "recepcao");

        var reaberto = await _agenda.ReabrirAsync(ag.Id, "recepcao");

        // Limpar a chegada seria uma segunda alteração silenciosa dentro de um comando
        // que o usuário pediu para DESFAZER: o paciente estava na recepção, e é para lá
        // que o cartão volta.
        reaberto.ChegadaEm.Should().NotBeNull();
        reaberto.Etapa.Should().Be(EtapaFila.Chegou);
    }

    [Fact]
    public async Task Reabrir_deixa_rastro_na_auditoria()
    {
        var paciente = await CriarPacienteAsync();
        var ag = await AgendarAsync(paciente);

        await _agenda.CancelarAsync(ag.Id, "recepcao");
        await _agenda.ReabrirAsync(ag.Id, "joana");

        var eventos = await _repo.EventosAuditoriaAsync();
        eventos.Should().Contain(e => e.Acao == "AgendamentoReaberto" && e.Operador == "joana");
    }

    [Fact]
    public async Task Horario_ja_em_aberto_nao_se_reabre()
    {
        var paciente = await CriarPacienteAsync();
        var ag = await AgendarAsync(paciente);

        var acao = () => _agenda.ReabrirAsync(ag.Id, "recepcao");

        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*já está em aberto*");
    }

    [Fact]
    public async Task Horario_que_virou_atendimento_nao_se_reabre_pela_fila()
    {
        var paciente = await CriarPacienteAsync();
        var ag = await AgendarAsync(paciente);

        await _agenda.ConfirmarPresencaAsync(ag.Id);

        // A guia já nasceu: reabrir aqui deixaria um atendimento faturado sem sessão na
        // agenda. O caminho é o estorno do atendimento.
        var acao = () => _agenda.ReabrirAsync(ag.Id, "recepcao");

        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Estorne o atendimento*");
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }
}
