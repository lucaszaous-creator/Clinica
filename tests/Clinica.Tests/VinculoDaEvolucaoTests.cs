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
/// O VÍNCULO da evolução com o horário, nas duas pontas em que ele se perdia.
///
/// É o mesmo defeito visto de dois lados, e nenhum dos dois quebra nada: a tela abre, o
/// build passa, os testes passam. O que acontece é a clínica ficar com DUAS evoluções do
/// mesmo atendimento — e prontuário que se contradiz é prontuário em que ninguém confia.
///
/// 1. <b>A escrita apagava o vínculo.</b> A janela de evolução da RECEPÇÃO nunca carregou
///    <c>AgendamentoId</c>/<c>AtendimentoId</c>, e o <c>ProntuarioService</c> copiava os
///    dois sem condição. Editar pelo balcão uma sessão escrita no consultório desligava a
///    evolução do horário, e o consultório voltava a cobrar o registro.
/// 2. <b>A leitura tinha duas definições.</b> O cartão do Meu dia casa por
///    <c>AgendamentoId</c> e cai para paciente + data; a tela de Atendimento só olhava o
///    <c>AgendamentoId</c>. O cartão dizia "Ver registro", o formulário abria EM BRANCO e o
///    Salvar criava a segunda evolução.
/// </summary>
public class VinculoDaEvolucaoTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ProntuarioService _prontuario;

    private static readonly DateOnly Dia = new(2026, 8, 12);

    public VinculoDaEvolucaoTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _prontuario = new ProntuarioService(new ClinicaRepositorio(_db));
    }

    private async Task<int> CriarPacienteAsync()
    {
        var p = new Paciente { Nome = "Maria", Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    [Fact]
    public async Task Editar_pelo_balcao_nao_apaga_o_vinculo_com_o_horario()
    {
        var pacienteId = await CriarPacienteAsync();

        var agendamento = new Agendamento
        {
            PacienteId = pacienteId,
            DataHora = Dia.ToDateTime(new TimeOnly(9, 0)),
            DuracaoMinutos = 30
        };
        var atendimento = new Atendimento
        {
            PacienteId = pacienteId,
            Data = Dia,
            Modalidade = ModalidadeAtendimento.AcupunturaSimples
        };
        _db.Agendamentos.Add(agendamento);
        _db.Atendimentos.Add(atendimento);
        await _db.SaveChangesAsync();

        // Nasce ligada ao horário, como o consultório a escreve.
        var original = await _prontuario.SalvarAsync(new Evolucao
        {
            PacienteId = pacienteId,
            Data = Dia,
            AgendamentoId = agendamento.Id,
            AtendimentoId = atendimento.Id,
            TextoEvolucao = "sessão de acupuntura"
        }, "medico");

        original.AgendamentoId.Should().Be(agendamento.Id);

        // A janela do BALCÃO não conhece os dois campos — eles chegam nulos. Nulo aqui
        // quer dizer "o chamador não sabe", nunca "desligue".
        var editada = await _prontuario.SalvarAsync(new Evolucao
        {
            Id = original.Id,
            PacienteId = pacienteId,
            Data = Dia,
            TextoEvolucao = "sessão de acupuntura — corrigido o lado"
        }, "secretaria");

        editada.AgendamentoId.Should().Be(agendamento.Id, "desligar a evolução do horário "
            + "faria o consultório cobrar de novo um registro que já existe");
        editada.AtendimentoId.Should().Be(atendimento.Id);
    }

    /// <summary>Uma sessão do paciente 9 no dia padrão, para a lista de irmãs.</summary>
    private static Agendamento Sessao(int id, int hora, StatusAgendamento status = StatusAgendamento.Realizado)
        => new()
        {
            Id = id,
            PacienteId = 9,
            DataHora = Dia.ToDateTime(new TimeOnly(hora, 0)),
            Status = status
        };

    [Fact]
    public void Atendimento_e_Meu_dia_concordam_sobre_a_sessao_ja_escrita()
    {
        // A evolução escrita pela RECEPÇÃO não conhece o agendamento. É o caminho de baixo
        // — paciente + data — que a encontra; sem ele, as duas telas discordam.
        var doBalcao = new Evolucao { Id = 1, PacienteId = 9, Data = Dia, AgendamentoId = null };
        var deOutroDia = new Evolucao { Id = 2, PacienteId = 9, Data = Dia.AddDays(-1) };
        var deOutroPaciente = new Evolucao { Id = 3, PacienteId = 10, Data = Dia };

        var achada = ConsultorioService.EvolucaoDoHorario(
            [doBalcao, deOutroDia, deOutroPaciente], agendamentoId: 77, pacienteId: 9, data: Dia,
            [Sessao(77, 9)]);

        achada.Should().BeSameAs(doBalcao);
    }

    [Fact]
    public void O_vinculo_direto_vence_o_caminho_de_baixo()
    {
        // Duas sessões no mesmo dia: a ligada ao horário é a daquele horário, e a solta
        // pertence a outro atendimento do mesmo dia. Casar pela data primeiro devolveria a
        // errada, e o profissional continuaria a sessão de outra consulta.
        var solta = new Evolucao { Id = 1, PacienteId = 9, Data = Dia, AgendamentoId = null };
        var doHorario = new Evolucao { Id = 2, PacienteId = 9, Data = Dia, AgendamentoId = 77 };

        ConsultorioService.EvolucaoDoHorario([solta, doHorario], 77, 9, Dia, [Sessao(77, 9)])
            .Should().BeSameAs(doHorario);
    }

    [Fact]
    public void Sessao_nao_escrita_devolve_nulo()
        => ConsultorioService.EvolucaoDoHorario(
                [new Evolucao { Id = 1, PacienteId = 9, Data = Dia.AddDays(-3) }],
                agendamentoId: 77, pacienteId: 9, data: Dia, [Sessao(77, 9)])
            .Should().BeNull();

    [Fact]
    public void Uma_avulsa_nao_cobre_duas_sessoes_do_mesmo_dia()
    {
        // Paciente com sessão de manhã e de tarde, e UMA evolução escrita sem vínculo.
        // Antes, o caminho de baixo dava as DUAS por escritas: a segunda sumia da cobrança
        // e abri-la na tela de Atendimento continuava o texto da primeira. A avulsa casa
        // com uma só — a mais cedo — e a outra continua pendente.
        var avulsa = new Evolucao { Id = 1, PacienteId = 9, Data = Dia, AgendamentoId = null };
        var irmas = new[] { Sessao(77, 9), Sessao(78, 15) };

        ConsultorioService.EvolucaoDoHorario([avulsa], 77, 9, Dia, irmas)
            .Should().BeSameAs(avulsa, "a sessão da manhã fica com a avulsa");
        ConsultorioService.EvolucaoDoHorario([avulsa], 78, 9, Dia, irmas)
            .Should().BeNull("a sessão da tarde continua sem registro — e sem cobrança ela some");
    }

    [Fact]
    public void Duas_avulsas_cobrem_as_duas_sessoes_na_ordem_em_que_foram_escritas()
    {
        var primeira = new Evolucao { Id = 1, PacienteId = 9, Data = Dia };
        var segunda = new Evolucao { Id = 2, PacienteId = 9, Data = Dia };
        var irmas = new[] { Sessao(77, 9), Sessao(78, 15) };

        ConsultorioService.EvolucaoDoHorario([segunda, primeira], 77, 9, Dia, irmas)
            .Should().BeSameAs(primeira);
        ConsultorioService.EvolucaoDoHorario([segunda, primeira], 78, 9, Dia, irmas)
            .Should().BeSameAs(segunda);
    }

    [Fact]
    public void Sessao_cancelada_nao_disputa_a_avulsa()
    {
        // A sessão da manhã foi cancelada: não aconteceu, não tem o que escrever. A avulsa
        // do dia é da sessão que ACONTECEU — deixar a cancelada na fila roubaria o registro
        // da sessão de verdade.
        var avulsa = new Evolucao { Id = 1, PacienteId = 9, Data = Dia };
        var irmas = new[] { Sessao(77, 9, StatusAgendamento.Cancelado), Sessao(78, 15) };

        ConsultorioService.EvolucaoDoHorario([avulsa], 78, 9, Dia, irmas)
            .Should().BeSameAs(avulsa);
    }

    [Fact]
    public void Sessao_com_evolucao_propria_nao_disputa_a_avulsa()
    {
        // A da manhã já tem a evolução DELA, vinculada. A avulsa que sobra é da tarde.
        var daManha = new Evolucao { Id = 1, PacienteId = 9, Data = Dia, AgendamentoId = 77 };
        var avulsa = new Evolucao { Id = 2, PacienteId = 9, Data = Dia };
        var irmas = new[] { Sessao(77, 9), Sessao(78, 15) };

        ConsultorioService.EvolucaoDoHorario([daManha, avulsa], 78, 9, Dia, irmas)
            .Should().BeSameAs(avulsa);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }
}
