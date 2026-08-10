using System.Net;
using System.Net.Sockets;
using System.Text;
using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

/// <summary>Uma requisição que o servidor falso recebeu, guardada inteira para conferência.</summary>
public sealed record RequisicaoS3(
    string Metodo, string Caminho, IReadOnlyDictionary<string, string> Cabecalhos, byte[] Corpo);

/// <summary>
/// Um S3 de mentira, em processo, sobre <see cref="HttpListener"/>.
///
/// Por que ele existe
/// ------------------
/// Os 30 testes de <c>PublicacaoDocumentoTests</c> usam um armazenamento FALSO em memória:
/// eles provam a REGRA (quem pode virar link, por quanto tempo, o que acontece ao vencer) e
/// não encostam no protocolo. O <c>ArmazenamentoS3</c> ficava com zero cobertura — e é
/// justamente ali que mora o erro que só apareceria na farmácia.
///
/// O que este servidor prova, e o que ele NÃO prova
/// ------------------------------------------------
/// Prova o que é NOSSO: que o SDK foi configurado para falar path-style, que o objeto sobe
/// com o tipo, a disposição e a ACL que a receita precisa, e que o DELETE vai ao mesmo
/// endereço. Não prova as manias do provedor real — se a Magalu aceita a ACL, se o balde
/// existe, se a chave tem permissão. Isso é o que o botão "Testar conexão" faz, contra o
/// provedor de verdade, e por isso ele grava de verdade em vez de listar o balde.
/// </summary>
public sealed class ServidorS3Falso : IDisposable
{
    private readonly HttpListener _ouvinte = new();
    private readonly CancellationTokenSource _parar = new();
    private readonly Task _laco;

    public List<RequisicaoS3> Requisicoes { get; } = [];
    public string Url { get; }

    public ServidorS3Falso()
    {
        var porta = PortaLivre();
        Url = $"http://127.0.0.1:{porta}";
        _ouvinte.Prefixes.Add($"{Url}/");
        _ouvinte.Start();
        _laco = Task.Run(AtenderAsync);
    }

    private static int PortaLivre()
    {
        var sonda = new TcpListener(IPAddress.Loopback, 0);
        sonda.Start();
        var porta = ((IPEndPoint)sonda.LocalEndpoint).Port;
        sonda.Stop();
        return porta;
    }

    private async Task AtenderAsync()
    {
        while (!_parar.IsCancellationRequested)
        {
            HttpListenerContext ctx;
            try { ctx = await _ouvinte.GetContextAsync(); }
            catch { return; }   // parada normal: o ouvinte foi fechado

            using var memoria = new MemoryStream();
            await ctx.Request.InputStream.CopyToAsync(memoria);

            var cabecalhos = ctx.Request.Headers.AllKeys
                .Where(k => k is not null)
                .ToDictionary(k => k!, k => ctx.Request.Headers[k] ?? string.Empty,
                    StringComparer.OrdinalIgnoreCase);

            lock (Requisicoes)
                Requisicoes.Add(new RequisicaoS3(
                    ctx.Request.HttpMethod,
                    ctx.Request.Url?.AbsolutePath ?? string.Empty,
                    cabecalhos,
                    memoria.ToArray()));

            // O S3 devolve ETag no PUT e 204 no DELETE. Sem o ETag o SDK trata como falha
            // e tenta de novo, o que encheria a lista de requisições repetidas.
            ctx.Response.Headers.Add("ETag", "\"0e0e0e\"");
            ctx.Response.StatusCode = ctx.Request.HttpMethod == "DELETE" ? 204 : 200;
            ctx.Response.Close();
        }
    }

    public void Dispose()
    {
        _parar.Cancel();
        _ouvinte.Close();
        try { _laco.Wait(TimeSpan.FromSeconds(2)); } catch { /* encerrando */ }
        _parar.Dispose();
    }
}

/// <summary>
/// O <see cref="ArmazenamentoS3"/> contra um S3 de mentira — a metade que faltava.
/// </summary>
public class ArmazenamentoS3Tests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ParametrosService _parametros;
    private readonly ServidorS3Falso _servidor = new();

    private const string Bucket = "documentos-clinica";
    private const string Regiao = "br-se1";

    public ArmazenamentoS3Tests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _parametros = new ParametrosService(new ClinicaRepositorio(_db));
    }

    private async Task<ArmazenamentoS3> ConfiguradoAsync()
    {
        // Se a máquina tiver as variáveis de ambiente definidas, elas VENCEM o banco e o
        // teste apontaria para o provedor de verdade. Melhor parar com uma frase do que
        // subir arquivo de teste num balde de produção.
        ProvedorOpcoesArmazenamento.VemDoAmbiente().Should().BeFalse(
            "as variáveis CLINICA_ARMAZENAMENTO_* precisam estar apagadas para este teste");

        await _parametros.SalvarCredenciaisArmazenamentoAsync(
            _servidor.Url, Regiao, Bucket, "a-chave", "o-segredo");

        return new ArmazenamentoS3(new ProvedorOpcoesArmazenamento(_parametros));
    }

    private RequisicaoS3 Unica(string metodo)
    {
        lock (_servidor.Requisicoes)
            return _servidor.Requisicoes.Single(r => r.Metodo == metodo);
    }

    // ============================================ o motivo de o SDK estar aqui

    [Fact]
    public async Task O_objeto_sobe_em_PATH_STYLE_e_e_por_isso_que_o_SDK_esta_aqui()
    {
        var armazenamento = await ConfiguradoAsync();

        await armazenamento.PublicarAsync("r/K7/K7M3PQ.pdf", [1, 2, 3], "application/pdf");

        // Sem ForcePathStyle o SDK tentaria "documentos-clinica.127.0.0.1", que sequer
        // resolve — e é exatamente a falha que apareceria só no provedor do cliente.
        Unica("PUT").Caminho.Should().Be($"/{Bucket}/r/K7/K7M3PQ.pdf");
    }

    [Fact]
    public async Task O_conteudo_sobe_byte_a_byte()
    {
        var armazenamento = await ConfiguradoAsync();
        var pdf = Encoding.UTF8.GetBytes("%PDF-1.7 receita assinada");

        await armazenamento.PublicarAsync("r/AB/ABC.pdf", pdf, "application/pdf");

        Unica("PUT").Corpo.Should().Equal(pdf);
    }

    // ==================================== o que faz a receita ABRIR no celular

    [Fact]
    public async Task Sobe_como_PDF_e_para_ABRIR_no_navegador_em_vez_de_baixar()
    {
        var armazenamento = await ConfiguradoAsync();

        await armazenamento.PublicarAsync("r/AB/ABC.pdf", [1], "application/pdf");

        var put = Unica("PUT");
        put.Cabecalhos["Content-Type"].Should().Be("application/pdf");

        // Sem isto o celular BAIXA o arquivo e o farmacêutico vai procurá-lo na pasta de
        // downloads com o cliente no balcão.
        put.Cabecalhos["Content-Disposition"].Should().Be("inline; filename=\"documento.pdf\"");
    }

    [Fact]
    public async Task Sobe_com_leitura_PUBLICA_porque_o_farmaceutico_e_anonimo()
    {
        var armazenamento = await ConfiguradoAsync();

        await armazenamento.PublicarAsync("r/AB/ABC.pdf", [1], "application/pdf");

        // O desenho inteiro depende disto: quem confere a receita não tem conta em lugar
        // nenhum. A barreira é o token de 128 bits, não uma credencial.
        Unica("PUT").Cabecalhos["x-amz-acl"].Should().Be("public-read");
    }

    [Fact]
    public async Task A_requisicao_vai_ASSINADA_com_a_regiao_configurada()
    {
        var armazenamento = await ConfiguradoAsync();

        await armazenamento.PublicarAsync("r/AB/ABC.pdf", [1], "application/pdf");

        var autorizacao = Unica("PUT").Cabecalhos["Authorization"];
        autorizacao.Should().StartWith("AWS4-HMAC-SHA256");
        autorizacao.Should().Contain("a-chave");
        autorizacao.Should().Contain(Regiao,
            "região errada faz o provedor recusar por assinatura inválida, e a mensagem "
            + "dele não fala nada sobre a tela de Configurações");
    }

    // ==================================================== tirar do ar

    [Fact]
    public async Task Remover_apaga_o_MESMO_endereco_que_publicou()
    {
        var armazenamento = await ConfiguradoAsync();

        await armazenamento.PublicarAsync("r/K7/K7M3PQ.pdf", [1], "application/pdf");
        await armazenamento.RemoverAsync("r/K7/K7M3PQ.pdf");

        Unica("DELETE").Caminho.Should().Be(Unica("PUT").Caminho,
            "expirar tem de alcançar exatamente o objeto que a publicação criou");
    }

    // ============================================ sem credencial, RECUSA falando

    [Fact]
    public async Task Sem_credencial_recusa_dizendo_o_que_falta()
    {
        var armazenamento = new ArmazenamentoS3(new ProvedorOpcoesArmazenamento(_parametros));

        var acao = () => armazenamento.PublicarAsync("r/AB/ABC.pdf", [1], "application/pdf");

        // Nem NullReferenceException na cara de quem está assinando, nem silêncio — que
        // faria a clínica acreditar que publicou e o farmacêutico achar endereço morto.
        (await acao.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*Configurações*");

        lock (_servidor.Requisicoes)
            _servidor.Requisicoes.Should().BeEmpty();
    }

    public void Dispose()
    {
        _servidor.Dispose();
        _db.Dispose();
        _conn.Dispose();
    }
}
