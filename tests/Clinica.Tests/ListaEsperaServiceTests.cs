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
/// Lista de espera e painel da recepção (parcela 1).
///
/// A lista existe pelo mesmo motivo do dashboard de pendências: o que a recepção guarda
/// só na cabeça, se perde. O teste que mais importa é o do <c>CandidatosPara</c> — é ele
/// que responde "quem eu chamo para este buraco" sem a secretária ligar para todo mundo.
/// </summary>
public class ListaEsperaServiceTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly AgendaService _agenda;
    private readonly EquipeService _equipe;
    private readonly ListaEsperaService _espera;
    private readonly PainelRecepcaoService _painel;

    private static readonly DateTime Manha = new(2026, 8, 3, 9, 0, 0);
    private static readonly DateTime Tarde = new(2026, 8, 3, 15, 0, 0);
    private static readonly DateOnly Dia = new(2026, 8, 3);

    public ListaEsperaServiceTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _agenda = new AgendaService(_repo, new AtendimentoService(_repo));
        _equipe = new EquipeService(_repo);
        _espera = new ListaEsperaService(_repo, _agenda);
        _painel = new PainelRecepcaoService(_repo, _agenda, new PendenciaService(_repo));
    }

    private async Task<int> CriarPacienteAsync(string nome = "Paciente")
    {
        var p = new Paciente { Nome = nome, Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    // ===== Entrada na lista =====

    [Fact]
    public async Task Adicionar_ColocaNaFilaAguardando()
    {
        var pacienteId = await CriarPacienteAsync();

        await _espera.AdicionarAsync(pacienteId, periodo: PeriodoPreferido.Manha);

        var fila = await _espera.AguardandoAsync();
        fila.Should().ContainSingle();
        fila[0].Status.Should().Be(StatusListaEspera.Aguardando);
        fila[0].Paciente!.Nome.Should().Be("Paciente");
    }

    [Fact]
    public async Task Adicionar_PacienteInexistente_Falha()
    {
        var acao = () => _espera.AdicionarAsync(9999);
        await acao.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Adicionar_DuasVezesParaOMesmoProfissional_Falha()
    {
        var pacienteId = await CriarPacienteAsync();
        await _espera.AdicionarAsync(pacienteId);

        var acao = () => _espera.AdicionarAsync(pacienteId);

        await acao.Should().ThrowAsync<InvalidOperationException>(
            "pedido repetido é erro de digitação, não urgência dobrada");
    }

    [Fact]
    public async Task Adicionar_JanelaInvertida_Falha()
    {
        var pacienteId = await CriarPacienteAsync();

        var acao = () => _espera.AdicionarAsync(pacienteId,
            disponivelDe: new DateOnly(2026, 8, 10),
            disponivelAte: new DateOnly(2026, 8, 5));

        await acao.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Aguardando_TrazPrioritarioPrimeiro()
    {
        var comum = await CriarPacienteAsync("Comum");
        var urgente = await CriarPacienteAsync("Urgente");

        await _espera.AdicionarAsync(comum);
        await _espera.AdicionarAsync(urgente, prioritario: true);

        var fila = await _espera.AguardandoAsync();
        fila[0].Paciente!.Nome.Should().Be("Urgente",
            "urgência passa na frente da ordem de chegada");
    }

    // ===== Quem serve para o horário que vagou =====

    [Fact]
    public async Task CandidatosPara_RespeitamOTurno()
    {
        var manha = await CriarPacienteAsync("Só de manhã");
        var tarde = await CriarPacienteAsync("Só à tarde");
        await _espera.AdicionarAsync(manha, periodo: PeriodoPreferido.Manha);
        await _espera.AdicionarAsync(tarde, periodo: PeriodoPreferido.Tarde);

        var paraManha = await _espera.CandidatosParaAsync(Manha);
        var paraTarde = await _espera.CandidatosParaAsync(Tarde);

        paraManha.Should().ContainSingle().Which.Paciente!.Nome.Should().Be("Só de manhã");
        paraTarde.Should().ContainSingle().Which.Paciente!.Nome.Should().Be("Só à tarde");
    }

    [Fact]
    public async Task CandidatosPara_RespeitamAJanelaDeDatas()
    {
        var pacienteId = await CriarPacienteAsync("Viajando");
        await _espera.AdicionarAsync(pacienteId, disponivelDe: new DateOnly(2026, 8, 20));

        (await _espera.CandidatosParaAsync(Manha)).Should().BeEmpty(
            "ele só pode vir a partir do dia 20");
        (await _espera.CandidatosParaAsync(new DateTime(2026, 8, 21, 9, 0, 0)))
            .Should().ContainSingle();
    }

    [Fact]
    public async Task CandidatosPara_QuemNaoEscolheuProfissionalServeParaTodos()
    {
        var ana = await _equipe.SalvarProfissionalAsync(new Profissional { Nome = "Ana" });
        var bruno = await _equipe.SalvarProfissionalAsync(new Profissional { Nome = "Bruno" });

        var qualquer = await CriarPacienteAsync("Tanto faz");
        var soAna = await CriarPacienteAsync("Só com a Ana");
        await _espera.AdicionarAsync(qualquer);
        await _espera.AdicionarAsync(soAna, profissionalId: ana.Id);

        var paraBruno = await _espera.CandidatosParaAsync(Manha, bruno.Id);

        paraBruno.Should().ContainSingle().Which.Paciente!.Nome.Should().Be("Tanto faz");
    }

    // ===== Chamar =====

    [Fact]
    public async Task Chamar_CriaOHorarioEFechaOPedido()
    {
        var prof = await _equipe.SalvarProfissionalAsync(new Profissional { Nome = "Ana" });
        var pacienteId = await CriarPacienteAsync();
        var pedido = await _espera.AdicionarAsync(pacienteId, profissionalId: prof.Id);

        var agendamento = await _espera.ChamarAsync(
            pedido.Id, Manha, ModalidadeAtendimento.AcupunturaSimples);

        agendamento.Origem.Should().Be(OrigemAgendamento.ListaEspera);
        agendamento.ProfissionalId.Should().Be(prof.Id, "o profissional pedido é herdado");

        var fechado = await _db.ListaEspera.AsNoTracking().FirstAsync(l => l.Id == pedido.Id);
        fechado.Status.Should().Be(StatusListaEspera.Agendado);
        fechado.AgendamentoId.Should().Be(agendamento.Id);
        (await _espera.AguardandoAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Chamar_DuasVezes_Falha()
    {
        var pacienteId = await CriarPacienteAsync();
        var pedido = await _espera.AdicionarAsync(pacienteId);
        await _espera.ChamarAsync(pedido.Id, Manha, ModalidadeAtendimento.AcupunturaSimples);

        var acao = () => _espera.ChamarAsync(pedido.Id, Tarde, ModalidadeAtendimento.AcupunturaSimples);

        await acao.Should().ThrowAsync<InvalidOperationException>(
            "duas secretárias chamando o mesmo paciente: a segunda para aqui");
    }

    [Fact]
    public async Task Chamar_ParaHorarioOcupado_RecusaSemEncaixe()
    {
        var prof = await _equipe.SalvarProfissionalAsync(new Profissional { Nome = "Ana" });
        var jaMarcado = await CriarPacienteAsync("Já marcado");
        await _agenda.AgendarAsync(jaMarcado, Manha,
            ModalidadeAtendimento.AcupunturaSimples, null, profissionalId: prof.Id);

        var naFila = await CriarPacienteAsync("Na fila");
        var pedido = await _espera.AdicionarAsync(naFila, profissionalId: prof.Id);

        var acao = () => _espera.ChamarAsync(pedido.Id, Manha, ModalidadeAtendimento.AcupunturaSimples);
        await acao.Should().ThrowAsync<InvalidOperationException>();

        // O pedido continua aberto: uma chamada que falhou não pode sumir da lista.
        var intacto = await _db.ListaEspera.AsNoTracking().FirstAsync(l => l.Id == pedido.Id);
        intacto.Status.Should().Be(StatusListaEspera.Aguardando);
    }

    [Fact]
    public async Task Remover_TiraDaFilaEGuardaOMotivo()
    {
        var pacienteId = await CriarPacienteAsync();
        var pedido = await _espera.AdicionarAsync(pacienteId);

        await _espera.RemoverAsync(pedido.Id, "resolveu em outro lugar");

        (await _espera.AguardandoAsync()).Should().BeEmpty();
        var removido = await _db.ListaEspera.AsNoTracking().FirstAsync(l => l.Id == pedido.Id);
        removido.Status.Should().Be(StatusListaEspera.Removido);
        removido.Observacoes.Should().Contain("resolveu em outro lugar");
        removido.ResolvidoEm.Should().NotBeNull();
    }

    [Fact]
    public async Task Reabrir_DevolveOPedidoParaAFila()
    {
        var pacienteId = await CriarPacienteAsync();
        var pedido = await _espera.AdicionarAsync(pacienteId);
        await _espera.RemoverAsync(pedido.Id);

        await _espera.ReabrirAsync(pedido.Id);

        (await _espera.AguardandoAsync()).Should().ContainSingle();
    }

    // ===== Painel da recepção =====

    [Fact]
    public async Task Resumo_ContaCadaEtapaDaFila()
    {
        var prof = await _equipe.SalvarProfissionalAsync(new Profissional { Nome = "Ana", DuracaoPadraoMinutos = 30 });

        await _agenda.AgendarAsync(await CriarPacienteAsync("Marcado"), Manha,
            ModalidadeAtendimento.AcupunturaSimples, null, profissionalId: prof.Id);
        var chegou = await _agenda.AgendarAsync(await CriarPacienteAsync("Chegou"), Manha.AddHours(1),
            ModalidadeAtendimento.AcupunturaSimples, null, profissionalId: prof.Id);
        var faltou = await _agenda.AgendarAsync(await CriarPacienteAsync("Faltou"), Manha.AddHours(2),
            ModalidadeAtendimento.AcupunturaSimples, null, profissionalId: prof.Id);

        await _agenda.RegistrarChegadaAsync(chegou.Id, "teste", Manha.AddHours(1));
        await _agenda.MarcarFaltaAsync(faltou.Id);
        await _espera.AdicionarAsync(await CriarPacienteAsync("Na espera"));

        var resumo = await _painel.ResumoAsync(Dia, Manha.AddHours(1).AddMinutes(20));

        resumo.Aguardando.Should().Be(1);
        resumo.NaRecepcao.Should().Be(1);
        resumo.EmAtendimento.Should().Be(0);
        resumo.Faltas.Should().Be(1);
        resumo.Agendados.Should().Be(2, "faltou não ocupa mais a agenda");
        resumo.NaListaDeEspera.Should().Be(1);
        resumo.EsperaMediaMinutos.Should().Be(20,
            "só quem fez check-in entra na média — zero de quem não chegou puxaria para baixo");
    }

    [Fact]
    public async Task Resumo_TaxaDeFaltaOlhaSoOsHorariosQueFecharam()
    {
        var atendido = await _agenda.AgendarAsync(await CriarPacienteAsync("Atendido"), Manha,
            ModalidadeAtendimento.AcupunturaSimples, null);
        var faltou = await _agenda.AgendarAsync(await CriarPacienteAsync("Faltou"), Manha.AddHours(1),
            ModalidadeAtendimento.AcupunturaSimples, null);
        // Ainda aguardando: não entra na conta — o dia não acabou.
        await _agenda.AgendarAsync(await CriarPacienteAsync("Vem depois"), Manha.AddHours(5),
            ModalidadeAtendimento.AcupunturaSimples, null);

        await _agenda.ConfirmarPresencaAsync(atendido.Id);
        await _agenda.MarcarFaltaAsync(faltou.Id);

        var resumo = await _painel.ResumoAsync(Dia, Manha.AddHours(2));

        resumo.TaxaFaltaPercentual.Should().Be(50);
    }

    [Fact]
    public async Task PendenciasDoDia_SoTrazemPacientesQueVemHoje()
    {
        var vemHoje = await CriarPacienteAsync("Vem hoje");
        var naoVem = await CriarPacienteAsync("Não vem");

        var atendimentos = new AtendimentoService(_repo);
        // Um atendimento no passado deixa código pendente para os dois pacientes.
        await atendimentos.LancarAsync(vemHoje, Dia.AddDays(-5), ModalidadeAtendimento.AcupunturaComEletro, null);
        await atendimentos.LancarAsync(naoVem, Dia.AddDays(-5), ModalidadeAtendimento.AcupunturaComEletro, null);

        await _agenda.AgendarAsync(vemHoje, Manha, ModalidadeAtendimento.AcupunturaSimples, null);

        var pendencias = await _painel.PendenciasDoDiaAsync(Dia);

        pendencias.Should().NotBeEmpty();
        pendencias.Should().OnlyContain(p => p.PacienteId == vemHoje,
            "o recorte existe para cobrar quem está no balcão agora");
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
