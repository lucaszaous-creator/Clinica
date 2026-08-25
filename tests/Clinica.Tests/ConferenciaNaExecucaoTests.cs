using Clinica.Application.Assinatura;
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
/// A CONFERÊNCIA NA HORA DE ADMINISTRAR, e o COREN obrigatório (parcela 72).
///
/// O buraco que estes testes fecham
/// --------------------------------
/// <c>PrescricaoInternaService.AssinarAsync</c> confere a folha contra as alergias do
/// paciente e RECUSA sem confirmação escrita desde a parcela 42. A EXECUÇÃO não conferia
/// nada: as únicas guardas eram "item já checado" e hora futura.
///
/// O caminho de dano é inteiro e concreto — e mora dentro do que a parcela 71 construiu:
/// a folha é assinada de manhã, sem alergia registrada; o item 2 causa reação; a PRÓPRIA
/// técnica grava a alergia pelo campo "Reação a registrar como alergia"; e os itens 3, 4 e
/// 5 seguem pendentes, com a folha na sala, sem ninguém reconferir. O sistema tinha o dado
/// — gravado por quem seria a vítima do silêncio — e não o usava.
///
/// ⚠️ <c>Alergia_gravada_na_propria_execucao_bloqueia_o_item_seguinte</c> é o teste que
/// reproduz esse caminho de ponta a ponta. Ele FALHA no código anterior a esta parcela.
/// </summary>
public class ConferenciaNaExecucaoTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly PrescricaoInternaService _prescricoes;
    private readonly ChecagemPrescricaoService _checagens;
    private readonly EvolucaoEnfermagemService _evolucoes;

    private static readonly DateTime MeioDia = DateTime.Today.AddHours(12);
    private static readonly TimeOnly Manha = new(9, 30);

    private static readonly IdentificacaoExecutante Tecnica =
        new(null, "Joana Técnica", "COREN-SP 999999");

    /// <summary>O login sem <c>Profissional</c> vinculado — o caso que a recusa cobre.</summary>
    private static readonly IdentificacaoExecutante SemConselho =
        new(null, "Joana Técnica");

    public ConferenciaNaExecucaoTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        _db = new ClinicaDbContext(
            new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        var conferencia = new PrescricaoService(_repo);
        _prescricoes = new PrescricaoInternaService(_repo, conferencia);
        _checagens = new ChecagemPrescricaoService(_repo, () => MeioDia, conferencia);
        _evolucoes = new EvolucaoEnfermagemService(_repo, () => MeioDia);
    }

    private async Task<PrescricaoInterna> PreparadaAsync(params string[] itens)
    {
        var paciente = new Paciente { Nome = "Maria", Convenio = Convenio.UnimedIntercambio };
        var medica = new Profissional
        {
            Nome = "Dra. Ana", RegistroConselho = "CRM 1", Cpf = "12345678909"
        };
        _db.Pacientes.Add(paciente);
        _db.Profissionais.Add(medica);
        await _db.SaveChangesAsync();

        var p = await _prescricoes.CriarAsync(paciente.Id, medica.Id);
        await _prescricoes.SalvarRascunhoAsync(
            p.Id, "Crise álgica", null,
            itens.Select(i => new ItemPrescricaoInterna { Descricao = i, Dose = "1 g" }).ToList());

        var carregada = (await _repo.ObterPrescricaoInternaAsync(p.Id))!;
        carregada.Situacao = SituacaoPrescricao.Assinada;
        carregada.Assinaturas.Add(new AssinaturaDocumento
        {
            Papel = PapelAssinatura.Prescritor,
            NomeAssinante = "Dra. Ana",
            CpfAssinante = "12345678909",
            AssinadoEm = MeioDia
        });
        await _repo.SalvarAsync();
        return carregada;
    }

    private async Task RegistrarAlergiaAsync(int pacienteId, string agente)
    {
        await _repo.AdicionarProblemaAsync(new ProblemaPaciente
        {
            PacienteId = pacienteId,
            Natureza = NaturezaProblema.Alergia,
            Descricao = agente,
            Situacao = SituacaoProblema.Ativo,
            Inicio = DateOnly.FromDateTime(MeioDia)
        });
        await _repo.SalvarAsync();
    }

    // ---- A conferência de alergia na administração ----

    [Fact]
    public async Task Administrar_alergeno_registrado_e_recusado_sem_confirmacao()
    {
        var p = await PreparadaAsync("Dipirona 500mg", "Soro fisiológico 500ml");
        await RegistrarAlergiaAsync(p.PacienteId, "Dipirona");

        var dipirona = (await _repo.ObterPrescricaoInternaAsync(p.Id))!.Itens
            .First(i => i.Descricao.Contains("Dipirona"));

        var recusa = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _checagens.ChecarAsync(dipirona.Id, SituacaoChecagem.Realizado, Manha, Tecnica));

        recusa.Message.Should().Contain("ALERGIA");
        // A frase nomeia o agente: "há uma alergia" sem dizer qual não decide nada.
        recusa.Message.Should().Contain("Dipirona");
    }

    [Fact]
    public async Task Confirmada_a_alergia_a_administracao_passa_e_fica_registrada()
    {
        var p = await PreparadaAsync("Dipirona 500mg");
        await RegistrarAlergiaAsync(p.PacienteId, "Dipirona");

        var dipirona = (await _repo.ObterPrescricaoInternaAsync(p.Id))!.Itens.Single();

        // Avisa e exige confirmação — NÃO impede. Pode haver dessensibilização, o registro
        // pode estar errado, e quem está com o paciente é quem decide.
        var checagem = await _checagens.ChecarAsync(
            dipirona.Id, SituacaoChecagem.Realizado, Manha, Tecnica, confirmouAlergia: true);

        checagem.Situacao.Should().Be(SituacaoChecagem.Realizado);
    }

    [Fact]
    public async Task Item_que_nao_bate_com_alergia_nenhuma_passa_direto()
    {
        var p = await PreparadaAsync("Dipirona 500mg", "Soro fisiológico 500ml");
        await RegistrarAlergiaAsync(p.PacienteId, "Dipirona");

        var soro = (await _repo.ObterPrescricaoInternaAsync(p.Id))!.Itens
            .First(i => i.Descricao.Contains("Soro"));

        // ⚠️ A conferência é do ITEM, não da folha: repetir a resposta da folha inteira
        // acenderia na linha do soro por causa da dipirona da linha de cima — e alerta que
        // dispara à toa é alerta que se fecha sem ler.
        var checagem = await _checagens.ChecarAsync(
            soro.Id, SituacaoChecagem.Realizado, Manha, Tecnica);

        checagem.Situacao.Should().Be(SituacaoChecagem.Realizado);
    }

    [Fact]
    public async Task Nao_realizar_o_alergeno_nunca_pede_confirmacao()
    {
        var p = await PreparadaAsync("Dipirona 500mg");
        await RegistrarAlergiaAsync(p.PacienteId, "Dipirona");

        var dipirona = (await _repo.ObterPrescricaoInternaAsync(p.Id))!.Itens.Single();

        // Não administrar é o desfecho SEGURO. Cobrar confirmação para a rodela treinaria
        // a equipe a confirmar sem ler, que é como se mata um alerta.
        var checagem = await _checagens.ChecarAsync(
            dipirona.Id, SituacaoChecagem.NaoRealizado, Manha, Tecnica,
            justificativa: "Paciente é alérgico — suspenso até falar com a médica.");

        checagem.Situacao.Should().Be(SituacaoChecagem.NaoRealizado);
    }

    [Fact]
    public async Task Retificar_para_realizado_confere_igual()
    {
        var p = await PreparadaAsync("Dipirona 500mg");
        await RegistrarAlergiaAsync(p.PacienteId, "Dipirona");

        var dipirona = (await _repo.ObterPrescricaoInternaAsync(p.Id))!.Itens.Single();
        await _checagens.ChecarAsync(
            dipirona.Id, SituacaoChecagem.NaoRealizado, Manha, Tecnica,
            justificativa: "Paciente recusou.");

        // Retificar de "não realizado" para "realizado" É administrar: deixar a conferência
        // só na checagem normal seria a cópia que fica para trás — o caminho de volta pelo
        // qual a recusa não vale nada.
        var recusa = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _checagens.RetificarAsync(
                dipirona.Id, SituacaoChecagem.Realizado, Manha, Tecnica,
                motivoRetificacao: "Paciente mudou de ideia."));

        recusa.Message.Should().Contain("ALERGIA");
    }

    [Fact]
    public async Task Alergia_gravada_na_propria_execucao_bloqueia_o_item_seguinte()
    {
        // O caso da clínica, inteiro: a folha é assinada de manhã, o item 1 causa reação,
        // a técnica grava a alergia ali mesmo — e o item 2, do mesmo agente, seguia
        // administrável porque ninguém reconferia.
        var p = await PreparadaAsync("Dipirona 500mg EV", "Dipirona 500mg VO");
        var itens = (await _repo.ObterPrescricaoInternaAsync(p.Id))!.Itens
            .OrderBy(i => i.Ordem).ToList();

        await _checagens.ChecarAsync(
            itens[0].Id, SituacaoChecagem.NaoRealizado, Manha, Tecnica,
            justificativa: "Apresentou reação alérgica.",
            alergiaObservada: "Dipirona");

        var recusa = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _checagens.ChecarAsync(itens[1].Id, SituacaoChecagem.Realizado, Manha, Tecnica));

        recusa.Message.Should().Contain("ALERGIA");
    }

    [Fact]
    public async Task A_conferencia_da_execucao_e_a_mesma_da_assinatura()
    {
        // ⚠️ Uma definição só. Duas responderiam diferente sobre o MESMO item, e a que
        // ninguém lembraria de ajustar é a de baixo — onde o erro entra na veia.
        var p = await PreparadaAsync("Dipirona 500mg");
        await RegistrarAlergiaAsync(p.PacienteId, "Dipirona");

        var conferencia = await _prescricoes.ConferirParaAssinaturaAsync(p.Id);
        conferencia.ExigeConfirmacao.Should().BeTrue();

        var item = (await _repo.ObterPrescricaoInternaAsync(p.Id))!.Itens.Single();
        var recusa = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _checagens.ChecarAsync(item.Id, SituacaoChecagem.Realizado, Manha, Tecnica));

        recusa.Message.Should().Contain("ALERGIA");
    }

    // ---- O COREN obrigatório ----

    [Fact]
    public async Task Checar_sem_registro_no_conselho_e_recusado()
    {
        var p = await PreparadaAsync("Soro fisiológico 500ml");
        var item = (await _repo.ObterPrescricaoInternaAsync(p.Id))!.Itens.Single();

        var recusa = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _checagens.ChecarAsync(item.Id, SituacaoChecagem.Realizado, Manha, SemConselho));

        recusa.Message.Should().Contain("conselho");
        // A frase nomeia o CONSERTO: degradar em silêncio é o que faz a clínica descobrir
        // meses depois, com a base cheia de registro sem COREN.
        recusa.Message.Should().Contain("Acessos");
    }

    [Fact]
    public async Task Evolucao_de_enfermagem_sem_conselho_e_recusada()
    {
        var paciente = new Paciente { Nome = "Maria", Convenio = Convenio.UnimedIntercambio };
        _db.Pacientes.Add(paciente);
        await _db.SaveChangesAsync();

        var recusa = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _evolucoes.RegistrarAsync(
                paciente.Id, DateOnly.FromDateTime(MeioDia), new TimeOnly(9, 0),
                "Curativo trocado.", SemConselho));

        recusa.Message.Should().Contain("conselho");
    }

    [Fact]
    public async Task Com_o_conselho_o_registro_grava_e_o_numero_fica_COPIADO()
    {
        var paciente = new Paciente { Nome = "Maria", Convenio = Convenio.UnimedIntercambio };
        _db.Pacientes.Add(paciente);
        await _db.SaveChangesAsync();

        var e = await _evolucoes.RegistrarAsync(
            paciente.Id, DateOnly.FromDateTime(MeioDia), new TimeOnly(9, 0),
            "Curativo trocado.", Tecnica);

        // Copiado no ato — e é por ser cópia que a recusa tinha de entrar ANTES de a base
        // encher: corrigir depois exigiria retificar registro a registro, com motivo.
        e.AutorConselho.Should().Be("COREN-SP 999999");
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }
}
