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
        var antes = await chat.PainelAsync(ana.Id, 0, Prefs);
        var id = await chat.EnviarAsync(paulo.Id, conversa, new("O João das 10h30 pode adiantar?"));

        var painel = await chat.PainelAsync(ana.Id, antes.UltimaMensagemId, Prefs);
        painel.TotalNaoLidas.Should().Be(1);
        painel.Diretas.First().PessoaId.Should().Be(paulo.Id, "quem tem o que ler sobe na lista");
        painel.Diretas.First().UltimaPrevia.Should().Be("Paulo: O João das 10h30 pode adiantar?");

        var comNovidade = await chat.PainelAsync(ana.Id, id - 1, Prefs);
        comNovidade.Novidades.Should().ContainSingle().Which.Som.Should().Be(SomChat.Direta);

        var aberta = await chat.AbrirAsync(ana.Id, conversa);
        aberta.UltimaLidaAntes.Should().Be(0, "é o ponto do separador \"Novas mensagens\"");
        await chat.MarcarLidaAsync(ana.Id, conversa, id);
        (await chat.PainelAsync(ana.Id, id, Prefs)).TotalNaoLidas.Should().Be(0);

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
        (await chat.PainelAsync(joana.Id, 0, Prefs)).Diretas.Should().OnlyContain(d => d.NaoLidas == 0);
    }

    [Fact]
    public async Task Citacao_toca_so_para_quem_pode_ler_o_canal()
    {
        await chat.EnviarAsync(ana.Id, ChatEquipeRegras.CanalGeral, new("bom dia"));
        var id = await chat.EnviarAsync(joana.Id, ChatEquipeRegras.CanalRecepcao, new("@Ana pode encaixar? @Paulo também"));
        var msg = await db.MensagensEquipe.AsNoTracking().SingleAsync(m => m.Id == id);
        ChatEquipeRegras.Mencionou(msg.Mencoes, ana.Id).Should().BeTrue();
        ChatEquipeRegras.Mencionou(msg.Mencoes, paulo.Id).Should().BeFalse("Paulo não lê o # Recepção");
        (await chat.PainelAsync(ana.Id, id - 1, Prefs)).Novidades.Single().Som.Should().Be(SomChat.Mencao);
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
        (await chat.AbrirAsync(ana.Id, conversa)).Mensagens.Single().Texto.Should().Be(laudo);
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
        (await chat.PainelAsync(nova.Id, 0, Prefs)).TotalNaoLidas.Should().Be(0);
        relogio = relogio.AddDays(2);
        await chat.EnviarAsync(ana.Id, ChatEquipeRegras.CanalGeral, new("bem-vinda"));
        (await chat.PainelAsync(nova.Id, 0, Prefs)).TotalNaoLidas.Should().Be(1);
    }

    [Fact]
    public async Task Escrevendo_aparece_para_os_outros_e_some_ao_enviar()
    {
        await chat.SinalizarAsync(joana.Id, ChatEquipeRegras.CanalRecepcao);
        (await chat.AbrirAsync(ana.Id, ChatEquipeRegras.CanalRecepcao)).Escrevendo.Should().Equal("Joana Reis");
        (await chat.AbrirAsync(joana.Id, ChatEquipeRegras.CanalRecepcao)).Escrevendo.Should().BeEmpty("não mostra a mim mesma");
        await chat.EnviarAsync(joana.Id, ChatEquipeRegras.CanalRecepcao, new("pronto"));
        (await chat.AbrirAsync(ana.Id, ChatEquipeRegras.CanalRecepcao)).Escrevendo.Should().BeEmpty();
        var online = await chat.PainelAsync(ana.Id, 0, Prefs);
        online.Diretas.Single(d => d.PessoaId == joana.Id).Presenca.Should().Be(PresencaChat.Online);
        relogio = relogio.AddMinutes(5);
        (await chat.PainelAsync(ana.Id, 0, Prefs)).Diretas.Single(d => d.PessoaId == joana.Id).Presenca.Should().Be(PresencaChat.Fora);
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
        (await chat.PainelAsync(ana.Id, 0, Prefs)).Diretas.First().UltimaPrevia.Should().Be("Paulo: [Lúcia Fictícia]");
        await FluentActions.Awaiting(() => chat.EnviarAsync(paulo.Id, conversa, new("", PacienteId: 999)))
            .Should().ThrowAsync<InvalidOperationException>();
    }
}
