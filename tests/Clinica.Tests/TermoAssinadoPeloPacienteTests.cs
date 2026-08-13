using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Tests;

/// <summary>
/// O termo que o PACIENTE assina (parcela 66) — o pedido da clínica para o BSV, com a
/// declaração de jejum junto.
///
/// O que estes testes protegem, e por quê:
///
/// 1. **O termo vale POR SESSÃO.** É a decisão da clínica, e a razão está no documento que
///    originou o pedido: a declaração de jejum afirma "ESTOU em jejum", e afirmação sobre o
///    estado de hoje não se herda da semana passada. Se alguém trocar a chave por um prazo
///    em dias, este teste cai.
/// 2. **A ordem das duas assinaturas.** O PDF não se assina incrementalmente: o traço do
///    paciente tem de entrar ANTES do selo ICP-Brasil do profissional. Colher depois
///    produziria, em silêncio, um arquivo cujo selo não fecha.
/// 3. **"Não estou em jejum" NÃO impede.** O termo é emitido do mesmo jeito, com a resposta
///    escrita, e o que acontece é um alerta VERMELHO. Bloquear produziria o desfecho pior:
///    ninguém emite o termo e o procedimento acontece sem registro nenhum.
/// 4. **Corrigir o modelo não reescreve o que foi assinado.** Aplicar COPIA — e aqui isso
///    não é desenho, é a Lei 13.787/2018.
/// 5. **A evidência é verificável.** Guardar um hash que ninguém recalcula é guardar um
///    número; o selo tem de acusar quando o conteúdo muda.
/// 6. **Os dois caminhos de leitura concordam.** A fila lê em lote e a ficha lê por
///    paciente: duas definições de "falta assinar" divergiriam, e a que ninguém lembraria
///    de ajustar é a do quadro — onde o erro aparece como cartão limpo, indistinguível de
///    termo em dia.
/// </summary>
public class TermoAssinadoPeloPacienteTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly DocumentoClinicoService _documentos;
    private readonly AssinaturaDoPacienteService _assinaturas;
    private readonly TermoProcedimentoService _termos;

    // O relógio REAL, e não uma data fixa: `EmitirTermoProcedimentoAsync` carimba
    // `DateTime.Today` de propósito — o termo é do dia em que foi apresentado —, e um
    // "hoje" inventado no teste faria o documento nascer noutra data que não a conferida.
    private static readonly DateOnly Hoje = DateOnly.FromDateTime(DateTime.Today);
    private static readonly DateOnly Ontem = Hoje.AddDays(-1);

    /// <summary>PNG mínimo válido, acima do piso de bytes que o serviço exige.</summary>
    private static byte[] TracoDeTeste() => new byte[512];

    public TermoAssinadoPeloPacienteTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);

        _documentos = new DocumentoClinicoService(
            _repo, new ProntuarioService(_repo), new ConsentimentoService(_repo));
        _assinaturas = new AssinaturaDoPacienteService(_repo);
        _termos = new TermoProcedimentoService(_repo);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }

    // ==================== Cenário ====================

    private async Task<int> PacienteAsync(string nome = "Maria Silva")
    {
        var paciente = new Paciente { Nome = nome, Convenio = Convenio.UnimedPadrao };
        _db.Pacientes.Add(paciente);
        await _db.SaveChangesAsync();
        return paciente.Id;
    }

    private async Task<ModeloDocumento> ModeloDoBsvAsync()
    {
        var modelo = new ModeloDocumento
        {
            Tipo = TipoDocumentoClinico.TermoProcedimento,
            Nome = "Termo do BSV",
            Titulo = "Consentimento para Bloqueio Simpático Venoso",
            Corpo = "Fui informado(a) dos riscos, das alternativas e concordo com o procedimento.",
            Itens =
            [
                new ItemModelo { Ordem = 1, Descricao = "Estou em jejum de 8 horas" },
                new ItemModelo { Ordem = 2, Descricao = "Informei todos os medicamentos que uso" }
            ]
        };

        _db.ModelosDocumento.Add(modelo);
        await _db.SaveChangesAsync();
        return modelo;
    }

    private async Task ExigirNoBsvAsync(int modeloId)
    {
        _db.ExigenciasTermo.Add(new ExigenciaTermoProcedimento
        {
            Modalidade = ModalidadeAtendimento.BsvApenas,
            ModeloDocumentoId = modeloId,
            Ativa = true
        });
        await _db.SaveChangesAsync();
    }

    private async Task AgendarBsvAsync(int pacienteId, DateOnly dia)
    {
        _db.Agendamentos.Add(new Agendamento
        {
            PacienteId = pacienteId,
            DataHora = dia.ToDateTime(new TimeOnly(9, 0)),
            ModalidadePrevista = ModalidadeAtendimento.BsvApenas,
            Status = StatusAgendamento.Agendado
        });
        await _db.SaveChangesAsync();
    }

    private async Task<DocumentoClinico> AssinarAsync(
        int documentoId, string? jejum = "Sim", string? medicamentos = "Sim")
        => await _assinaturas.ColherAsync(
            documentoId, TracoDeTeste(), 600, 220,
            new Dictionary<int, string?> { [1] = jejum, [2] = medicamentos },
            "CPF 123.456.789-00", "ana.recepcao");

    // ==================== 1. Vale por SESSÃO ====================

    [Fact]
    public async Task Termo_assinado_ontem_nao_vale_para_a_sessao_de_hoje()
    {
        var paciente = await PacienteAsync();
        var modelo = await ModeloDoBsvAsync();
        await ExigirNoBsvAsync(modelo.Id);

        // Assinado ONTEM, com tudo em ordem.
        await AgendarBsvAsync(paciente, Ontem);
        var deOntem = await _documentos.EmitirTermoProcedimentoAsync(paciente, modelo.Id);
        deOntem.Data = Ontem;
        await _db.SaveChangesAsync();
        await AssinarAsync(deOntem.Id);

        var ontem = await _termos.SituacaoDoDiaAsync(paciente, Ontem);
        ontem.Should().ContainSingle().Which.Assinado.Should().BeTrue();

        // Hoje há OUTRA sessão: o termo de ontem não a cobre.
        await AgendarBsvAsync(paciente, Hoje);
        var hoje = await _termos.SituacaoDoDiaAsync(paciente, Hoje);

        hoje.Should().ContainSingle().Which.Pendente.Should().BeTrue(
            "a declaração de jejum afirma o estado de HOJE — herdá-la de ontem seria "
            + "aceitar uma declaração sobre o futuro");
    }

    [Fact]
    public async Task Sem_exigencia_cadastrada_nao_ha_termo_a_cobrar()
    {
        var paciente = await PacienteAsync();
        await AgendarBsvAsync(paciente, Hoje);

        var situacao = await _termos.SituacaoDoDiaAsync(paciente, Hoje);

        situacao.Should().BeEmpty(
            "a clínica que não cadastrou exigência nenhuma não pode ver o balcão cobrar papel");
    }

    [Fact]
    public async Task Agendamento_cancelado_nao_pede_termo()
    {
        var paciente = await PacienteAsync();
        var modelo = await ModeloDoBsvAsync();
        await ExigirNoBsvAsync(modelo.Id);

        _db.Agendamentos.Add(new Agendamento
        {
            PacienteId = paciente,
            DataHora = Hoje.ToDateTime(new TimeOnly(9, 0)),
            ModalidadePrevista = ModalidadeAtendimento.BsvApenas,
            Status = StatusAgendamento.Cancelado
        });
        await _db.SaveChangesAsync();

        var situacao = await _termos.SituacaoDoDiaAsync(paciente, Hoje);

        situacao.Should().BeEmpty("não há procedimento para consentir");
    }

    // ==================== 2. A ordem das assinaturas ====================

    [Fact]
    public async Task Traco_do_paciente_e_recusado_depois_da_assinatura_do_profissional()
    {
        var paciente = await PacienteAsync();
        var modelo = await ModeloDoBsvAsync();
        var termo = await _documentos.EmitirTermoProcedimentoAsync(paciente, modelo.Id);

        // O profissional selou primeiro — o que inverte a ordem correta.
        termo.AssinaturaTipo = TipoAssinatura.IcpBrasil;
        termo.AssinadoEm = DateTime.Now;
        await _db.SaveChangesAsync();

        var acao = async () => await AssinarAsync(termo.Id);

        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ANTES*",
                "a assinatura sela uma faixa de BYTES: acrescentar o traço depois "
                + "invalidaria o selo, e o PDF abriria como inválido no validador");
    }

    [Fact]
    public async Task Termo_ja_assinado_pelo_paciente_nao_se_assina_de_novo()
    {
        var paciente = await PacienteAsync();
        var modelo = await ModeloDoBsvAsync();
        var termo = await _documentos.EmitirTermoProcedimentoAsync(paciente, modelo.Id);

        await AssinarAsync(termo.Id);

        var acao = async () => await AssinarAsync(termo.Id);

        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*já foi assinado*");
    }

    // ==================== 3. "Não" não impede ====================

    [Fact]
    public async Task Responder_nao_ao_jejum_grava_o_termo_e_marca_a_declaracao_negada()
    {
        var paciente = await PacienteAsync();
        var modelo = await ModeloDoBsvAsync();
        await ExigirNoBsvAsync(modelo.Id);
        await AgendarBsvAsync(paciente, Hoje);

        var termo = await _documentos.EmitirTermoProcedimentoAsync(paciente, modelo.Id);

        // O paciente chegou SEM jejum, e diz isso.
        var assinado = await AssinarAsync(termo.Id, jejum: "Não");

        assinado.PacienteAssinou.Should().BeTrue(
            "o termo existe para registrar a VERDADE: bloquear faria ninguém emitir o "
            + "papel e o procedimento acontecer sem registro nenhum");

        var situacao = (await _termos.SituacaoDoDiaAsync(paciente, Hoje)).Should().ContainSingle().Subject;

        situacao.Assinado.Should().BeTrue();
        situacao.Pendente.Should().BeFalse();
        situacao.TemDeclaracaoNegada.Should().BeTrue();
        situacao.DeclaracoesNegadas.Should().ContainSingle()
            .Which.Should().Be("Estou em jejum de 8 horas");
    }

    [Fact]
    public async Task Recusa_exige_motivo_e_nao_deixa_o_termo_pendente()
    {
        var paciente = await PacienteAsync();
        var modelo = await ModeloDoBsvAsync();
        await ExigirNoBsvAsync(modelo.Id);
        await AgendarBsvAsync(paciente, Hoje);

        var termo = await _documentos.EmitirTermoProcedimentoAsync(paciente, modelo.Id);

        var semMotivo = async () => await _assinaturas.RecusarAsync(termo.Id, "  ", "ana.recepcao");
        await semMotivo.Should().ThrowAsync<InvalidOperationException>();

        await _assinaturas.RecusarAsync(termo.Id, "Quis conversar com a família antes.", "ana.recepcao");

        var situacao = (await _termos.SituacaoDoDiaAsync(paciente, Hoje)).Should().ContainSingle().Subject;

        situacao.Recusado.Should().BeTrue();
        situacao.Pendente.Should().BeFalse(
            "recusa é decisão tomada: repeti-la como pendência mandaria o balcão apresentar "
            + "de novo um termo que a pessoa recusou na frente dele");
        situacao.MotivoRecusa.Should().Contain("família");
    }

    // ==================== 4. Aplicar COPIA ====================

    [Fact]
    public async Task Corrigir_o_modelo_nao_reescreve_o_termo_ja_assinado()
    {
        var paciente = await PacienteAsync();
        var modelo = await ModeloDoBsvAsync();

        var termo = await _documentos.EmitirTermoProcedimentoAsync(paciente, modelo.Id);
        await AssinarAsync(termo.Id);

        // A clínica corrige o roteiro depois.
        modelo.Corpo = "TEXTO NOVO, escrito depois que a paciente já tinha assinado.";
        modelo.Itens.Clear();
        await _db.SaveChangesAsync();

        var guardado = await _repo.ObterDocumentoAsync(termo.Id);

        guardado!.Corpo.Should().Contain("Fui informado",
            "a Lei 13.787/2018 exige que o registro guarde o que ele dizia — referência "
            + "viva faria corrigir uma palavra hoje reescrever o que foi assinado ontem");
        guardado.Itens.Should().HaveCount(2);
    }

    // ==================== 5. A evidência é verificável ====================

    [Fact]
    public async Task Selo_do_conteudo_acusa_quando_o_termo_e_alterado_depois_da_assinatura()
    {
        var paciente = await PacienteAsync();
        var modelo = await ModeloDoBsvAsync();
        var termo = await _documentos.EmitirTermoProcedimentoAsync(paciente, modelo.Id);

        var assinado = await AssinarAsync(termo.Id);

        AssinaturaDoPacienteService.ConteudoIntacto(assinado).Should().BeTrue();

        // Alguém mexe no que já estava assinado.
        assinado.Corpo = "Texto trocado por baixo da assinatura.";

        AssinaturaDoPacienteService.ConteudoIntacto(assinado).Should().BeFalse(
            "guardar um hash que ninguém recalcula é guardar um número");
    }

    [Fact]
    public async Task Documento_sem_selo_responde_nao_sei_em_vez_de_nao_bate()
    {
        var paciente = await PacienteAsync();
        var modelo = await ModeloDoBsvAsync();
        var termo = await _documentos.EmitirTermoProcedimentoAsync(paciente, modelo.Id);

        AssinaturaDoPacienteService.ConteudoIntacto(termo).Should().BeNull(
            "\"não sei\" é resposta diferente de \"não bate\", e confundi-las acusaria "
            + "adulteração em todo termo ainda não assinado");
    }

    [Fact]
    public async Task Assinatura_em_branco_e_recusada()
    {
        var paciente = await PacienteAsync();
        var modelo = await ModeloDoBsvAsync();
        var termo = await _documentos.EmitirTermoProcedimentoAsync(paciente, modelo.Id);

        var acao = async () => await _assinaturas.ColherAsync(
            termo.Id, new byte[8], 600, 220, new Dictionary<int, string?>(),
            "CPF 123.456.789-00", "ana.recepcao");

        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*em branco*",
                "um canvas vazio exportado ainda é um PNG válido, e gravá-lo daria um termo "
                + "\"assinado\" com a linha vazia — que some da lista de quem falta");
    }

    [Fact]
    public async Task Sem_documento_conferido_nao_se_colhe_assinatura()
    {
        var paciente = await PacienteAsync();
        var modelo = await ModeloDoBsvAsync();
        var termo = await _documentos.EmitirTermoProcedimentoAsync(paciente, modelo.Id);

        var acao = async () => await _assinaturas.ColherAsync(
            termo.Id, TracoDeTeste(), 600, 220, new Dictionary<int, string?>(),
            "   ", "ana.recepcao");

        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*documento de identidade*",
                "o paciente não assina com certificado: a conferência do documento é o "
                + "que responde \"quem assinou?\"");
    }

    // ==================== 6. Os dois caminhos concordam ====================

    [Fact]
    public async Task Leitura_em_lote_da_fila_concorda_com_a_leitura_por_paciente()
    {
        var modelo = await ModeloDoBsvAsync();
        await ExigirNoBsvAsync(modelo.Id);

        var semAssinar = await PacienteAsync("Sem assinar");
        var assinou = await PacienteAsync("Já assinou");
        var semBsv = await PacienteAsync("Faz acupuntura");

        await AgendarBsvAsync(semAssinar, Hoje);
        await AgendarBsvAsync(assinou, Hoje);

        _db.Agendamentos.Add(new Agendamento
        {
            PacienteId = semBsv,
            DataHora = Hoje.ToDateTime(new TimeOnly(11, 0)),
            ModalidadePrevista = ModalidadeAtendimento.AcupunturaSimples,
            Status = StatusAgendamento.Agendado
        });
        await _db.SaveChangesAsync();

        var termo = await _documentos.EmitirTermoProcedimentoAsync(assinou, modelo.Id);
        await AssinarAsync(termo.Id);

        var lote = await _termos.DoDiaAsync(Hoje);

        // Quem é do BSV aparece; quem não é, não.
        lote.Should().ContainKey(semAssinar);
        lote.Should().ContainKey(assinou);
        lote.Should().NotContainKey(semBsv,
            "a acupuntura não exige termo, e cobrar papel de quem não precisa é o alerta "
            + "que dispara para todo mundo e ninguém lê");

        lote[semAssinar].Should().ContainSingle().Which.Pendente.Should().BeTrue();
        lote[assinou].Should().ContainSingle().Which.Pendente.Should().BeFalse();

        // E o caminho de um paciente dá EXATAMENTE a mesma resposta.
        foreach (var pacienteId in new[] { semAssinar, assinou, semBsv })
        {
            var porPaciente = await _termos.SituacaoDoDiaAsync(pacienteId, Hoje);
            var doLote = lote.TryGetValue(pacienteId, out var v) ? v : [];

            porPaciente.Select(s => (s.ModeloId, s.Pendente, s.Assinado))
                .Should().BeEquivalentTo(
                    doLote.Select(s => (s.ModeloId, s.Pendente, s.Assinado)),
                    "duas definições de \"falta assinar\" divergem na primeira correção, e a "
                    + "que ninguém lembra de ajustar é a do quadro — onde o erro aparece "
                    + "como cartão limpo, indistinguível de termo em dia");
        }
    }

    // ==================== Configuração ====================

    [Fact]
    public async Task Modelo_de_outro_tipo_nao_serve_de_termo()
    {
        var receita = new ModeloDocumento
        {
            Tipo = TipoDocumentoClinico.Receita,
            Nome = "Receita padrão",
            Corpo = "Tomar 1 comprimido."
        };
        _db.ModelosDocumento.Add(receita);
        await _db.SaveChangesAsync();

        var acao = async () => await _termos.ExigirAsync(
            ModalidadeAtendimento.BsvApenas, receita.Id);

        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*termo de procedimento*",
                "um modelo de receita amarrado como termo produziria um \"consentimento\" "
                + "com o corpo de uma prescrição, e ninguém perceberia até o paciente ler");
    }

    [Fact]
    public async Task Mesma_modalidade_nao_aceita_duas_exigencias()
    {
        var modelo = await ModeloDoBsvAsync();
        await _termos.ExigirAsync(ModalidadeAtendimento.BsvApenas, modelo.Id);

        var acao = async () => await _termos.ExigirAsync(ModalidadeAtendimento.BsvApenas, modelo.Id);

        await acao.Should().ThrowAsync<InvalidOperationException>(
            "duas exigências fariam o paciente assinar o mesmo papel duas vezes na sessão");
    }

    [Fact]
    public async Task Exigencia_desligada_para_de_cobrar_sem_apagar_o_que_foi_assinado()
    {
        var paciente = await PacienteAsync();
        var modelo = await ModeloDoBsvAsync();
        var exigencia = await _termos.ExigirAsync(ModalidadeAtendimento.BsvApenas, modelo.Id);
        await AgendarBsvAsync(paciente, Hoje);

        (await _termos.SituacaoDoDiaAsync(paciente, Hoje)).Should().ContainSingle();

        await _termos.AlternarAsync(exigencia.Id, ativa: false);

        (await _termos.SituacaoDoDiaAsync(paciente, Hoje)).Should().BeEmpty();
        (await _termos.ExigenciasAsync()).Should().ContainSingle()
            .Which.Ativa.Should().BeFalse("desligar não é apagar: o texto e a amarração ficam");
    }

    // ==================== O que o rodapé promete ====================

    [Fact]
    public async Task A_frase_do_rodape_nao_chama_o_traco_de_assinatura_digital()
    {
        var paciente = await PacienteAsync();
        var modelo = await ModeloDoBsvAsync();
        var termo = await _documentos.EmitirTermoProcedimentoAsync(paciente, modelo.Id);

        var assinado = await AssinarAsync(termo.Id);
        var frase = assinado.FraseAssinaturaPaciente;

        frase.Should().Contain("eletrônica simples");
        frase.Should().Contain("presencialmente");
        frase.Should().Contain("ana.recepcao", "a testemunha é o que substitui o certificado");
        frase.Should().Contain(assinado.CodigoVerificacao);

        frase.Should().NotContain("digital",
            "garantia aparente é pior que ausência de garantia — a regra do carimbo "
            + "escaneado, da parcela 3");
        frase.Should().NotContain("ICP-Brasil");
    }

    [Fact]
    public async Task O_termo_nasce_numerado_e_com_codigo_de_conferencia()
    {
        var paciente = await PacienteAsync();
        var modelo = await ModeloDoBsvAsync();

        var termo = await _documentos.EmitirTermoProcedimentoAsync(paciente, modelo.Id);

        termo.Numero.Should().MatchRegex(@"^\d{4}/\d{4}$",
            "é por ele que a clínica acha o termo depois");
        termo.CodigoVerificacao.Should().NotBeNullOrWhiteSpace();

        // E o número acha o documento de volta.
        var achado = await _documentos.PorCodigoAsync(termo.CodigoVerificacao);
        achado!.Id.Should().Be(termo.Id);
    }

    [Fact]
    public async Task Declaracoes_nascem_sem_resposta()
    {
        var paciente = await PacienteAsync();
        var modelo = await ModeloDoBsvAsync();

        var termo = await _documentos.EmitirTermoProcedimentoAsync(paciente, modelo.Id);

        termo.Itens.Should().HaveCount(2);
        termo.Itens.Should().OnlyContain(i => i.Quantidade == null,
            "pré-marcar \"Sim\" seria fabricar a resposta mais conveniente para a clínica, "
            + "que é o oposto do que o termo existe para provar");
    }
}
