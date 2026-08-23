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
/// A ANAMNESE DO PACIENTE (parcela 75) — o que se pergunta uma vez e se revisa.
///
/// Ela responde <i>"quem é esta pessoa"</i>, enquanto a evolução responde <i>"o que
/// aconteceu hoje"</i>. As asserções que carregam esta suíte são as de CONFORMIDADE: a
/// anamnese é registro clínico, então alterar guarda o que ela dizia antes (ponto 2 do
/// compromisso, art. 3º da Lei 13.787/2018) e nada se apaga.
/// </summary>
public class AnamneseDoPacienteTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly AnamneseService _servico;

    public AnamneseDoPacienteTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var o = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(o);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _servico = new AnamneseService(_repo);
    }

    private async Task<int> PacienteAsync()
    {
        var p = new Paciente { Nome = "Marisa", Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    private static AnamnesePaciente Colhida() => new()
    {
        AntecedentesPessoais = "Apendicectomia em 2015. Nega HAS e DM.",
        AntecedentesFamiliares = "Mãe hipertensa. Pai infartou aos 58.",
        HabitosDeVida = "Nega tabagismo. Sedentária. Dorme 5h por noite.",
        RevisaoDeSistemas = "Nega dispneia, palpitação, febre."
    };

    // ===== Colher =====

    [Fact]
    public async Task Ainda_nao_colhida_devolve_NULO_e_nao_um_objeto_vazio()
    {
        // "Ainda não perguntamos" e "perguntamos e não há nada" são respostas diferentes, e a
        // segunda é a que o objeto vazio contaria — a tela escreveria "sem antecedentes"
        // sobre uma ficha que ninguém abriu.
        (await _servico.DoPacienteAsync(await PacienteAsync())).Should().BeNull();
    }

    [Fact]
    public async Task Colher_grava_os_campos_e_deixa_linha_na_trilha()
    {
        var id = await PacienteAsync();

        var a = await _servico.SalvarAsync(id, Colhida(), "dra.ana");

        a.AntecedentesFamiliares.Should().Be("Mãe hipertensa. Pai infartou aos 58.");
        a.CriadaPor.Should().Be("dra.ana");
        a.Versoes.Should().BeEmpty();

        var trilha = await _db.Auditoria.Where(e => e.Acao == "AnamneseColhida").ToListAsync();
        trilha.Should().ContainSingle();
        trilha[0].PacienteId.Should().Be(id);
    }

    [Fact]
    public async Task Anamnese_em_BRANCO_e_recusada()
    {
        var id = await PacienteAsync();

        // Sem esta recusa, um clique no Salvar sem nada digitado criaria a linha, carimbaria
        // "revisada hoje" e faria a ficha AFIRMAR que a anamnese foi colhida. Registro em
        // branco que parece registro é pior do que registro nenhum.
        var acao = () => _servico.SalvarAsync(id, new AnamnesePaciente(), "dra.ana");

        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ao menos um dos campos*");
    }

    [Fact]
    public async Task Espaco_em_branco_nao_conta_como_conteudo()
    {
        var id = await PacienteAsync();

        var acao = () => _servico.SalvarAsync(
            id, new AnamnesePaciente { HabitosDeVida = "   \n  " }, "dra.ana");

        await acao.Should().ThrowAsync<InvalidOperationException>();
    }

    // ===== Revisar =====

    [Fact]
    public async Task Revisar_GUARDA_o_que_a_anamnese_dizia_antes()
    {
        var id = await PacienteAsync();
        await _servico.SalvarAsync(id, Colhida(), "dra.ana");

        var revisada = Colhida();
        revisada.HabitosDeVida = "Tabagista, 20 maços-ano. Passou a caminhar 3x/semana.";
        await _servico.SalvarAsync(id, revisada, "dra.ana", "paciente revelou tabagismo");

        var a = (await _servico.DoPacienteAsync(id))!;
        a.HabitosDeVida.Should().StartWith("Tabagista");

        // É o art. 3º da Lei 13.787/2018: a retificação é rastreável porque o que o registro
        // dizia ANTES continua recuperável. Sem isto, corrigir "nega tabagismo" para
        // "tabagista" apagaria a informação de que a pessoa havia NEGADO.
        var versoes = await _servico.VersoesAsync(a.Id);
        versoes.Should().ContainSingle();
        versoes[0].Versao.Should().Be(1);
        versoes[0].HabitosDeVida.Should().StartWith("Nega tabagismo");
        versoes[0].Motivo.Should().Be("paciente revelou tabagismo");
        versoes[0].SubstituidaPor.Should().Be("dra.ana");
    }

    [Fact]
    public async Task A_numeracao_das_versoes_NAO_reinicia_na_segunda_correcao()
    {
        var id = await PacienteAsync();
        await _servico.SalvarAsync(id, Colhida(), "dra.ana");

        for (var i = 2; i <= 4; i++)
        {
            var r = Colhida();
            r.Observacoes = $"revisão {i}";
            await _servico.SalvarAsync(id, r, "dra.ana");
        }

        var a = (await _servico.DoPacienteAsync(id))!;
        var versoes = await _servico.VersoesAsync(a.Id);

        // ⚠️ A numeração sai da CONTAGEM das versões já gravadas, e por isso a leitura tem de
        // trazê-las (Include). Sem elas a contagem seria sempre zero e toda correção nasceria
        // como "versão 1" — o histórico existiria e não se leria em ordem.
        versoes.Select(v => v.Versao).Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task Revisar_deixa_linha_PROPRIA_na_trilha()
    {
        var id = await PacienteAsync();
        await _servico.SalvarAsync(id, Colhida(), "dra.ana");
        await _servico.SalvarAsync(id, Colhida(), "dra.ana");

        // Colher e revisar são atos diferentes: juntá-los apagaria de quem lê a trilha a
        // diferença entre a primeira consulta e uma correção.
        (await _db.Auditoria.CountAsync(e => e.Acao == "AnamneseColhida")).Should().Be(1);
        (await _db.Auditoria.CountAsync(e => e.Acao == "AnamneseRevisada")).Should().Be(1);
    }

    // ===== O invariante da unicidade =====

    [Fact]
    public async Task Salvar_duas_vezes_NAO_cria_duas_anamneses()
    {
        var id = await PacienteAsync();
        await _servico.SalvarAsync(id, Colhida(), "dra.ana");
        await _servico.SalvarAsync(id, Colhida(), "dr.bruno");

        // Duas dariam duas verdades sobre a mesma pessoa, e a tela escolheria uma sem dizer
        // qual. O índice único é a rede; este teste prova que o serviço não esbarra nele.
        (await _db.Anamneses.CountAsync(a => a.PacienteId == id)).Should().Be(1);
    }

    [Fact]
    public async Task A_anamnese_de_um_paciente_nao_vaza_para_outro()
    {
        var um = await PacienteAsync();
        var outro = await PacienteAsync();
        await _servico.SalvarAsync(um, Colhida(), "dra.ana");

        (await _servico.DoPacienteAsync(outro)).Should().BeNull();
    }

    [Fact]
    public async Task Paciente_inexistente_e_recusado()
    {
        var acao = () => _servico.SalvarAsync(9999, Colhida(), "dra.ana");
        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*não encontrado*");
    }

    // ===== A leitura da tela =====

    [Fact]
    public async Task UltimaRevisao_diz_quando_ela_foi_TOCADA_pela_ultima_vez()
    {
        var id = await PacienteAsync();
        var a = await _servico.SalvarAsync(id, Colhida(), "dra.ana");
        a.UltimaRevisao.Should().Be(a.CriadaEm);

        await _servico.SalvarAsync(id, Colhida(), "dra.ana");
        var relida = (await _servico.DoPacienteAsync(id))!;

        // Anamnese de três anos atrás não está errada; está VELHA, e as duas coisas se
        // tratam diferente. É esta data que deixa quem atende decidir se vale reperguntar.
        relida.UltimaRevisao.Should().Be(relida.AtualizadaEm!.Value);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
