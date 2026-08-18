using Clinica.Application.Modelos;
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
/// Agenda multiprofissional e fila em kanban (parcela 1).
///
/// Duas regras carregam esta suíte:
/// 1. o choque é por INTERVALO e por RECURSO (profissional/sala) — a comparação antiga,
///    de igualdade de horário, deixava passar a sessão que invade a seguinte;
/// 2. quem não informa profissional nem sala (o faturamento) enxerga o comportamento
///    de sempre — nada é barrado.
/// </summary>
public class AgendaMultiprofissionalTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly AgendaService _agenda;
    private readonly EquipeService _equipe;

    private static readonly DateTime Manha = new(2026, 8, 3, 9, 0, 0);

    public AgendaMultiprofissionalTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _agenda = new AgendaService(_repo, new AtendimentoService(_repo));
        _equipe = new EquipeService(_repo);
    }

    private async Task<int> CriarPacienteAsync(string nome = "Paciente")
    {
        var p = new Paciente { Nome = nome, Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    private Task<Profissional> CriarProfissionalAsync(string nome = "Ana", int? duracao = null)
        => _equipe.SalvarProfissionalAsync(new Profissional { Nome = nome, DuracaoPadraoMinutos = duracao });

    private Task<Sala> CriarSalaAsync(string nome = "Consultório 1", int capacidade = 1)
        => _equipe.SalvarSalaAsync(new Sala { Nome = nome, Capacidade = capacidade });

    // ===== Choque por recurso =====

    [Fact]
    public async Task Agendar_MesmoProfissionalNoMesmoHorario_Recusa()
    {
        var prof = await CriarProfissionalAsync();
        await _agenda.AgendarAsync(await CriarPacienteAsync("Ana Paciente"), Manha,
            ModalidadeAtendimento.AcupunturaSimples, null, profissionalId: prof.Id);
        var outroId = await CriarPacienteAsync("Outro");

        var acao = () => _agenda.AgendarAsync(outroId, Manha,
            ModalidadeAtendimento.AcupunturaSimples, null, profissionalId: prof.Id);

        await acao.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Agendar_HorarioQueInvadeAAnterior_Recusa()
    {
        var prof = await CriarProfissionalAsync(duracao: 60);
        await _agenda.AgendarAsync(await CriarPacienteAsync("Primeiro"), Manha,
            ModalidadeAtendimento.AcupunturaSimples, null, profissionalId: prof.Id);

        var segundoId = await CriarPacienteAsync("Segundo");

        // 09:30 cai DENTRO da sessão de 60 min que começou às 09:00 — a comparação
        // antiga, por igualdade de horário, deixaria passar.
        var acao = () => _agenda.AgendarAsync(segundoId,
            Manha.AddMinutes(30), ModalidadeAtendimento.AcupunturaSimples, null,
            profissionalId: prof.Id);

        await acao.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Agendar_ProfissionaisDiferentesNoMesmoHorario_Permite()
    {
        var ana = await CriarProfissionalAsync("Ana");
        var bruno = await CriarProfissionalAsync("Bruno");

        await _agenda.AgendarAsync(await CriarPacienteAsync("Um"), Manha,
            ModalidadeAtendimento.AcupunturaSimples, null, profissionalId: ana.Id);
        await _agenda.AgendarAsync(await CriarPacienteAsync("Dois"), Manha,
            ModalidadeAtendimento.AcupunturaSimples, null, profissionalId: bruno.Id);

        var doDia = await _agenda.DoDiaAsync(DateOnly.FromDateTime(Manha));
        doDia.Should().HaveCount(2, "dois consultórios atendendo ao mesmo tempo é o normal da clínica");
    }

    [Fact]
    public async Task Agendar_MesmaSalaAlemDaCapacidade_Recusa()
    {
        var ana = await CriarProfissionalAsync("Ana");
        var bruno = await CriarProfissionalAsync("Bruno");
        var sala = await CriarSalaAsync(capacidade: 1);

        await _agenda.AgendarAsync(await CriarPacienteAsync("Um"), Manha,
            ModalidadeAtendimento.AcupunturaSimples, null, profissionalId: ana.Id, salaId: sala.Id);

        var doisId = await CriarPacienteAsync("Dois");

        var acao = () => _agenda.AgendarAsync(doisId, Manha,
            ModalidadeAtendimento.AcupunturaSimples, null, profissionalId: bruno.Id, salaId: sala.Id);

        await acao.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Agendar_SalaComDuasMacas_PermiteDoisNoMesmoHorario()
    {
        var ana = await CriarProfissionalAsync("Ana");
        var bruno = await CriarProfissionalAsync("Bruno");
        var sala = await CriarSalaAsync("Sala coletiva", capacidade: 2);

        await _agenda.AgendarAsync(await CriarPacienteAsync("Um"), Manha,
            ModalidadeAtendimento.AcupunturaSimples, null, profissionalId: ana.Id, salaId: sala.Id);
        await _agenda.AgendarAsync(await CriarPacienteAsync("Dois"), Manha,
            ModalidadeAtendimento.AcupunturaSimples, null, profissionalId: bruno.Id, salaId: sala.Id);

        (await _agenda.DoDiaAsync(DateOnly.FromDateTime(Manha))).Should().HaveCount(2);
    }

    [Fact]
    public async Task Agendar_SemProfissionalNemSala_NuncaEhBarrado()
    {
        // É o caminho do faturamento: ele avisa na tela e marca assim mesmo.
        await _agenda.AgendarAsync(await CriarPacienteAsync("Um"), Manha,
            ModalidadeAtendimento.AcupunturaSimples, null);
        await _agenda.AgendarAsync(await CriarPacienteAsync("Dois"), Manha,
            ModalidadeAtendimento.AcupunturaSimples, null);

        (await _agenda.DoDiaAsync(DateOnly.FromDateTime(Manha))).Should().HaveCount(2);
    }

    [Fact]
    public async Task Agendar_ComEncaixe_PassaPorCimaDoChoque()
    {
        var prof = await CriarProfissionalAsync();
        await _agenda.AgendarAsync(await CriarPacienteAsync("Primeiro"), Manha,
            ModalidadeAtendimento.AcupunturaSimples, null, profissionalId: prof.Id);

        var encaixado = await _agenda.AgendarAsync(await CriarPacienteAsync("Encaixado"), Manha,
            ModalidadeAtendimento.AcupunturaSimples, null, profissionalId: prof.Id, encaixe: true);

        encaixado.Encaixe.Should().BeTrue("o encaixe fica registrado, não vira choque silencioso");
        (await _agenda.DoDiaAsync(DateOnly.FromDateTime(Manha))).Should().HaveCount(2);
    }

    [Fact]
    public async Task Agendar_SobreHorarioCancelado_NaoAcusaChoque()
    {
        var prof = await CriarProfissionalAsync();
        var ag = await _agenda.AgendarAsync(await CriarPacienteAsync("Desistiu"), Manha,
            ModalidadeAtendimento.AcupunturaSimples, null, profissionalId: prof.Id);
        await _agenda.CancelarAsync(ag.Id);

        var novo = await _agenda.AgendarAsync(await CriarPacienteAsync("Entrou no lugar"), Manha,
            ModalidadeAtendimento.AcupunturaSimples, null, profissionalId: prof.Id);

        novo.Id.Should().NotBe(ag.Id);
        novo.Encaixe.Should().BeFalse("o horário estava livre — cancelado não ocupa agenda");
    }

    [Fact]
    public async Task Conflitos_ChoqueDoProprioPaciente_NaoImpedeMarcar()
    {
        var ana = await CriarProfissionalAsync("Ana");
        var bruno = await CriarProfissionalAsync("Bruno");
        var pacienteId = await CriarPacienteAsync();

        await _agenda.AgendarAsync(pacienteId, Manha,
            ModalidadeAtendimento.AcupunturaSimples, null, profissionalId: ana.Id);

        // Dois procedimentos seguidos no mesmo horário com profissionais diferentes é
        // improvável, mas é decisão da recepção — o sistema avisa e deixa marcar.
        var conflitos = await _agenda.ConflitosAsync(
            Manha, profissionalId: bruno.Id, pacienteId: pacienteId);

        conflitos.Should().ContainSingle().Which.Recurso.Should().Be(RecursoAgenda.Paciente);

        var acao = () => _agenda.AgendarAsync(pacienteId, Manha,
            ModalidadeAtendimento.AcupunturaSimples, null, profissionalId: bruno.Id);
        await acao.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Remarcar_SemInformarRecursos_PreservaProfissionalESala()
    {
        var prof = await CriarProfissionalAsync();
        var sala = await CriarSalaAsync();
        var ag = await _agenda.AgendarAsync(await CriarPacienteAsync(), Manha,
            ModalidadeAtendimento.AcupunturaSimples, null,
            profissionalId: prof.Id, salaId: sala.Id, duracaoMinutos: 45);

        // É como o faturamento remarca: ele não conhece profissional nem sala.
        await _agenda.RemarcarAsync(ag.Id, Manha.AddHours(2), null);

        var atualizado = await _db.Agendamentos.AsNoTracking().FirstAsync(a => a.Id == ag.Id);
        atualizado.ProfissionalId.Should().Be(prof.Id);
        atualizado.SalaId.Should().Be(sala.Id);
        atualizado.DuracaoMinutos.Should().Be(45);
        atualizado.DataHora.Should().Be(Manha.AddHours(2));
    }

    [Fact]
    public async Task Remarcar_ParaHorarioOcupadoDoMesmoProfissional_Recusa()
    {
        var prof = await CriarProfissionalAsync();
        await _agenda.AgendarAsync(await CriarPacienteAsync("Fixo"), Manha,
            ModalidadeAtendimento.AcupunturaSimples, null, profissionalId: prof.Id);
        var movel = await _agenda.AgendarAsync(await CriarPacienteAsync("Móvel"), Manha.AddHours(3),
            ModalidadeAtendimento.AcupunturaSimples, null, profissionalId: prof.Id);

        var acao = () => _agenda.RemarcarAsync(movel.Id, Manha, null);

        await acao.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task DuracaoEfetiva_SegueOProfissionalQuandoOHorarioNaoDiz()
    {
        var prof = await CriarProfissionalAsync(duracao: 50);
        var ag = await _agenda.AgendarAsync(await CriarPacienteAsync(), Manha,
            ModalidadeAtendimento.AcupunturaSimples, null, profissionalId: prof.Id);

        var lido = await _agenda.ObterAsync(ag.Id);

        lido!.DuracaoEfetiva.Should().Be(50);
        lido.FimPrevisto.Should().Be(Manha.AddMinutes(50));
    }

    [Fact]
    public async Task Ocupacao_AgrupaPorProfissionalESomaOsMinutos()
    {
        var ana = await CriarProfissionalAsync("Ana", duracao: 30);
        await _agenda.AgendarAsync(await CriarPacienteAsync("Um"), Manha,
            ModalidadeAtendimento.AcupunturaSimples, null, profissionalId: ana.Id);
        await _agenda.AgendarAsync(await CriarPacienteAsync("Dois"), Manha.AddHours(1),
            ModalidadeAtendimento.AcupunturaSimples, null, profissionalId: ana.Id);
        // Horário sem profissional: vira a faixa "Sem profissional", no fim da lista.
        await _agenda.AgendarAsync(await CriarPacienteAsync("Três"), Manha.AddHours(2),
            ModalidadeAtendimento.AcupunturaSimples, null);

        var ocupacao = await _agenda.OcupacaoDoDiaAsync(DateOnly.FromDateTime(Manha));

        ocupacao.Should().HaveCount(2);
        ocupacao[0].Nome.Should().Be("Ana");
        ocupacao[0].Total.Should().Be(2);
        ocupacao[0].MinutosOcupados.Should().Be(60);
        ocupacao[1].ProfissionalId.Should().BeNull();
    }

    // ===== Fila em kanban =====

    [Fact]
    public async Task Etapa_ComecaEmAguardandoEAvancaComOsCarimbos()
    {
        var ag = await _agenda.AgendarAsync(await CriarPacienteAsync(), Manha,
            ModalidadeAtendimento.AcupunturaSimples, null);

        (await _agenda.ObterAsync(ag.Id))!.Etapa.Should().Be(EtapaFila.Aguardando);

        await _agenda.RegistrarChegadaAsync(ag.Id, "teste", Manha);
        (await _agenda.ObterAsync(ag.Id))!.Etapa.Should().Be(EtapaFila.Chegou);

        await _agenda.IniciarAtendimentoAsync(ag.Id, "teste", Manha.AddMinutes(12));
        (await _agenda.ObterAsync(ag.Id))!.Etapa.Should().Be(EtapaFila.EmAtendimento);

        await _agenda.ConfirmarPresencaAsync(ag.Id);
        (await _agenda.ObterAsync(ag.Id))!.Etapa.Should().Be(EtapaFila.Finalizado);
    }

    [Fact]
    public async Task Espera_ContaDaChegadaAteSerChamado()
    {
        var ag = await _agenda.AgendarAsync(await CriarPacienteAsync(), Manha,
            ModalidadeAtendimento.AcupunturaSimples, null);

        await _agenda.RegistrarChegadaAsync(ag.Id, "teste", Manha);
        await _agenda.IniciarAtendimentoAsync(ag.Id, "teste", Manha.AddMinutes(18));

        var lido = await _agenda.ObterAsync(ag.Id);
        // Depois de chamado a espera CONGELA: não continua correndo com o relógio.
        lido!.EsperaMinutos(Manha.AddHours(5)).Should().Be(18);
    }

    [Fact]
    public async Task Espera_SemCheckIn_EhNula()
    {
        var ag = await _agenda.AgendarAsync(await CriarPacienteAsync(), Manha,
            ModalidadeAtendimento.AcupunturaSimples, null);

        (await _agenda.ObterAsync(ag.Id))!.EsperaMinutos(Manha.AddHours(1)).Should().BeNull(
            "quem não fez check-in não tem espera para medir — zero mentiria na média");
    }

    [Fact]
    public async Task Chamar_SemPassarPeloCheckIn_CarimbaAChegadaTambem()
    {
        var ag = await _agenda.AgendarAsync(await CriarPacienteAsync(), Manha,
            ModalidadeAtendimento.AcupunturaSimples, null);

        await _agenda.IniciarAtendimentoAsync(ag.Id, "teste", Manha);

        var lido = await _agenda.ObterAsync(ag.Id);
        lido!.ChegadaEm.Should().NotBeNull();
        lido.EsperaMinutos(Manha.AddHours(1)).Should().Be(0);
    }

    [Fact]
    public async Task VoltarEtapa_DesfazUmPassoPorVez()
    {
        var ag = await _agenda.AgendarAsync(await CriarPacienteAsync(), Manha,
            ModalidadeAtendimento.AcupunturaSimples, null);
        await _agenda.RegistrarChegadaAsync(ag.Id, "teste", Manha);
        await _agenda.IniciarAtendimentoAsync(ag.Id, "teste", Manha.AddMinutes(5));

        // A parcela 38 pôs "Chamado" entre "Chegou" e "Em atendimento": quem entrou na
        // sala foi chamado antes, e voltar uma etapa devolve o cartão para a chamada —
        // não direto para a sala de espera.
        await _agenda.VoltarEtapaAsync(ag.Id, "teste");
        (await _agenda.ObterAsync(ag.Id))!.Etapa.Should().Be(EtapaFila.Chamado);

        await _agenda.VoltarEtapaAsync(ag.Id, "teste");
        (await _agenda.ObterAsync(ag.Id))!.Etapa.Should().Be(EtapaFila.Chegou);

        await _agenda.VoltarEtapaAsync(ag.Id, "teste");
        (await _agenda.ObterAsync(ag.Id))!.Etapa.Should().Be(EtapaFila.Aguardando);

        var acao = () => _agenda.VoltarEtapaAsync(ag.Id, "teste");
        await acao.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task VoltarEtapa_DepoisDeConcluir_Recusa()
    {
        var ag = await _agenda.AgendarAsync(await CriarPacienteAsync(), Manha,
            ModalidadeAtendimento.AcupunturaSimples, null);
        await _agenda.IniciarAtendimentoAsync(ag.Id, "teste", Manha);
        await _agenda.ConfirmarPresencaAsync(ag.Id);

        var acao = () => _agenda.VoltarEtapaAsync(ag.Id, "teste");

        await acao.Should().ThrowAsync<InvalidOperationException>(
            "o atendimento e os códigos já existem — o caminho é estornar");
    }

    /// <summary>
    /// Confirmar presença não cria horário nenhum — nem herdando profissional e sala.
    ///
    /// O teste antigo fixava a herança do "retorno sugerido" do 2º código. Ele deixou de
    /// existir na parcela 58: guia não é atendimento, e materializar a pendência como
    /// horário punha na agenda do profissional um paciente que não vem.
    /// </summary>
    [Fact]
    public async Task ConfirmarPresenca_NaoOcupaAAgendaDoProfissionalComOSegundoCodigo()
    {
        var prof = await CriarProfissionalAsync();
        var sala = await CriarSalaAsync();
        var ag = await _agenda.AgendarAsync(await CriarPacienteAsync(), Manha,
            ModalidadeAtendimento.AcupunturaComEletro, null,
            profissionalId: prof.Id, salaId: sala.Id);

        await _agenda.ConfirmarPresencaAsync(ag.Id);

        (await _db.Agendamentos.AsNoTracking().CountAsync()).Should().Be(1,
            "a agenda do profissional não recebe horário por causa de uma guia pendente");
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
