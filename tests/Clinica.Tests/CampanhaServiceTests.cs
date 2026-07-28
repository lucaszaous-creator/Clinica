using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Tests;

/// <summary>
/// Campanhas: confirmação de sessão, NPS e recall (parcela 5 — features 11 e 02).
///
/// As regras que carregam a parcela:
/// 1. rodar a campanha duas vezes NÃO duplica ninguém (a origem é a chave do fato);
/// 2. NPS e recall exigem consentimento de comunicação; confirmar a própria sessão, não;
/// 3. quem fica de fora aparece CONTADO no resultado — campanha que filtra em silêncio
///    parece quebrada.
/// </summary>
public class CampanhaServiceTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly CampanhaService _campanhas;
    private readonly ConsentimentoService _consentimentos;

    private static readonly DateOnly Hoje = new(2026, 8, 10);

    public CampanhaServiceTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _campanhas = new CampanhaService(_repo);
        _consentimentos = new ConsentimentoService(_repo);
    }

    private async Task<Paciente> CriarPacienteAsync(string nome = "Maria Souza", string? telefone = "11988887777")
    {
        var p = new Paciente
        {
            Nome = nome,
            Telefone = telefone,
            Convenio = Convenio.UnimedIntercambio,
            Sexo = Sexo.Feminino
        };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p;
    }

    private async Task<Agendamento> MarcarAsync(
        int pacienteId, DateOnly dia, StatusAgendamento status = StatusAgendamento.Agendado,
        int? atendimentoId = null)
    {
        var ag = new Agendamento
        {
            PacienteId = pacienteId,
            DataHora = dia.ToDateTime(new TimeOnly(14, 0)),
            ModalidadePrevista = ModalidadeAtendimento.Acupuntura,
            Status = status,
            AtendimentoId = atendimentoId
        };
        _db.Agendamentos.Add(ag);
        await _db.SaveChangesAsync();
        return ag;
    }

    private async Task<Atendimento> AtenderAsync(int pacienteId, DateOnly dia)
    {
        var a = new Atendimento
        {
            PacienteId = pacienteId,
            Data = dia,
            Modalidade = ModalidadeAtendimento.Acupuntura
        };
        _db.Atendimentos.Add(a);
        await _db.SaveChangesAsync();
        return a;
    }

    private Task ConsentirAsync(int pacienteId, bool concedido = true)
        => _consentimentos.RegistrarAsync(
            pacienteId, FinalidadeConsentimento.ComunicacaoEMarketing, concedido);

    // ===== Confirmação de sessão =====

    [Fact]
    public async Task Confirmacao_GeraUmContatoPorSessaoDoDia()
    {
        var paciente = await CriarPacienteAsync();
        await MarcarAsync(paciente.Id, Hoje);

        var r = await _campanhas.GerarConfirmacoesAsync(Hoje);

        r.Gerados.Should().Be(1);
        var fila = await _campanhas.ContatosAsync(TipoContato.ConfirmacaoSessao, null, Hoje, Hoje);
        fila.Should().ContainSingle();
        fila[0].Status.Should().Be(StatusContato.Pendente);
    }

    [Fact]
    public async Task Confirmacao_NaoExigeConsentimento()
    {
        // Avisar o paciente sobre o horário que ELE pediu é transacional, não marketing.
        var paciente = await CriarPacienteAsync();
        await MarcarAsync(paciente.Id, Hoje);

        var r = await _campanhas.GerarConfirmacoesAsync(Hoje);

        r.Gerados.Should().Be(1);
        r.SemConsentimento.Should().Be(0);
    }

    [Fact]
    public async Task Confirmacao_RodarDuasVezes_NaoDuplica()
    {
        var paciente = await CriarPacienteAsync();
        await MarcarAsync(paciente.Id, Hoje);

        await _campanhas.GerarConfirmacoesAsync(Hoje);
        var segunda = await _campanhas.GerarConfirmacoesAsync(Hoje);

        segunda.Gerados.Should().Be(0);
        segunda.JaExistiam.Should().Be(1);

        var fila = await _campanhas.ContatosAsync(TipoContato.ConfirmacaoSessao, null, Hoje, Hoje);
        fila.Should().ContainSingle("mandar a mesma mensagem duas vezes é o pior defeito desta tela");
    }

    [Fact]
    public async Task Confirmacao_IgnoraCanceladoFaltouEJaRealizado()
    {
        var p1 = await CriarPacienteAsync("Cancelado");
        var p2 = await CriarPacienteAsync("Faltou");
        var p3 = await CriarPacienteAsync("Já veio");
        await MarcarAsync(p1.Id, Hoje, StatusAgendamento.Cancelado);
        await MarcarAsync(p2.Id, Hoje, StatusAgendamento.Faltou);
        await MarcarAsync(p3.Id, Hoje, StatusAgendamento.Realizado);

        var r = await _campanhas.GerarConfirmacoesAsync(Hoje);

        r.Gerados.Should().Be(0);
    }

    [Fact]
    public async Task Confirmacao_SemTelefone_EntraNaFilaEEhContada()
    {
        // A recepção ainda pode ligar; sumir com a linha esconderia o cadastro incompleto.
        var paciente = await CriarPacienteAsync("Sem Fone", telefone: null);
        await MarcarAsync(paciente.Id, Hoje);

        var r = await _campanhas.GerarConfirmacoesAsync(Hoje);

        r.Gerados.Should().Be(1);
        r.SemTelefone.Should().Be(1);
    }

    // ===== NPS =====

    [Fact]
    public async Task Nps_SoSaiParaQuemConsentiuComunicacao()
    {
        var comConsentimento = await CriarPacienteAsync("Consentiu");
        var semConsentimento = await CriarPacienteAsync("Não consentiu");
        await ConsentirAsync(comConsentimento.Id);

        var a1 = await AtenderAsync(comConsentimento.Id, Hoje);
        var a2 = await AtenderAsync(semConsentimento.Id, Hoje);
        await MarcarAsync(comConsentimento.Id, Hoje, StatusAgendamento.Realizado, a1.Id);
        await MarcarAsync(semConsentimento.Id, Hoje, StatusAgendamento.Realizado, a2.Id);

        var r = await _campanhas.GerarNpsAsync(Hoje);

        r.Gerados.Should().Be(1);
        r.SemConsentimento.Should().Be(1);
    }

    [Fact]
    public async Task Nps_ConsentimentoRevogadoNaoVale()
    {
        var paciente = await CriarPacienteAsync();
        await ConsentirAsync(paciente.Id);
        await ConsentirAsync(paciente.Id, concedido: false); // mudou de ideia: o mais recente vence

        var atendimento = await AtenderAsync(paciente.Id, Hoje);
        await MarcarAsync(paciente.Id, Hoje, StatusAgendamento.Realizado, atendimento.Id);

        var r = await _campanhas.GerarNpsAsync(Hoje);

        r.Gerados.Should().Be(0);
        r.SemConsentimento.Should().Be(1);
    }

    [Fact]
    public async Task Nps_RodarDuasVezes_NaoDuplica()
    {
        var paciente = await CriarPacienteAsync();
        await ConsentirAsync(paciente.Id);
        var atendimento = await AtenderAsync(paciente.Id, Hoje);
        await MarcarAsync(paciente.Id, Hoje, StatusAgendamento.Realizado, atendimento.Id);

        await _campanhas.GerarNpsAsync(Hoje);
        var segunda = await _campanhas.GerarNpsAsync(Hoje);

        segunda.Gerados.Should().Be(0);
        segunda.JaExistiam.Should().Be(1);
    }

    [Fact]
    public async Task Nps_NotaForaDaEscala_EhRecusada()
    {
        var contato = await CriarContatoNpsAsync();

        var acao = () => _campanhas.RegistrarNotaAsync(contato.Id, 11);

        await acao.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0, ClasseNps.Detrator)]
    [InlineData(6, ClasseNps.Detrator)]
    [InlineData(7, ClasseNps.Neutro)]
    [InlineData(8, ClasseNps.Neutro)]
    [InlineData(9, ClasseNps.Promotor)]
    [InlineData(10, ClasseNps.Promotor)]
    public void Nps_ClassificaPelaMetodologia(int nota, ClasseNps esperada)
        => ContatoCampanha.Classificar(nota).Should().Be(esperada);

    [Fact]
    public async Task Nps_Pontuacao_EhPromotoresMenosDetratores()
    {
        // 2 promotores, 1 neutro, 1 detrator em 4 respostas: 50% − 25% = 25.
        await ResponderAsync(10);
        await ResponderAsync(9);
        await ResponderAsync(8);
        await ResponderAsync(3);

        var resumo = await _campanhas.NpsAsync(Hoje, Hoje);

        resumo.Respondidos.Should().Be(4);
        resumo.Promotores.Should().Be(2);
        resumo.Neutros.Should().Be(1);
        resumo.Detratores.Should().Be(1);
        resumo.Pontuacao.Should().Be(25);
        resumo.Medido.Should().BeTrue();
    }

    [Fact]
    public async Task Nps_SemRespostas_NaoFingeNotaZero()
    {
        await CriarContatoNpsAsync();

        var resumo = await _campanhas.NpsAsync(Hoje, Hoje);

        // "Ninguém respondeu" e "a nota é 0" são coisas diferentes.
        resumo.Medido.Should().BeFalse();
        resumo.Gerados.Should().Be(1);
    }

    private async Task<ContatoCampanha> CriarContatoNpsAsync(string nome = "Maria Souza")
    {
        var paciente = await CriarPacienteAsync(nome);
        await ConsentirAsync(paciente.Id);
        var atendimento = await AtenderAsync(paciente.Id, Hoje);
        await MarcarAsync(paciente.Id, Hoje, StatusAgendamento.Realizado, atendimento.Id);
        await _campanhas.GerarNpsAsync(Hoje);

        // O recém-criado é o pendente de maior id: a fila mistura os já respondidos.
        var fila = await _campanhas.ContatosAsync(TipoContato.Nps, StatusContato.Pendente, Hoje, Hoje);
        return fila.OrderBy(c => c.Id).Last();
    }

    private async Task ResponderAsync(int nota)
    {
        var contato = await CriarContatoNpsAsync($"Paciente {Guid.NewGuid():N}");
        await _campanhas.RegistrarNotaAsync(contato.Id, nota);
    }

    [Fact]
    public async Task Nps_RegistrarNota_ContaComoEnviado()
    {
        // Se a nota veio por telefone, o envio pode nunca ter sido marcado — sem isto a
        // taxa de resposta passaria de 100%.
        var contato = await CriarContatoNpsAsync();

        await _campanhas.RegistrarNotaAsync(contato.Id, 9);

        var resumo = await _campanhas.NpsAsync(Hoje, Hoje);
        resumo.Enviados.Should().Be(1);
        resumo.TaxaResposta.Should().Be(100);
    }

    // ===== Recall =====

    [Fact]
    public async Task Recall_ChamaQuemNaoVemHaMaisDoQueOPrazo()
    {
        var sumido = await CriarPacienteAsync("Sumido");
        var recente = await CriarPacienteAsync("Recente");
        await ConsentirAsync(sumido.Id);
        await ConsentirAsync(recente.Id);
        await AtenderAsync(sumido.Id, Hoje.AddDays(-90));
        await AtenderAsync(recente.Id, Hoje.AddDays(-10));

        var candidatos = await _campanhas.CandidatosRecallAsync(Hoje, diasInatividade: 60);

        candidatos.Should().ContainSingle();
        candidatos[0].Nome.Should().Be("Sumido");
        candidatos[0].DiasSemVir.Should().Be(90);
    }

    [Fact]
    public async Task Recall_IgnoraQuemNuncaVeio()
    {
        // Não sumiu: nunca chegou. Chamar de volta seria uma mensagem sem sentido.
        var paciente = await CriarPacienteAsync("Nunca veio");
        await ConsentirAsync(paciente.Id);

        var candidatos = await _campanhas.CandidatosRecallAsync(Hoje);

        candidatos.Should().BeEmpty();
    }

    [Fact]
    public async Task Recall_IgnoraQuemJaTemHorarioMarcado()
    {
        var paciente = await CriarPacienteAsync("Já voltou");
        await ConsentirAsync(paciente.Id);
        await AtenderAsync(paciente.Id, Hoje.AddDays(-90));
        await MarcarAsync(paciente.Id, Hoje.AddDays(3));

        var candidatos = await _campanhas.CandidatosRecallAsync(Hoje);

        candidatos.Should().BeEmpty();
    }

    [Fact]
    public async Task Recall_QuemNaoConsentiu_ApareceMarcadoENaoEhGerado()
    {
        var paciente = await CriarPacienteAsync("Sem consentimento");
        await AtenderAsync(paciente.Id, Hoje.AddDays(-90));

        var candidatos = await _campanhas.CandidatosRecallAsync(Hoje);
        candidatos.Should().ContainSingle();
        candidatos[0].TemConsentimento.Should().BeFalse(
            "a clínica precisa saber que falta colher o consentimento");

        var r = await _campanhas.GerarRecallAsync(Hoje);
        r.Gerados.Should().Be(0);
        r.SemConsentimento.Should().Be(1);
    }

    [Fact]
    public async Task Recall_RodarDuasVezesNoMesmoMes_NaoDuplica()
    {
        var paciente = await CriarPacienteAsync("Sumido");
        await ConsentirAsync(paciente.Id);
        await AtenderAsync(paciente.Id, Hoje.AddDays(-90));

        await _campanhas.GerarRecallAsync(Hoje);
        var segunda = await _campanhas.GerarRecallAsync(Hoje.AddDays(2));

        segunda.Gerados.Should().Be(0);
        segunda.JaExistiam.Should().Be(1);
    }

    [Fact]
    public async Task Recall_NoMesSeguinte_ChamaDeNovo()
    {
        var paciente = await CriarPacienteAsync("Sumido");
        await ConsentirAsync(paciente.Id);
        await AtenderAsync(paciente.Id, Hoje.AddDays(-90));

        await _campanhas.GerarRecallAsync(Hoje);
        var mesSeguinte = await _campanhas.GerarRecallAsync(Hoje.AddMonths(1));

        mesSeguinte.Gerados.Should().Be(1);
    }

    // ===== Fila de contatos =====

    [Fact]
    public async Task Envio_MarcaComoEnviadoEGuardaQuemMandou()
    {
        var paciente = await CriarPacienteAsync();
        await MarcarAsync(paciente.Id, Hoje);
        await _campanhas.GerarConfirmacoesAsync(Hoje);
        var contato = (await _campanhas.ContatosAsync(TipoContato.ConfirmacaoSessao, null, Hoje, Hoje))[0];

        await _campanhas.RegistrarEnvioAsync(contato.Id, "ana");

        var atualizado = await _repo.ObterContatoAsync(contato.Id);
        atualizado!.Status.Should().Be(StatusContato.Enviado);
        atualizado.EnviadoPor.Should().Be("ana");
        atualizado.EnviadoEm.Should().NotBeNull();
    }

    [Fact]
    public async Task Dispensar_SemMotivo_EhRecusado()
    {
        var paciente = await CriarPacienteAsync();
        await MarcarAsync(paciente.Id, Hoje);
        await _campanhas.GerarConfirmacoesAsync(Hoje);
        var contato = (await _campanhas.ContatosAsync(TipoContato.ConfirmacaoSessao, null, Hoje, Hoje))[0];

        var acao = () => _campanhas.DispensarAsync(contato.Id, "   ");

        // "Não mandamos" sem explicação é indistinguível de esquecimento.
        await acao.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Dispensar_GuardaOMotivoESaiDaFilaPendente()
    {
        var paciente = await CriarPacienteAsync();
        await MarcarAsync(paciente.Id, Hoje);
        await _campanhas.GerarConfirmacoesAsync(Hoje);
        var contato = (await _campanhas.ContatosAsync(TipoContato.ConfirmacaoSessao, null, Hoje, Hoje))[0];

        await _campanhas.DispensarAsync(contato.Id, "Pediu para não receber mensagens.");

        var pendentes = await _campanhas.ContatosAsync(
            TipoContato.ConfirmacaoSessao, StatusContato.Pendente, Hoje, Hoje);
        pendentes.Should().BeEmpty();

        var atualizado = await _repo.ObterContatoAsync(contato.Id);
        atualizado!.Comentario.Should().Contain("não receber");
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }
}
