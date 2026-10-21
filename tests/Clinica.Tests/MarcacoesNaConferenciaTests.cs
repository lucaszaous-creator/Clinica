using Clinica.Application.Formatacao;
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
/// A conferência do balcão (aba Lançamentos) mostra também os HORÁRIOS MARCADOS (out/2026).
/// Com a guia nascendo só quando o profissional finaliza, marcar não cria atendimento — e a tela,
/// que só lia atendimentos, ficava vazia enquanto a secretária marcava o dia inteiro.
/// </summary>
public sealed class MarcacoesNaConferenciaTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly AgendaService _agenda;
    private readonly int _prof;
    private readonly int _paciente;

    public MarcacoesNaConferenciaTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        _db = new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options);
        _db.Database.EnsureCreated();
        var prof = new Profissional { Nome = "Dra. Ana Lima" };
        var paciente = new Paciente { Nome = "Maria Souza", Convenio = Convenio.UnimedPadrao, ConvenioCodigo = "UnimedPadrao", Sexo = Sexo.Feminino };
        _db.Profissionais.Add(prof);
        _db.Pacientes.Add(paciente);
        _db.SaveChanges();
        _prof = prof.Id;
        _paciente = paciente.Id;
        var repo = new ClinicaRepositorio(_db);
        _agenda = new AgendaService(repo, new AtendimentoService(repo));
    }

    public void Dispose() { _db.Dispose(); _conn.Dispose(); }

    private ClinicaRepositorio RepositorioNovo()
        => new(new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options));

    [Fact]
    public async Task O_que_a_secretaria_marcou_aparece_mesmo_sem_atendimento()
    {
        var marcado = await _agenda.AgendarAsync(_paciente, DateTime.Today.AddDays(7).AddHours(9),
            ModalidadeAtendimento.AcupunturaComEletro, null, profissionalId: _prof, operador: "Rayssa");

        // Escopo NOVO, como em produção: prova a projeção, não o rastreio do EF.
        var repo = RepositorioNovo();
        var lidos = await repo.AgendamentosMarcadosNoPeriodoAsync(DateTime.Today, DateTime.Today.AddDays(1));
        (await repo.AtendimentosNoPeriodoAsync(DateOnly.FromDateTime(DateTime.Today), DateOnly.FromDateTime(DateTime.Today)))
            .Should().BeEmpty("marcar não cria atendimento — é por isso que a lista nova existe");

        lidos.Should().ContainSingle(a => a.Id == marcado.Id);
        var linha = LancamentosNaTela.MontarMarcacao(lidos.Single());
        linha.Paciente.Should().Be("Maria Souza");
        linha.Operador.Should().Be("Rayssa");
        linha.Marcacao.Should().Be("por Rayssa");
        linha.Sessao.Should().EndWith("· Dra. Ana Lima");
        linha.Situacao.Should().Be("Aguardando atendimento");
        linha.Convenio.Should().NotBe("—");
    }

    [Fact]
    public async Task O_periodo_e_pela_data_em_que_foi_MARCADO_e_o_mais_recente_vem_primeiro()
    {
        var a = await _agenda.AgendarAsync(_paciente, DateTime.Today.AddHours(8), ModalidadeAtendimento.AcupunturaComEletro, null, profissionalId: _prof, operador: "Ana");
        var b = await _agenda.AgendarAsync(_paciente, DateTime.Today.AddHours(10), ModalidadeAtendimento.AcupunturaComEletro, null, profissionalId: _prof, operador: "Bia");
        var antigo = await _agenda.AgendarAsync(_paciente, DateTime.Today.AddHours(12), ModalidadeAtendimento.AcupunturaComEletro, null, profissionalId: _prof, operador: "Ana");
        var semRegistro = await _agenda.AgendarAsync(_paciente, DateTime.Today.AddHours(14), ModalidadeAtendimento.AcupunturaComEletro, null, profissionalId: _prof);

        await _db.Agendamentos.Where(x => x.Id == a.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.CriadoEm, DateTime.Today.AddHours(9)));
        await _db.Agendamentos.Where(x => x.Id == b.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.CriadoEm, DateTime.Today.AddHours(11)));
        await _db.Agendamentos.Where(x => x.Id == antigo.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.CriadoEm, DateTime.Today.AddDays(-3)));
        await _db.Agendamentos.Where(x => x.Id == semRegistro.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.CriadoEm, (DateTime?)null));

        var lidos = await RepositorioNovo().AgendamentosMarcadosNoPeriodoAsync(DateTime.Today, DateTime.Today.AddDays(1));

        lidos.Select(x => x.Id).Should().Equal(b.Id, a.Id);
    }

    [Theory]
    [InlineData(StatusAgendamento.Agendado, "Aguardando atendimento", "aviso")]
    [InlineData(StatusAgendamento.Realizado, "Atendido", "sucesso")]
    [InlineData(StatusAgendamento.Cancelado, "Cancelado", "perigo")]
    [InlineData(StatusAgendamento.Faltou, "Faltou", "perigo")]
    public void A_situacao_diz_o_que_aconteceu_depois_da_marcacao(StatusAgendamento status, string situacao, string tom)
    {
        var l = LancamentosNaTela.MontarMarcacao(new Agendamento
        {
            Id = 3, DataHora = new DateTime(2026, 10, 28, 9, 0, 0), Status = status,
            ModalidadePrevista = ModalidadeAtendimento.AcupunturaComEletro,
            CriadoEm = new DateTime(2026, 10, 21, 14, 32, 0), CriadoPor = null,
            Paciente = new Paciente { Nome = "João" }
        });
        l.Situacao.Should().Be(situacao);
        l.TomSituacao.Should().Be(tom);
        l.MarcadoRotulo.Should().Be("21/10 14:32");
        l.Sessao.Should().Be("28/10 09:00 · sem profissional");
        l.Operador.Should().Be(LancamentosNaTela.SemRegistroDeOperador);
        l.Marcacao.Should().Be("sem registro de quem marcou");
    }

    [Fact]
    public void O_filtro_de_quem_marcou_vale_e_o_de_guia_por_liberar_nao_esconde_marcacao()
    {
        var l = LancamentosNaTela.MontarMarcacao(new Agendamento
        {
            DataHora = new DateTime(2026, 10, 28, 9, 0, 0), ModalidadePrevista = ModalidadeAtendimento.AcupunturaComEletro,
            CriadoEm = new DateTime(2026, 10, 21, 9, 0, 0), CriadoPor = "Rayssa", Paciente = new Paciente { Nome = "João" }
        });
        LancamentosNaTela.Passa(l, new FiltroLancamentos(null, null, "Rayssa", false)).Should().BeTrue();
        LancamentosNaTela.Passa(l, new FiltroLancamentos(null, null, "Ana", false)).Should().BeFalse();
        LancamentosNaTela.Passa(l, new FiltroLancamentos(null, null, null, true)).Should().BeTrue();
        LancamentosNaTela.ResumoMarcacoes(3, 12).Should().Be("3 de 12 horário(s) marcado(s)");
        LancamentosNaTela.ResumoMarcacoes(12, 12).Should().Be("12 horário(s) marcado(s)");
    }
}
