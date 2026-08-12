using Clinica.Application.Abstracoes;
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
/// Armazenamento falso, em memória. Prova o serviço inteiro sem contratar nuvem nenhuma —
/// e é o que garante que a escolha do provedor continue sendo decisão comercial.
/// </summary>
public sealed class ArmazenamentoFake : IArmazenamentoPublico
{
    public Dictionary<string, byte[]> Objetos { get; } = new();
    public List<string> Removidos { get; } = [];

    /// <summary>Quando true, toda publicação falha — para provar a degradação.</summary>
    public bool Quebrado { get; set; }

    /// <summary>
    /// Quando true, a REMOÇÃO falha. Separado do <see cref="Quebrado"/> porque as duas
    /// falhas têm gravidades opostas: publicar que falha deixa o documento fora do ar
    /// (chato); remover que falha deixa dado de saúde NO ar (grave).
    /// </summary>
    public bool RecusaRemover { get; set; }

    public Task PublicarAsync(
        string caminho, byte[] conteudo, string tipoConteudo, CancellationToken ct = default)
    {
        if (Quebrado) throw new InvalidOperationException("armazenamento fora do ar");
        Objetos[caminho] = conteudo;
        return Task.CompletedTask;
    }

    public Task RemoverAsync(string caminho, CancellationToken ct = default)
    {
        if (RecusaRemover) throw new InvalidOperationException("armazenamento recusou a remoção");
        Objetos.Remove(caminho);
        Removidos.Add(caminho);
        return Task.CompletedTask;
    }
}

/// <summary>
/// A PUBLICAÇÃO DO DOCUMENTO ASSINADO (parcela 53).
///
/// O que está sendo provado aqui não é upload de arquivo — é a REGRA de quem pode virar
/// link e por quanto tempo. Publicar é abrir um endereço na internet para dado de saúde,
/// e a única coisa entre ele e o mundo é a entropia do token.
/// </summary>
public class PublicacaoDocumentoTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly ArmazenamentoFake _armazenamento = new();
    private readonly ParametrosService _parametros;
    private PublicacaoDocumentoService _servico;

    private static readonly DateOnly Hoje = new(2026, 8, 9);
    private const string BaseUrl = "https://receita.clinicasemdor.com.br";

    public PublicacaoDocumentoTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _parametros = new ParametrosService(_repo);
        _servico = new PublicacaoDocumentoService(_repo, _armazenamento, _parametros, () => Hoje);
    }

    private void EmDia(DateOnly dia)
        => _servico = new PublicacaoDocumentoService(_repo, _armazenamento, _parametros, () => dia);

    private async Task LigarAsync() => await _parametros.SalvarUrlPublicacaoAsync(BaseUrl);

    private async Task<DocumentoClinico> DocumentoAsync(
        TipoDocumentoClinico tipo = TipoDocumentoClinico.Receita)
    {
        var paciente = new Paciente { Nome = "Marina", Sexo = Sexo.Feminino };
        _db.Pacientes.Add(paciente);
        await _db.SaveChangesAsync();

        var doc = new DocumentoClinico
        {
            Numero = "2026/0087",
            CodigoVerificacao = "H4TX9B26",
            Tipo = tipo,
            PacienteId = paciente.Id,
            Data = Hoje
        };
        _db.DocumentosClinicos.Add(doc);
        await _db.SaveChangesAsync();
        return doc;
    }

    // ======================================= o que pode e o que NUNCA pode virar link

    [Fact]
    public void Só_o_que_o_paciente_leva_para_fora_se_publica()
    {
        PublicacaoDocumento.PodePublicar(TipoDocumentoClinico.Receita).Should().BeTrue();
        PublicacaoDocumento.PodePublicar(TipoDocumentoClinico.Atestado).Should().BeTrue();
        PublicacaoDocumento.PodePublicar(TipoDocumentoClinico.PedidoExame).Should().BeTrue();
        PublicacaoDocumento.PodePublicar(TipoDocumentoClinico.Comparecimento).Should().BeTrue();

        // Prontuário NÃO vira URL. Estes três são registro interno — publicá-los criaria
        // um endereço na internet para o histórico clínico da pessoa.
        PublicacaoDocumento.PodePublicar(TipoDocumentoClinico.RelatorioEvolucao).Should().BeFalse();
        PublicacaoDocumento.PodePublicar(TipoDocumentoClinico.Anamnese).Should().BeFalse();
        PublicacaoDocumento.PodePublicar(TipoDocumentoClinico.Consentimento).Should().BeFalse();
    }

    [Fact]
    public async Task Relatorio_de_evolucao_nao_ganha_token_nem_sobe()
    {
        await LigarAsync();
        var doc = await DocumentoAsync(TipoDocumentoClinico.RelatorioEvolucao);

        (await _servico.GarantirTokenAsync(doc)).Should().BeNull();
        (await _servico.PublicarAsync(doc, [1, 2, 3])).Publicou.Should().BeFalse();

        doc.TokenPublicacao.Should().BeNull();
        _armazenamento.Objetos.Should().BeEmpty("prontuário não vira URL, nunca");
    }

    // ============================================================= desligado por padrão

    [Fact]
    public async Task Sem_dominio_configurado_o_sistema_funciona_como_antes()
    {
        var doc = await DocumentoAsync();

        (await _servico.LigadaAsync()).Should().BeFalse();
        (await _servico.GarantirTokenAsync(doc)).Should().BeNull(
            "sem publicação o QR volta a apontar para o validador do ITI");
        _armazenamento.Objetos.Should().BeEmpty();
    }

    // ==================================================================== o caminho feliz

    [Fact]
    public async Task O_token_nasce_antes_e_a_URL_e_deterministica()
    {
        await LigarAsync();
        var doc = await DocumentoAsync();

        var url = await _servico.GarantirTokenAsync(doc);

        doc.TokenPublicacao.Should().NotBeNullOrWhiteSpace();
        doc.TokenPublicacao!.Length.Should().Be(PublicacaoDocumento.TamanhoToken);
        url.Should().Be(PublicacaoDocumento.Url(BaseUrl, doc.TokenPublicacao!));
        url.Should().StartWith(BaseUrl);
    }

    [Fact]
    public async Task Chamar_duas_vezes_NAO_troca_o_token()
    {
        await LigarAsync();
        var doc = await DocumentoAsync();

        var primeira = await _servico.GarantirTokenAsync(doc);
        var segunda = await _servico.GarantirTokenAsync(doc);

        segunda.Should().Be(primeira,
            "o token é selado no QR do PDF assinado — trocá-lo mataria o arquivo que o "
            + "paciente já tem na mão");
    }

    [Fact]
    public async Task Publicar_sobe_o_arquivo_e_abre_a_janela()
    {
        await LigarAsync();
        var doc = await DocumentoAsync();
        await _servico.GarantirTokenAsync(doc);

        var resultado = await _servico.PublicarAsync(doc, [9, 9, 9]);

        resultado.Publicou.Should().BeTrue();
        resultado.Ate.Should().Be(Hoje.AddDays(PublicacaoDocumento.DiasPublicadoPadrao));
        _armazenamento.Objetos.Should()
            .ContainKey(PublicacaoDocumento.CaminhoDoObjeto(doc.TokenPublicacao!));
        doc.LinkNoAr(Hoje).Should().BeTrue();

        (await _db.Auditoria.AsNoTracking().Select(e => e.Acao).ToListAsync())
            .Should().Contain("DocumentoPublicado");
    }

    // ================================================================== a expiração

    [Fact]
    public async Task O_link_expira_e_o_registro_FICA()
    {
        await LigarAsync();
        var doc = await DocumentoAsync();
        await _servico.GarantirTokenAsync(doc);
        await _servico.PublicarAsync(doc, [9, 9, 9]);
        var caminho = PublicacaoDocumento.CaminhoDoObjeto(doc.TokenPublicacao!);

        EmDia(Hoje.AddDays(PublicacaoDocumento.DiasPublicadoPadrao + 1));
        (await _servico.ExpirarVencidosAsync()).Should().Be(1);

        _armazenamento.Objetos.Should().NotContainKey(caminho, "o objeto sai do ar");

        // …e o documento continua na base, com o token, pronto para republicar. O que
        // expirou foi a PUBLICAÇÃO, não o registro — a guarda de 20 anos não é tocada.
        var guardado = await _db.DocumentosClinicos.AsNoTracking()
            .SingleAsync(d => d.Id == doc.Id);
        guardado.TokenPublicacao.Should().NotBeNull();
        guardado.PublicadoAte.Should().BeNull();
    }

    [Fact]
    public async Task Renovar_reusa_o_MESMO_token_para_o_QR_antigo_voltar_a_valer()
    {
        await LigarAsync();
        var doc = await DocumentoAsync();
        await _servico.GarantirTokenAsync(doc);
        var token = doc.TokenPublicacao!;

        // O arquivo assinado guardado é o que a renovação re-sobe.
        var arquivo = new ArquivoAssinado { Conteudo = [7, 7, 7], NomeArquivo = "r.pdf" };
        _db.ArquivosAssinados.Add(arquivo);
        await _db.SaveChangesAsync();
        doc.ArquivoAssinadoId = arquivo.Id;
        await _db.SaveChangesAsync();

        EmDia(Hoje.AddDays(90));
        var resultado = await _servico.RenovarAsync(doc.Id);

        resultado.Publicou.Should().BeTrue();
        doc.TokenPublicacao.Should().Be(token);
        resultado.Url.Should().Be(PublicacaoDocumento.Url(BaseUrl, token));
    }

    // ============================================================ degradação e recusas

    [Fact]
    public async Task Falha_ao_publicar_NAO_invalida_a_assinatura_mas_APARECE()
    {
        await LigarAsync();
        var doc = await DocumentoAsync();
        await _servico.GarantirTokenAsync(doc);
        _armazenamento.Quebrado = true;

        var resultado = await _servico.PublicarAsync(doc, [9, 9, 9]);

        resultado.Publicou.Should().BeFalse();
        resultado.Erro.Should().Contain("assinado",
            "a frase precisa dizer que o documento continua válido — senão alguém emite "
            + "outro achando que o primeiro se perdeu");
        doc.LinkNoAr(Hoje).Should().BeFalse();
    }

    [Fact]
    public async Task Publicar_sem_token_e_RECUSADO_em_vez_de_cunhar_um_novo()
    {
        await LigarAsync();
        var doc = await DocumentoAsync();

        var resultado = await _servico.PublicarAsync(doc, [9, 9, 9]);

        // Cunhar agora seria pior: o PDF já assinado tem o QR de OUTRO endereço.
        resultado.Publicou.Should().BeFalse();
        resultado.Erro.Should().Contain("QR");
        _armazenamento.Objetos.Should().BeEmpty();
    }

    [Fact]
    public async Task Documento_cancelado_nao_volta_ao_ar()
    {
        await LigarAsync();
        var doc = await DocumentoAsync();
        doc.CanceladoEm = DateTime.Now;
        doc.MotivoCancelamento = "erro de posologia";
        await _db.SaveChangesAsync();

        (await _servico.RenovarAsync(doc.Id)).Publicou.Should().BeFalse();
    }

    // ======================================================= a janela é da CLÍNICA

    [Fact]
    public async Task A_clinica_escolhe_por_quantos_dias_o_documento_fica_no_ar()
    {
        await LigarAsync();
        await _parametros.SalvarDiasPublicacaoAsync(180);
        var doc = await DocumentoAsync();
        await _servico.GarantirTokenAsync(doc);

        var resultado = await _servico.PublicarAsync(doc, [9, 9, 9]);

        // Quem atende uso contínuo quer 180 dias; quem só emite receita simples quer 30.
        // Amarrar no código obrigaria a publicar versão nova para mudar de ideia.
        resultado.Ate.Should().Be(Hoje.AddDays(180));
    }

    [Theory]
    [InlineData(null, PublicacaoDocumento.DiasPublicadoPadrao)]
    [InlineData(0, PublicacaoDocumento.DiasPublicadoPadrao)]
    [InlineData(-5, PublicacaoDocumento.DiasPublicadoPadrao)]
    [InlineData(5000, PublicacaoDocumento.DiasPublicadoPadrao)]
    [InlineData(30, 30)]
    [InlineData(365, 365)]
    public void Valor_ausente_ou_fora_da_faixa_cai_no_padrao(int? configurado, int esperado)
        => PublicacaoDocumento.DiasPublicadoValidos(configurado).Should().Be(esperado);

    [Fact]
    public async Task O_prazo_de_GUARDA_nao_se_confunde_com_a_janela_de_publicacao()
    {
        await LigarAsync();
        await _parametros.SalvarDiasPublicacaoAsync(30);
        var doc = await DocumentoAsync();
        await _servico.GarantirTokenAsync(doc);
        await _servico.PublicarAsync(doc, [9, 9, 9]);

        EmDia(Hoje.AddDays(31));
        await _servico.ExpirarVencidosAsync();

        // A publicação é política da clínica e acaba; a guarda é LEI e não é tocada.
        var guardado = await _db.DocumentosClinicos.AsNoTracking().SingleAsync(d => d.Id == doc.Id);
        guardado.Should().NotBeNull();
        guardado.PublicadoAte.Should().BeNull();
        GuardaProntuario.AnosDeGuarda.Should().Be(20);
    }

    // ================================================== o teste de conexão

    [Fact]
    public async Task Testar_conexao_grava_E_apaga_sem_deixar_lixo()
    {
        var erro = await _servico.TestarConexaoAsync();

        erro.Should().BeNull();
        _armazenamento.Objetos.Should().BeEmpty("teste que deixa lixo ensina a clínica a não testar");
        _armazenamento.Removidos.Should().Contain(PublicacaoDocumentoService.CaminhoDeTeste);
    }

    [Fact]
    public void Testar_conexao_NAO_usa_o_prefixo_dos_documentos()
        => PublicacaoDocumentoService.CaminhoDeTeste.Should().NotStartWith("r/",
            "um teste jamais pode colidir com uma receita publicada");

    [Fact]
    public async Task Testar_conexao_devolve_o_motivo_quando_o_armazenamento_recusa()
    {
        _armazenamento.Quebrado = true;

        var erro = await _servico.TestarConexaoAsync();

        erro.Should().NotBeNull();
        erro.Should().Contain("fora do ar", "a frase precisa dizer o que o provedor respondeu");
    }

    // ============================================== o que decide se há armazenamento

    [Theory]
    [InlineData(null, "b", "c", "s")]
    [InlineData("https://e", null, "c", "s")]
    [InlineData("https://e", "b", null, "s")]
    [InlineData("https://e", "b", "c", null)]
    [InlineData("  ", "b", "c", "s")]
    public void Meia_credencial_e_o_mesmo_que_credencial_nenhuma(
        string? endpoint, string? bucket, string? chave, string? segredo)
        => OpcoesArmazenamento.De(endpoint, null, bucket, chave, segredo).Should().BeNull(
            "aceitar conjunto incompleto faria a publicação parecer ligada e estourar no "
            + "clique de quem está assinando, com o paciente esperando");

    [Fact]
    public void Endpoint_que_nao_e_URL_e_recusado_antes_de_chegar_ao_provedor()
        => OpcoesArmazenamento.De("br-se1.magaluobjects.com", null, "b", "c", "s")
            .Should().BeNull("sem esquema o SDK erra com uma mensagem que não fala desta tela");

    [Fact]
    public void Sem_regiao_informada_cai_no_padrao_que_os_provedores_aceitam()
        => OpcoesArmazenamento.De("https://br-se1.magaluobjects.com", null, "b", "c", "s")!
            .Regiao.Should().Be(OpcoesArmazenamento.RegiaoPadrao);

    // ================================================================= o token em si

    [Fact]
    public void O_token_e_longo_e_sorteado_por_gerador_criptografico()
    {
        var tokens = Enumerable.Range(0, 500)
            .Select(_ => PublicacaoDocumento.GerarToken()).ToList();

        tokens.Distinct().Should().HaveCount(500, "colisão de token é vazamento de receita");
        tokens.Should().OnlyContain(t => t.Length == PublicacaoDocumento.TamanhoToken);

        // Sem os caracteres que se confundem lidos à mão (I, L, O, U, 0, 1): o token não é
        // digitado, mas alguém vai lê-lo em log e em suporte.
        tokens.Should().OnlyContain(t => !t.Any(c => "ILOU01".Contains(c)));
    }

    // ======================================= TIRAR DO AR (parcela 63)

    /// <summary>
    /// O par que faltava do publicar. Até a parcela 63 o link só saía do ar sozinho, na
    /// expiração: uma receita publicada por engano — o paciente errado, o documento
    /// errado — ficava acessível a quem tivesse o endereço por 30 ou 180 dias.
    ///
    /// E o que sai do ar é a PUBLICAÇÃO, nunca o registro: os bytes assinados continuam no
    /// banco pelos 20 anos da Lei 13.787/2018.
    /// </summary>
    [Fact]
    public async Task Tirar_do_ar_remove_o_arquivo_e_preserva_o_documento()
    {
        await LigarAsync();
        var doc = await DocumentoAsync();
        await _servico.GarantirTokenAsync(doc);
        var token = doc.TokenPublicacao!;

        await _servico.PublicarAsync(doc, [1, 2, 3]);
        _armazenamento.Objetos.Should().ContainKey(PublicacaoDocumento.CaminhoDoObjeto(token));

        var saiu = await _servico.DespublicarAsync(doc, operador: "ana");

        saiu.Should().BeTrue();
        _armazenamento.Objetos.Should().NotContainKey(PublicacaoDocumento.CaminhoDoObjeto(token));
        doc.PublicadoAte.Should().BeNull();

        // O TOKEN fica: republicar reusa o mesmo, e é isso que faz o QR já impresso voltar
        // a funcionar. Sortear outro mataria o papel que o paciente guardou.
        doc.TokenPublicacao.Should().Be(token);
    }

    /// <summary>
    /// Quem tirou do ar vai para a trilha. Desde que existe BOTÃO, "o sistema expirou o
    /// link" e "a Ana tirou a receita do ar" são fatos diferentes — e é o segundo que uma
    /// investigação procura.
    /// </summary>
    [Fact]
    public async Task Quem_tirou_do_ar_fica_na_trilha()
    {
        await LigarAsync();
        var doc = await DocumentoAsync();
        await _servico.GarantirTokenAsync(doc);
        await _servico.PublicarAsync(doc, [1]);

        await _servico.DespublicarAsync(doc, operador: "ana");

        var evento = _db.Auditoria.Single(e => e.Acao == "DocumentoDespublicado");
        evento.Operador.Should().Be("ana");
    }

    /// <summary>
    /// ⚠️ <b>O defeito que a parcela 63 achou</b>: a documentação do serviço afirmava, desde
    /// a parcela 53, que o cancelamento tirava o link do ar. Ele <b>nunca fez isso</b> — a
    /// única chamada de <c>DespublicarAsync</c> era a da expiração.
    ///
    /// O papel dizia "CANCELADA" e o endereço público continuava entregando o PDF assinado
    /// por até 180 dias. É a pior espécie de documento no ar: um que a clínica já
    /// invalidou, com assinatura criptograficamente válida.
    ///
    /// A correção mora no SERVIÇO porque o cancelamento tem QUATRO portas — a ficha do
    /// paciente, as Prescrições e dois caminhos da central. É este teste que impede a
    /// quinta porta de nascer sem ela.
    /// </summary>
    [Fact]
    public async Task Cancelar_o_documento_TIRA_o_link_do_ar()
    {
        await LigarAsync();
        var doc = await DocumentoAsync();
        await _servico.GarantirTokenAsync(doc);
        await _servico.PublicarAsync(doc, [1, 2, 3]);

        var caminho = PublicacaoDocumento.CaminhoDoObjeto(doc.TokenPublicacao!);
        _armazenamento.Objetos.Should().ContainKey(caminho);

        var documentos = new DocumentoClinicoService(
            _repo, new ProntuarioService(_repo), new ConsentimentoService(_repo), _servico);

        await documentos.CancelarAsync(doc.Id, "Paciente errado.", "ana");

        _armazenamento.Objetos.Should().NotContainKey(caminho,
            "receita cancelada baixável pelo QR é a pior espécie de arquivo no ar");

        var salvo = _db.DocumentosClinicos.Single(d => d.Id == doc.Id);
        salvo.Cancelado.Should().BeTrue();
        salvo.PublicadoAte.Should().BeNull();
    }

    /// <summary>
    /// O cancelamento é o fato que NÃO pode falhar. Armazenamento fora do ar não desfaz a
    /// invalidação — deixar válido um documento que a clínica acabou de cancelar é o pior
    /// dos dois desfechos, e a falha vai para o log com o caminho do arquivo.
    /// </summary>
    [Fact]
    public async Task Armazenamento_fora_do_ar_nao_desfaz_o_cancelamento()
    {
        await LigarAsync();
        var doc = await DocumentoAsync();
        await _servico.GarantirTokenAsync(doc);
        await _servico.PublicarAsync(doc, [1]);

        _armazenamento.RecusaRemover = true;

        var documentos = new DocumentoClinicoService(
            _repo, new ProntuarioService(_repo), new ConsentimentoService(_repo), _servico);

        await documentos.CancelarAsync(doc.Id, "Paciente errado.", "ana");

        _db.DocumentosClinicos.Single(d => d.Id == doc.Id).Cancelado.Should().BeTrue();
    }

    /// <summary>
    /// E a tela precisa saber que NÃO saiu: dizer "saiu do ar" com o arquivo ainda
    /// acessível é falha exibida como sucesso, que este projeto recusa desde a parcela 3 —
    /// a pessoa concluiria que resolveu e não voltaria ao assunto.
    /// </summary>
    [Fact]
    public async Task Remocao_recusada_devolve_falso_em_vez_de_fingir()
    {
        await LigarAsync();
        var doc = await DocumentoAsync();
        await _servico.GarantirTokenAsync(doc);
        await _servico.PublicarAsync(doc, [1]);

        _armazenamento.RecusaRemover = true;

        var saiu = await _servico.DespublicarAsync(doc, operador: "ana");

        saiu.Should().BeFalse();
        doc.PublicadoAte.Should().NotBeNull("o link continua no ar, e o campo não pode mentir");
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
