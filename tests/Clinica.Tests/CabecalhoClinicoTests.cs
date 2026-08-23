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
/// O CRACHÁ CLÍNICO do paciente (parcela 74).
///
/// Ele responde, de relance, as quatro perguntas que quem atende faz antes de abrir a
/// boca: que idade tem, de que convênio é, desde quando se trata aqui e <b>o que não se
/// pode esquecer</b>. Todos os dados já existiam no banco; nenhum tinha leitor aqui.
///
/// A asserção que carrega a suíte é a da ALERGIA: ela entra no crachá mesmo dada por
/// RESOLVIDA. "Resolvida" numa alergia é quase sempre "não reagiu da última vez", e o dia
/// em que reagir é o dia em que o crachá teria valido — só o DESCARTE a cala, porque
/// descartar exige motivo escrito e é a afirmação de que o registro estava errado.
/// </summary>
public class CabecalhoClinicoTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly ConsultorioService _consultorio;

    public CabecalhoClinicoTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _consultorio = new ConsultorioService(_repo);
    }

    private async Task<int> CriarAsync(DateOnly? nascimento = null)
    {
        var p = new Paciente
        {
            Nome = "Marisa Silva",
            Convenio = Convenio.UnimedIntercambio,
            Sexo = Sexo.Feminino,
            DataNascimento = nascimento
        };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    private async Task ProblemaAsync(int pacienteId, NaturezaProblema natureza,
                                     string descricao, SituacaoProblema situacao)
    {
        _db.ProblemasPaciente.Add(new ProblemaPaciente
        {
            PacienteId = pacienteId,
            Natureza = natureza,
            Descricao = descricao,
            Situacao = situacao,
            MotivoDescarte = situacao == SituacaoProblema.Descartado ? "lançado no paciente errado" : null
        });
        await _db.SaveChangesAsync();
    }

    // ===== A alergia =====

    [Fact]
    public async Task Alergia_RESOLVIDA_continua_no_cracha()
    {
        var id = await CriarAsync();
        await ProblemaAsync(id, NaturezaProblema.Alergia, "dipirona", SituacaoProblema.Resolvido);

        var c = (await _consultorio.CabecalhoAsync(id))!;

        c.Alergias.Should().ContainSingle().Which.Should().Be("dipirona");
        c.TemAlergia.Should().BeTrue();
        c.AlergiasTexto.Should().Be("Alergia: dipirona");
    }

    [Fact]
    public async Task Alergia_DESCARTADA_sai_do_cracha()
    {
        var id = await CriarAsync();
        await ProblemaAsync(id, NaturezaProblema.Alergia, "penicilina", SituacaoProblema.Descartado);

        var c = (await _consultorio.CabecalhoAsync(id))!;

        // Descartar exige motivo escrito: é a afirmação de que o registro estava errado.
        c.Alergias.Should().BeEmpty();
        c.AlergiasTexto.Should().BeNull();
    }

    [Fact]
    public async Task Alergia_nao_se_mistura_com_problema_em_acompanhamento()
    {
        var id = await CriarAsync();
        await ProblemaAsync(id, NaturezaProblema.Alergia, "AAS", SituacaoProblema.Ativo);
        await ProblemaAsync(id, NaturezaProblema.Diagnostico, "lombalgia", SituacaoProblema.Ativo);

        var c = (await _consultorio.CabecalhoAsync(id))!;

        // São dois blocos e duas cores: uma se resolve ANTES de prescrever, a outra é
        // contexto do tratamento.
        c.Alergias.Should().ContainSingle().Which.Should().Be("AAS");
        c.ProblemasAtivos.Should().ContainSingle().Which.Should().Be("lombalgia");
    }

    [Fact]
    public async Task Problema_RESOLVIDO_sai_do_acompanhamento()
    {
        var id = await CriarAsync();
        await ProblemaAsync(id, NaturezaProblema.Diagnostico, "tendinite", SituacaoProblema.Resolvido);

        // Ao contrário da alergia: um diagnóstico resolvido é história, não é o que se
        // acompanha hoje — e o crachá tem uma linha.
        (await _consultorio.CabecalhoAsync(id))!.ProblemasAtivos.Should().BeEmpty();
    }

    // ===== A idade =====

    [Theory]
    // Aniversário já passou no ano.
    [InlineData(1980, 3, 15, 2026, 8, 24, 46)]
    // Aniversário ainda NÃO chegou: a conta pelo ano subtraído erraria aqui.
    [InlineData(1980, 12, 15, 2026, 8, 24, 45)]
    // Exatamente no aniversário.
    [InlineData(1980, 8, 24, 2026, 8, 24, 46)]
    public void Idade_conta_anos_COMPLETOS(int an, int mn, int dn,
                                           int ah, int mh, int dh, int esperado)
    {
        var idade = typeof(ConsultorioService)
            .GetMethod("IdadeEm", System.Reflection.BindingFlags.NonPublic
                                  | System.Reflection.BindingFlags.Static)!
            .Invoke(null, [(DateOnly?)new DateOnly(an, mn, dn), new DateOnly(ah, mh, dh)]);

        idade.Should().Be(esperado);
    }

    [Fact]
    public async Task Sem_data_de_nascimento_a_idade_e_nula_e_a_linha_pula_o_campo()
    {
        var id = await CriarAsync(nascimento: null);

        var c = (await _consultorio.CabecalhoAsync(id))!;

        // Nula, nunca zero: "0 ano" seria um recém-nascido.
        c.Idade.Should().BeNull();
        // A frase é montada no modelo justamente porque ela sabe PULAR — binding
        // concatenado deixaria "· anos ·" com um vão no meio.
        c.Linha.Should().NotContain("anos");
        c.Linha.Should().Contain("feminino");
    }

    // ===== As últimas hipóteses =====

    [Fact]
    public async Task Ultimas_hipoteses_saem_DISTINTAS_e_da_mais_recente()
    {
        var id = await CriarAsync();
        foreach (var (dia, hipotese) in new[]
                 {
                     (1, "lombalgia mecânica"), (2, "lombalgia mecânica"),
                     (3, "cervicalgia"), (4, "cefaleia tensional"),
                     (5, "fibromialgia")
                 })
            _db.Evolucoes.Add(new Evolucao
            {
                PacienteId = id,
                Data = new DateOnly(2026, 8, dia),
                HipoteseDiagnostica = hipotese
            });
        await _db.SaveChangesAsync();

        var c = (await _consultorio.CabecalhoAsync(id))!;

        // Três, e sem repetir: repetir "lombalgia" nas oito últimas sessões gastaria a
        // linha inteira dizendo uma coisa só.
        c.UltimosDiagnosticos.Should().Equal("fibromialgia", "cefaleia tensional", "cervicalgia");
    }

    [Fact]
    public async Task Hipotese_de_sessao_CANCELADA_nao_entra()
    {
        var id = await CriarAsync();
        _db.Evolucoes.Add(new Evolucao
        {
            PacienteId = id,
            Data = new DateOnly(2026, 8, 10),
            HipoteseDiagnostica = "lançada no paciente errado",
            CanceladaEm = new DateTime(2026, 8, 11, 8, 0, 0),
            CanceladaPor = "medica",
            MotivoCancelamento = "paciente errado"
        });
        await _db.SaveChangesAsync();

        // Sessão cancelada continua na base (o prontuário não se apaga) e não pode voltar
        // como se valesse — é o inverso da regra da via impressa, onde ela aparece MARCADA.
        (await _consultorio.CabecalhoAsync(id))!.UltimosDiagnosticos.Should().BeEmpty();
    }

    // ===== O histórico de sessões =====

    [Fact]
    public async Task Conta_so_a_sessao_que_ACONTECEU()
    {
        var id = await CriarAsync();
        _db.Atendimentos.Add(new Atendimento
        {
            PacienteId = id, Data = new DateOnly(2026, 3, 2),
            Modalidade = ModalidadeAtendimento.AcupunturaSimples,
            RealizadoEm = new DateTime(2026, 3, 2, 9, 0, 0)
        });
        _db.Atendimentos.Add(new Atendimento
        {
            PacienteId = id, Data = new DateOnly(2026, 5, 8),
            Modalidade = ModalidadeAtendimento.AcupunturaSimples,
            RealizadoEm = new DateTime(2026, 5, 8, 9, 0, 0)
        });
        // Marcada para semana que vem: desde a parcela 70 a guia nasce no agendamento, e
        // contar linhas de Atendimento somaria o que ainda não aconteceu.
        _db.Atendimentos.Add(new Atendimento
        {
            PacienteId = id, Data = new DateOnly(2026, 9, 1),
            Modalidade = ModalidadeAtendimento.AcupunturaSimples
        });
        await _db.SaveChangesAsync();

        var c = (await _consultorio.CabecalhoAsync(id))!;

        c.TotalSessoes.Should().Be(2);
        c.PrimeiraSessao.Should().Be(new DateOnly(2026, 3, 2));
        c.Linha.Should().Contain("paciente desde 02/03/2026");
        c.Linha.Should().Contain("2 sessões");
    }

    [Fact]
    public async Task Cadastro_novo_nao_inventa_data_nem_contagem()
    {
        var c = (await _consultorio.CabecalhoAsync(await CriarAsync()))!;

        c.PrimeiraSessao.Should().BeNull();
        c.TotalSessoes.Should().Be(0);
        c.Linha.Should().NotContain("desde");
        c.Linha.Should().NotContain("sessõe");
        // Sem alergia, sem problema e sem hipótese, a região do contexto clínico SOME em
        // vez de mostrar três traços.
        c.TemContextoClinico.Should().BeFalse();
    }

    [Fact]
    public async Task Paciente_inexistente_devolve_nulo_em_vez_de_um_cracha_vazio()
        => (await _consultorio.CabecalhoAsync(9999)).Should().BeNull();

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
