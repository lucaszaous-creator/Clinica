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

/// <summary>As regras puras do chat da equipe: quem vê o quê, citação, som, presença, recorte.</summary>
public class ChatEquipeRegrasTests
{
    private static readonly DateTime Agora = new(2026, 10, 21, 10, 0, 0);

    [Theory]
    [InlineData("Ana Paula Lima", "AL")]
    [InlineData("Otávio Diretor (fictício)", "OD")]
    [InlineData("Dra. Helena Duarte", "HD")]
    [InlineData("Maria da Silva", "MS")]
    [InlineData("Joana", "JO")]
    [InlineData("(fictício)", "?")]
    public void Iniciais_so_usam_palavras_do_nome(string nome, string esperado)
        => ChatEquipeRegras.Iniciais(nome).Should().Be(esperado);

    [Theory]
    [InlineData("C:\\laudos\\laudo.pdf", "laudo.pdf")]
    [InlineData("/home/ana/laudo.pdf", "laudo.pdf")]
    [InlineData("  laudo.pdf  ", "laudo.pdf")]
    [InlineData("C:\\pasta\\", "")]
    public void Nome_do_arquivo_perde_a_pasta_em_qualquer_sistema(string nome, string esperado)
        => ChatEquipeRegras.NomeDoArquivo(nome).Should().Be(esperado);

    [Fact]
    public void Direta_e_a_mesma_chave_dos_dois_lados()
    {
        ChatEquipeRegras.Direta(7, 3).Should().Be("direta:3:7");
        ChatEquipeRegras.Direta(3, 7).Should().Be(ChatEquipeRegras.Direta(7, 3));
        ChatEquipeRegras.OutroDaDireta("direta:3:7", 3).Should().Be(7);
        ChatEquipeRegras.OutroDaDireta("direta:3:7", 7).Should().Be(3);
        ChatEquipeRegras.OutroDaDireta("direta:3:7", 5).Should().BeNull();
        ChatEquipeRegras.OutroDaDireta("direta:7:3", 3).Should().BeNull("chave fora de ordem não é chave válida");
        FluentActions.Invoking(() => ChatEquipeRegras.Direta(4, 4)).Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(PerfilAcesso.Recepcao, true, true, false)]
    [InlineData(PerfilAcesso.Enfermagem, true, true, true)]
    [InlineData(PerfilAcesso.Profissional, true, false, true)]
    [InlineData(PerfilAcesso.Financeiro, true, false, false)]
    [InlineData(PerfilAcesso.Gerente, true, true, true)]
    public void Cada_perfil_ve_os_canais_de_quem_trabalha_junto(PerfilAcesso perfil, bool geral, bool recepcao, bool enfermagem)
    {
        ChatEquipeRegras.ParticipaDoCanal(perfil, ChatEquipeRegras.CanalGeral).Should().Be(geral);
        ChatEquipeRegras.ParticipaDoCanal(perfil, ChatEquipeRegras.CanalRecepcao).Should().Be(recepcao);
        ChatEquipeRegras.ParticipaDoCanal(perfil, ChatEquipeRegras.CanalEnfermagem).Should().Be(enfermagem);
    }

    [Fact]
    public void Ninguem_le_a_direta_dos_outros_nem_usuario_inativo_le_nada()
    {
        var u = new UsuarioSistema { Id = 5, Perfil = PerfilAcesso.Gerente };
        ChatEquipeRegras.PodeVer(u, "direta:3:7").Should().BeFalse("nem a direção lê a direta alheia");
        ChatEquipeRegras.PodeVer(u, "direta:3:5").Should().BeTrue();
        u.Ativo = false;
        ChatEquipeRegras.PodeVer(u, ChatEquipeRegras.CanalGeral).Should().BeFalse();
    }

    [Fact]
    public void Citacao_casa_o_primeiro_nome_sem_acento_e_sem_caixa()
    {
        var pessoas = new[] { (1, "Ana Souza"), (2, "Ânia Lima"), (3, "Paulo Lima"), (4, "Ana Paula") };
        ChatEquipeRegras.Mencionados("@ana o Roberto pode subir", pessoas).Should().BeEquivalentTo([1, 4]);
        ChatEquipeRegras.Mencionados("falar com @ÂNIA, ok?", pessoas).Should().Equal(2);
        ChatEquipeRegras.Mencionados("email ana@clinica.com", pessoas).Should().BeEmpty("@ no meio de palavra não é citação");
        ChatEquipeRegras.Mencionados("sem citação", pessoas).Should().BeEmpty();
        ChatEquipeRegras.Mencionou(ChatEquipeRegras.GravarMencoes([3, 13]), 3).Should().BeTrue();
        ChatEquipeRegras.Mencionou("|13|", 3).Should().BeFalse("o id 3 não está dentro do 13");
    }

    [Fact]
    public void So_direta_citacao_e_pedido_tocam_e_silenciado_nao_toca()
    {
        var prefs = new PreferenciasSomChat();
        ChatEquipeRegras.Som("direta:1:2", 2, null, 1, prefs, Agora).Should().Be(SomChat.Direta);
        ChatEquipeRegras.Som(ChatEquipeRegras.CanalGeral, 2, null, 1, prefs, Agora).Should().Be(SomChat.Nenhum);
        ChatEquipeRegras.Som(ChatEquipeRegras.CanalGeral, 2, "|1|", 1, prefs, Agora).Should().Be(SomChat.Mencao);
        ChatEquipeRegras.Som("direta:1:2", 1, null, 1, prefs, Agora).Should().Be(SomChat.Nenhum, "mensagem minha não toca");
        ChatEquipeRegras.Som("direta:1:2", 2, null, 1, prefs with { Direta = false }, Agora).Should().Be(SomChat.Nenhum);
        var silenciado = prefs with { SilenciadoAte = Agora.AddMinutes(30) };
        ChatEquipeRegras.Som("direta:1:2", 2, "|1|", 1, silenciado, Agora).Should().Be(SomChat.Nenhum);
        ChatEquipeRegras.SomDoPedido(silenciado, Agora).Should().Be(SomChat.Nenhum);
        ChatEquipeRegras.SomDoPedido(prefs, Agora).Should().Be(SomChat.Pedido);
        ChatEquipeRegras.Som("direta:1:2", 2, null, 1, silenciado, Agora.AddHours(1)).Should().Be(SomChat.Direta, "o silêncio vence");
    }

    [Fact]
    public void Presenca_tem_validade_e_escrevendo_some_sozinho()
    {
        ChatEquipeRegras.Presenca(Agora.AddSeconds(-30), false, Agora).Should().Be(PresencaChat.Online);
        ChatEquipeRegras.Presenca(Agora.AddSeconds(-30), true, Agora).Should().Be(PresencaChat.EmAtendimento);
        ChatEquipeRegras.Presenca(Agora.AddMinutes(-5), true, Agora).Should().Be(PresencaChat.Fora);
        ChatEquipeRegras.Presenca(null, false, Agora).Should().Be(PresencaChat.Fora);
        var p = new PresencaEquipe { VistoEm = Agora, EscrevendoEm = "canal:geral", EscrevendoAte = Agora.AddSeconds(5) };
        ChatEquipeRegras.Escrevendo(p, "canal:geral", Agora).Should().BeTrue();
        ChatEquipeRegras.Escrevendo(p, "canal:recepcao", Agora).Should().BeFalse();
        ChatEquipeRegras.Escrevendo(p, "canal:geral", Agora.AddSeconds(10)).Should().BeFalse();
    }

    [Fact]
    public void Texto_longo_chega_recolhido_e_cortado_em_palavra()
    {
        var laudo = string.Join(' ', Enumerable.Repeat("acupuntura", 80));
        ChatEquipeRegras.Recolhida(laudo).Should().BeTrue();
        var recorte = ChatEquipeRegras.Recorte(laudo);
        recorte.Should().EndWith("…");
        recorte.Length.Should().BeLessThan(laudo.Length);
        recorte.TrimEnd('…').Split(' ').Should().OnlyContain(p => p == "acupuntura", "não corta no meio da palavra");
        ChatEquipeRegras.Recolhida("curto").Should().BeFalse();
        ChatEquipeRegras.Recorte("curto").Should().Be("curto");
        ChatEquipeRegras.Recolhida(string.Join('\n', Enumerable.Repeat("x", 9))).Should().BeTrue("muitas linhas também recolhe");
    }

    [Fact]
    public void Previa_e_uma_linha_e_anexo_sem_texto_diz_o_que_e()
    {
        ChatEquipeRegras.Previa("Bom dia!\nPode subir").Should().Be("Bom dia! Pode subir");
        ChatEquipeRegras.Previa("", "Laudo médico 2026/0189").Should().Be("[Laudo médico 2026/0189]");
        ChatEquipeRegras.Previa(new string('a', 30) + " " + new string('b', 100), limite: 50).Should().EndWith("…");
    }

    [Fact]
    public void Lida_por_diz_Lida_na_direta_e_conta_no_canal()
    {
        var leituras = new List<(int, int)> { (1, 10), (2, 10), (3, 5) };
        ChatEquipeRegras.LidaPor(10, ChatEquipeRegras.CanalGeral, 1, leituras).Should().Be("Lida por 1");
        ChatEquipeRegras.LidaPor(10, "direta:1:2", 1, leituras).Should().Be("Lida");
        ChatEquipeRegras.LidaPor(11, "direta:1:2", 1, leituras).Should().BeNull("ninguém leu ainda");
    }
    [Theory]
    [InlineData("laudo.pdf", true)]
    [InlineData("C:\\laudos\\Foto do Exame.JPG", true)]
    [InlineData("planilha.xlsx", true)]
    [InlineData("guias.xml", true)]
    [InlineData("instalar.exe", false)]
    [InlineData("atualizar.bat", false)]
    [InlineData("atalho.lnk", false)]
    [InlineData("script.js", false)]
    [InlineData("pasta.zip", false)]
    [InlineData("laudo.pdf.exe", false)]
    [InlineData("sem-extensao", false)]
    [InlineData(".pdf", false)]
    public void So_documento_planilha_imagem_e_texto_vao_pelo_chat(string nome, bool aceito)
        => ChatEquipeRegras.ArquivoPermitido(nome).Should().Be(aceito);

    [Fact]
    public void Previa_nao_diz_o_tipo_nem_o_numero_do_documento()
    {
        ChatEquipeRegras.AnexoNaPrevia("Relatório 12/2026", "Lúcia", null).Should().Be("documento clínico");
        ChatEquipeRegras.AnexoNaPrevia(null, "Lúcia", "x.pdf").Should().Be("Lúcia");
        ChatEquipeRegras.AnexoNaPrevia(null, null, "x.pdf").Should().Be("x.pdf");
    }

    private static MensagemNaConversa Msg(int id, int autor, string texto, int eu = 1)
        => new(id, autor, "Fulano", "FU", texto, Agora, null, autor == eu, false, null, null, null, null, null, null, null);

    [Fact]
    public void Releitura_mescla_sem_perder_as_anteriores_carregadas()
    {
        // Na tela: 3 e 4 vieram de "Ver mensagens anteriores", 10 e 11 da abertura.
        var aberta = new ConversaAberta("direta:1:2", "Paulo", "online", 2,
            [Msg(3, 2, "antiga"), Msg(4, 1, "minha antiga"), Msg(10, 2, "oi"), Msg(11, 1, "tudo bem?")],
            0, [], TemAnteriores: true, Instante: Agora);
        var novidade = new AtualizacaoDaConversa(
            Novas: [Msg(11, 1, "tudo bem?"), Msg(12, 2, "sim")], HaMaisNovas: false,
            Editadas: [Msg(3, 2, "antiga (corrigida)")],
            Leituras: [new LeituraNaConversa(2, 11), new LeituraNaConversa(1, 12)],
            Escrevendo: ["Paulo Lima"], Subtitulo: null, Instante: Agora.AddSeconds(4));

        var mesclada = ChatEquipeRegras.Mesclar(aberta, novidade, eu: 1);

        mesclada.Mensagens.Select(m => m.Id).Should().Equal(3, 4, 10, 11, 12);
        mesclada.Mensagens[0].Texto.Should().Be("antiga (corrigida)");
        mesclada.Mensagens.Single(m => m.Id == 11).LidaPor.Should().Be("Lida", "Paulo leu até a 11");
        mesclada.Mensagens.Single(m => m.Id == 4).LidaPor.Should().Be("Lida");
        mesclada.TemAnteriores.Should().BeTrue();
        mesclada.Subtitulo.Should().Be("online", "releitura sem presença não apaga o subtítulo");
        mesclada.Escrevendo.Should().Equal("Paulo Lima");
        mesclada.Instante.Should().Be(Agora.AddSeconds(4));
    }

    [Fact]
    public void Citacao_por_primeiro_nome_ou_por_nome_e_sobrenome()
    {
        var pessoas = new[] { (1, "Ana Souza"), (2, "Ana Paula Lima"), (3, "Dra. Helena Duarte (fictícia)"), (4, "Paulo Lima") };
        ChatEquipeRegras.Mencionados("@Ana pode ver?", pessoas).Should().BeEquivalentTo([1, 2], "o primeiro nome chama as duas Anas");
        ChatEquipeRegras.Mencionados("@Ana_Souza pode ver?", pessoas).Should().Equal(1);
        ChatEquipeRegras.Mencionados("@ana_lima e @Ana_Paula_Lima", pessoas).Should().Equal(2);
        ChatEquipeRegras.Mencionados("@helena, obrigada", pessoas).Should().Equal([3], "\"Dra.\" e \"(fictícia)\" não contam como nome");
        ChatEquipeRegras.Mencionados("mande para ana@clinica.com", pessoas).Should().BeEmpty("e-mail não é citação");
    }

    [Fact]
    public void Sugestao_de_citacao_escreve_o_nome_que_so_casa_com_a_pessoa_escolhida()
    {
        var nomes = new[] { "Ana Souza", "Ana Paula Lima", "Paulo Lima" };
        ChatEquipeRegras.MencaoDe("Paulo Lima", nomes).Should().Be("@Paulo");
        ChatEquipeRegras.MencaoDe("Ana Souza", nomes).Should().Be("@Ana_Souza");
        ChatEquipeRegras.MencaoDe("Ana Paula Lima", nomes).Should().Be("@Ana_Lima");
        // O que a sugestão escreve volta a citar só a escolhida.
        var pessoas = nomes.Select((n, i) => (i + 1, n)).ToList();
        ChatEquipeRegras.Mencionados(ChatEquipeRegras.MencaoDe("Ana Souza", nomes) + " oi", pessoas).Should().Equal(1);

        var sugestao = ChatEquipeRegras.SugestoesDeMencao("Bom dia @li", nomes, n => n);
        sugestao!.Value.Inicio.Should().Be(8);
        sugestao.Value.Pessoas.Should().Equal("Ana Paula Lima", "Paulo Lima");
        ChatEquipeRegras.SugestoesDeMencao("Bom dia @", nomes, n => n)!.Value.Pessoas.Should().HaveCount(3);
        ChatEquipeRegras.SugestoesDeMencao("ana@cli", nomes, n => n).Should().BeNull("e-mail não abre sugestão");
        ChatEquipeRegras.SugestoesDeMencao("@xyz", nomes, n => n).Should().BeNull();
        ChatEquipeRegras.SugestoesDeMencao("@Ana já viu", nomes, n => n).Should().BeNull("a sugestão é só para a palavra que está sendo escrita");
    }

    [Fact]
    public void Busca_procura_palavras_sem_acento_e_mostra_o_trecho()
    {
        ChatEquipeRegras.PalavrasDaBusca("  Laudo da LÚCIA, 10h! ").Should().Equal("laudo", "da", "lucia", "10h");
        ChatEquipeRegras.PalavrasDaBusca("a ! ?").Should().BeEmpty("letra solta não é busca");
        ChatEquipeRegras.ConsultaDeTexto(["laudo", "lucia"]).Should().Be("laudo:* & lucia:*");
        ChatEquipeRegras.TextoDeBusca("Lúcia ÁGUA").Should().Be("lucia agua");

        var texto = string.Join(' ', Enumerable.Repeat("bla", 80)) + " o laudo da Lúcia ficou pronto " + string.Join(' ', Enumerable.Repeat("fim", 80));
        var trecho = ChatEquipeRegras.Trecho(texto, ["lucia"]);
        trecho.Should().Contain("Lúcia").And.StartWith("…").And.EndWith("…");
        trecho.Length.Should().BeLessThanOrEqualTo(145);
        ChatEquipeRegras.Trecho("curto", ["x"]).Should().Be("curto");
    }

    [Fact]
    public void Perfis_do_canal_sao_os_mesmos_de_quem_participa()
    {
        foreach (var canal in ChatEquipeRegras.Canais)
            ChatEquipeRegras.PerfisDoCanal(canal).Should().BeEquivalentTo(
                Enum.GetValues<PerfilAcesso>().Where(p => ChatEquipeRegras.ParticipaDoCanal(p, canal)));
    }

    [Fact]
    public void Mensagem_longa_e_recolhida_pelo_comeco_e_pelo_tamanho()
    {
        ChatEquipeRegras.Recolhida("curta", 5).Should().BeFalse();
        ChatEquipeRegras.Recolhida(new string('x', 600), 3000).Should().BeTrue();
        ChatEquipeRegras.Recolhida(string.Join('\n', Enumerable.Repeat("l", 9)), 17).Should().BeTrue("muitas linhas");
    }
}

/// <summary>O serviço do chat contra o banco: envio, leitura, não lidas, edição e acesso.</summary>
public sealed class ChatEquipeServiceTests : IDisposable
{
    private readonly SqliteConnection conexao = new("Data Source=:memory:");
    private readonly ClinicaDbContext db;
    private DateTime relogio = new(2026, 10, 21, 9, 0, 0);
    private readonly ChatEquipeService chat;
    private readonly UsuarioSistema ana = new() { Nome = "Ana Souza", Login = "ana", Perfil = PerfilAcesso.Recepcao, CriadoEm = new(2026, 1, 1) };
    private readonly UsuarioSistema paulo = new() { Nome = "Paulo Lima", Login = "paulo", Perfil = PerfilAcesso.Profissional, CriadoEm = new(2026, 1, 1) };
    private readonly UsuarioSistema joana = new() { Nome = "Joana Reis", Login = "joana", Perfil = PerfilAcesso.Enfermagem, CriadoEm = new(2026, 1, 1) };

    public ChatEquipeServiceTests()
    {
        conexao.Open();
        db = new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(conexao).Options);
        db.Database.EnsureCreated();
        db.Usuarios.AddRange(ana, paulo, joana);
        db.SaveChanges();
        chat = new ChatEquipeService(db, () => relogio);
    }

    public void Dispose() { db.Dispose(); conexao.Dispose(); }

    private static readonly PreferenciasSomChat Prefs = new();

    [Fact]
    public async Task Direta_chega_como_nao_lida_toca_e_some_ao_ler()
    {
        var conversa = ChatEquipeRegras.Direta(ana.Id, paulo.Id);
        await chat.EnviarAsync(ana.Id, ChatEquipeRegras.CanalGeral, new("bom dia")); // a bolinha já viu esta
        var antes = await chat.PulsoAsync(ana.Id, 0, Prefs);
        antes.Novidades.Should().BeEmpty("a primeira batida só aprende onde a conversa está");
        var id = await chat.EnviarAsync(paulo.Id, conversa, new("O João das 10h30 pode adiantar?"));

        var painel = await chat.PainelAsync(ana.Id);
        painel.TotalNaoLidas.Should().Be(1);
        painel.Diretas.First().PessoaId.Should().Be(paulo.Id, "quem tem o que ler sobe na lista");
        painel.Diretas.First().UltimaPrevia.Should().Be("Paulo: O João das 10h30 pode adiantar?");

        var pulso = await chat.PulsoAsync(ana.Id, antes.UltimaMensagemId, Prefs);
        pulso.Novidades.Should().ContainSingle().Which.Som.Should().Be(SomChat.Direta);
        pulso.UltimaMensagemId.Should().Be(id);
        (await chat.PulsoAsync(paulo.Id, antes.UltimaMensagemId, Prefs)).Novidades.Should().BeEmpty("a minha mensagem não é novidade para mim");
        (await chat.PulsoAsync(joana.Id, antes.UltimaMensagemId, Prefs)).Novidades.Should().BeEmpty("a direta dos outros não chega a mim");

        (await chat.AbrirAsync(ana.Id, conversa, antesDe: id)).Should().NotBeNull();
        (await chat.PainelAsync(ana.Id)).TotalNaoLidas.Should().Be(1, "a página de mensagens antigas não marca nada como lido");
        var aberta = await chat.AbrirAsync(ana.Id, conversa);
        aberta.UltimaLidaAntes.Should().Be(0, "é o ponto do separador \"Novas mensagens\"");
        (await chat.PainelAsync(ana.Id)).TotalNaoLidas.Should().Be(0, "abrir é ler: a marcação vai na mesma chamada");

        (await chat.AbrirAsync(paulo.Id, conversa)).Mensagens.Single().LidaPor.Should().Be("Lida");
    }

    [Fact]
    public async Task Quem_esta_fora_da_conversa_nao_le_nem_escreve()
    {
        var conversa = ChatEquipeRegras.Direta(ana.Id, paulo.Id);
        await chat.EnviarAsync(ana.Id, conversa, new("só nós dois"));
        await FluentActions.Awaiting(() => chat.AbrirAsync(joana.Id, conversa)).Should().ThrowAsync<UnauthorizedAccessException>();
        await FluentActions.Awaiting(() => chat.EnviarAsync(paulo.Id, ChatEquipeRegras.CanalRecepcao, new("oi")))
            .Should().ThrowAsync<UnauthorizedAccessException>("quem atende não está no # Recepção");
        (await chat.PainelAsync(joana.Id)).Diretas.Should().OnlyContain(d => d.NaoLidas == 0);
    }

    [Fact]
    public async Task Citacao_toca_so_para_quem_pode_ler_o_canal()
    {
        await chat.EnviarAsync(ana.Id, ChatEquipeRegras.CanalGeral, new("bom dia"));
        var id = await chat.EnviarAsync(joana.Id, ChatEquipeRegras.CanalRecepcao, new("@Ana pode encaixar? @Paulo também"));
        var msg = await db.MensagensEquipe.AsNoTracking().SingleAsync(m => m.Id == id);
        ChatEquipeRegras.Mencionou(msg.Mencoes, ana.Id).Should().BeTrue();
        ChatEquipeRegras.Mencionou(msg.Mencoes, paulo.Id).Should().BeFalse("Paulo não lê o # Recepção");
        (await chat.PulsoAsync(ana.Id, id - 1, Prefs)).Novidades.Single().Som.Should().Be(SomChat.Mencao);
    }

    [Fact]
    public async Task Editar_guarda_a_versao_anterior_e_so_o_autor_edita()
    {
        var id = await chat.EnviarAsync(ana.Id, ChatEquipeRegras.CanalGeral, new("Reunião às 14h"));
        relogio = relogio.AddMinutes(2);
        await chat.EditarAsync(ana.Id, id, "Reunião às 15h");
        var m = await db.MensagensEquipe.AsNoTracking().Include(x => x.Versoes).SingleAsync(x => x.Id == id);
        m.Texto.Should().Be("Reunião às 15h");
        m.EditadaEm.Should().Be(relogio);
        m.Versoes.Single().Texto.Should().Be("Reunião às 14h");
        await FluentActions.Awaiting(() => chat.EditarAsync(paulo.Id, id, "x")).Should().ThrowAsync<UnauthorizedAccessException>();
        await FluentActions.Awaiting(() => chat.EditarAsync(ana.Id, id, "  ")).Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Laudo_inteiro_e_arquivo_vao_e_voltam_sem_corte()
    {
        var laudo = string.Join("\n", Enumerable.Repeat("Paciente em acompanhamento por lombalgia crônica.", 60));
        var conversa = ChatEquipeRegras.Direta(ana.Id, paulo.Id);
        var id = await chat.EnviarAsync(paulo.Id, conversa, new(laudo, ArquivoNome: "C:\\laudos\\laudo.pdf", ArquivoConteudo: [1, 2, 3]));
        // A conversa traz o começo; o laudo inteiro vem quando pedido ("Ver inteiro", "Copiar").
        var naConversa = (await chat.AbrirAsync(ana.Id, conversa)).Mensagens.Single();
        naConversa.Texto.Should().Be(laudo[..ChatEquipeRegras.TextoNaConversa]);
        naConversa.TextoCompleto.Should().BeFalse();
        naConversa.Tamanho.Should().Be(laudo.Length);
        ChatEquipeRegras.Recolhida(naConversa.Texto, naConversa.Tamanho).Should().Be(ChatEquipeRegras.Recolhida(laudo));
        (await chat.TextoAsync(ana.Id, id)).Should().Be(laudo);
        await FluentActions.Awaiting(() => chat.TextoAsync(joana.Id, id)).Should().ThrowAsync<UnauthorizedAccessException>();
        var arq = await chat.ArquivoAsync(ana.Id, id);
        arq!.Value.Nome.Should().Be("laudo.pdf");
        arq.Value.Conteudo.Should().Equal(1, 2, 3);
        await FluentActions.Awaiting(() => chat.ArquivoAsync(joana.Id, id)).Should().ThrowAsync<UnauthorizedAccessException>();
        var grande = new byte[ChatEquipeRegras.TamanhoMaximoArquivo + 1];
        await FluentActions.Awaiting(() => chat.EnviarAsync(paulo.Id, conversa, new("", ArquivoNome: "x.bin", ArquivoConteudo: grande)))
            .Should().ThrowAsync<InvalidOperationException>();
        await FluentActions.Awaiting(() => chat.EnviarAsync(paulo.Id, conversa, new("   ")))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Quem_chegou_depois_nao_herda_o_historico_como_nao_lido()
    {
        await chat.EnviarAsync(ana.Id, ChatEquipeRegras.CanalGeral, new("mensagem antiga"));
        var nova = new UsuarioSistema { Nome = "Carla Nova", Login = "carla", Perfil = PerfilAcesso.Recepcao, CriadoEm = relogio.AddDays(1) };
        db.Usuarios.Add(nova);
        await db.SaveChangesAsync();
        (await chat.PainelAsync(nova.Id)).TotalNaoLidas.Should().Be(0);
        relogio = relogio.AddDays(2);
        await chat.EnviarAsync(ana.Id, ChatEquipeRegras.CanalGeral, new("bem-vinda"));
        (await chat.PainelAsync(nova.Id)).TotalNaoLidas.Should().Be(1);
    }

    [Fact]
    public async Task Escrevendo_aparece_para_os_outros_e_some_ao_enviar()
    {
        await chat.SinalizarAsync(joana.Id, ChatEquipeRegras.CanalRecepcao);
        (await chat.AbrirAsync(ana.Id, ChatEquipeRegras.CanalRecepcao)).Escrevendo.Should().Equal("Joana Reis");
        (await chat.AbrirAsync(joana.Id, ChatEquipeRegras.CanalRecepcao)).Escrevendo.Should().BeEmpty("não mostra a mim mesma");
        await chat.EnviarAsync(joana.Id, ChatEquipeRegras.CanalRecepcao, new("pronto"));
        (await chat.AbrirAsync(ana.Id, ChatEquipeRegras.CanalRecepcao)).Escrevendo.Should().BeEmpty();
        var online = await chat.PainelAsync(ana.Id);
        online.Diretas.Single(d => d.PessoaId == joana.Id).Presenca.Should().Be(PresencaChat.Online);
        relogio = relogio.AddMinutes(5);
        (await chat.PainelAsync(ana.Id)).Diretas.Single(d => d.PessoaId == joana.Id).Presenca.Should().Be(PresencaChat.Fora);
    }

    [Fact]
    public async Task Paciente_e_documento_anexados_levam_o_nome_copiado()
    {
        var paciente = new Paciente { Nome = "Lúcia Fictícia" };
        db.Pacientes.Add(paciente);
        await db.SaveChangesAsync();
        var conversa = ChatEquipeRegras.Direta(ana.Id, paulo.Id);
        await chat.EnviarAsync(paulo.Id, conversa, new("", PacienteId: paciente.Id));
        var m = (await chat.AbrirAsync(ana.Id, conversa)).Mensagens.Single();
        m.PacienteNome.Should().Be("Lúcia Fictícia");
        (await chat.PainelAsync(ana.Id)).Diretas.First().UltimaPrevia.Should().Be("Paulo: [Lúcia Fictícia]");
        await FluentActions.Awaiting(() => chat.EnviarAsync(paulo.Id, conversa, new("", PacienteId: 999)))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Releitura_traz_so_o_que_chegou_e_o_que_foi_corrigido()
    {
        var conversa = ChatEquipeRegras.Direta(ana.Id, paulo.Id);
        var primeira = await chat.EnviarAsync(paulo.Id, conversa, new("Laudo longo da Lúcia"));
        var aberta = await chat.AbrirAsync(ana.Id, conversa);
        aberta.Instante.Should().Be(relogio);

        relogio = relogio.AddSeconds(4);
        (await chat.AtualizarAsync(ana.Id, conversa, primeira, aberta.Instante)).Novas.Should().BeEmpty("nada mudou: o laudo não volta");

        relogio = relogio.AddSeconds(4);
        await chat.EditarAsync(paulo.Id, primeira, "Laudo longo da Lúcia (corrigido)");
        var nova = await chat.EnviarAsync(paulo.Id, conversa, new("viu?"));
        var r = await chat.AtualizarAsync(ana.Id, conversa, primeira, aberta.Instante);
        r.Novas.Select(m => m.Id).Should().Equal(nova);
        r.Editadas.Single().Texto.Should().Be("Laudo longo da Lúcia (corrigido)");
        r.Subtitulo.Should().Be("online");
        r.Leituras.Should().Contain(new LeituraNaConversa(ana.Id, primeira), "abrir marcou até a primeira");
        (await chat.AtualizarAsync(ana.Id, conversa, nova, r.Instante)).Leituras
            .Should().Contain(new LeituraNaConversa(ana.Id, nova), "a releitura marcou a nova como lida");

        await chat.EnviarAsync(paulo.Id, conversa, new("e agora?"));
        (await chat.AtualizarAsync(ana.Id, conversa, primeira, r.Instante, limite: 1)).HaMaisNovas
            .Should().BeTrue("passou do limite: a tela relê a conversa inteira");
        (await chat.AtualizarAsync(ana.Id, conversa, int.MaxValue, default)).Editadas.Should().ContainSingle("sem hora anterior, traz toda correção");
        await FluentActions.Awaiting(() => chat.AtualizarAsync(joana.Id, conversa, 0, relogio))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Correcao_feita_com_o_relogio_do_outro_posto_atrasado_ainda_chega()
    {
        var conversa = ChatEquipeRegras.Direta(ana.Id, paulo.Id);
        var id = await chat.EnviarAsync(paulo.Id, conversa, new("Reunião às 14h"));
        var aberta = await chat.AbrirAsync(ana.Id, conversa);
        relogio = relogio.AddMinutes(-3); // o posto do Paulo está 3 minutos atrás
        await chat.EditarAsync(paulo.Id, id, "Reunião às 15h");
        (await chat.AtualizarAsync(ana.Id, conversa, id, aberta.Instante)).Editadas.Single().Texto.Should().Be("Reunião às 15h");
    }

    [Fact]
    public async Task Programa_e_compactado_nao_vao_pelo_chat()
    {
        var conversa = ChatEquipeRegras.Direta(ana.Id, paulo.Id);
        await FluentActions.Awaiting(() => chat.EnviarAsync(paulo.Id, conversa, new("", ArquivoNome: "laudo.pdf.exe", ArquivoConteudo: [1])))
            .Should().ThrowAsync<InvalidOperationException>();
        await FluentActions.Awaiting(() => chat.EnviarAsync(paulo.Id, conversa, new("", ArquivoNome: "exames.zip", ArquivoConteudo: [1])))
            .Should().ThrowAsync<InvalidOperationException>();
        (await db.MensagensEquipe.CountAsync()).Should().Be(0);
    }

    private async Task<(Paciente Lucia, Paciente Outro, DocumentoClinico Relatorio)> RelatorioDaLuciaAsync()
    {
        var lucia = new Paciente { Nome = "Lúcia Fictícia" };
        var outro = new Paciente { Nome = "Outro Fictício" };
        db.Pacientes.AddRange(lucia, outro);
        await db.SaveChangesAsync();
        var relatorio = new DocumentoClinico
        {
            Tipo = TipoDocumentoClinico.RelatorioEvolucao, Numero = "12/2026", PacienteId = lucia.Id, CodigoVerificacao = "ABC",
        };
        db.DocumentosClinicos.Add(relatorio);
        await db.SaveChangesAsync();
        return (lucia, outro, relatorio);
    }

    [Fact]
    public async Task So_anexa_documento_quem_pode_ve_lo_e_so_do_paciente_anexado()
    {
        var (lucia, outro, relatorio) = await RelatorioDaLuciaAsync();
        CentralDocumentosService.PodeVer(ana.Efetivas, relatorio.Tipo).Should().BeFalse("premissa: a recepção não lê o relatório de evolução");
        CentralDocumentosService.PodeVer(paulo.Efetivas, relatorio.Tipo).Should().BeTrue();
        var conversa = ChatEquipeRegras.Direta(ana.Id, paulo.Id);

        await FluentActions.Awaiting(() => chat.EnviarAsync(ana.Id, conversa, new("", PacienteId: lucia.Id, DocumentoClinicoId: relatorio.Id)))
            .Should().ThrowAsync<UnauthorizedAccessException>("a tela não oferece; o serviço também não aceita");
        await FluentActions.Awaiting(() => chat.EnviarAsync(paulo.Id, conversa, new("", PacienteId: outro.Id, DocumentoClinicoId: relatorio.Id)))
            .Should().ThrowAsync<InvalidOperationException>("o documento é da Lúcia");
        await FluentActions.Awaiting(() => chat.EnviarAsync(paulo.Id, conversa, new("", DocumentoClinicoId: relatorio.Id)))
            .Should().ThrowAsync<InvalidOperationException>("documento vai com o paciente dele");

        relatorio.CanceladoEm = relogio;
        await db.SaveChangesAsync();
        await FluentActions.Awaiting(() => chat.EnviarAsync(paulo.Id, conversa, new("", PacienteId: lucia.Id, DocumentoClinicoId: relatorio.Id)))
            .Should().ThrowAsync<InvalidOperationException>("documento cancelado não vai");
        (await db.MensagensEquipe.CountAsync()).Should().Be(0);

        var semFicha = new UsuarioSistema
        {
            Nome = "Sem Ficha", Login = "semficha", Perfil = PerfilAcesso.Recepcao,
            PermissoesNegadas = Permissao.VerFichaPaciente, CriadoEm = new(2026, 1, 1),
        };
        db.Usuarios.Add(semFicha);
        await db.SaveChangesAsync();
        await FluentActions.Awaiting(() => chat.EnviarAsync(semFicha.Id, ChatEquipeRegras.CanalGeral, new("", PacienteId: lucia.Id)))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Documento_chega_sem_tipo_nem_numero_para_quem_o_acesso_nao_abre()
    {
        var (lucia, _, relatorio) = await RelatorioDaLuciaAsync();
        var conversa = ChatEquipeRegras.Direta(ana.Id, paulo.Id);
        await chat.EnviarAsync(paulo.Id, conversa, new("", PacienteId: lucia.Id, DocumentoClinicoId: relatorio.Id));

        var deQuemMandou = (await chat.AbrirAsync(paulo.Id, conversa)).Mensagens.Single();
        deQuemMandou.DocumentoId.Should().Be(relatorio.Id);
        deQuemMandou.DocumentoRotulo.Should().Contain("12/2026");

        var daRecepcao = (await chat.AbrirAsync(ana.Id, conversa)).Mensagens.Single();
        daRecepcao.DocumentoId.Should().BeNull("o acesso da recepção não abre o relatório");
        daRecepcao.DocumentoRotulo.Should().Be(ChatEquipeRegras.DocumentoSemAcesso);
        daRecepcao.PacienteNome.Should().Be("Lúcia Fictícia");
        (await chat.AtualizarAsync(ana.Id, conversa, 0, relogio)).Novas.Single().DocumentoId.Should().BeNull();
        (await chat.PainelAsync(ana.Id)).Diretas.First().UltimaPrevia.Should().Be("Paulo: [documento clínico]");
    }

    [Fact]
    public async Task Busca_acha_sem_acento_so_no_que_a_pessoa_ve()
    {
        var direta = ChatEquipeRegras.Direta(ana.Id, paulo.Id);
        var noGeral = await chat.EnviarAsync(paulo.Id, ChatEquipeRegras.CanalGeral, new("O laudo da Lúcia ficou pronto"));
        var naDireta = await chat.EnviarAsync(paulo.Id, direta, new("Laudo LUCIA: segunda via na recepção"));
        await chat.EnviarAsync(paulo.Id, ChatEquipeRegras.CanalGeral, new("Reunião às 15h"));

        var daAna = await chat.BuscarAsync(ana.Id, "lucia laudo");
        daAna.Select(r => r.MensagemId).Should().Equal(naDireta, noGeral);
        daAna[0].Titulo.Should().Be("Paulo Lima", "a direta aparece com o nome da outra pessoa");
        daAna[1].Titulo.Should().Be("# Geral");
        daAna[1].Trecho.Should().Be("O laudo da Lúcia ficou pronto");

        (await chat.BuscarAsync(joana.Id, "lucia")).Select(r => r.MensagemId).Should().Equal([noGeral], "a direta dos outros não aparece na busca");
        (await chat.BuscarAsync(ana.Id, "lucia", conversa: ChatEquipeRegras.CanalGeral)).Should().ContainSingle();
        (await chat.BuscarAsync(ana.Id, "x")).Should().BeEmpty();
        await FluentActions.Awaiting(() => chat.BuscarAsync(joana.Id, "lucia", conversa: direta)).Should().ThrowAsync<UnauthorizedAccessException>();

        relogio = relogio.AddMinutes(1);
        await chat.EditarAsync(paulo.Id, noGeral, "O exame da Marta ficou pronto");
        (await chat.BuscarAsync(ana.Id, "lucia")).Select(r => r.MensagemId).Should().Equal([naDireta], "a busca acompanha a correção");
        (await chat.BuscarAsync(ana.Id, "marta")).Should().ContainSingle();
    }

    [Fact]
    public async Task Direta_com_quem_saiu_da_equipe_fica_so_para_leitura()
    {
        var direta = ChatEquipeRegras.Direta(ana.Id, paulo.Id);
        await chat.EnviarAsync(paulo.Id, direta, new("Até amanhã!"));
        var paraPaulo = await db.Usuarios.SingleAsync(u => u.Id == paulo.Id);
        paraPaulo.Ativo = false;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var painel = await chat.PainelAsync(ana.Id);
        var linha = painel.Diretas.Single(d => d.PessoaId == paulo.Id);
        linha.PessoaAtiva.Should().BeFalse();
        linha.Subtitulo.Should().Be("saiu da equipe");
        linha.NaoLidas.Should().Be(1, "o que ele mandou antes de sair continua para ler");
        painel.Diretas.Last().PessoaId.Should().Be(paulo.Id, "quem saiu vai para o fim da lista");

        var aberta = await chat.AbrirAsync(ana.Id, direta);
        aberta.SoLeitura.Should().BeTrue();
        aberta.Subtitulo.Should().Contain("só leitura");
        aberta.Mensagens.Should().ContainSingle();
        aberta.Pessoas.Should().BeEmpty("não se cita quem saiu");
        await FluentActions.Awaiting(() => chat.EnviarAsync(ana.Id, direta, new("oi")))
            .Should().ThrowAsync<InvalidOperationException>();
        (await chat.BuscarAsync(ana.Id, "amanha")).Should().ContainSingle("o histórico continua na busca");
    }

    [Fact]
    public async Task Nao_lidas_sao_contadas_inteiras_sem_teto()
    {
        // 3.500 mensagens: a contagem antiga trazia no máximo 3.000 linhas e errava calada.
        const int quantas = 3500;
        var momento = relogio;
        for (var i = 0; i < quantas; i++)
            db.MensagensEquipe.Add(new MensagemEquipe
            {
                Conversa = ChatEquipeRegras.CanalGeral, AutorId = paulo.Id, AutorNome = paulo.Nome,
                Texto = $"aviso {i}", TextoBusca = $"aviso {i}", EnviadaEm = momento,
            });
        await db.SaveChangesAsync();
        var ultima = await db.MensagensEquipe.MaxAsync(m => m.Id);
        db.ConversasEquipe.Add(new ConversaEquipe
        {
            Conversa = ChatEquipeRegras.CanalGeral, UltimaMensagemId = ultima, UltimaEm = momento,
            UltimoAutorId = paulo.Id, UltimoAutorNome = paulo.Nome, UltimaPrevia = "aviso",
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        (await chat.PainelAsync(ana.Id, completo: false)).TotalNaoLidas.Should().Be(quantas);
        var pulso = await chat.PulsoAsync(ana.Id, ultima - 100, new());
        pulso.Novidades.Should().HaveCount(10, "a prévia mostra as últimas, não as cem");
        pulso.UltimaMensagemId.Should().Be(ultima);
    }

    [Fact]
    public async Task Primeira_mensagem_cria_a_conversa_e_as_seguintes_atualizam()
    {
        var direta = ChatEquipeRegras.Direta(ana.Id, joana.Id);
        await chat.EnviarAsync(ana.Id, direta, new("primeira"));
        var segunda = await chat.EnviarAsync(joana.Id, direta, new("segunda"));
        var resumo = await db.ConversasEquipe.AsNoTracking().SingleAsync(c => c.Conversa == direta);
        resumo.UltimaMensagemId.Should().Be(segunda);
        resumo.UltimoAutorNome.Should().Be("Joana Reis");
        (resumo.ParticipanteA, resumo.ParticipanteB).Should().Be((Math.Min(ana.Id, joana.Id), Math.Max(ana.Id, joana.Id)));

        relogio = relogio.AddMinutes(1);
        await chat.EditarAsync(joana.Id, segunda, "segunda, corrigida");
        (await db.ConversasEquipe.AsNoTracking().SingleAsync(c => c.Conversa == direta)).UltimaPrevia.Should().Be("segunda, corrigida");
        (await db.PresencasEquipe.AsNoTracking().SingleAsync(p => p.UsuarioId == joana.Id)).EscrevendoEm.Should().BeNull();
    }
}
