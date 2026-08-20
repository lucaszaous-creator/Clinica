using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
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
/// A 2ª ASSINATURA — a eletrônica da ENFERMAGEM sobre o registro de execução (decisão da
/// direção, 14/08/2026).
///
/// O que mudou, e o que NÃO mudou
/// ------------------------------
/// Até aqui a execução era assinada só à caneta, na via impressa — e nas folhas sem o
/// campo marcado continua sendo (o teste do regime padrão mora em
/// <c>PrescricaoInternaPdfTests</c>). O que a direção pediu é a ESCOLHA, folha a folha:
/// marcada, a enfermeira sela o registro de execução com o certificado DELA, no
/// encerramento.
///
/// A restrição que desenhou tudo: em PDF não se assina incrementalmente, então a 2ª
/// assinatura NUNCA é no mesmo arquivo — são dois documentos encadeados, um por
/// signatário. O motor congelado (AssinaturaDigitalService, SafeID) é reusado tal e qual;
/// estes testes provam a ORQUESTRAÇÃO em volta dele.
/// </summary>
public class SegundaAssinaturaExecucaoTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly PrescricaoInternaService _prescricoes;
    private readonly ChecagemPrescricaoService _checagens;
    private readonly PrescricaoInternaPdfService _pdfs;
    private readonly AssinaturaDigitalService _assinador = new(exigirCadeiaConfiavel: false);
    private readonly AssinaturaDePrescricaoService _orquestra;

    private const string CpfMedica = "12345678909";
    private const string CpfEnfermeira = "98765432100";

    private static readonly IdentificacaoExecutante Tecnica =
        new(null, "Joana Técnica", "COREN-SP 999999");

    public SegundaAssinaturaExecucaoTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);

        var conferencia = new PrescricaoService(_repo);
        _prescricoes = new PrescricaoInternaService(_repo, conferencia);
        _checagens = new ChecagemPrescricaoService(_repo, () => DateTime.Today.AddHours(12));
        _pdfs = new PrescricaoInternaPdfService(_repo, conferencia);
        _orquestra = new AssinaturaDePrescricaoService(
            _repo, _prescricoes, _pdfs, _assinador, new ParametrosService(_repo));
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }

    // ==================== O campo ====================

    [Fact]
    public async Task O_campo_marcado_persiste_e_desmarcado_mantem_o_regime_do_papel()
    {
        var cenario = await CenarioAsync();
        var prescricao = await RascunhoAsync(cenario, exigir: true);

        (await _repo.ObterPrescricaoInternaAsync(prescricao.Id))!
            .ExigeAssinaturaEletronicaDaExecucao.Should().BeTrue();

        var semPedido = await RascunhoAsync(cenario, exigir: false);
        (await _repo.ObterPrescricaoInternaAsync(semPedido.Id))!
            .ExigeAssinaturaEletronicaDaExecucao.Should().BeFalse();
    }

    /// <summary>
    /// O parâmetro é anulável de propósito: nulo significa "quem chamou não mexe no
    /// campo". Sem isso, um chamador antigo do SalvarRascunho DESMARCARIA a 2ª assinatura
    /// a cada salvamento, em silêncio — e a folha encerraria sem colher o que a médica
    /// pediu.
    /// </summary>
    [Fact]
    public async Task Salvar_sem_falar_do_campo_nao_desmarca_o_que_a_medica_pediu()
    {
        var cenario = await CenarioAsync();
        var prescricao = await RascunhoAsync(cenario, exigir: true);

        await _prescricoes.SalvarRascunhoAsync(prescricao.Id, "Indicação editada", null,
            [new ItemPrescricaoInterna { Descricao = "Soro fisiológico 0,9%", Dose = "500 mL" }]);

        (await _repo.ObterPrescricaoInternaAsync(prescricao.Id))!
            .ExigeAssinaturaEletronicaDaExecucao.Should().BeTrue();
    }

    // ==================== As recusas ====================

    [Fact]
    public async Task Nao_se_assina_execucao_de_folha_que_ainda_nao_encerrou()
    {
        var cenario = await CenarioAsync();
        var prescricao = await AssinadaPelaMedicaAsync(cenario, exigir: true);
        // Em execução — checada, mas NÃO encerrada.
        await ChecarTudoAsync(prescricao);

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _orquestra.AssinarExecucaoAsync(
                prescricao.Id, ECpfDeTeste("Joana Técnica", CpfEnfermeira),
                cenario.UsuarioEnfermeiraId));

        erro.Message.Should().Contain("ainda não foi encerrada");
    }

    [Fact]
    public async Task Folha_sem_o_campo_marcado_recusa_a_assinatura_eletronica()
    {
        var cenario = await CenarioAsync();
        var prescricao = await EncerradaAsync(cenario, exigir: false);

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _orquestra.AssinarExecucaoAsync(
                prescricao.Id, ECpfDeTeste("Joana Técnica", CpfEnfermeira),
                cenario.UsuarioEnfermeiraId));

        erro.Message.Should().Contain("via impressa");
    }

    /// <summary>
    /// O teste que dá sentido à feature inteira: o certificado é conferido contra o CPF de
    /// QUEM ESTÁ ASSINANDO — a enfermeira logada —, nunca contra o do prescritor. Sem
    /// isto, o e-CPF da médica assinaria a execução e o documento diria que quem executou
    /// foi quem não executou.
    /// </summary>
    [Fact]
    public async Task O_certificado_da_MEDICA_nao_assina_a_execucao_da_enfermeira()
    {
        var cenario = await CenarioAsync();
        var prescricao = await EncerradaAsync(cenario, exigir: true);

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _orquestra.AssinarExecucaoAsync(
                prescricao.Id, ECpfDeTeste("Dra. Ana Souza", CpfMedica),
                cenario.UsuarioEnfermeiraId));

        erro.Message.Should().Contain("Cada um assina com o próprio certificado");

        // E a folha continua aguardando — nada foi gravado pela metade.
        (await _repo.ObterPrescricaoInternaAsync(prescricao.Id))!
            .AguardaAssinaturaDaExecucao.Should().BeTrue();
    }

    [Fact]
    public async Task Usuario_sem_vinculo_com_profissional_e_recusado_dizendo_onde_corrigir()
    {
        var cenario = await CenarioAsync();
        var prescricao = await EncerradaAsync(cenario, exigir: true);

        var soLogin = new UsuarioSistema { Nome = "Balcão", Login = "balcao", ProfissionalId = null };
        _db.Usuarios.Add(soLogin);
        await _db.SaveChangesAsync();

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _orquestra.AssinarExecucaoAsync(
                prescricao.Id, ECpfDeTeste("Balcão", CpfEnfermeira), soLogin.Id));

        erro.Message.Should().Contain("Equipe");
    }

    [Fact]
    public async Task Duas_assinaturas_da_execucao_nao_existem()
    {
        var cenario = await CenarioAsync();
        var prescricao = await EncerradaAsync(cenario, exigir: true);

        await _orquestra.AssinarExecucaoAsync(
            prescricao.Id, ECpfDeTeste("Joana Técnica", CpfEnfermeira),
            cenario.UsuarioEnfermeiraId);

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _orquestra.AssinarExecucaoAsync(
                prescricao.Id, ECpfDeTeste("Joana Técnica", CpfEnfermeira),
                cenario.UsuarioEnfermeiraId));

        erro.Message.Should().Contain("já foi assinado");
    }

    // ==================== O caminho completo ====================

    [Fact]
    public async Task A_enfermeira_assina_e_as_DUAS_assinaturas_convivem_cada_uma_no_seu_arquivo()
    {
        var cenario = await CenarioAsync();
        var prescricao = await EncerradaAsync(cenario, exigir: true);

        var assinada = await _orquestra.AssinarExecucaoAsync(
            prescricao.Id, ECpfDeTeste("Joana Técnica", CpfEnfermeira),
            cenario.UsuarioEnfermeiraId, operador: "joana");

        // Dois signatários, dois papéis. O ARQUIVO da enfermeira é a MESMA folha da médica
        // com uma revisão anexada — os bytes dela ficam intactos como prefixo —, e a linha
        // dela aponta para esse arquivo novo, não para o da médica.
        assinada.Assinaturas.Should().HaveCount(2);

        var daMedica = assinada.AssinaturaDoPrescritor!;
        var daEnfermeira = assinada.AssinaturaDaExecucao!;

        daEnfermeira.Papel.Should().Be(PapelAssinatura.Executante);
        daEnfermeira.CpfAssinante.Should().Be(CpfEnfermeira);
        daEnfermeira.NomeAssinante.Should().Be("Joana Técnica");
        daEnfermeira.ArquivoId.Should().NotBe(daMedica.ArquivoId);

        // A do prescritor não foi tocada — o arquivo dela continua selado e íntegro.
        var arquivoDaMedica = (await _repo.ObterArquivoAssinadoAsync(daMedica.ArquivoId!.Value))!;
        _assinador.Conferir(arquivoDaMedica.Conteudo).Integra.Should().BeTrue();

        // E o registro de execução assinado confere sozinho.
        var arquivoDaExecucao = (await _repo.ObterArquivoAssinadoAsync(daEnfermeira.ArquivoId!.Value))!;
        _assinador.Conferir(arquivoDaExecucao.Conteudo).Integra.Should().BeTrue();

        assinada.AguardaAssinaturaDaExecucao.Should().BeFalse();

        Despejar("folha-real-duas-assinaturas.pdf", arquivoDaExecucao.Conteudo);

        // A trilha responde "quem fez isso?" com o ato próprio.
        (await _db.Auditoria.SingleAsync(e => e.Acao == "PrescricaoExecucaoAssinada"))
            .Detalhe.Should().Contain("registro de execução");
    }

    /// <summary>
    /// A segunda via da PRESCRIÇÃO passa a sair com as DUAS assinaturas.
    ///
    /// ⚠️ É o teste que a 2ª rodada (16/08/2026) inverteu, e vale registrar por quê: até
    /// aqui ele afirmava que a reimpressão da folha de EXECUÇÃO devolvia bytes selados,
    /// porque a enfermagem assinava aquele arquivo. A clínica descreveu o fluxo real — ela
    /// assina A MESMA prescrição —, e a premissa que impedia isso foi medida e derrubada.
    /// Devolver aqui o arquivo da médica entregaria uma segunda via sem a assinatura de
    /// quem executou, que é a metade que a clínica precisa provar.
    /// </summary>
    [Fact]
    public async Task Segunda_via_da_prescricao_sai_com_as_DUAS_assinaturas()
    {
        var cenario = await CenarioAsync();
        var prescricao = await EncerradaAsync(cenario, exigir: true);

        await _orquestra.AssinarExecucaoAsync(
            prescricao.Id, ECpfDeTeste("Joana Técnica", CpfEnfermeira),
            cenario.UsuarioEnfermeiraId);

        var folha = await _orquestra.FolhaAsync(prescricao.Id, FolhaPrescricao.Prescricao);

        folha.Assinatura!.Papel.Should().Be(PapelAssinatura.Executante);
        folha.Conferencia!.Integra.Should().BeTrue();

        var conferencias = _assinador.ConferirTodas(folha.Pdf);
        conferencias.Should().HaveCount(2, "a folha prova quem mandou e quem executou");
        conferencias.Should().OnlyContain(c => c.Conferida && c.Integra);

        // Estável: reimprimir devolve os mesmos bytes, nunca um arquivo regerado.
        var denovo = await _orquestra.FolhaAsync(prescricao.Id, FolhaPrescricao.Prescricao);
        denovo.Pdf.Should().Equal(folha.Pdf);
    }

    /// <summary>
    /// ⚠️ O TESTE QUE FALTAVA, e sem ele a feature NUNCA teria funcionado na clínica.
    ///
    /// Todos os outros testes desta classe compartilham UM <c>DbContext</c>: gravam o
    /// <c>ArquivoAssinado</c> e releem a prescrição pelo mesmo contexto, e aí o
    /// <i>relationship fixup</i> do EF preenche a navegação <c>Assinatura.Arquivo</c> a
    /// partir do change tracker — mesmo sem <c>ThenInclude</c>.
    ///
    /// Em PRODUÇÃO cada operação abre o próprio escopo (<c>_escopos.CreateScope()</c>), o
    /// tracker nasce vazio, e <c>ObterPrescricaoInternaAsync</c> não carrega
    /// <c>Assinaturas.Arquivo</c>. Ler a navegação ali devolvia NULO <b>sempre</b>: a
    /// enfermeira levaria a recusa na primeira tentativa — que nesta área é COBRADA pelo
    /// PSC —, com 1608 testes verdes.
    ///
    /// Este teste reproduz o escopo separado, e é ele que fixa a regra: <b>bytes de
    /// arquivo assinado se buscam por <c>ObterArquivoAssinadoAsync(arquivoId)</c>, nunca
    /// pela navegação.</b>
    /// </summary>
    [Fact]
    public async Task Assina_com_ESCOPO_SEPARADO_como_em_producao()
    {
        var cenario = await CenarioAsync();
        var prescricao = await EncerradaAsync(cenario, exigir: true);

        // Um contexto NOVO sobre a mesma conexão: é o que a tela faz a cada clique.
        using var outroDb = new ClinicaDbContext(
            new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options);
        var outroRepo = new ClinicaRepositorio(outroDb);
        var conferencia = new PrescricaoService(outroRepo);

        var orquestraNova = new AssinaturaDePrescricaoService(
            outroRepo,
            new PrescricaoInternaService(outroRepo, conferencia),
            new PrescricaoInternaPdfService(outroRepo, conferencia),
            _assinador,
            new ParametrosService(outroRepo));

        // A prova de que o cenário é mesmo o de produção: a navegação está VAZIA aqui.
        var relida = (await outroRepo.ObterPrescricaoInternaAsync(prescricao.Id))!;
        relida.AssinaturaDoPrescritor!.Arquivo.Should().BeNull(
            "ObterPrescricaoInternaAsync não faz ThenInclude(a => a.Arquivo), e sem "
            + "change tracker compartilhado a navegação não se preenche sozinha");

        await orquestraNova.AssinarExecucaoAsync(
            prescricao.Id, ECpfDeTeste("Joana Técnica", CpfEnfermeira),
            cenario.UsuarioEnfermeiraId);

        var folha = await orquestraNova.FolhaAsync(prescricao.Id, FolhaPrescricao.Prescricao);
        _assinador.ConferirTodas(folha.Pdf).Should().HaveCount(2)
            .And.OnlyContain(c => c.Conferida && c.Integra);
    }

    /// <summary>
    /// ⚠️ A PORTA da folha que ficou sem assinar.
    ///
    /// A folha só fica assinável DEPOIS de encerrar — e encerrar era justamente o que a
    /// tirava da fila da sala: encerradas nascem escondidas na lista, e a fila é travada
    /// em <c>p.Data == hoje</c>, então no dia seguinte ela sumia de vez. A única volta era
    /// digitar o código impresso, que só existe se alguém imprimiu o papel. Era o "alerta
    /// sem porta" na pior variante: o que a técnica precisa reencontrar é exatamente o que
    /// a lista escondia.
    ///
    /// A consulta nova não tem filtro de data de propósito: a pendência não vence à
    /// meia-noite.
    /// </summary>
    [Fact]
    public async Task Folha_sem_a_2a_assinatura_e_encontravel_em_qualquer_dia()
    {
        var cenario = await CenarioAsync();
        var prescricao = await EncerradaAsync(cenario, exigir: true);

        // Empurra a folha para ONTEM: é o caso que sumia de vez.
        var linha = await _db.PrescricoesInternas.SingleAsync(p => p.Id == prescricao.Id);
        linha.Data = DateOnly.FromDateTime(DateTime.Today).AddDays(-1);
        await _db.SaveChangesAsync();

        var hoje = DateOnly.FromDateTime(DateTime.Today);

        // A fila de hoje NÃO a mostra — nem marcando "incluir encerradas".
        (await _checagens.DoDiaAsync(hoje, incluirEncerradas: true))
            .Should().NotContain(p => p.Id == prescricao.Id);

        // A consulta da pendência mostra.
        var aguardando = await _checagens.AguardandoAssinaturaAsync();
        aguardando.Should().ContainSingle(p => p.Id == prescricao.Id);

        // E ela SAI da lista assim que a enfermagem assina — pendência que não some é
        // pendência que ensina a ignorar a lista.
        await _orquestra.AssinarExecucaoAsync(
            prescricao.Id, ECpfDeTeste("Joana Técnica", CpfEnfermeira),
            cenario.UsuarioEnfermeiraId);

        (await _checagens.AguardandoAssinaturaAsync()).Should().BeEmpty();
    }

    /// <summary>
    /// A folha que NÃO pede assinatura eletrônica (regime do papel) nunca entra na lista
    /// de pendência — senão toda folha encerrada da história viraria uma cobrança eterna.
    /// </summary>
    [Fact]
    public async Task Regime_do_papel_nao_entra_na_lista_de_pendencia()
    {
        var cenario = await CenarioAsync();
        await EncerradaAsync(cenario, exigir: false);

        (await _checagens.AguardandoAssinaturaAsync()).Should().BeEmpty();
    }

    /// <summary>
    /// O invariante que sustenta tudo: a revisão da enfermagem é ANEXADA, e os bytes que a
    /// médica selou continuam lá, byte a byte, como prefixo do arquivo novo.
    /// </summary>
    [Fact]
    public async Task A_assinatura_da_enfermagem_nao_toca_nos_bytes_da_medica()
    {
        var cenario = await CenarioAsync();
        var prescricao = await EncerradaAsync(cenario, exigir: true);

        var carregada = (await _repo.ObterPrescricaoInternaAsync(prescricao.Id))!;
        var daMedica = (await _repo.ObterArquivoAssinadoAsync(
            carregada.AssinaturaDoPrescritor!.ArquivoId!.Value))!.Conteudo;

        await _orquestra.AssinarExecucaoAsync(
            prescricao.Id, ECpfDeTeste("Joana Técnica", CpfEnfermeira),
            cenario.UsuarioEnfermeiraId);

        var comAsDuas = (await _orquestra.FolhaAsync(
            prescricao.Id, FolhaPrescricao.Prescricao)).Pdf;

        comAsDuas.AsSpan(0, daMedica.Length).SequenceEqual(daMedica)
            .Should().BeTrue("a assinatura da médica cobre os bytes 0..N do arquivo dela");
    }

    /// <summary>
    /// A folha do regime do papel NÃO muda: execução encerrada sem o campo marcado segue
    /// sem assinatura eletrônica e com o registro montado na hora — é o comportamento que
    /// <c>PrescricaoInternaPdfTests</c> fixa desde a parcela 42, reafirmado aqui de
    /// propósito: a feature nova não pode mudar o caminho de quem não a pediu.
    /// </summary>
    [Fact]
    public async Task Folha_sem_o_pedido_segue_no_regime_do_papel()
    {
        var cenario = await CenarioAsync();
        var prescricao = await EncerradaAsync(cenario, exigir: false);

        var carregada = (await _repo.ObterPrescricaoInternaAsync(prescricao.Id))!;
        carregada.AguardaAssinaturaDaExecucao.Should().BeFalse();

        var folha = await _orquestra.FolhaAsync(prescricao.Id, FolhaPrescricao.RegistroExecucao);
        folha.Assinatura.Should().BeNull();
    }

    /// <summary>
    /// O circuito COMPLETO com item NÃO REALIZADO — o caso que a clínica relatou em
    /// 20/08/2026 ("marca não realizou ou suspenso e o sistema não pede a assinatura").
    /// Os testes existentes só exercitavam a folha TODA realizada.
    /// </summary>
    [Fact]
    public async Task Nao_realizado_segue_pedindo_a_assinatura_e_ela_fecha()
    {
        var cenario = await CenarioAsync();
        var prescricao = await AssinadaPelaMedicaAsync(cenario, exigir: true);

        var itens = (await _repo.ObterPrescricaoInternaAsync(prescricao.Id))!
            .Itens.OrderBy(i => i.Ordem).ToList();
        itens.Should().NotBeEmpty();

        await _checagens.ChecarAsync(itens[0].Id, SituacaoChecagem.NaoRealizado,
            new TimeOnly(9, 40), Tecnica, justificativa: "Paciente recusou.");

        await _checagens.EncerrarAsync(prescricao.Id, Tecnica);

        (await _repo.ObterPrescricaoInternaAsync(prescricao.Id))!
            .AguardaAssinaturaDaExecucao.Should().BeTrue(
                "não realizado é um destino como qualquer outro — a folha continua pedindo "
                + "a assinatura de quem executou");

        var assinada = await _orquestra.AssinarExecucaoAsync(
            prescricao.Id, ECpfDeTeste("Joana Técnica", CpfEnfermeira),
            cenario.UsuarioEnfermeiraId, operador: "joana");

        assinada.Assinaturas.Should().HaveCount(2);
        assinada.AguardaAssinaturaDaExecucao.Should().BeFalse();

        var arquivo = (await _repo.ObterArquivoAssinadoAsync(
            assinada.AssinaturaDaExecucao!.ArquivoId!.Value))!;
        _assinador.ConferirTodas(arquivo.Conteudo).Should().HaveCount(2)
            .And.OnlyContain(c => c.Conferida && c.Integra);
    }

    /// EXPERIMENTO — gera as folhas DEPOIS da 2ª assinatura, para ver o que o papel afirma.
    [Fact]
    public async Task Despeja_as_folhas_depois_da_segunda_assinatura()
    {
        var pasta = Environment.GetEnvironmentVariable("CLINICA_DUMP_PDF");
        if (string.IsNullOrWhiteSpace(pasta)) return;

        var cenario = await CenarioAsync();
        var prescricao = await AssinadaPelaMedicaAsync(cenario, exigir: true);
        var itens = (await _repo.ObterPrescricaoInternaAsync(prescricao.Id))!.Itens.ToList();

        await _checagens.ChecarAsync(itens[0].Id, SituacaoChecagem.NaoRealizado,
            new TimeOnly(9, 40), Tecnica, justificativa: "Paciente recusou — mal-estar.");
        await _checagens.EncerrarAsync(prescricao.Id, Tecnica);
        await _orquestra.AssinarExecucaoAsync(
            prescricao.Id, ECpfDeTeste("Joana Técnica", CpfEnfermeira),
            cenario.UsuarioEnfermeiraId, operador: "joana");

        var folha = await _orquestra.FolhaAsync(prescricao.Id, FolhaPrescricao.Prescricao);
        File.WriteAllBytes(Path.Combine(pasta, "pos-prescricao.pdf"), folha.Pdf);

        var registro = await _orquestra.FolhaAsync(prescricao.Id, FolhaPrescricao.RegistroExecucao);
        File.WriteAllBytes(Path.Combine(pasta, "pos-registro.pdf"), registro.Pdf);
    }

    /// <summary>
    /// ⚠️ O REGISTRO DE EXECUÇÃO NÃO PODE AFIRMAR UMA ASSINATURA QUE ELE NÃO CARREGA.
    ///
    /// Ele é montado NA HORA a cada impressão — não tem <c>/ByteRange</c>, não tem PKCS#7,
    /// não tem carimbo. Depois de a enfermeira assinar a PRESCRIÇÃO, o rodapé dele passava
    /// a escrever "Assinado digitalmente com certificado ICP-Brasil (assinatura
    /// qualificada) · titular Joana Técnica" — e, ao afirmar isso, engolia a linha de
    /// caneta. A folha saía sem carimbo digital e sem onde assinar à mão: <b>sem prova
    /// nenhuma de autoria</b>. É a garantia aparente que este projeto recusa desde a
    /// parcela 3, e foi a clínica que a viu antes de nós.
    /// </summary>
    [Fact]
    public async Task O_registro_de_execucao_nao_se_diz_assinado()
    {
        var cenario = await CenarioAsync();
        var prescricao = await EncerradaAsync(cenario, exigir: true);

        // Antes de assinar: nada a apontar — o rodapé imprime a linha de caneta.
        PrescricaoInternaPdfService.AvisoDoRegistroDeExecucao(
            (await _repo.ObterPrescricaoInternaAsync(prescricao.Id))!).Should().BeNull();

        await _orquestra.AssinarExecucaoAsync(
            prescricao.Id, ECpfDeTeste("Joana Técnica", CpfEnfermeira),
            cenario.UsuarioEnfermeiraId, operador: "joana");

        var aviso = PrescricaoInternaPdfService.AvisoDoRegistroDeExecucao(
            (await _repo.ObterPrescricaoInternaAsync(prescricao.Id))!);

        aviso.Should().NotBeNull();
        aviso.Should().Contain("NÃO é assinado",
            "o papel tem de dizer o que ele é — um espelho, não um documento selado");
        aviso.Should().Contain(prescricao.Numero,
            "e tem de dizer ONDE está a assinatura, senão o aviso vira só uma negativa");
        aviso.Should().Contain("Joana Técnica");
        aviso.Should().NotContain("assinatura qualificada",
            "esta é a frase que só a folha REALMENTE selada pode carregar");

        // A prova de que a asserção acima pega o defeito real: a frase ANTIGA — que saía
        // deste mesmo rodapé — é justamente a que afirma a assinatura qualificada.
        PrescricaoInternaPdfService.FraseDoNivel(
                (await _repo.ObterPrescricaoInternaAsync(prescricao.Id))!.AssinaturaDaExecucao)
            .Should().Contain("assinatura qualificada",
                "senão este teste inteiro não provaria nada");
    }

    /// <summary>
    /// A folha do regime do papel não muda em nada: sem o campo marcado, não há assinatura
    /// eletrônica a apontar, e o rodapé segue com a linha de caneta.
    /// </summary>
    [Fact]
    public async Task Regime_do_papel_nao_ganha_aviso_nenhum()
    {
        var cenario = await CenarioAsync();
        var prescricao = await EncerradaAsync(cenario, exigir: false);

        PrescricaoInternaPdfService.AvisoDoRegistroDeExecucao(
            (await _repo.ObterPrescricaoInternaAsync(prescricao.Id))!).Should().BeNull();
    }

    /// <summary>
    /// ⚠️ O REGISTRO DE EXECUÇÃO SAI SELADO NO MESMO ATO (decisão da direção, 20/08/2026).
    ///
    /// A clínica reclamou de que a folha assinada não mostrava o item não realizado nem o
    /// suspenso. E não mostra mesmo: a PRESCRIÇÃO é selada pela médica ANTES da execução, e
    /// acrescentar-lhe uma página faz o validador de fora acusar modificação ILEGAL na
    /// assinatura dela — foi medido no pyhanko antes de decidir. Quem mostra a execução é o
    /// registro, e para ele valer como prova precisa ser selado também.
    ///
    /// São DOIS arquivos de UM ato: a enfermeira escolhe o certificado uma vez.
    /// </summary>
    [Fact]
    public async Task O_registro_de_execucao_sai_selado_no_mesmo_ato()
    {
        var cenario = await CenarioAsync();
        var prescricao = await EncerradaAsync(cenario, exigir: true);

        var assinada = await _orquestra.AssinarExecucaoAsync(
            prescricao.Id, ECpfDeTeste("Joana Técnica", CpfEnfermeira),
            cenario.UsuarioEnfermeiraId, operador: "joana");

        var daExecucao = assinada.AssinaturaDaExecucao!;
        daExecucao.ArquivoId.Should().NotBeNull("a prescrição leva os dois carimbos");
        daExecucao.ArquivoRegistroId.Should().NotBeNull(
            "e o registro — a folha que MOSTRA o que foi feito — sai selado também");
        daExecucao.ArquivoRegistroId.Should().NotBe(daExecucao.ArquivoId,
            "são dois documentos, não o mesmo arquivo gravado duas vezes");

        // A folha do registro devolve os BYTES SELADOS e confere sozinha.
        var folha = await _orquestra.FolhaAsync(prescricao.Id, FolhaPrescricao.RegistroExecucao);
        folha.Assinatura.Should().NotBeNull();
        folha.Conferencia!.Conferida.Should().BeTrue();
        folha.Conferencia.Integra.Should().BeTrue();
        _assinador.ConferirTodas(folha.Pdf).Should().ContainSingle(
            "o registro é assinado UMA vez, pela enfermagem — a médica não assina um "
            + "documento que ainda não existia quando ela assinou");

        // E a prescrição continua sendo a folha das DUAS.
        var daPrescricao = await _orquestra.FolhaAsync(prescricao.Id, FolhaPrescricao.Prescricao);
        _assinador.ConferirTodas(daPrescricao.Pdf).Should().HaveCount(2)
            .And.OnlyContain(c => c.Conferida && c.Integra);
    }

    /// <summary>
    /// A reimpressão do registro devolve os MESMOS bytes — a regra que faz a segunda via
    /// valer. Antes desta parcela ele era remontado a cada impressão, e um arquivo "igual"
    /// regerado agora abriria como inválido.
    /// </summary>
    [Fact]
    public async Task A_reimpressao_do_registro_selado_devolve_os_mesmos_bytes()
    {
        var cenario = await CenarioAsync();
        var prescricao = await EncerradaAsync(cenario, exigir: true);
        await _orquestra.AssinarExecucaoAsync(
            prescricao.Id, ECpfDeTeste("Joana Técnica", CpfEnfermeira),
            cenario.UsuarioEnfermeiraId, operador: "joana");

        var uma = await _orquestra.FolhaAsync(prescricao.Id, FolhaPrescricao.RegistroExecucao);
        var outra = await _orquestra.FolhaAsync(prescricao.Id, FolhaPrescricao.RegistroExecucao);

        outra.Pdf.Should().Equal(uma.Pdf);
    }

    /// <summary>
    /// ⚠️ Falhar ao selar o registro NÃO desfaz a assinatura da prescrição — é a hierarquia
    /// da parcela 65 nesta área: o ato irreversível não depende do passo que veio depois
    /// dele. E a degradação é HONESTA: sem o selo, o registro é montado na hora e diz que
    /// não é assinado, apontando a folha que é.
    /// </summary>
    [Fact]
    public async Task Registro_sem_selo_e_montado_na_hora_e_diz_que_nao_e_assinado()
    {
        var cenario = await CenarioAsync();
        var prescricao = await EncerradaAsync(cenario, exigir: true);
        await _orquestra.AssinarExecucaoAsync(
            prescricao.Id, ECpfDeTeste("Joana Técnica", CpfEnfermeira),
            cenario.UsuarioEnfermeiraId, operador: "joana");

        // Encena a falha da 2ª selagem: a coluna fica nula, como ficaria se o PSC recusasse.
        var linha = await _db.AssinaturasDocumento
            .SingleAsync(a => a.PrescricaoInternaId == prescricao.Id
                              && a.Papel == PapelAssinatura.Executante);
        linha.ArquivoRegistroId = null;
        await _db.SaveChangesAsync();

        var folha = await _orquestra.FolhaAsync(prescricao.Id, FolhaPrescricao.RegistroExecucao);

        folha.Assinatura.Should().BeNull("a folha montada na hora não carrega assinatura");
        folha.Conferencia.Should().BeNull();
        _assinador.ConferirTodas(folha.Pdf).Should()
            .OnlyContain(c => !c.Conferida, "o arquivo montado na hora não tem assinatura");

        // A prescrição não foi tocada: as duas assinaturas continuam lá.
        var daPrescricao = await _orquestra.FolhaAsync(prescricao.Id, FolhaPrescricao.Prescricao);
        _assinador.ConferirTodas(daPrescricao.Pdf).Should().HaveCount(2);

        PrescricaoInternaPdfService.AvisoDoRegistroDeExecucao(
                (await _repo.ObterPrescricaoInternaAsync(prescricao.Id))!)
            .Should().Contain("NÃO é assinado");
    }

    // ==================== Apoio ====================

    private sealed record Cenario(int PacienteId, int ProfissionalMedicaId, int UsuarioEnfermeiraId);

    private async Task<Cenario> CenarioAsync()
    {
        var paciente = new Paciente { Nome = "Maria de Teste", Convenio = Convenio.UnimedIntercambio };
        var medica = new Profissional
        {
            Nome = "Dra. Ana Souza", RegistroConselho = "CRM-SP 123456", Cpf = CpfMedica
        };
        var enfermeira = new Profissional
        {
            Nome = "Joana Técnica", RegistroConselho = "COREN-SP 999999", Cpf = CpfEnfermeira
        };
        _db.Pacientes.Add(paciente);
        _db.Profissionais.AddRange(medica, enfermeira);
        await _db.SaveChangesAsync();

        var usuaria = new UsuarioSistema
        {
            Nome = "Joana Técnica",
            Login = "joana",
            Perfil = PerfilAcesso.Enfermagem,
            ProfissionalId = enfermeira.Id
        };
        _db.Usuarios.Add(usuaria);
        await _db.SaveChangesAsync();

        return new Cenario(paciente.Id, medica.Id, usuaria.Id);
    }

    private async Task<PrescricaoInterna> RascunhoAsync(Cenario cenario, bool exigir)
    {
        var prescricao = await _prescricoes.CriarAsync(cenario.PacienteId, cenario.ProfissionalMedicaId);
        await _prescricoes.SalvarRascunhoAsync(prescricao.Id, "Crise álgica", null,
            [new ItemPrescricaoInterna { Descricao = "Soro fisiológico 0,9%", Dose = "500 mL" }],
            exigeAssinaturaEletronicaDaExecucao: exigir);
        return prescricao;
    }

    private async Task<PrescricaoInterna> AssinadaPelaMedicaAsync(Cenario cenario, bool exigir)
    {
        var prescricao = await RascunhoAsync(cenario, exigir);
        await _orquestra.AssinarPrescricaoAsync(
            prescricao.Id, ECpfDeTeste("Dra. Ana Souza", CpfMedica));
        return prescricao;
    }

    private async Task ChecarTudoAsync(PrescricaoInterna prescricao)
    {
        var carregada = (await _repo.ObterPrescricaoInternaAsync(prescricao.Id))!;
        foreach (var item in carregada.Itens)
            await _checagens.ChecarAsync(
                item.Id, SituacaoChecagem.Realizado, new TimeOnly(9, 30), Tecnica);
    }

    private async Task<PrescricaoInterna> EncerradaAsync(Cenario cenario, bool exigir)
    {
        var prescricao = await AssinadaPelaMedicaAsync(cenario, exigir);
        await ChecarTudoAsync(prescricao);
        await _checagens.EncerrarAsync(prescricao.Id, Tecnica);
        return prescricao;
    }

    /// <summary>Um e-CPF de mentira, com o CPF na extensão 2.16.76.1.3.1 — como os dos outros arquivos.</summary>
    private static CertificadoAssinatura ECpfDeTeste(string nome, string cpf)
    {
        using var rsa = RSA.Create(2048);
        var pedido = new CertificateRequest(
            $"CN={nome}, OU=Teste, C=BR", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        pedido.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation, true));
        pedido.CertificateExtensions.Add(ExtensaoECpf(cpf));

        var certificado = pedido.CreateSelfSigned(
            DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddYears(1));

        return CertificadoIcpBrasil.Ler(new X509Certificate2(
            certificado.Export(X509ContentType.Pfx, "t"), "t", X509KeyStorageFlags.Exportable));
    }

    private static X509Extension ExtensaoECpf(string cpf)
    {
        var conteudo = System.Text.Encoding.ASCII.GetBytes(
            "14031978" + cpf + "12345678901" + "123456".PadLeft(15, '0') + "SSPSP ");

        var escritor = new System.Formats.Asn1.AsnWriter(System.Formats.Asn1.AsnEncodingRules.DER);
        using (escritor.PushSequence())
        using (escritor.PushSequence(new System.Formats.Asn1.Asn1Tag(
                   System.Formats.Asn1.TagClass.ContextSpecific, 0)))
        {
            escritor.WriteObjectIdentifier(CertificadoIcpBrasil.OidPessoaFisica);
            using (escritor.PushSequence(new System.Formats.Asn1.Asn1Tag(
                       System.Formats.Asn1.TagClass.ContextSpecific, 0)))
                escritor.WriteOctetString(conteudo);
        }

        return new X509Extension("2.5.29.17", escritor.Encode(), critical: false);
    }

    /// <summary>
    /// Grava a folha REAL quando <c>CLINICA_DUMP_PDF</c> aponta uma pasta — é como se
    /// confere, de graça, o que só a folha montada mostra: onde cada carimbo caiu e se ele
    /// sobrevive à impressão. Cada tentativa de assinatura no SafeID é COBRADA.
    /// </summary>
    private static void Despejar(string nome, byte[] pdf)
    {
        var pasta = Environment.GetEnvironmentVariable("CLINICA_DUMP_PDF");
        if (!string.IsNullOrWhiteSpace(pasta) && Directory.Exists(pasta))
            File.WriteAllBytes(Path.Combine(pasta, nome), pdf);
    }
}
