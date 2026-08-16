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

    [Fact]
    public void Atendimento_e_Meu_dia_concordam_sobre_a_sessao_ja_escrita()
    {
        // A evolução escrita pela RECEPÇÃO não conhece o agendamento. É o caminho de baixo
        // — paciente + data — que a encontra; sem ele, as duas telas discordam.
        var doBalcao = new Evolucao { Id = 1, PacienteId = 9, Data = Dia, AgendamentoId = null };
        var deOutroDia = new Evolucao { Id = 2, PacienteId = 9, Data = Dia.AddDays(-1) };
        var deOutroPaciente = new Evolucao { Id = 3, PacienteId = 10, Data = Dia };

        var achada = ConsultorioService.EvolucaoDoHorario(
            [doBalcao, deOutroDia, deOutroPaciente], agendamentoId: 77, pacienteId: 9, data: Dia);

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

        ConsultorioService.EvolucaoDoHorario([solta, doHorario], 77, 9, Dia)
            .Should().BeSameAs(doHorario);
    }

    [Fact]
    public void Sessao_nao_escrita_devolve_nulo()
        => ConsultorioService.EvolucaoDoHorario(
                [new Evolucao { Id = 1, PacienteId = 9, Data = Dia.AddDays(-3) }],
                agendamentoId: 77, pacienteId: 9, data: Dia)
            .Should().BeNull();

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }
}
