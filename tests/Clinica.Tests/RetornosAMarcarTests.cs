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
/// A fila "Retornos a marcar" (set/2026): quem saiu do atendimento com pedido de retorno
/// e ainda não tem horário. A montagem é pura e é testada sozinha; o serviço executa as
/// duas consultas novas do repositório contra o SQLite — método de repositório sem teste
/// que o EXECUTE é código que ninguém rodou.
/// </summary>
public class RetornosAMarcarTests : IDisposable
{
    private static readonly DateOnly Hoje = new(2026, 9, 10);

    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly RetornosAMarcarService _servico;

    public RetornosAMarcarTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _servico = new RetornosAMarcarService(new ClinicaRepositorio(_db), () => Hoje);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }

    // ==================== A montagem pura ====================

    private static RetornoSugerido Sugestao(
        int paciente, DateOnly sessao, DateOnly retorno, int id = 0, string? nota = null)
        => new(id == 0 ? paciente * 10 + sessao.Day : id, paciente, $"Paciente {paciente}", "11999990000",
            sessao, retorno, nota, 7, "Dra. Ana", "Ana Lima");

    [Fact]
    public void Sem_horario_depois_da_sessao_o_retorno_esta_pendente()
    {
        var linhas = RetornosAMarcar.Montar(
            [Sugestao(1, new DateOnly(2026, 9, 3), new DateOnly(2026, 9, 10))],
            [],
            Hoje);

        linhas.Should().ContainSingle();
        linhas[0].PacienteId.Should().Be(1);
        linhas[0].Profissional.Should().Be("Dra. Ana");
        linhas[0].DiasDeAtraso.Should().Be(0);
        linhas[0].Situacao.Should().Be("é hoje");
    }

    /// <summary>
    /// "Coberto" é ter horário ATIVO depois do DIA DA SESSÃO — não depois de hoje. O
    /// retorno pedido em 20/08 para 27/08, marcado e já realizado em 27/08, foi atendido;
    /// contar "futuro a partir de hoje" o daria como pendente para sempre.
    /// </summary>
    [Fact]
    public void Horario_depois_da_sessao_cobre_o_retorno_mesmo_que_ja_tenha_acontecido()
    {
        var linhas = RetornosAMarcar.Montar(
            [Sugestao(1, new DateOnly(2026, 8, 20), new DateOnly(2026, 8, 27))],
            [new HorarioPosterior(1, new DateTime(2026, 8, 27, 14, 0, 0))],
            Hoje);

        linhas.Should().BeEmpty();
    }

    /// <summary>O horário do PRÓPRIO dia da sessão é a sessão — não cobre o retorno.</summary>
    [Fact]
    public void Horario_no_mesmo_dia_da_sessao_nao_cobre()
    {
        var linhas = RetornosAMarcar.Montar(
            [Sugestao(1, new DateOnly(2026, 9, 3), new DateOnly(2026, 9, 17))],
            [new HorarioPosterior(1, new DateTime(2026, 9, 3, 9, 0, 0))],
            Hoje);

        linhas.Should().ContainSingle();
    }

    /// <summary>
    /// Por paciente vale a sessão MAIS RECENTE: quem foi visto de novo e recebeu novo
    /// pedido tem um retorno só, o último.
    /// </summary>
    [Fact]
    public void Vale_a_sugestao_da_sessao_mais_recente_do_paciente()
    {
        var linhas = RetornosAMarcar.Montar(
            [
                Sugestao(1, new DateOnly(2026, 8, 5), new DateOnly(2026, 8, 12), id: 1, nota: "antiga"),
                Sugestao(1, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 15), id: 2, nota: "nova")
            ],
            [],
            Hoje);

        linhas.Should().ContainSingle();
        linhas[0].Nota.Should().Be("nova");
        linhas[0].RetornoEm.Should().Be(new DateOnly(2026, 9, 15));
    }

    [Fact]
    public void Os_atrasados_vem_primeiro_e_a_situacao_diz_quanto()
    {
        var linhas = RetornosAMarcar.Montar(
            [
                Sugestao(1, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 20)),
                Sugestao(2, new DateOnly(2026, 8, 25), new DateOnly(2026, 9, 5)),
                Sugestao(3, new DateOnly(2026, 9, 2), new DateOnly(2026, 9, 11))
            ],
            [],
            Hoje);

        linhas.Select(l => l.PacienteId).Should().Equal(2, 3, 1);
        linhas[0].Atrasado.Should().BeTrue();
        linhas[0].Situacao.Should().Be("atrasado há 5 dias");
        linhas[1].Situacao.Should().Be("amanhã");
        linhas[2].Situacao.Should().Be("em 10 dias");
    }

    [Fact]
    public void Nota_em_branco_vira_nula_e_nome_curto_em_branco_cai_para_o_nome()
    {
        var sugestao = new RetornoSugerido(1, 1, "Maria", null,
            new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 12), "   ", 7, "  ", "Ana Lima");

        var linhas = RetornosAMarcar.Montar([sugestao], [], Hoje);

        linhas[0].Nota.Should().BeNull();
        linhas[0].TemNota.Should().BeFalse();
        linhas[0].TemTelefone.Should().BeFalse();
        linhas[0].Profissional.Should().Be("Ana Lima");
    }

    // ==================== O serviço, executando as consultas ====================

    private async Task<int> PacienteAsync(string nome)
    {
        var p = new Paciente { Nome = nome, Telefone = "11 99999-0000" };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    private async Task<int> ProfissionalAsync()
    {
        var p = new Profissional { Nome = "Ana Lima", NomeCurto = "Dra. Ana" };
        _db.Profissionais.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    private async Task SessaoAsync(int pacienteId, int profissionalId, DateOnly data, DateOnly? retorno,
        string? nota = null, bool cancelada = false)
    {
        _db.Evolucoes.Add(new Evolucao
        {
            PacienteId = pacienteId,
            ProfissionalId = profissionalId,
            Data = data,
            QueixaPrincipal = "lombalgia",
            RetornoSugeridoEm = retorno,
            RetornoSugeridoNota = nota,
            CanceladaEm = cancelada ? new DateTime(2026, 9, 4, 10, 0, 0) : null
        });
        await _db.SaveChangesAsync();
    }

    private async Task HorarioAsync(int pacienteId, int profissionalId, DateTime quando,
        StatusAgendamento status = StatusAgendamento.Agendado)
    {
        _db.Agendamentos.Add(new Agendamento
        {
            PacienteId = pacienteId,
            ProfissionalId = profissionalId,
            DataHora = quando,
            ModalidadePrevista = ModalidadeAtendimento.Consulta,
            ModalidadeCodigo = nameof(ModalidadeAtendimento.Consulta),
            Status = status
        });
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task O_servico_le_a_sugestao_com_paciente_telefone_e_quem_pediu()
    {
        var prof = await ProfissionalAsync();
        var maria = await PacienteAsync("Maria Silva");
        await SessaoAsync(maria, prof, new DateOnly(2026, 9, 3), new DateOnly(2026, 9, 10), "reavaliar a EVA");

        var linhas = await _servico.ListarAsync();

        linhas.Should().ContainSingle();
        linhas[0].Paciente.Should().Be("Maria Silva");
        linhas[0].Telefone.Should().Be("11 99999-0000");
        linhas[0].Profissional.Should().Be("Dra. Ana");
        linhas[0].Nota.Should().Be("reavaliar a EVA");
        linhas[0].Sessao.Should().Be(new DateOnly(2026, 9, 3));
    }

    [Fact]
    public async Task Horario_marcado_depois_tira_da_fila_e_cancelado_nao_tira()
    {
        var prof = await ProfissionalAsync();
        var maria = await PacienteAsync("Maria Silva");
        var joao = await PacienteAsync("João Souza");
        await SessaoAsync(maria, prof, new DateOnly(2026, 9, 3), new DateOnly(2026, 9, 10));
        await SessaoAsync(joao, prof, new DateOnly(2026, 9, 3), new DateOnly(2026, 9, 10));
        await HorarioAsync(maria, prof, new DateTime(2026, 9, 10, 14, 0, 0));
        await HorarioAsync(joao, prof, new DateTime(2026, 9, 10, 15, 0, 0), StatusAgendamento.Cancelado);

        var linhas = await _servico.ListarAsync();

        linhas.Select(l => l.PacienteId).Should().Equal(joao);
    }

    /// <summary>Sessão cancelada é registro desdito: o retorno dela não é pedido nenhum.</summary>
    [Fact]
    public async Task Sessao_cancelada_e_retorno_fora_da_janela_nao_entram()
    {
        var prof = await ProfissionalAsync();
        var cancelada = await PacienteAsync("Cancelada");
        var antiga = await PacienteAsync("Antiga");
        await SessaoAsync(cancelada, prof, new DateOnly(2026, 9, 3), new DateOnly(2026, 9, 10), cancelada: true);
        await SessaoAsync(antiga, prof, new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 15));

        var linhas = await _servico.ListarAsync();

        linhas.Should().BeEmpty();
    }

    [Fact]
    public async Task Sem_sugestao_nenhuma_a_fila_e_vazia_sem_segunda_consulta()
    {
        (await _servico.ListarAsync()).Should().BeEmpty();
    }
}
