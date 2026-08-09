using System.Text;
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
/// Prontuário e escala EVA (parcela 2).
///
/// A regra que carrega a suíte: a EVA vale em PAR. Uma medida solta não diz se o
/// tratamento funcionou, então não pode entrar na evolução da dor — deixá-la entrar
/// faria a linha oscilar por falta de dado, não por dor.
/// </summary>
public class ProntuarioServiceTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly ProntuarioService _prontuario;
    private readonly EquipeService _equipe;

    private static readonly DateOnly Dia = new(2026, 8, 3);

    public ProntuarioServiceTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _prontuario = new ProntuarioService(_repo);
        _equipe = new EquipeService(_repo);
    }

    private async Task<int> CriarPacienteAsync(string nome = "Paciente")
    {
        var p = new Paciente { Nome = nome, Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    private Task<Evolucao> RegistrarAsync(
        int pacienteId, DateOnly data, int? antes, int? depois, string? queixa = "dor lombar")
        => _prontuario.SalvarAsync(new Evolucao
        {
            PacienteId = pacienteId,
            Data = data,
            EvaAntes = antes,
            EvaDepois = depois,
            QueixaPrincipal = queixa
        }, "secretaria");

    // ===== Registro =====

    [Fact]
    public async Task Salvar_RegistraASessaoNoProntuarioDoPaciente()
    {
        var pacienteId = await CriarPacienteAsync();

        await RegistrarAsync(pacienteId, Dia, 8, 3);

        var prontuario = await _prontuario.DoPacienteAsync(pacienteId);
        prontuario.Should().ContainSingle();
        prontuario[0].EvaAntes.Should().Be(8);
        prontuario[0].EvaDepois.Should().Be(3);
        prontuario[0].VariacaoEva.Should().Be(5);
    }

    [Fact]
    public async Task Salvar_GuardaQuemAtendeu()
    {
        var pacienteId = await CriarPacienteAsync();
        var prof = await _equipe.SalvarProfissionalAsync(new Profissional { Nome = "Ana" });

        await _prontuario.SalvarAsync(new Evolucao
        {
            PacienteId = pacienteId,
            ProfissionalId = prof.Id,
            Data = Dia,
            QueixaPrincipal = "dor no ombro"
        }, "secretaria");

        var prontuario = await _prontuario.DoPacienteAsync(pacienteId);
        prontuario[0].ProfissionalId.Should().Be(prof.Id);
        prontuario[0].Profissional!.Nome.Should().Be("Ana");
    }

    [Fact]
    public async Task Salvar_EditaSemCriarUmSegundoRegistro()
    {
        var pacienteId = await CriarPacienteAsync();
        var evolucao = await RegistrarAsync(pacienteId, Dia, 7, 4);

        await _prontuario.SalvarAsync(new Evolucao
        {
            Id = evolucao.Id,
            PacienteId = pacienteId,
            Data = Dia,
            EvaAntes = 7,
            EvaDepois = 2,
            QueixaPrincipal = "dor lombar"
        }, "secretaria");

        var prontuario = await _prontuario.DoPacienteAsync(pacienteId);
        prontuario.Should().ContainSingle();
        prontuario[0].EvaDepois.Should().Be(2);
        prontuario[0].AtualizadoEm.Should().NotBeNull();
    }

    [Fact]
    public async Task Salvar_EvaForaDaEscala_Falha()
    {
        var pacienteId = await CriarPacienteAsync();

        var acao = () => RegistrarAsync(pacienteId, Dia, 11, 3);

        await acao.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Salvar_SessaoTotalmenteVazia_Falha()
    {
        var pacienteId = await CriarPacienteAsync();

        var acao = () => RegistrarAsync(pacienteId, Dia, null, null, queixa: null);

        await acao.Should().ThrowAsync<InvalidOperationException>(
            "sessão sem dor medida e sem texto nenhum é ruído no prontuário");
    }

    [Fact]
    public async Task Salvar_SoComEva_EhAceita()
    {
        var pacienteId = await CriarPacienteAsync();

        await RegistrarAsync(pacienteId, Dia, 6, 2, queixa: null);

        (await _prontuario.DoPacienteAsync(pacienteId)).Should().ContainSingle();
    }

    [Fact]
    public async Task Salvar_DeixaRastroNaAuditoria()
    {
        var pacienteId = await CriarPacienteAsync();

        await RegistrarAsync(pacienteId, Dia, 8, 3);

        var evento = await _db.Auditoria.AsNoTracking().SingleAsync();
        evento.Acao.Should().Be("EvolucaoRegistrada");
        evento.Operador.Should().Be("secretaria");
        evento.Detalhe.Should().Contain("EVA 8→3");
    }

    [Fact]
    public async Task Cancelar_tira_do_prontuario_mas_NAO_apaga_a_linha()
    {
        var pacienteId = await CriarPacienteAsync();
        var evolucao = await RegistrarAsync(pacienteId, Dia, 8, 3);

        await _prontuario.CancelarAsync(evolucao.Id, "lançada no paciente errado", "secretaria");

        // Sai do prontuário que se lê…
        (await _prontuario.DoPacienteAsync(pacienteId)).Should().BeEmpty();

        // …e CONTINUA no banco, que é o que a Lei 13.787/2018 exige guardar por 20 anos.
        var guardada = await _db.Evolucoes.AsNoTracking().SingleAsync(e => e.Id == evolucao.Id);
        guardada.CanceladaEm.Should().NotBeNull();
        guardada.MotivoCancelamento.Should().Be("lançada no paciente errado");
        guardada.CanceladaPor.Should().Be("secretaria");

        (await _db.Auditoria.AsNoTracking().ToListAsync())
            .Should().Contain(e => e.Acao == "EvolucaoCancelada");
    }

    [Fact]
    public async Task Cancelar_exige_motivo()
    {
        var pacienteId = await CriarPacienteAsync();
        var evolucao = await RegistrarAsync(pacienteId, Dia, 8, 3);

        var acao = () => _prontuario.CancelarAsync(evolucao.Id, "  ", "secretaria");

        await acao.Should().ThrowAsync<InvalidOperationException>();
        (await _prontuario.DoPacienteAsync(pacienteId)).Should().HaveCount(1);
    }

    [Fact]
    public async Task Sessao_cancelada_nao_se_edita()
    {
        var pacienteId = await CriarPacienteAsync();
        var evolucao = await RegistrarAsync(pacienteId, Dia, 8, 3);
        await _prontuario.CancelarAsync(evolucao.Id, "engano", "secretaria");

        var acao = () => _prontuario.SalvarAsync(
            new Evolucao { Id = evolucao.Id, PacienteId = pacienteId, Data = Dia, TextoEvolucao = "outro" },
            "secretaria");

        await acao.Should().ThrowAsync<InvalidOperationException>();
    }

    // ===== Retificação rastreável (Lei 13.787/2018, art. 3º) =====

    [Fact]
    public async Task Corrigir_a_sessao_GUARDA_o_que_estava_escrito_antes()
    {
        var pacienteId = await CriarPacienteAsync();
        var evolucao = await RegistrarAsync(pacienteId, Dia, 8, 3);

        await _prontuario.SalvarAsync(new Evolucao
        {
            Id = evolucao.Id,
            PacienteId = pacienteId,
            Data = Dia,
            EvaAntes = 8,
            EvaDepois = 3,
            TextoEvolucao = "texto corrigido"
        }, "dra.ana", "corrigido o lado da queixa");

        var versoes = await _prontuario.VersoesAsync(evolucao.Id);

        versoes.Should().HaveCount(1, "a versão anterior tem de sobreviver à correção");
        versoes[0].Versao.Should().Be(1);
        versoes[0].QueixaPrincipal.Should().Be("dor lombar",
            "é EXATAMENTE esta a pergunta que uma perícia faz: o que estava escrito antes?");
        versoes[0].Motivo.Should().Be("corrigido o lado da queixa");
        versoes[0].SubstituidaPor.Should().Be("dra.ana");

        // E a sessão continua sendo UMA no prontuário — o histórico não vira sessão nova.
        (await _prontuario.DoPacienteAsync(pacienteId)).Should().HaveCount(1);
    }

    [Fact]
    public async Task Correcoes_sucessivas_empilham_versoes_em_ordem()
    {
        var pacienteId = await CriarPacienteAsync();
        var evolucao = await RegistrarAsync(pacienteId, Dia, 8, 3);

        foreach (var texto in new[] { "segunda", "terceira" })
            await _prontuario.SalvarAsync(new Evolucao
            {
                Id = evolucao.Id,
                PacienteId = pacienteId,
                Data = Dia,
                TextoEvolucao = texto
            }, "dra.ana");

        var versoes = await _prontuario.VersoesAsync(evolucao.Id);

        versoes.Select(v => v.Versao).Should().Equal(1, 2);
        versoes.Select(v => v.QueixaPrincipal).Should().Equal("dor lombar", null);
        versoes.Select(v => v.TextoEvolucao).Should().Equal(null, "segunda");
    }

    [Fact]
    public async Task Sessao_nova_nao_cria_versao()
    {
        var pacienteId = await CriarPacienteAsync();
        var evolucao = await RegistrarAsync(pacienteId, Dia, 8, 3);

        (await _prontuario.VersoesAsync(evolucao.Id)).Should().BeEmpty(
            "a versão 1 é a própria sessão; duplicá-la daria duas verdades sobre o mesmo registro");
    }

    // ===== Evolução da dor =====

    [Fact]
    public async Task EvolucaoDaDor_OrdenaCronologicamenteECalculaOGanho()
    {
        var pacienteId = await CriarPacienteAsync();
        // Registradas fora de ordem de propósito: o gráfico precisa do cronológico.
        await RegistrarAsync(pacienteId, Dia.AddDays(14), 4, 2);
        await RegistrarAsync(pacienteId, Dia, 8, 6);
        await RegistrarAsync(pacienteId, Dia.AddDays(7), 6, 4);

        var dor = await _prontuario.EvolucaoDaDorAsync(pacienteId);

        dor.Pontos.Select(p => p.Antes).Should().Equal(8, 6, 4);
        dor.DorInicial.Should().Be(8);
        dor.DorAtual.Should().Be(2);
        dor.GanhoAcumulado.Should().Be(6, "começou em 8 e está em 2");
        dor.AlivioMedioPorSessao.Should().Be(2);
    }

    [Fact]
    public async Task EvolucaoDaDor_IgnoraSessaoComMedidaPelaMetade()
    {
        var pacienteId = await CriarPacienteAsync();
        await RegistrarAsync(pacienteId, Dia, 8, 3);
        await RegistrarAsync(pacienteId, Dia.AddDays(7), 7, null);

        var dor = await _prontuario.EvolucaoDaDorAsync(pacienteId);

        dor.SessoesRegistradas.Should().Be(2);
        dor.SessoesComMedida.Should().Be(1,
            "meia medida não diz se aliviou — entraria no gráfico como ruído");
        dor.DorAtual.Should().Be(3);
    }

    [Fact]
    public async Task EvolucaoDaDor_SemNenhumaMedida_NaoInventaNumero()
    {
        var pacienteId = await CriarPacienteAsync();
        await RegistrarAsync(pacienteId, Dia, null, null, "só conversa");

        var dor = await _prontuario.EvolucaoDaDorAsync(pacienteId);

        dor.SessoesComMedida.Should().Be(0);
        dor.DorInicial.Should().BeNull();
        dor.DorAtual.Should().BeNull();
        dor.GanhoAcumulado.Should().BeNull();
        dor.AlivioMedioPorSessao.Should().Be(0);
    }

    [Fact]
    public async Task EvolucaoDaDor_QuandoPiora_OGanhoFicaNegativo()
    {
        var pacienteId = await CriarPacienteAsync();
        await RegistrarAsync(pacienteId, Dia, 3, 3);
        await RegistrarAsync(pacienteId, Dia.AddDays(7), 5, 6);

        var dor = await _prontuario.EvolucaoDaDorAsync(pacienteId);

        dor.GanhoAcumulado.Should().Be(-3, "começou em 3 e está em 6");
    }

    // ===== Anexos =====

    [Fact]
    public async Task Anexar_GuardaOArquivoEOTamanho()
    {
        var pacienteId = await CriarPacienteAsync();
        var evolucao = await RegistrarAsync(pacienteId, Dia, 8, 3);
        var conteudo = Encoding.UTF8.GetBytes("laudo do exame");

        await _prontuario.AnexarAsync(
            evolucao.Id, "laudo.pdf", conteudo, TipoAnexo.Documento, operador: "secretaria");

        var anexos = await _prontuario.AnexosAsync(evolucao.Id);
        anexos.Should().ContainSingle();
        anexos[0].NomeArquivo.Should().Be("laudo.pdf");
        anexos[0].Tamanho.Should().Be(conteudo.Length);

        (await _prontuario.ConteudoAnexoAsync(anexos[0].Id)).Should().Equal(conteudo);
    }

    [Fact]
    public async Task Anexar_ArquivoVazio_Falha()
    {
        var pacienteId = await CriarPacienteAsync();
        var evolucao = await RegistrarAsync(pacienteId, Dia, 8, 3);

        var acao = () => _prontuario.AnexarAsync(evolucao.Id, "vazio.pdf", []);

        await acao.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Anexar_AcimaDoLimite_Falha()
    {
        var pacienteId = await CriarPacienteAsync();
        var evolucao = await RegistrarAsync(pacienteId, Dia, 8, 3);
        var gigante = new byte[ProntuarioService.TamanhoMaximoAnexo + 1];

        var acao = () => _prontuario.AnexarAsync(evolucao.Id, "enorme.jpg", gigante);

        await acao.Should().ThrowAsync<InvalidOperationException>(
            "um anexo gigante trava a sincronização de todo mundo no banco remoto");
    }

    [Fact]
    public async Task Cancelar_a_sessao_NAO_apaga_os_anexos()
    {
        var pacienteId = await CriarPacienteAsync();
        var evolucao = await RegistrarAsync(pacienteId, Dia, 8, 3);
        await _prontuario.AnexarAsync(evolucao.Id, "laudo.pdf", [1, 2, 3], TipoAnexo.Documento);

        await _prontuario.CancelarAsync(evolucao.Id, "paciente trocado", "secretaria");

        // Antes da parcela 52 este método fazia Remove() e levava o anexo junto. O laudo
        // que sustentou uma conduta é parte da prova de que a conduta era razoável — e a
        // guarda de 20 anos não admite que um clique o destrua.
        (await _db.AnexosProntuario.AsNoTracking().ToListAsync())
            .Should().HaveCount(1, "o anexo é registro clínico e fica guardado com a sessão");
    }

    [Fact]
    public async Task Anexo_retirado_sai_da_lista_e_continua_no_banco()
    {
        var pacienteId = await CriarPacienteAsync();
        var evolucao = await RegistrarAsync(pacienteId, Dia, 8, 3);
        var anexo = await _prontuario.AnexarAsync(evolucao.Id, "errado.pdf", [1, 2, 3], TipoAnexo.Documento);

        await _prontuario.CancelarAnexoAsync(anexo.Id, "laudo de outro paciente", "secretaria");

        (await _prontuario.AnexosAsync(evolucao.Id)).Should().BeEmpty();
        (await _db.AnexosProntuario.AsNoTracking().ToListAsync()).Should().HaveCount(1);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
