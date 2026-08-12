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
/// "Todo atendimento e toda consulta refletem no faturamento" (parcela 46).
///
/// Por que este arquivo existe
/// ---------------------------
/// A parcela tirou "Novo atendimento" e "Consultas" do app de faturamento e as pôs na
/// Recepção. O risco de uma mudança dessas não é a tela: é o CIRCUITO. Se o caminho novo
/// deixasse de gravar o <c>CodigoFaturamento</c>, nenhum teste de unidade falharia — a
/// guia simplesmente não existiria, e o faturamento veria um dia mais fraco em vez de um
/// erro. É o mesmo motivo pelo qual <c>CircuitoCompletoTests</c> existe: o que liga um
/// módulo ao outro aqui não é chamada de método, é CHAVE ESTRANGEIRA no mesmo banco.
///
/// O que se prova abaixo é que os DOIS caminhos de atendimento desembocam no mesmo ponto
/// único (<see cref="AtendimentoService.LancarAsync"/>) e produzem guias que o faturamento
/// enxerga pelas suas próprias consultas — não por uma consulta escrita para o teste.
/// </summary>
public class AtendimentoNaRecepcaoTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;

    public AtendimentoNaRecepcaoTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
    }

    /// <summary>
    /// O caminho AVULSO — o que mudou de app. É exatamente a chamada que a tela nova da
    /// Recepção faz, e a guia tem de chegar ao painel de pendências do faturamento.
    /// </summary>
    [Fact]
    public async Task Atendimento_avulso_lancado_na_recepcao_vira_pendencia_no_faturamento()
    {
        var paciente = await SemearPacienteAsync(Convenio.UnimedIntercambio);

        // ⚠️ Desde a parcela 60 o avulso NÃO chama `LancarAsync` direto: ele marca um
        // ENCAIXE na hora real e a esteira da Fila conclui — a mesma esteira, o mesmo
        // serviço e a mesma janela de quem veio da agenda. É por isso que esta chamada
        // mudou de forma sem mudar de resultado: a guia continua chegando ao faturamento.
        var agenda = new AgendaService(_repo, new AtendimentoService(_repo));
        var ag = await agenda.AgendarAsync(
            paciente.Id, DateTime.Today.AddHours(15).AddMinutes(30),
            ModalidadeAtendimento.AcupunturaComEletro, null,
            modalidadeCodigo: ModalidadeAtendimento.AcupunturaComEletro.ToString(),
            encaixe: true, operador: "recepcao");
        var resultado = await agenda.ConfirmarPresencaAsync(ag.Id);

        resultado.Atendimento.Codigos.Should().NotBeEmpty("lançar atendimento CRIA as guias");

        // A prova não é olhar o objeto devolvido: é o faturamento ACHAR a guia pela
        // consulta dele. Elo partido não viraria erro, viraria lista vazia.
        var pendentes = await new PendenciaService(_repo, new ParametrosService(_repo))
            .CodigosPendentesAsync(DateOnly.FromDateTime(DateTime.Today));

        pendentes.Should().Contain(p => p.PacienteNome == paciente.Nome);
    }

    /// <summary>
    /// O caminho da AGENDA (Fila → Finalizar), que a Recepção já tinha. Os dois desembocam
    /// no mesmo <see cref="AtendimentoService.LancarAsync"/> — este teste é o que impede
    /// alguém de "otimizar" um dos dois para gravar o atendimento direto no repositório,
    /// pulando o motor de regras e nascendo sem guia.
    /// </summary>
    [Fact]
    public async Task Atendimento_pela_agenda_gera_as_mesmas_guias_do_avulso()
    {
        var pelaAgenda = await SemearPacienteAsync(Convenio.UnimedIntercambio);
        var avulso = await SemearPacienteAsync(Convenio.UnimedIntercambio);

        var atendimentos = new AtendimentoService(_repo);
        var agenda = new AgendaService(_repo, atendimentos);

        var agendamento = new Agendamento
        {
            PacienteId = pelaAgenda.Id,
            DataHora = DateTime.Today.AddHours(9),
            ModalidadePrevista = ModalidadeAtendimento.AcupunturaComEletro,
            Status = StatusAgendamento.Agendado
        };
        _db.Add(agendamento);
        await _db.SaveChangesAsync();

        var porAgenda = await agenda.ConfirmarPresencaAsync(agendamento.Id);
        var porAvulso = await atendimentos.LancarAsync(
            avulso.Id, DateOnly.FromDateTime(DateTime.Today),
            ModalidadeAtendimento.AcupunturaComEletro);

        porAgenda.Atendimento.Codigos.Select(c => c.Tipo).Should()
            .BeEquivalentTo(porAvulso.Atendimento.Codigos.Select(c => c.Tipo),
                "a regra do convênio é a mesma; o que muda é só a porta");

        // E o 2º código, que é o defeito que dá nome ao produto, nasce nos dois.
        porAgenda.Atendimento.Codigos.Should().Contain(c => c.Ordem == OrdemCodigo.Segundo);
        porAvulso.Atendimento.Codigos.Should().Contain(c => c.Ordem == OrdemCodigo.Segundo);
    }

    /// <summary>
    /// A guia nascida no balcão aparece na CONSULTA DE GUIAS do faturamento — a tela que a
    /// direção usa para ver o que vem sendo feito, e que ganhou os filtros na parcela 45.
    /// </summary>
    [Fact]
    public async Task Guia_lancada_na_recepcao_aparece_na_consulta_de_guias_do_faturamento()
    {
        var paciente = await SemearPacienteAsync(Convenio.Amil);

        await new AtendimentoService(_repo).LancarAsync(
            paciente.Id, new DateOnly(2026, 8, 6), ModalidadeAtendimento.AcupunturaSimples);

        var achadas = await _repo.ConsultarCodigosAsync(new Application.Modelos.FiltroConsultaGuias
        {
            Modalidade = ModalidadeAtendimento.AcupunturaSimples,
            Inicio = new DateOnly(2026, 8, 6),
            Fim = new DateOnly(2026, 8, 6)
        });

        achadas.Should().NotBeEmpty();
        achadas.Should().OnlyContain(c => c.Atendimento!.Paciente!.Nome == paciente.Nome);
    }

    /// <summary>
    /// A consulta renovada no balcão é a MESMA linha que o faturamento lê. O serviço é
    /// compartilhado e a tela mudou de app — o que não pode mudar é o dado.
    /// </summary>
    [Fact]
    public async Task Consulta_renovada_na_recepcao_e_lida_pelo_faturamento()
    {
        var paciente = await SemearPacienteAsync(Convenio.UnimedPadrao);
        var consultas = new ConsultaService(_repo, new ParametrosService(_repo));
        var hoje = DateOnly.FromDateTime(DateTime.Today);

        // Antes: o paciente precisa renovar (nunca emitiu).
        var antes = await consultas.DoPacienteAsync(paciente.Id, hoje);
        antes!.PrecisaRenovar.Should().BeTrue();

        // A recepção renova — exatamente a chamada da tela nova.
        await consultas.RenovarAsync(paciente.Id, hoje);

        // O faturamento lê pelo caminho dele e vê a consulta vigente.
        var depois = await consultas.DoPacienteAsync(paciente.Id, hoje);
        depois!.UltimaEmissao.Should().Be(hoje);
        depois.Vencida.Should().BeFalse();
        depois.PrecisaRenovar.Should().BeFalse();
    }

    private async Task<Paciente> SemearPacienteAsync(Convenio convenio)
    {
        var paciente = new Paciente
        {
            Nome = $"Paciente {Guid.NewGuid():N}"[..20],
            Convenio = convenio,
            Sexo = Sexo.Feminino
        };
        _db.Add(paciente);
        await _db.SaveChangesAsync();
        return paciente;
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
