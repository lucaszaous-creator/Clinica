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
/// O relatório de origem dos pacientes (parcela 69) — o leitor agregado da pergunta que o
/// cadastro faz a todo paciente novo e cuja resposta ninguém somava.
/// </summary>
public class OrigemPacientesTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly OrigemPacientesService _servico;

    public OrigemPacientesTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _servico = new OrigemPacientesService(new ClinicaRepositorio(_db));
    }

    private async Task<int> CriarPacienteAsync(
        OrigemPaciente? origem, string? indicadoPor = null, DateOnly? primeiroAtendimento = null)
    {
        var p = new Paciente
        {
            Nome = "Paciente",
            Convenio = Convenio.UnimedIntercambio,
            Sexo = Sexo.Feminino,
            Origem = origem,
            IndicadoPor = indicadoPor
        };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();

        if (primeiroAtendimento is { } data)
        {
            _db.Atendimentos.Add(new Atendimento
            {
                PacienteId = p.Id,
                Data = data,
                Modalidade = ModalidadeAtendimento.AcupunturaComEletro
            });
            await _db.SaveChangesAsync();
        }

        return p.Id;
    }

    private static readonly DateOnly Inicio = new(2026, 1, 1);
    private static readonly DateOnly Fim = new(2026, 8, 18);

    /// <summary>
    /// Toda origem aparece, MESMO zerada — a regra do aging (parcela 23): sem a linha
    /// "Redes sociais — 0", a direção lê "não medimos" onde o fato é "ninguém veio por aí".
    /// E o "não perguntado" é linha de primeira classe, nunca escondida.
    /// </summary>
    [Fact]
    public async Task Toda_origem_aparece_mesmo_zerada_e_o_nao_perguntado_e_linha()
    {
        await CriarPacienteAsync(OrigemPaciente.Indicacao, "Maria Silva");
        await CriarPacienteAsync(null);

        var resumo = await _servico.ResumoAsync(Inicio, Fim);

        resumo.Linhas.Should().HaveCount(Enum.GetValues<OrigemPaciente>().Length + 1,
            "uma linha por origem do enum mais o 'não perguntado'");
        resumo.Linhas.Should().ContainSingle(l => l.Origem == null)
            .Which.TotalNaBase.Should().Be(1);
        resumo.Linhas.Should().ContainSingle(l => l.Origem == OrigemPaciente.RedesSociais)
            .Which.TotalNaBase.Should().Be(0, "zerada não é ausente");
        resumo.SemResposta.Should().Be(1);
        resumo.TotalPacientes.Should().Be(2);
    }

    /// <summary>
    /// Estreia é o PRIMEIRO atendimento no período. Quem já vinha antes e continuou vindo
    /// não estreou; quem foi cadastrado e nunca veio conta na base e em estreia nenhuma.
    /// Sem isso o número mediria movimento, não chegada — e "vale manter o anúncio?" se
    /// responde com chegada.
    /// </summary>
    [Fact]
    public async Task Estreia_e_o_primeiro_atendimento_no_periodo_nunca_o_retorno()
    {
        // Estreou em março: conta.
        await CriarPacienteAsync(OrigemPaciente.Internet, primeiroAtendimento: new DateOnly(2026, 3, 10));

        // Veterana: primeiro atendimento em 2025, voltou em 2026 — NÃO conta.
        var veterana = await CriarPacienteAsync(
            OrigemPaciente.Internet, primeiroAtendimento: new DateOnly(2025, 6, 1));
        _db.Atendimentos.Add(new Atendimento
        {
            PacienteId = veterana,
            Data = new DateOnly(2026, 2, 2),
            Modalidade = ModalidadeAtendimento.AcupunturaComEletro
        });
        await _db.SaveChangesAsync();

        // Cadastrada e nunca veio: base sim, estreia não.
        await CriarPacienteAsync(OrigemPaciente.Internet);

        var resumo = await _servico.ResumoAsync(Inicio, Fim);

        var internet = resumo.Linhas.Single(l => l.Origem == OrigemPaciente.Internet);
        internet.TotalNaBase.Should().Be(3);
        internet.EstreiasNoPeriodo.Should().Be(1,
            "só quem teve o PRIMEIRO atendimento dentro do período estreou");
    }

    /// <summary>
    /// "maria silva" e "Maria Silva " são a mesma pessoa digitada por duas recepcionistas.
    /// Separá-las esconderia justamente a maior indicadora da clínica — que é o que o
    /// ranking existe para achar.
    /// </summary>
    [Fact]
    public async Task Quem_indica_agrupa_por_nome_normalizado()
    {
        await CriarPacienteAsync(OrigemPaciente.Indicacao, "Maria Silva");
        await CriarPacienteAsync(OrigemPaciente.Indicacao, "maria silva ");
        await CriarPacienteAsync(OrigemPaciente.Indicacao, "Dr. Souza");

        var resumo = await _servico.ResumoAsync(Inicio, Fim);

        resumo.QuemMaisIndica.Should().HaveCount(2);
        resumo.QuemMaisIndica[0].Indicados.Should().Be(2, "as duas grafias são a mesma pessoa");
        resumo.QuemMaisIndica[0].Nome.Should().Be("Maria Silva");
    }

    /// <summary>Base vazia devolve as linhas (zeradas) e nenhum indicador — nunca lança.</summary>
    [Fact]
    public async Task Base_vazia_devolve_linhas_zeradas()
    {
        var resumo = await _servico.ResumoAsync(Inicio, Fim);

        resumo.TotalPacientes.Should().Be(0);
        resumo.Linhas.Should().OnlyContain(l => l.TotalNaBase == 0 && l.EstreiasNoPeriodo == 0);
        resumo.QuemMaisIndica.Should().BeEmpty();
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
