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

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }
}
