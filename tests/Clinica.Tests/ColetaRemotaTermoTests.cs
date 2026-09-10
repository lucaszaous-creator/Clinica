using System.Text;
using System.Text.Json;
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
/// O TERMO PELO WHATSAPP (parcela 81) — a decisão inteira em docs/termo-pelo-whatsapp.md.
///
/// O que se fixa aqui é o CONTRATO com a borda e as regras que protegem o paciente: o
/// pedido publicado é MINIMIZADO (nem sobrenome sai), o token tem a entropia das receitas,
/// o segundo clique reaproveita o envio em aberto, o vencido é cancelado com registro, e
/// concluir tira TUDO do ar. O write-once é do Worker (borda, fora do alcance destes
/// testes) e está coberto pela validação do lado de cá: traço inválido manda reenviar.
/// </summary>
public class ColetaRemotaTermoTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly DocumentoClinicoService _documentos;
    private readonly ArmazenamentoFake _balde = new();
    private readonly ParametrosService _parametros;
    private readonly ColetaRemotaTermoService _servico;

    /// <summary>Relógio injetado: a expiração é regra de segurança, e regra de segurança
    /// que não dá para testar apodrece sem ninguém notar.</summary>
    private DateTime _agora = DateTime.Today.AddHours(9);

    public ColetaRemotaTermoTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        _db = new ClinicaDbContext(
            new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _documentos = new DocumentoClinicoService(
            _repo, new ProntuarioService(_repo), new ConsentimentoService(_repo));
        _parametros = new ParametrosService(_repo);
        _servico = new ColetaRemotaTermoService(_repo, _balde, _parametros, () => _agora);
    }

    private async Task<(int PacienteId, int DocumentoId)> CenarioAsync(
        string telefone = "(22) 99999-0000")
    {
        await _parametros.SalvarUrlPublicacaoAsync("https://docs.clinica.exemplo");

        var paciente = new Paciente
        {
            Nome = "Maria Aparecida dos Santos",
            Convenio = Convenio.UnimedPadrao,
            Telefone = telefone,
            Documento = "123.456.789-00"
        };
        _db.Pacientes.Add(paciente);

        var modelo = new ModeloDocumento
        {
            Tipo = TipoDocumentoClinico.TermoProcedimento,
            Nome = "Termo do BSV",
            Titulo = "Consentimento para Bloqueio Simpático Venoso",
            Corpo = "Fui informado(a) dos riscos e concordo com o procedimento.",
            Itens =
            [
                new ItemModelo { Ordem = 1, Descricao = "Estou em jejum de 8 horas" },
                new ItemModelo { Ordem = 2, Descricao = "Informei os medicamentos que uso" }
            ]
        };
        _db.ModelosDocumento.Add(modelo);
        await _db.SaveChangesAsync();

        var termo = await _documentos.EmitirTermoProcedimentoAsync(paciente.Id, modelo.Id);
        return (paciente.Id, termo.Id);
    }

    private static string TracoValido()
        => "data:image/png;base64," + Convert.ToBase64String(new byte[600]);

    private void ResponderNoBalde(string token, string? traco = null, string ip = "203.0.113.7")
        => _balde.Objetos[ColetaRemotaTermo.CaminhoResposta(token)] = Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new
            {
                versao = 1,
                respostas = new Dictionary<string, string> { ["1"] = "Sim", ["2"] = "Não" },
                traco = traco ?? TracoValido(),
                tracoLargura = 600,
                tracoAltura = 200,
                respondidoEmUnixMs = new DateTimeOffset(_agora.AddMinutes(3))
                    .ToUnixTimeMilliseconds(),
                ip,
                aparelho = "Mozilla/5.0 (Android)"
            }));

    // ==================== O envio ====================

    [Fact]
    public async Task Envia_publica_o_pedido_MINIMIZADO_e_devolve_o_link()
    {
        var (_, documentoId) = await CenarioAsync();

        var envio = await _servico.EnviarAsync(documentoId, "evelyn");

        envio.Token.Should().MatchRegex("^[A-Z2-9]{26}$",
            "o token é a única barreira de acesso, com a entropia das receitas");
        envio.Url.Should().Be($"https://docs.clinica.exemplo/t/{envio.Token}");
        envio.Telefone.Should().Be("(22) 99999-0000");
        envio.Mensagem.Should().Contain(envio.Url);

        var pedido = Encoding.UTF8.GetString(
            _balde.Objetos[ColetaRemotaTermo.CaminhoPedido(envio.Token)]);

        // O conteúdo confere pelos VALORES (o contrato do Worker), não por substring cega.
        using var json = JsonDocument.Parse(pedido);
        var raiz = json.RootElement;
        raiz.GetProperty("titulo").GetString()
            .Should().Be("Consentimento para Bloqueio Simpático Venoso");
        raiz.GetProperty("paciente").GetString().Should().Be("Maria",
            "o primeiro nome é o que o paciente precisa para se reconhecer");
        raiz.GetProperty("declaracoes")[0].GetProperty("texto").GetString()
            .Should().Be("Estou em jejum de 8 horas");
        raiz.GetProperty("expiraEmUnixMs").GetInt64().Should().BeGreaterThan(0);

        // ⚠️ A MINIMIZAÇÃO é o teste que importa (docs §3): cada campo a mais aqui é dado
        // de saúde a mais no ar. Nem o sobrenome sai.
        pedido.Should().NotContain("Aparecida");
        pedido.Should().NotContain("Santos");
        pedido.Should().NotContain("123.456.789-00");
        pedido.Should().NotContain("99999-0000");
    }

    [Fact]
    public async Task Sem_endereco_publico_recusa_dizendo_onde_configurar()
    {
        var (_, documentoId) = await CenarioAsync();
        await _parametros.SalvarUrlPublicacaoAsync(null);

        var enviar = () => _servico.EnviarAsync(documentoId, "evelyn");

        await enviar.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Configurações*Publicação*");
        _balde.Objetos.Should().BeEmpty("recusa não publica nada");
    }

    [Fact]
    public async Task Sem_celular_na_ficha_recusa_mandando_cadastrar()
    {
        var (_, documentoId) = await CenarioAsync(telefone: "");

        var enviar = () => _servico.EnviarAsync(documentoId, "evelyn");

        await enviar.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*não tem celular cadastrado*");
    }

    [Fact]
    public async Task Segundo_clique_reaproveita_o_envio_em_aberto()
    {
        var (_, documentoId) = await CenarioAsync();

        var primeiro = await _servico.EnviarAsync(documentoId, "evelyn");
        var segundo = await _servico.EnviarAsync(documentoId, "evelyn");

        segundo.Token.Should().Be(primeiro.Token,
            "o paciente pode já estar com o primeiro link na mão — token novo o mataria");
        _db.ColetasRemotasTermo.Count().Should().Be(1);
    }

    [Fact]
    public async Task Envio_vencido_gera_token_novo_e_o_velho_fica_cancelado_e_fora_do_ar()
    {
        var (_, documentoId) = await CenarioAsync();
        var primeiro = await _servico.EnviarAsync(documentoId, "evelyn");

        _agora = _agora.AddHours(ColetaRemotaTermo.HorasNoAr + 1);
        var segundo = await _servico.EnviarAsync(documentoId, "evelyn");

        segundo.Token.Should().NotBe(primeiro.Token);
        _balde.Objetos.Should().NotContainKey(ColetaRemotaTermo.CaminhoPedido(primeiro.Token),
            "o pedido vencido sai do ar quando o reenvio o substitui");

        var linhas = _db.ColetasRemotasTermo.OrderBy(c => c.Id).ToList();
        linhas.Should().HaveCount(2);
        linhas[0].CanceladaEm.Should().NotBeNull("a linha NÃO se apaga — fica marcada");
        linhas[1].EmAberto.Should().BeTrue();
    }

    // ==================== A resposta ====================

    [Fact]
    public async Task Sem_resposta_no_balde_devolve_null_e_nada_muda()
    {
        var (_, documentoId) = await CenarioAsync();
        await _servico.EnviarAsync(documentoId, "evelyn");

        (await _servico.ColherRespostaAsync(documentoId)).Should().BeNull();
    }

    [Fact]
    public async Task Resposta_volta_com_as_respostas_do_PACIENTE_o_traco_e_a_evidencia()
    {
        var (_, documentoId) = await CenarioAsync();
        var envio = await _servico.EnviarAsync(documentoId, "evelyn");
        ResponderNoBalde(envio.Token);

        var resposta = await _servico.ColherRespostaAsync(documentoId);

        resposta.Should().NotBeNull();
        resposta!.Respostas[1].Should().Be("Sim");
        resposta.Respostas[2].Should().Be("Não",
            "o \"não\" do paciente é registrado como ele respondeu — avisa, não impede");
        resposta.TracoPng.Length.Should().Be(600);
        resposta.Largura.Should().Be(600);
        resposta.TelefoneDestino.Should().Be("(22) 99999-0000");

        var linha = _db.ColetasRemotasTermo.Single();
        linha.RespondidaEm.Should().NotBeNull();
        linha.EvidenciaResposta.Should().Contain("203.0.113.7")
            .And.Contain("Android", "IP e aparelho são a evidência do canal");
    }

    [Fact]
    public async Task Traco_invalido_e_recusado_mandando_cancelar_e_reenviar()
    {
        var (_, documentoId) = await CenarioAsync();
        var envio = await _servico.EnviarAsync(documentoId, "evelyn");
        ResponderNoBalde(envio.Token,
            traco: "data:image/png;base64," + Convert.ToBase64String(new byte[10]));

        var colher = () => _servico.ColherRespostaAsync(documentoId);

        // Write-once não tem segunda gravação: a saída é reenviar, e a frase diz isso.
        await colher.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cancele este envio*");
    }

    // ==================== O fim do circuito ====================

    [Fact]
    public async Task Concluir_marca_a_linha_e_tira_os_DOIS_objetos_do_ar()
    {
        var (_, documentoId) = await CenarioAsync();
        var envio = await _servico.EnviarAsync(documentoId, "evelyn");
        ResponderNoBalde(envio.Token);
        await _servico.ColherRespostaAsync(documentoId);

        var limpou = await _servico.ConcluirAsync(documentoId, "evelyn");

        limpou.Should().BeTrue();
        _balde.Objetos.Should().BeEmpty("dado de saúde não fica no ar depois de colhido");
        _db.ColetasRemotasTermo.Single().ConcluidaEm.Should().NotBeNull();
    }

    [Fact]
    public async Task Concluir_com_o_balde_fora_do_ar_conclui_MESMO_ASSIM_e_avisa_devolvendo_false()
    {
        var (_, documentoId) = await CenarioAsync();
        var envio = await _servico.EnviarAsync(documentoId, "evelyn");
        ResponderNoBalde(envio.Token);
        await _servico.ColherRespostaAsync(documentoId);

        _balde.RecusaRemover = true;
        var limpou = await _servico.ConcluirAsync(documentoId, "evelyn");

        limpou.Should().BeFalse("a chamadora avisa — dado de saúde no ar não passa calado");
        _db.ColetasRemotasTermo.Single().ConcluidaEm.Should().NotBeNull(
            "o selo já existe: a falha de remoção não desfaz a coleta");
    }

    [Fact]
    public async Task Limpeza_cancela_as_vencidas_e_apaga_os_objetos()
    {
        var (_, documentoId) = await CenarioAsync();
        var envio = await _servico.EnviarAsync(documentoId, "evelyn");

        _agora = _agora.AddHours(ColetaRemotaTermo.HorasNoAr + 1);
        var quantas = await _servico.LimparVencidasAsync();

        quantas.Should().Be(1);
        _balde.Objetos.Should().BeEmpty();
        var linha = _db.ColetasRemotasTermo.Single();
        linha.CanceladaEm.Should().NotBeNull();
        linha.CanceladaPor.Should().Be("expiração automática");
        _ = envio;
    }

    // ====================================================================
    // A ASSINATURA QUE CHEGOU NÃO SE PERDE (set/2026)
    // ====================================================================
    //
    // A clínica enviou o link, a paciente assinou e confirmou — e o termo não apareceu em
    // lugar nenhum. Uma das causas foi de PERMISSÃO (a recepção não alcançava o papel que
    // colheu); a outra é esta: o traço vivia SÓ no balde, e quem o trazia para dentro era
    // a janela do termo ABERTA. Quem fechava a janela perdia a assinatura, porque a
    // varredura de 24 h cancelava a coleta e apagava o objeto.
    //
    // Nada falhava: o termo voltava a parecer "nunca assinado".

    [Fact]
    public async Task A_resposta_lida_fica_GUARDADA_no_banco_com_o_traco_e_as_declaracoes()
    {
        var (_, documentoId) = await CenarioAsync();
        var envio = await _servico.EnviarAsync(documentoId, "evelyn");
        ResponderNoBalde(envio.Token);

        await _servico.ColherRespostaAsync(documentoId);

        var linha = _db.ColetasRemotasTermo.Include(c => c.TracoAssinatura).Single();
        linha.TracoAssinatura.Should().NotBeNull("o traço é a assinatura — ele fica");
        linha.TracoAssinatura!.Conteudo.Length.Should().Be(600);
        linha.TracoAssinatura.Largura.Should().Be(600);
        linha.RespostasJson.Should().NotBeNullOrWhiteSpace(
            "guardar o traço sem as declarações seria meia recuperação: a conferência "
            + "traria a assinatura com o formulário em branco");
        linha.AguardaConferencia.Should().BeTrue();
    }

    [Fact]
    public async Task Com_o_balde_VAZIO_a_resposta_guardada_continua_respondendo()
    {
        var (_, documentoId) = await CenarioAsync();
        var envio = await _servico.EnviarAsync(documentoId, "evelyn");
        ResponderNoBalde(envio.Token);
        await _servico.ColherRespostaAsync(documentoId);

        // O objeto sai do ar (é o que a limpeza faz) e a janela é reaberta amanhã.
        _balde.Objetos.Clear();

        var resposta = await _servico.ColherRespostaAsync(documentoId);

        resposta.Should().NotBeNull("o que sai do ar é o objeto no balde, nunca o registro");
        resposta!.TracoPng.Length.Should().Be(600);
        resposta.Respostas[1].Should().Be("Sim");
        resposta.Respostas[2].Should().Be("Não");
        resposta.Evidencia.Should().Contain("203.0.113.7");
    }

    [Fact]
    public async Task A_sincronizacao_colhe_SEM_a_janela_do_termo_aberta()
    {
        var (_, documentoId) = await CenarioAsync();
        var envio = await _servico.EnviarAsync(documentoId, "evelyn");

        // Ninguém está com a janela aberta: quem lê o balde é a lista do dia do balcão.
        ResponderNoBalde(envio.Token);

        (await _servico.SincronizarRespostasAsync()).Should().Be(1);
        _db.ColetasRemotasTermo.Single().AguardaConferencia.Should().BeTrue();

        // Idempotente: a batida seguinte não tem o que colher e não vai ao balde de novo.
        (await _servico.SincronizarRespostasAsync()).Should().Be(0);
    }

    [Fact]
    public async Task A_sincronizacao_NAO_derruba_a_lista_quando_uma_coleta_esta_ruim()
    {
        var (_, documentoId) = await CenarioAsync();
        var envio = await _servico.EnviarAsync(documentoId, "evelyn");
        ResponderNoBalde(envio.Token,
            traco: "data:image/png;base64," + Convert.ToBase64String(new byte[10]));

        // Varredura de fundo, chamada por uma tela com paciente na frente: traço ilegível
        // de UMA coleta vira log, nunca exceção que apaga a agenda do dia.
        var colhidas = await _servico.SincronizarRespostasAsync();

        colhidas.Should().Be(0);
        _db.ColetasRemotasTermo.Single().AguardaConferencia.Should().BeFalse();
    }

    [Fact]
    public async Task A_limpeza_COLHE_antes_de_apagar_e_NAO_cancela_quem_ja_assinou()
    {
        var (_, documentoId) = await CenarioAsync();
        var envio = await _servico.EnviarAsync(documentoId, "evelyn");

        // A paciente assina perto do fim das 24 h e ninguém tinha a janela aberta.
        ResponderNoBalde(envio.Token);
        _agora = _agora.AddHours(ColetaRemotaTermo.HorasNoAr + 1);

        var canceladas = await _servico.LimparVencidasAsync();

        canceladas.Should().Be(0, "o que venceu foi o LINK, e ele já cumpriu o papel dele");
        _balde.Objetos.Should().BeEmpty("o objeto sai do ar; o fato fica");

        var linha = _db.ColetasRemotasTermo.Include(c => c.TracoAssinatura).Single();
        linha.CanceladaEm.Should().BeNull();
        linha.AguardaConferencia.Should().BeTrue("ela entra na fila de conferência");
        linha.TracoAssinatura!.Conteudo.Length.Should().Be(600);

        // E a assinatura continua alcançável depois de o balde ter sido esvaziado.
        (await _servico.ColherRespostaAsync(documentoId)).Should().NotBeNull();
    }

    [Fact]
    public async Task A_coleta_da_versao_ANTERIOR_e_recuperada_do_balde_em_vez_de_virar_beco()
    {
        var (_, documentoId) = await CenarioAsync();
        var envio = await _servico.EnviarAsync(documentoId, "evelyn");
        ResponderNoBalde(envio.Token);

        // Como a versão anterior deixava: carimbada como respondida, com o traço só no
        // balde. O reenvio está recusado (ela já assinou) — sem a queda para o balde,
        // ninguém alcançaria a assinatura e a limpeza a apagaria.
        var linha = _db.ColetasRemotasTermo.Single();
        linha.RespondidaEm = _agora;
        linha.EvidenciaResposta = "IP 203.0.113.7";
        await _db.SaveChangesAsync();

        var resposta = await _servico.ColherRespostaAsync(documentoId);

        resposta.Should().NotBeNull();
        resposta!.TracoPng.Length.Should().Be(600);
        _db.ColetasRemotasTermo.Single().TracoAssinaturaId.Should().NotBeNull(
            "e ela passa a estar guardada — a próxima limpeza já não a destrói");
    }

    [Fact]
    public async Task A_limpeza_RESGATA_a_coleta_da_versao_anterior_antes_de_apagar()
    {
        var (_, documentoId) = await CenarioAsync();
        var envio = await _servico.EnviarAsync(documentoId, "evelyn");
        ResponderNoBalde(envio.Token);

        var linha = _db.ColetasRemotasTermo.Single();
        linha.RespondidaEm = _agora;
        await _db.SaveChangesAsync();

        _agora = _agora.AddHours(ColetaRemotaTermo.HorasNoAr + 1);
        await _servico.LimparVencidasAsync();

        _balde.Objetos.Should().BeEmpty();
        _db.ColetasRemotasTermo.Single().TracoAssinaturaId.Should().NotBeNull(
            "colher ANTES de apagar é a correção inteira: o que sai do ar é o objeto");
        (await _servico.ColherRespostaAsync(documentoId)).Should().NotBeNull();
    }

    [Fact]
    public async Task A_limpeza_NAO_apaga_o_objeto_de_quem_assinou_e_ainda_nao_foi_colhido()
    {
        var (_, documentoId) = await CenarioAsync();
        var envio = await _servico.EnviarAsync(documentoId, "evelyn");
        ResponderNoBalde(envio.Token);

        // Respondida pela versão anterior E a colheita de hoje falha (balde fora do ar
        // para LER). Apagar o objeto aqui destruiria a assinatura pelo caminho exato que
        // esta correção existe para fechar.
        _db.ColetasRemotasTermo.Single().RespondidaEm = _agora;
        await _db.SaveChangesAsync();
        _balde.RecusaLer = true;

        _agora = _agora.AddHours(ColetaRemotaTermo.HorasNoAr + 1);
        await _servico.LimparVencidasAsync();

        _balde.Objetos.Should().ContainKey(ColetaRemotaTermo.CaminhoResposta(envio.Token),
            "ela fica no ar mais um dia e a varredura seguinte tenta de novo");
        var linha = _db.ColetasRemotasTermo.Single();
        linha.CanceladaEm.Should().BeNull();

        // E a varredura seguinte, com o balde de volta, resgata.
        _balde.RecusaLer = false;
        await _servico.LimparVencidasAsync();
        _db.ColetasRemotasTermo.Single().TracoAssinaturaId.Should().NotBeNull();
    }

    [Fact]
    public async Task Reenviar_para_quem_JA_ASSINOU_e_recusado_dizendo_o_que_fazer()
    {
        var (_, documentoId) = await CenarioAsync();
        var envio = await _servico.EnviarAsync(documentoId, "evelyn");
        ResponderNoBalde(envio.Token);
        await _servico.ColherRespostaAsync(documentoId);

        var reenviar = () => _servico.EnviarAsync(documentoId, "evelyn");

        // O link é write-once: reenviar faria a paciente ler o termo de novo para levar um
        // "não foi possível" no fim — e, antes desta versão, apagaria a assinatura dada.
        await reenviar.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*JÁ ASSINOU*");
        _db.ColetasRemotasTermo.Single().TracoAssinaturaId.Should().NotBeNull();
    }

    [Fact]
    public async Task Concluir_sela_o_termo_como_assinado_PELO_CELULAR()
    {
        var (_, documentoId) = await CenarioAsync();
        var envio = await _servico.EnviarAsync(documentoId, "evelyn");
        ResponderNoBalde(envio.Token);

        await _servico.SincronizarRespostasAsync();
        var resposta = await _servico.ColherRespostaAsync(documentoId);

        var assinaturas = new AssinaturaDoPacienteService(_repo);
        await assinaturas.ColherAsync(
            documentoId, resposta!.TracoPng, resposta.Largura, resposta.Altura,
            resposta.Respostas, "CPF 123.456.789-00", "evelyn",
            MeioAssinaturaPaciente.LinkRemoto);

        var termo = _db.DocumentosClinicos.Single(d => d.Id == documentoId);
        termo.PacienteAssinou.Should().BeTrue();
        termo.PacienteAssinaturaMeio.Should().Be(MeioAssinaturaPaciente.LinkRemoto,
            "gravar \"assinou na clínica\" sobre um termo assinado em casa seria o sistema "
            + "afirmando algo falso sobre o próprio documento");
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }
}
