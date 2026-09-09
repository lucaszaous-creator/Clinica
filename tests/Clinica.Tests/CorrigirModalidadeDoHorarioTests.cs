using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// CORRIGIR O QUE A SESSÃO É, e o circuito até a tela de quem atende (set/2026).
///
/// A importação do Smart Clinic trouxe todo horário como <b>"Consulta"</b> — o sistema
/// antigo não guardava mais que isso —, e "Consulta" não é nenhuma das modalidades que a
/// clínica atende. Enquanto o horário diz isso, o médico lê "Consulta" no dia dele.
///
/// O que estes testes prendem são as DUAS metades do pedido: a correção pelo balcão
/// (<c>RemarcarAsync</c> com a modalidade nova e a MESMA data e hora — o que o botão
/// "Editar" da agenda do dia faz) e a chegada dela ao <c>ConsultorioService</c>, que é a
/// lista do médico. Elo partido aqui não vira erro: vira uma tela que continua dizendo
/// "Consulta" depois de alguém ter corrigido, e ninguém sabe de quem é a culpa.
/// </summary>
public class CorrigirModalidadeDoHorarioTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly AgendaService _agenda;
    private readonly ConsultorioService _consultorio;

    private static readonly DateOnly Hoje = new(2026, 9, 9);
    private const string Consulta = nameof(ModalidadeAtendimento.Consulta);
    private const string AcupEletro = nameof(ModalidadeAtendimento.AcupunturaComEletro);
    private const string Psiquiatria = nameof(Especialidade.Psiquiatria);

    public CorrigirModalidadeDoHorarioTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _agenda = new AgendaService(_repo, new AtendimentoService(_repo));
        _consultorio = new ConsultorioService(_repo);

        // O catálogo como a clínica o tem: as embutidas mais a variante dela.
        CatalogoModalidades.Atualizar(
        [
            new EntradaModalidade(Consulta, "Consulta", ModalidadeAtendimento.Consulta, true),
            new EntradaModalidade(AcupEletro, "Acupuntura + eletro", ModalidadeAtendimento.AcupunturaComEletro, true),
            new EntradaModalidade("AcupDomiciliar", "Acupuntura (domiciliar)", ModalidadeAtendimento.AcupunturaComEletro, true)
        ]);
        CatalogoEspecialidades.Atualizar(
        [
            new EntradaEspecialidade(Psiquiatria, "Psiquiatria", true),
            new EntradaEspecialidade(nameof(Especialidade.Geriatria), "Geriatria", true)
        ]);
    }

    public void Dispose()
    {
        CatalogoModalidades.Atualizar([]);
        CatalogoEspecialidades.Atualizar([]);
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }

    // ---------------------------------------------------------------- apoio

    private async Task<int> CriarPacienteAsync(string nome = "Sueli")
    {
        var p = new Paciente { Nome = nome, Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    private async Task<int> CriarProfissionalAsync(string nome = "Dr Gustavo")
    {
        var p = new Profissional { Nome = nome };
        _db.Profissionais.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    /// <summary>Um horário como a importação o deixou: "Consulta", sem especialidade.</summary>
    private async Task<Agendamento> ImportadoAsync(int pacienteId, int profissionalId, int hora = 14)
    {
        var ag = new Agendamento
        {
            PacienteId = pacienteId,
            ProfissionalId = profissionalId,
            DataHora = Hoje.ToDateTime(new TimeOnly(hora, 0)),
            ModalidadePrevista = ModalidadeAtendimento.Consulta,
            ModalidadeCodigo = Consulta,
            Observacoes = "Importado do Smart Clinic · Consulta",
            ChaveImportacao = "IMPORT:smartclinic:9001",
            Status = StatusAgendamento.Agendado
        };
        _db.Agendamentos.Add(ag);
        await _db.SaveChangesAsync();
        return ag;
    }

    private async Task<string> ModalidadeQueOMedicoLeAsync(int agendamentoId, int profissionalId)
    {
        var dia = await _consultorio.DoDiaAsync(Hoje, profissionalId);
        return dia.Sessoes.Single(s => s.AgendamentoId == agendamentoId).Modalidade;
    }

    // ------------------------------------------------- a frase: o que a sessão É

    [Fact]
    public void Consulta_com_especialidade_diz_as_DUAS_coisas()
        => CatalogoModalidades.NomeComEspecialidade(Consulta, ModalidadeAtendimento.Consulta, Psiquiatria)
            .Should().Be("Consulta · Psiquiatria",
                "a especialidade é o que separa a consulta de psiquiatria da de geriatria");

    [Fact]
    public void Consulta_sem_especialidade_diz_so_a_modalidade()
        => CatalogoModalidades.NomeComEspecialidade(Consulta, ModalidadeAtendimento.Consulta, null)
            .Should().Be("Consulta");

    /// <summary>
    /// A especialidade só tem onde morar na consulta — <c>RemarcarAsync</c> a limpa nas
    /// outras. Escrevê-la ao lado de "Acupuntura" seria afirmar sobre a sessão algo que o
    /// horário não guarda.
    /// </summary>
    [Fact]
    public void Modalidade_que_nao_e_consulta_IGNORA_a_especialidade()
        => CatalogoModalidades.NomeComEspecialidade(
                AcupEletro, ModalidadeAtendimento.AcupunturaComEletro, Psiquiatria)
            .Should().Be("Acupuntura + eletro");

    /// <summary>
    /// A armadilha do <c>Base("")</c>, que já custou uma tela inteira (parcela 67): quem
    /// grava esses códigos normaliza "vale para a família" como STRING VAZIA, e o padrão
    /// do <c>Base</c> é acupuntura com eletro — a consulta deixaria de ser consulta e a
    /// especialidade sumiria da frase.
    /// </summary>
    [Fact]
    public void Codigo_em_branco_cai_na_FAMILIA_e_nao_no_padrao_do_Base()
    {
        CatalogoModalidades.NomeComEspecialidade("", ModalidadeAtendimento.Consulta, Psiquiatria)
            .Should().Be("Consulta · Psiquiatria");
        CatalogoModalidades.NomeComEspecialidade(null, ModalidadeAtendimento.Consulta, Psiquiatria)
            .Should().Be("Consulta · Psiquiatria");
    }

    /// <summary>Especialidade que não está no catálogo sai pelo que está gravado, nunca em branco.</summary>
    [Fact]
    public void Especialidade_fora_do_catalogo_sai_pelo_codigo_gravado()
        => CatalogoModalidades.NomeComEspecialidade(Consulta, ModalidadeAtendimento.Consulta, "Dermato")
            .Should().Be("Consulta · Dermato");

    // ------------------------------------------------- o circuito: balcão → médico

    /// <summary>
    /// O caminho do botão "Editar" da agenda do dia: mesma data, mesma hora, modalidade
    /// nova. É a correção que a secretária faz com o paciente na frente dela.
    /// </summary>
    [Fact]
    public async Task Corrigir_a_modalidade_do_importado_chega_ao_dia_do_medico()
    {
        var pacienteId = await CriarPacienteAsync();
        var profissionalId = await CriarProfissionalAsync();
        var ag = await ImportadoAsync(pacienteId, profissionalId);

        (await ModalidadeQueOMedicoLeAsync(ag.Id, profissionalId))
            .Should().Be("Consulta", "é assim que a importação deixou o horário");

        await _agenda.RemarcarAsync(
            ag.Id, ag.DataHora, ag.Observacoes,
            modalidadeCodigo: AcupEletro, operador: "Recepção");

        (await ModalidadeQueOMedicoLeAsync(ag.Id, profissionalId))
            .Should().Be("Acupuntura + eletro",
                "o balcão corrigiu, e é na lista do médico que a correção precisa aparecer");
    }

    /// <summary>
    /// Corrigir SÓ a especialidade: a modalidade continua sendo consulta, e o que muda é
    /// justamente o que o médico não via — o horário já a gravava e nenhuma tela dele a
    /// lia.
    /// </summary>
    [Fact]
    public async Task Corrigir_so_a_especialidade_tambem_chega_ao_dia_do_medico()
    {
        var pacienteId = await CriarPacienteAsync();
        var profissionalId = await CriarProfissionalAsync();
        var ag = await ImportadoAsync(pacienteId, profissionalId);

        await _agenda.RemarcarAsync(
            ag.Id, ag.DataHora, ag.Observacoes,
            modalidadeCodigo: Consulta, especialidadeConsultaCodigo: Psiquiatria,
            operador: "Recepção");

        (await ModalidadeQueOMedicoLeAsync(ag.Id, profissionalId))
            .Should().Be("Consulta · Psiquiatria");
    }

    /// <summary>
    /// Corrigir a modalidade NÃO é remarcar: a data não mudou, então os carimbos da fila
    /// ficam. Sem isto, editar o horário de quem já está no balcão o mandaria de volta
    /// para "Aguardando" — e a espera dele recomeçaria do zero.
    /// </summary>
    [Fact]
    public async Task Corrigir_no_MESMO_horario_preserva_o_check_in_do_paciente()
    {
        var pacienteId = await CriarPacienteAsync();
        var profissionalId = await CriarProfissionalAsync();
        var ag = await ImportadoAsync(pacienteId, profissionalId);

        var chegada = Hoje.ToDateTime(new TimeOnly(13, 45));
        ag.ChegadaEm = chegada;
        await _db.SaveChangesAsync();

        await _agenda.RemarcarAsync(
            ag.Id, ag.DataHora, ag.Observacoes,
            modalidadeCodigo: AcupEletro, operador: "Recepção");

        var depois = await _agenda.ObterAsync(ag.Id);
        depois!.ChegadaEm.Should().Be(chegada, "quem corrige a modalidade não mudou o horário de dia");
        depois.Status.Should().Be(StatusAgendamento.Agendado);
    }

    /// <summary>
    /// A observação da importação é o que diz DE ONDE o horário veio — e é por ela que a
    /// clínica reconhece o que ainda falta corrigir. Editar não pode levá-la embora.
    /// </summary>
    [Fact]
    public async Task Corrigir_preserva_a_procedencia_escrita_na_observacao()
    {
        var pacienteId = await CriarPacienteAsync();
        var profissionalId = await CriarProfissionalAsync();
        var ag = await ImportadoAsync(pacienteId, profissionalId);

        await _agenda.RemarcarAsync(
            ag.Id, ag.DataHora, ag.Observacoes,
            modalidadeCodigo: AcupEletro, operador: "Recepção");

        var depois = await _agenda.ObterAsync(ag.Id);
        depois!.Observacoes.Should().Be("Importado do Smart Clinic · Consulta");
        depois.ChaveImportacao.Should().Be("IMPORT:smartclinic:9001");
    }
}
