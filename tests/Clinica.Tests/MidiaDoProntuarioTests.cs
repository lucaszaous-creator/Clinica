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
/// A MÍDIA DO PRONTUÁRIO (set/2026): o vídeo da marcha e o áudio da ausculta, que não
/// cabem no banco.
///
/// O que estes testes prendem não é o upload — é a DECISÃO: pequeno continua no banco (e
/// a clínica sem armazenamento contratado não perde nada), grande vai para o
/// armazenamento pelo verbo PRIVADO, e falhar ao guardar IMPEDE o anexo. A última é a
/// assimetria que mais importa: linha gravada com o arquivo perdido é um "abrir" que não
/// abre, num registro de guarda de 20 anos.
/// </summary>
public class MidiaDoProntuarioTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly ProntuarioService _prontuario;
    private readonly AnexoPacienteService _anexos;
    private readonly ArmazenamentoFake _armazenamento = new();
    private readonly MidiaProntuarioService _midia;
    private readonly int _pacienteId;

    private static readonly DateOnly Dia = new(2026, 9, 1);

    public MidiaDoProntuarioTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        _db = new ClinicaDbContext(
            new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options);
        _db.Database.EnsureCreated();

        var p = new Paciente { Nome = "Maria", Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        _db.SaveChanges();
        _pacienteId = p.Id;

        _repo = new ClinicaRepositorio(_db);
        _prontuario = new ProntuarioService(_repo);
        _anexos = new AnexoPacienteService(_repo);
        _midia = new MidiaProntuarioService(_armazenamento);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<int> SessaoAsync()
    {
        var e = await _prontuario.SalvarAsync(
            new Evolucao { PacienteId = _pacienteId, Data = Dia, QueixaPrincipal = "Lombalgia" });
        return e.Id;
    }

    private static byte[] Bytes(int tamanho) => new byte[tamanho];

    // ==================== A decisão: banco × armazenamento ====================

    [Fact]
    public async Task Arquivo_pequeno_continua_no_BANCO()
    {
        // Não é conservadorismo: é o que faz o anexo comum continuar funcionando numa
        // clínica que nunca contratou armazenamento nenhum.
        var evolucaoId = await SessaoAsync();

        var anexo = await _prontuario.AnexarAsync(
            evolucaoId, "laudo.pdf", Bytes(1024), TipoAnexo.Documento,
            tipoConteudo: "application/pdf", midia: _midia);

        anexo.CaminhoRemoto.Should().BeNull();
        anexo.Conteudo.Should().HaveCount(1024);
        _armazenamento.Objetos.Should().BeEmpty();
    }

    [Fact]
    public async Task Arquivo_GRANDE_vai_para_o_armazenamento_e_nao_para_o_banco()
    {
        var evolucaoId = await SessaoAsync();

        var anexo = await _prontuario.AnexarAsync(
            evolucaoId, "marcha.mp4", Bytes(MidiaClinica.LimiteDoBanco + 1), TipoAnexo.Video,
            tipoConteudo: "video/mp4", midia: _midia);

        anexo.CaminhoRemoto.Should().NotBeNull();
        // Os bytes NÃO ficam nos dois lugares: duplicá-los pagaria o preço que a decisão
        // existe para evitar.
        anexo.Conteudo.Should().BeEmpty();
        // O tamanho fica na linha — é ele que a tela mostra sem baixar nada.
        anexo.Tamanho.Should().Be(MidiaClinica.LimiteDoBanco + 1);
        _armazenamento.Objetos.Should().ContainKey(anexo.CaminhoRemoto!);
    }

    [Fact]
    public async Task A_midia_e_guardada_pelo_verbo_PRIVADO_nunca_pelo_que_publica()
    {
        // A decisão da feature: a receita publicada abre para um farmacêutico ANÔNIMO de
        // propósito; um vídeo do paciente é dado de saúde, e endereço "inadivinhável" vaza
        // por print e por encaminhamento.
        var evolucaoId = await SessaoAsync();

        var anexo = await _prontuario.AnexarAsync(
            evolucaoId, "marcha.mp4", Bytes(MidiaClinica.LimiteDoBanco + 1), TipoAnexo.Video,
            tipoConteudo: "video/mp4", midia: _midia);

        _armazenamento.Privados.Should().Contain(anexo.CaminhoRemoto!);
    }

    [Fact]
    public async Task Guardar_que_FALHA_impede_o_anexo()
    {
        // A assimetria deliberada: quase tudo no sistema degrada e avisa. Aqui não —
        // linha gravada com o arquivo perdido é um "abrir" que não abre.
        var evolucaoId = await SessaoAsync();
        _armazenamento.Quebrado = true;

        var acao = () => _prontuario.AnexarAsync(
            evolucaoId, "marcha.mp4", Bytes(MidiaClinica.LimiteDoBanco + 1), TipoAnexo.Video,
            tipoConteudo: "video/mp4", midia: _midia);

        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*NÃO foi gravado*");

        var anexos = await _repo.AnexosDaEvolucaoAsync(evolucaoId);
        anexos.Should().BeEmpty();
    }

    [Fact]
    public async Task Acima_do_teto_da_midia_a_recusa_diz_o_que_fazer()
    {
        var evolucaoId = await SessaoAsync();

        var acao = () => _prontuario.AnexarAsync(
            evolucaoId, "cirurgia.mp4", Bytes(MidiaClinica.TamanhoMaximo + 1), TipoAnexo.Video,
            tipoConteudo: "video/mp4", midia: _midia);

        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*anexe o trecho que importa*");
        _armazenamento.Objetos.Should().BeEmpty();
    }

    [Fact]
    public async Task SEM_o_servico_de_midia_o_teto_continua_sendo_o_do_banco()
    {
        // A porta antiga não pode passar a aceitar 200 MB por acidente: quem não passa o
        // serviço está gravando na COLUNA.
        var evolucaoId = await SessaoAsync();

        var acao = () => _prontuario.AnexarAsync(
            evolucaoId, "marcha.mp4", Bytes(ProntuarioService.TamanhoMaximoAnexo + 1));

        await acao.Should().ThrowAsync<InvalidOperationException>();
    }

    // ==================== A leitura, venha de onde vier ====================

    [Fact]
    public async Task Ler_devolve_os_bytes_do_ARMAZENAMENTO_quando_e_de_la_que_ele_veio()
    {
        var evolucaoId = await SessaoAsync();
        var conteudo = Bytes(MidiaClinica.LimiteDoBanco + 7);
        conteudo[0] = 42;

        var anexo = await _prontuario.AnexarAsync(
            evolucaoId, "marcha.mp4", conteudo, TipoAnexo.Video,
            tipoConteudo: "video/mp4", midia: _midia);

        var lido = await _prontuario.ConteudoAnexoAsync(anexo.Id, _midia);

        lido.Should().NotBeNull();
        lido!.Length.Should().Be(conteudo.Length);
        lido[0].Should().Be(42);
    }

    [Fact]
    public async Task Ler_devolve_os_bytes_do_BANCO_quando_o_anexo_e_pequeno()
    {
        var evolucaoId = await SessaoAsync();
        var conteudo = new byte[] { 1, 2, 3 };

        var anexo = await _prontuario.AnexarAsync(
            evolucaoId, "laudo.pdf", conteudo, TipoAnexo.Documento, midia: _midia);

        var lido = await _prontuario.ConteudoAnexoAsync(anexo.Id, _midia);

        lido.Should().Equal(conteudo);
    }

    // ==================== O anexo da FICHA (a outra porta) ====================

    [Fact]
    public async Task O_arquivo_da_FICHA_segue_a_mesma_regra()
    {
        // Os dois lados foram feitos no MESMO commit de propósito: a cópia que fica para
        // trás é onde a capacidade some — e aqui ela sumiria no vídeo que o paciente
        // mandou por WhatsApp, que não pertence a sessão nenhuma.
        var anexo = await _anexos.AnexarAsync(
            _pacienteId, Dia, "Vídeo da marcha", "marcha.mp4",
            Bytes(MidiaClinica.LimiteDoBanco + 1), tipoConteudo: "video/mp4", midia: _midia);

        anexo.CaminhoRemoto.Should().NotBeNull();
        _armazenamento.Privados.Should().Contain(anexo.CaminhoRemoto!);

        var lido = await _anexos.ConteudoAsync(anexo.Id, _midia);
        lido.Should().NotBeNull();
        lido!.Length.Should().Be(MidiaClinica.LimiteDoBanco + 1);
    }

    [Fact]
    public async Task Arquivo_da_ficha_no_armazenamento_nao_grava_bytes_no_banco()
    {
        var anexo = await _anexos.AnexarAsync(
            _pacienteId, Dia, "Vídeo da marcha", "marcha.mp4",
            Bytes(MidiaClinica.LimiteDoBanco + 1), tipoConteudo: "video/mp4", midia: _midia);

        var noBanco = await _repo.ConteudoDoAnexoPacienteAsync(anexo.Id);
        noBanco.Should().BeNull();
    }

    // ==================== O tipo e o caminho ====================

    [Theory]
    [InlineData("video/mp4", "marcha.mp4", TipoAnexo.Video)]
    [InlineData(null, "marcha.MOV", TipoAnexo.Video)]
    [InlineData("audio/mpeg", "ausculta.mp3", TipoAnexo.Audio)]
    [InlineData(null, "ausculta.m4a", TipoAnexo.Audio)]
    [InlineData("image/jpeg", "regiao.jpg", TipoAnexo.Imagem)]
    [InlineData("application/pdf", "laudo.pdf", TipoAnexo.Documento)]
    [InlineData(null, "coisa.xyz", TipoAnexo.Documento)]
    public void O_tipo_sai_do_arquivo(string? mime, string nome, TipoAnexo esperado)
        => MidiaClinica.TipoDe(mime, nome).Should().Be(esperado);

    [Fact]
    public void O_caminho_saneia_a_extensao_e_nunca_deixa_o_nome_compor_a_chave()
    {
        // O nome vem de fora: deixá-lo compor o caminho cru é como se escreve um caminho
        // com "../" dentro.
        var token = MidiaClinica.GerarToken();

        var caminho = MidiaClinica.CaminhoDoObjeto(token, "../../etc/passwd");

        caminho.Should().StartWith($"m/{token[..2]}/{token}");
        caminho.Should().NotContain("..");
        caminho.Should().NotContain("/etc/");
    }

    [Fact]
    public void O_prefixo_da_midia_e_SEPARADO_do_das_receitas()
    {
        // Uma é pública com prazo, a outra é privada e guardada 20 anos: uma varredura de
        // expiração que confundisse as duas apagaria registro clínico.
        var token = MidiaClinica.GerarToken();

        MidiaClinica.CaminhoDoObjeto(token, "marcha.mp4").Should().StartWith("m/");
        PublicacaoDocumento.CaminhoDoObjeto(token).Should().StartWith("r/");
    }

    [Fact]
    public void MIME_desconhecido_devolve_NULO_e_nunca_um_palpite()
        => MidiaClinica.MimeDe("arquivo.xyz").Should().BeNull();
}
