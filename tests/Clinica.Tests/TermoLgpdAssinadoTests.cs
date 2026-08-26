using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Clinica.Domain;
using Clinica.Domain.Prontuario;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O TERMO LGPD QUE O PACIENTE ASSINA (parcela 89) — e o circuito de volta.
///
/// O que estes testes existem para impedir
/// ---------------------------------------
/// Até aqui o consentimento era uma CAIXINHA que o balcão marcava, e o termo era o recibo
/// dela. A direção inverteu: <b>o paciente responde e assina, e é essa resposta que
/// vale</b>.
///
/// ⚠️ O risco que a inversão fecha é o de DUAS VERDADES. Com o termo sendo recibo, o
/// paciente podia responder "Não" ao marketing no celular e a clínica continuar mandando
/// campanha, porque a caixinha do balcão seguia marcada — e ninguém veria, porque nada
/// falha: a campanha simplesmente sai.
///
/// Por isso o teste que carrega o arquivo não olha a tabela de consentimento: ele pergunta
/// ao <see cref="ConsentimentoService.VigenteAsync"/>, que é <b>o portão que a campanha, o
/// recall e o NPS de fato consultam</b>.
/// </summary>
public class TermoLgpdAssinadoTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly DocumentoClinicoService _documentos;
    private readonly AssinaturaDoPacienteService _assinaturas;
    private readonly ConsentimentoService _consentimentos;
    private readonly DocumentosClinicosPdfService _pdfs;

    private static byte[] TracoDeTeste() => new byte[512];

    /// <summary>Um PNG 1×1 de verdade, para o desenho do traço no PDF.</summary>
    private static byte[] PngDeTeste() => Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    public TermoLgpdAssinadoTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);

        _consentimentos = new ConsentimentoService(_repo);
        _documentos = new DocumentoClinicoService(
            _repo, new ProntuarioService(_repo), _consentimentos);
        _assinaturas = new AssinaturaDoPacienteService(_repo);
        _pdfs = new DocumentosClinicosPdfService(_repo);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<int> PacienteAsync()
    {
        var p = new Paciente { Nome = "Maria Silva", Convenio = Convenio.UnimedIntercambio };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    /// <summary>Assina o termo respondendo cada finalidade pelo CÓDIGO, nunca pela ordem.</summary>
    private async Task<DocumentoClinico> AssinarAsync(
        DocumentoClinico termo, params (FinalidadeConsentimento Finalidade, bool Sim)[] respostas)
    {
        var porFinalidade = respostas.ToDictionary(r => r.Finalidade.ToString(), r => r.Sim);

        var mapa = termo.Itens.ToDictionary(
            i => i.Ordem,
            i => (string?)(porFinalidade.TryGetValue(i.Codigo ?? string.Empty, out var sim)
                ? (sim ? "Sim" : "Não")
                : "Sim"));

        return await _assinaturas.ColherAsync(
            termo.Id, TracoDeTeste(), 600, 220, mapa,
            "CPF 123.456.789-00", "ana.recepcao");
    }

    // ==================== O portão ====================

    /// <summary>
    /// O teste que carrega o arquivo: a resposta do paciente chega ao PORTÃO, e não só à
    /// tabela. Elo partido aqui não vira erro — vira campanha enviada a quem recusou.
    /// </summary>
    [Fact]
    public async Task A_resposta_assinada_vira_o_consentimento_que_o_sistema_consulta()
    {
        var paciente = await PacienteAsync();

        foreach (var f in ConsentimentoService.Finalidades)
            (await _consentimentos.VigenteAsync(paciente, f))
                .Should().BeFalse("nada foi consentido antes de o termo existir");

        var termo = await _documentos.EmitirTermoConsentimentoAsync(paciente, operador: "ana");
        await AssinarAsync(termo,
            (FinalidadeConsentimento.ComunicacaoEMarketing, false));

        (await _consentimentos.VigenteAsync(paciente, FinalidadeConsentimento.TratamentoDeDados))
            .Should().BeTrue();
        (await _consentimentos.VigenteAsync(paciente, FinalidadeConsentimento.UsoDeImagem))
            .Should().BeTrue();
        (await _consentimentos.VigenteAsync(
            paciente, FinalidadeConsentimento.ComunicacaoEMarketing))
            .Should().BeFalse("ele respondeu NÃO, e é a resposta assinada que vale");
    }

    /// <summary>
    /// ⚠️ A DECISÃO DA DIREÇÃO: recusar no termo REVOGA o que estava vigente. Sem isto, a
    /// caixinha marcada no balcão continuaria valendo sobre um "Não" de próprio punho.
    /// </summary>
    [Fact]
    public async Task Recusar_no_termo_REVOGA_o_que_o_balcao_havia_concedido()
    {
        var paciente = await PacienteAsync();

        await _consentimentos.RegistrarAsync(
            paciente, FinalidadeConsentimento.ComunicacaoEMarketing,
            concedido: true, operador: "ana");

        (await _consentimentos.VigenteAsync(
            paciente, FinalidadeConsentimento.ComunicacaoEMarketing)).Should().BeTrue();

        var termo = await _documentos.EmitirTermoConsentimentoAsync(paciente, operador: "ana");
        await AssinarAsync(termo, (FinalidadeConsentimento.ComunicacaoEMarketing, false));

        (await _consentimentos.VigenteAsync(
            paciente, FinalidadeConsentimento.ComunicacaoEMarketing))
            .Should().BeFalse("a resposta assinada do paciente vence a caixinha do balcão");

        // A linha antiga NÃO some — ela prova que houve consentimento no período em que os
        // dados foram tratados —, mas passa a ter fim declarado.
        var historico = await _consentimentos.HistoricoAsync(paciente);
        var concedida = historico.Single(
            c => c.Finalidade == FinalidadeConsentimento.ComunicacaoEMarketing && c.Concedido);

        concedida.RevogadoEm.Should().NotBeNull(
            "quem lê o histórico veria uma autorização sem fim ao lado de uma recusa posterior");
        concedida.Observacoes.Should().Contain(termo.Numero);
    }

    // ==================== O vínculo é por CÓDIGO ====================

    /// <summary>
    /// ⚠️ Acrescentar uma finalidade no MEIO não pode trocar as respostas. Casar por
    /// <c>Ordem</c> faria o "Sim" do uso de imagem virar autorização para compartilhar com
    /// o convênio — sem quebrar build nenhum (o contrato de índice da parcela 41).
    /// </summary>
    [Fact]
    public void O_codigo_manda_na_leitura_e_a_ordem_nao()
    {
        var documento = new DocumentoClinico
        {
            Tipo = TipoDocumentoClinico.Consentimento,
            Itens =
            [
                new ItemDocumento
                {
                    Ordem = 9,
                    Codigo = FinalidadeConsentimento.UsoDeImagem.ToString(),
                    Descricao = "…",
                    Quantidade = "Não"
                },
                new ItemDocumento
                {
                    Ordem = 1,
                    Codigo = FinalidadeConsentimento.TratamentoDeDados.ToString(),
                    Descricao = "…",
                    Quantidade = "Sim"
                }
            ]
        };

        var decisoes = TermoConsentimento.Decisoes(documento);

        decisoes.Should().ContainSingle(
            d => d.Finalidade == FinalidadeConsentimento.UsoDeImagem && !d.Concedido);
        decisoes.Should().ContainSingle(
            d => d.Finalidade == FinalidadeConsentimento.TratamentoDeDados && d.Concedido);
    }

    /// <summary>
    /// Item sem código reconhecível é IGNORADO, nunca adivinhado. São dois casos legítimos
    /// — o termo emitido antes desta parcela e uma finalidade que só a versão nova conhece
    /// — e nos dois gravar consentimento seria gravar o errado.
    /// </summary>
    [Fact]
    public void Item_sem_codigo_reconhecivel_nao_vira_consentimento()
    {
        var documento = new DocumentoClinico
        {
            Tipo = TipoDocumentoClinico.Consentimento,
            Itens =
            [
                new ItemDocumento { Ordem = 1, Descricao = "termo antigo", Quantidade = "Sim" },
                new ItemDocumento
                {
                    Ordem = 2, Codigo = "FinalidadeQueNaoExisteNestaVersao",
                    Descricao = "…", Quantidade = "Sim"
                }
            ]
        };

        TermoConsentimento.Decisoes(documento).Should().BeEmpty();
    }

    // ==================== O que NÃO pode mudar ====================

    /// <summary>
    /// ⚠️ O termo de PROCEDIMENTO não pode ganhar consentimento de carona. Ele passa pelo
    /// mesmo <c>ColherAsync</c>, e um "Sim" na declaração de jejum não é autorização de
    /// tratamento de dados.
    /// </summary>
    [Fact]
    public void Termo_de_procedimento_nao_gera_consentimento_LGPD()
    {
        var documento = new DocumentoClinico
        {
            Tipo = TipoDocumentoClinico.TermoProcedimento,
            Itens =
            [
                new ItemDocumento
                {
                    Ordem = 1,
                    Codigo = FinalidadeConsentimento.TratamentoDeDados.ToString(),
                    Descricao = "Estou em jejum",
                    Quantidade = "Sim"
                }
            ]
        };

        TermoConsentimento.Decisoes(documento).Should().BeEmpty(
            "o tipo do documento manda — o código sozinho não faz um termo virar LGPD");
    }

    /// <summary>
    /// As declarações nascem SEM RESPOSTA, e o serviço recusa assinar assim. Pré-marcar
    /// com a situação atual fabricaria a resposta mais conveniente para a clínica — o
    /// oposto do que o termo existe para provar.
    /// </summary>
    [Fact]
    public async Task O_termo_nasce_sem_resposta_e_nao_se_assina_em_branco()
    {
        var paciente = await PacienteAsync();
        var termo = await _documentos.EmitirTermoConsentimentoAsync(paciente, operador: "ana");

        termo.Itens.Should().HaveCount(ConsentimentoService.Finalidades.Count);
        termo.Itens.Should().OnlyContain(i => i.Quantidade == null);
        termo.Itens.Should().OnlyContain(i => i.Codigo != null);

        var emBranco = async () => await _assinaturas.ColherAsync(
            termo.Id, TracoDeTeste(), 600, 220,
            new Dictionary<int, string?>(), "CPF 1", "ana.recepcao");

        await emBranco.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Falta responder*");
    }

    /// <summary>
    /// O portão que abre o resto: sem <c>AssinadoPeloPaciente</c> o termo LGPD não entraria
    /// na coleta do balcão nem no envio pelo WhatsApp — as duas são genéricas sobre
    /// <c>DocumentoClinico</c> e perguntam só isto.
    /// </summary>
    [Fact]
    public async Task O_termo_LGPD_entra_no_fluxo_de_assinatura_do_paciente()
    {
        var paciente = await PacienteAsync();
        var termo = await _documentos.EmitirTermoConsentimentoAsync(paciente, operador: "ana");

        TipoDocumentoInfo.AssinadoPeloPaciente(TipoDocumentoClinico.Consentimento)
            .Should().BeTrue();
        termo.AguardaAssinaturaDoPaciente.Should().BeTrue(
            "é o que o envio pelo WhatsApp exige para deixar mandar o link");

        var assinado = await AssinarAsync(termo);

        assinado.PacienteAssinou.Should().BeTrue();
        assinado.AguardaAssinaturaDoPaciente.Should().BeFalse();
    }
    /// <summary>
    /// O PAPEL diz o que o paciente RESPONDEU — inclusive o "Não".
    ///
    /// ⚠️ O desenho antigo do termo LGPD (<c>ListaFinalidades</c>) marcava um X quando a
    /// resposta era a palavra "Autorizado" e escrevia "Pendente" em todo o resto. Com as
    /// respostas em "Sim"/"Não", ele imprimiria TODA finalidade como pendente — e um "Não"
    /// sairia idêntico a uma pergunta que ninguém respondeu.
    ///
    /// Num papel que o paciente leva para provar o que RECUSOU, isso é a garantia aparente
    /// que este projeto recusa desde a parcela 3. Este teste falha no desenho antigo:
    /// a palavra "Autorizado" não existe mais em resposta nenhuma, então o X nunca sairia.
    ///
    /// Ele não lê o texto do PDF de volta (o QuestPDF embute a fonte em subconjunto e
    /// escreve os glifos por id). O que ele fixa é o que dá para fixar sem adivinhação: as
    /// respostas gravadas são exatamente as que o desenho sabe ler, e a folha SAI.
    /// </summary>
    [Fact]
    public async Task A_via_do_paciente_carrega_as_respostas_que_o_desenho_sabe_ler()
    {
        var paciente = await PacienteAsync();
        var termo = await _documentos.EmitirTermoConsentimentoAsync(paciente, operador: "ana");

        await _assinaturas.ColherAsync(
            termo.Id, TracoDeTeste(), 300, 100,
            new Dictionary<int, string?>
            {
                [1] = "Sim",
                [2] = "Sim",
                [3] = "Não",
                [4] = "Não"
            },
            documentoConferido: "CPF 123.456.789-00",
            testemunha: "ana");

        var gravado = await _repo.ObterDocumentoAsync(termo.Id);

        // Toda resposta é lida pelo MESMO reconhecedor que o desenho usa. Se alguém
        // reintroduzir "Autorizado"/"Pendente" de um lado só, este par deixa de fechar.
        gravado!.Itens.Should().OnlyContain(i =>
            RespostaDeclaracao.EhPositiva(i.Quantidade) || RespostaDeclaracao.EhNegativa(i.Quantidade));

        gravado.Itens.Count(i => RespostaDeclaracao.EhNegativa(i.Quantidade)).Should().Be(2);

        // ⚠️ Um PNG 1×1 de VERDADE: o serviço só confere o tamanho do traço, e o QuestPDF
        // decodifica a imagem ao desenhar — `new byte[512]` derruba a geração.
        var pdf = _pdfs.Gerar(gravado, tracoPaciente: PngDeTeste());
        pdf.Should().NotBeNullOrEmpty();

        // Confere com os olhos quando `CLINICA_DUMP_PDF` aponta uma pasta: renderizar é de
        // graça, e é o único jeito de ver o que só a folha montada mostra (parcela 68).
        if (Environment.GetEnvironmentVariable("CLINICA_DUMP_PDF") is { } pasta
            && Directory.Exists(pasta))
        {
            await File.WriteAllBytesAsync(Path.Combine(pasta, "termo-lgpd.pdf"), pdf);
        }
    }

}
