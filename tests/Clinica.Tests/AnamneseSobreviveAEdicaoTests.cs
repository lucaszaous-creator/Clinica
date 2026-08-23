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
/// Os campos da parcela 73 sobrevivem ao SALVAR, à EDIÇÃO e vão para a VERSÃO anterior.
///
/// ⚠️ Esta suíte nasce de um defeito que passou por tudo: <c>ProntuarioService.SalvarAsync</c>
/// copia campo a campo para a entidade rastreada, e os quatro que a parcela 73 criou
/// (história da doença atual, exame físico, hipótese, CID) <b>não estavam na lista</b>.
///
/// O efeito era o pior possível porque a CRIAÇÃO funcionava: o objeto é novo, então tudo era
/// gravado e a tela mostrava certo. Só a primeira EDIÇÃO — acrescentar uma linha à evolução —
/// apagava a anamnese, o exame físico, a hipótese e o CID. Sem erro, sem aviso. E
/// <c>GuardarVersao</c> também não os copiava, então a versão anterior <b>não os tinha</b>: o
/// dado sumia para sempre, que é justamente o que o ponto 2 do compromisso de conformidade e
/// o art. 3º da Lei 13.787/2018 proíbem.
///
/// A lição: <b>campo novo de prontuário entra em TRÊS lugares no mesmo commit</b> — a cópia do
/// serviço, o <c>GuardarVersao</c> e a validação de "evolução vazia". Nenhum dos três quebra o
/// build quando é esquecido, e os testes da parcela que criou os campos só exercitavam a
/// criação.
/// </summary>
public class AnamneseSobreviveAEdicaoTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ProntuarioService _servico;

    public AnamneseSobreviveAEdicaoTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var o = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(o);
        _db.Database.EnsureCreated();
        _servico = new ProntuarioService(new ClinicaRepositorio(_db));
    }

    private async Task<int> PacienteAsync()
    {
        var p = new Paciente { Nome = "Marisa", Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    private async Task<Evolucao> ComAnamneseAsync(int pacienteId)
        => await _servico.SalvarAsync(new Evolucao
        {
            PacienteId = pacienteId,
            Data = new DateOnly(2026, 8, 23),
            QueixaPrincipal = "lombalgia",
            HistoriaDoencaAtual = "há 3 meses, piora ao sentar",
            ExameFisico = "dor à palpação de L4-L5",
            HipoteseDiagnostica = "lombalgia mecânica",
            CidSessao = "M54.5",
            Conduta = "acupuntura, 20 min",
            PlanoTerapeutico = "10 sessões, 2x/semana, reavaliar EVA em 4 semanas"
        }, "medica");

    [Fact]
    public async Task Ao_CRIAR_a_sessao_os_quatro_campos_ficam_gravados()
    {
        var salva = await ComAnamneseAsync(await PacienteAsync());

        var lida = await _db.Evolucoes.AsNoTracking().FirstAsync(e => e.Id == salva.Id);
        lida.HistoriaDoencaAtual.Should().Be("há 3 meses, piora ao sentar");
        lida.ExameFisico.Should().Be("dor à palpação de L4-L5");
        lida.HipoteseDiagnostica.Should().Be("lombalgia mecânica");
        lida.CidSessao.Should().Be("M54.5");
    }

    [Fact]
    public async Task Ao_EDITAR_a_sessao_os_quatro_campos_NAO_se_perdem()
    {
        var pacienteId = await PacienteAsync();
        var salva = await ComAnamneseAsync(pacienteId);

        // ⚠️ Objeto NOVO, como as duas telas de produção fazem — nunca a entidade rastreada
        // que SalvarAsync devolveu. Mutar aquela e reenviá-la faria GuardarVersao copiar o
        // valor JÁ ALTERADO, e a versão anterior nasceria igual à nova.
        await _servico.SalvarAsync(new Evolucao
        {
            Id = salva.Id,
            PacienteId = pacienteId,
            Data = salva.Data,
            QueixaPrincipal = salva.QueixaPrincipal,
            HistoriaDoencaAtual = salva.HistoriaDoencaAtual,
            ExameFisico = salva.ExameFisico,
            HipoteseDiagnostica = salva.HipoteseDiagnostica,
            CidSessao = salva.CidSessao,
            Conduta = salva.Conduta,
            TextoEvolucao = "referiu melhora após a sessão"
        }, "medica");

        var lida = await _db.Evolucoes.AsNoTracking().FirstAsync(e => e.Id == salva.Id);
        lida.HistoriaDoencaAtual.Should().Be("há 3 meses, piora ao sentar");
        lida.ExameFisico.Should().Be("dor à palpação de L4-L5");
        lida.HipoteseDiagnostica.Should().Be("lombalgia mecânica");
        lida.CidSessao.Should().Be("M54.5");
    }

    [Fact]
    public async Task A_VERSAO_anterior_guarda_a_anamnese_que_foi_sobrescrita()
    {
        var pacienteId = await PacienteAsync();
        var salva = await ComAnamneseAsync(pacienteId);

        await _servico.SalvarAsync(new Evolucao
        {
            Id = salva.Id,
            PacienteId = pacienteId,
            Data = salva.Data,
            QueixaPrincipal = salva.QueixaPrincipal,
            HistoriaDoencaAtual = salva.HistoriaDoencaAtual,
            ExameFisico = "Lasègue positivo à direita",
            HipoteseDiagnostica = "hérnia de disco",
            CidSessao = "M51.1",
            Conduta = salva.Conduta
        }, "medica", motivoDaCorrecao: "corrigida após a ressonância");

        var versoes = await _servico.VersoesAsync(salva.Id);
        versoes.Should().ContainSingle();

        // É o que o art. 3º da Lei 13.787/2018 exige: a retificação é rastreável porque o
        // que o registro dizia ANTES continua recuperável.
        versoes[0].HipoteseDiagnostica.Should().Be("lombalgia mecânica");
        versoes[0].ExameFisico.Should().Be("dor à palpação de L4-L5");
        versoes[0].HistoriaDoencaAtual.Should().Be("há 3 meses, piora ao sentar");
        versoes[0].CidSessao.Should().Be("M54.5");
        versoes[0].Motivo.Should().Be("corrigida após a ressonância");
    }

    [Fact]
    public async Task Sessao_SO_com_anamnese_e_aceita()
    {
        var pacienteId = await PacienteAsync();

        // É o caso normal da PRIMEIRA consulta: história, achado e hipótese, antes de haver
        // conduta. Recusá-la como "evolução vazia" nomearia campos que o médico preencheu.
        var acao = () => _servico.SalvarAsync(new Evolucao
        {
            PacienteId = pacienteId,
            Data = new DateOnly(2026, 8, 23),
            HistoriaDoencaAtual = "há 3 meses",
            ExameFisico = "dor à palpação",
            HipoteseDiagnostica = "lombalgia mecânica"
        }, "medica");

        await acao.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Sessao_realmente_VAZIA_continua_recusada()
    {
        var pacienteId = await PacienteAsync();

        // O campo novo alarga a porta, não a abre: evolução sem nada continua sendo ruído
        // no prontuário. O CID sozinho não conta — ele é o rótulo de uma hipótese, não o
        // registro dela.
        var acao = () => _servico.SalvarAsync(new Evolucao
        {
            PacienteId = pacienteId,
            Data = new DateOnly(2026, 8, 23),
            CidSessao = "M54.5"
        }, "medica");

        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ao menos a dor*");
    }

    [Fact]
    public async Task Editar_pela_janela_do_BALCAO_nao_apaga_a_anamnese_do_medico()
    {
        var pacienteId = await PacienteAsync();
        var salva = await ComAnamneseAsync(pacienteId);

        // A janela da Recepção não TEM esses campos na tela; ela os carrega e os devolve
        // intactos. Este teste reproduz o que ela manda: os campos dela, mais os quatro
        // preservados. Sem a preservação, corrigir um horário no balcão apagaria a anamnese
        // que o médico escreveu — sem erro e sem aviso.
        await _servico.SalvarAsync(new Evolucao
        {
            Id = salva.Id,
            PacienteId = pacienteId,
            Data = salva.Data,
            QueixaPrincipal = salva.QueixaPrincipal,
            Conduta = salva.Conduta,
            TextoEvolucao = "corrigido pelo balcão",
            Orientacoes = salva.Orientacoes,
            HistoriaDoencaAtual = salva.HistoriaDoencaAtual,
            ExameFisico = salva.ExameFisico,
            HipoteseDiagnostica = salva.HipoteseDiagnostica,
            CidSessao = salva.CidSessao
        }, "recepcao");

        var lida = await _db.Evolucoes.AsNoTracking().FirstAsync(e => e.Id == salva.Id);
        lida.HipoteseDiagnostica.Should().Be("lombalgia mecânica");
        lida.ExameFisico.Should().Be("dor à palpação de L4-L5");
        lida.TextoEvolucao.Should().Be("corrigido pelo balcão");
    }

    // ===== O PLANO TERAPÊUTICO (parcela 75) =====

    /// <summary>
    /// O campo novo passa pelos MESMOS oito lugares — e este teste cobre os que o build não
    /// cobre: a cópia na edição e o congelamento na versão.
    ///
    /// Ele nasce nesta suíte de propósito, e não numa nova: ela existe justamente porque a
    /// parcela 73 acrescentou quatro campos e esqueceu dois desses lugares.
    /// </summary>
    [Fact]
    public async Task O_plano_sobrevive_a_EDICAO_e_vai_para_a_VERSAO()
    {
        var pacienteId = await PacienteAsync();
        var salva = await ComAnamneseAsync(pacienteId);
        salva.PlanoTerapeutico.Should().StartWith("10 sessões");

        await _servico.SalvarAsync(new Evolucao
        {
            Id = salva.Id,
            PacienteId = pacienteId,
            Data = salva.Data,
            QueixaPrincipal = salva.QueixaPrincipal,
            HistoriaDoencaAtual = salva.HistoriaDoencaAtual,
            ExameFisico = salva.ExameFisico,
            HipoteseDiagnostica = salva.HipoteseDiagnostica,
            CidSessao = salva.CidSessao,
            Conduta = salva.Conduta,
            PlanoTerapeutico = "alta em 2 sessões"
        }, "medica", motivoDaCorrecao: "paciente evoluiu melhor que o esperado");

        var lida = await _db.Evolucoes.AsNoTracking().FirstAsync(e => e.Id == salva.Id);
        lida.PlanoTerapeutico.Should().Be("alta em 2 sessões");

        var versoes = await _servico.VersoesAsync(salva.Id);
        versoes[0].PlanoTerapeutico.Should().StartWith("10 sessões");
    }

    [Fact]
    public async Task Sessao_SO_com_plano_e_aceita()
    {
        var pacienteId = await PacienteAsync();

        // O campo novo alarga a porta da validação de "evolução vazia" (lugar 5): sem isto,
        // a sessão em que o profissional só ajustou o plano seria recusada nomeando campos
        // que ele não precisava preencher.
        var acao = () => _servico.SalvarAsync(new Evolucao
        {
            PacienteId = pacienteId,
            Data = new DateOnly(2026, 8, 23),
            PlanoTerapeutico = "alta"
        }, "medica");

        await acao.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Editar_pela_janela_do_BALCAO_nao_apaga_o_plano()
    {
        var pacienteId = await PacienteAsync();
        var salva = await ComAnamneseAsync(pacienteId);

        // Lugar 6: quem não edita, PRESERVA. A janela do balcão não tem o campo na tela e
        // carrega/devolve o valor intacto — sem isso, corrigir um horário lá apagaria o
        // plano que o médico escreveu.
        await _servico.SalvarAsync(new Evolucao
        {
            Id = salva.Id,
            PacienteId = pacienteId,
            Data = salva.Data,
            QueixaPrincipal = salva.QueixaPrincipal,
            Conduta = salva.Conduta,
            TextoEvolucao = "corrigido pelo balcão",
            HistoriaDoencaAtual = salva.HistoriaDoencaAtual,
            ExameFisico = salva.ExameFisico,
            HipoteseDiagnostica = salva.HipoteseDiagnostica,
            CidSessao = salva.CidSessao,
            PlanoTerapeutico = salva.PlanoTerapeutico
        }, "recepcao");

        (await _db.Evolucoes.AsNoTracking().FirstAsync(e => e.Id == salva.Id))
            .PlanoTerapeutico.Should().StartWith("10 sessões");
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
