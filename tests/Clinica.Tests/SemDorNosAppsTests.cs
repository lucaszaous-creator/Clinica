using Clinica.Application.Servicos;
using Clinica.Crm.Bridge;
using Clinica.Crm.Contracts;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O @SemDor com IA nos apps (out/2026 — Clinica#307): a pergunta que as regras do chat não entendem
/// vai à fila só quando o CRM passou há pouco; a releitura do CRM é respondida pela clínica, com as
/// permissões de quem perguntou; nada fica "relendo" para sempre; e os avisos "Atenção" do CRM
/// substituem a lista do sino da Recepção.
/// </summary>
public sealed class SemDorNosAppsTests : IDisposable
{
    private readonly SqliteConnection conexao = new("Data Source=:memory:");
    private readonly ClinicaDbContext db;
    private DateTime relogio = new(2026, 10, 23, 9, 0, 0);
    private readonly UsuarioSistema rayssa = new() { Nome = "Rayssa Lima", Login = "rayssa", Perfil = PerfilAcesso.Recepcao, CriadoEm = new(2026, 1, 1) };
    private const string NaoEntende = "qual a senha do wifi";

    public SemDorNosAppsTests()
    {
        conexao.Open();
        db = new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(conexao).Options);
        db.Database.EnsureCreated();
        var helena = new Profissional { Nome = "Dra. Helena Duarte", Ativo = true };
        var maria = new Paciente { Nome = "Maria José Fictícia" };
        db.Profissionais.Add(helena);
        db.Pacientes.Add(maria);
        db.Usuarios.Add(rayssa);
        db.SaveChanges();
        db.Agendamentos.Add(new Agendamento { PacienteId = maria.Id, DataHora = new DateTime(2026, 10, 23, 10, 0, 0), ProfissionalId = helena.Id });
        db.SaveChanges();
        db.ChangeTracker.Clear();
    }

    public void Dispose() { db.Dispose(); conexao.Dispose(); }

    private ChatEquipeService Chat() => new(db, () => relogio);
    private PerguntasAoSemDorService Fila() => new(db, () => relogio);
    private string Sistema => ChatEquipeRegras.Sistema(rayssa.Id);

    private async Task<List<MensagemEquipe>> Conversa(string conversa)
        => await db.MensagensEquipe.AsNoTracking().Where(m => m.Conversa == conversa).OrderBy(m => m.Id).ToListAsync();

    /// <summary>O CRM leu a fila agora (é assim que o pulso nasce).</summary>
    private async Task CrmPassou() => await Fila().PendentesAsync();

    [Fact]
    public void O_pulso_vale_por_dois_minutos_e_a_releitura_e_limpa()
    {
        SemDorNosAppsRegras.IaDisponivel(null, relogio).Should().BeFalse();
        SemDorNosAppsRegras.IaDisponivel(relogio.AddSeconds(-90), relogio).Should().BeTrue();
        SemDorNosAppsRegras.IaDisponivel(relogio.AddMinutes(-3), relogio).Should().BeFalse();
        SemDorNosAppsRegras.Reescrita("  \"agenda de amanhã\"\n").Should().Be("agenda de amanhã");
        SemDorNosAppsRegras.Reescrita("   ").Should().BeNull();
    }

    [Fact]
    public async Task Sem_o_CRM_a_pergunta_recebe_a_ajuda_na_hora_e_nada_entra_na_fila()
    {
        await Chat().EnviarAsync(rayssa.Id, Sistema, new(NaoEntende));

        var msgs = await Conversa(Sistema);
        msgs.Should().HaveCount(2);
        msgs[1].Texto.Should().NotBe(SemDorNosAppsRegras.Relendo);
        msgs[1].Texto.Should().Contain(SistemaEquipeRegras.TituloNaoEntendi);
        (await db.PerguntasAoSemDor.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Com_o_CRM_passando_a_pergunta_entra_na_fila_e_a_resposta_diz_que_esta_relendo()
    {
        await CrmPassou();
        await Chat().EnviarAsync(rayssa.Id, Sistema, new(NaoEntende));

        (await Conversa(Sistema)).Last().Texto.Should().Be(SemDorNosAppsRegras.Relendo);
        var p = await db.PerguntasAoSemDor.AsNoTracking().SingleAsync();
        (p.UsuarioId, p.Conversa, p.Texto, p.Situacao).Should().Be((rayssa.Id, Sistema, NaoEntende, SemDorNosAppsRegras.Aguardando));
        (await Fila().PendentesAsync()).Should().ContainSingle(x => x.Id == p.Id && x.Texto == NaoEntende);
    }

    [Fact]
    public async Task Pergunta_entendida_pelas_regras_nunca_vai_a_fila()
    {
        await CrmPassou();
        await Chat().EnviarAsync(rayssa.Id, Sistema, new("agenda de hoje"));
        await Chat().EnviarAsync(rayssa.Id, Sistema, new("atestado Maria José 2 dias"));
        (await db.PerguntasAoSemDor.CountAsync()).Should().Be(0, "documento e pergunta conhecida não vão à IA");
    }

    [Fact]
    public async Task A_releitura_e_respondida_pela_clinica_uma_vez_so()
    {
        await CrmPassou();
        await Chat().EnviarAsync(rayssa.Id, Sistema, new(NaoEntende));
        var id = (await db.PerguntasAoSemDor.AsNoTracking().SingleAsync()).Id;

        (await Fila().ResponderAsync(id, "agenda de hoje")).Should().BeTrue();
        (await Fila().ResponderAsync(id, "agenda de hoje")).Should().BeFalse("a mesma releitura não responde duas vezes");

        var ultima = (await Conversa(Sistema)).Last();
        ultima.Texto.Should().StartWith("Entendi como \"agenda de hoje\".");
        ultima.DoSistema.Should().BeTrue();
        ultima.Texto.Should().Contain("Maria", "a resposta vem da agenda da clínica");
        var p = await db.PerguntasAoSemDor.AsNoTracking().SingleAsync();
        (p.Situacao, p.Reescrita).Should().Be((SemDorNosAppsRegras.Respondida, "agenda de hoje"));
        (await Fila().PendentesAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Quando_a_IA_nao_acha_pergunta_conhecida_vai_a_ajuda()
    {
        await CrmPassou();
        await Chat().EnviarAsync(rayssa.Id, Sistema, new(NaoEntende));
        var id = (await db.PerguntasAoSemDor.AsNoTracking().SingleAsync()).Id;

        await Fila().ResponderAsync(id, null);

        (await Conversa(Sistema)).Last().Texto.Should().Contain(SistemaEquipeRegras.TituloNaoEntendi);
        (await db.PerguntasAoSemDor.AsNoTracking().SingleAsync()).Situacao.Should().Be(SemDorNosAppsRegras.SemResposta);
    }

    [Fact]
    public async Task Pergunta_que_espera_demais_recebe_a_ajuda_e_sai_da_fila()
    {
        await CrmPassou();
        await Chat().EnviarAsync(rayssa.Id, Sistema, new(NaoEntende));
        relogio = relogio.Add(SemDorNosAppsRegras.Validade).AddSeconds(1);

        (await Fila().PendentesAsync()).Should().BeEmpty();

        (await Conversa(Sistema)).Last().Texto.Should().Contain(SistemaEquipeRegras.TituloNaoEntendi);
        (await db.PerguntasAoSemDor.AsNoTracking().SingleAsync()).Situacao.Should().Be(SemDorNosAppsRegras.SemResposta);
        (await Fila().ResponderAsync((await db.PerguntasAoSemDor.AsNoTracking().SingleAsync()).Id, "agenda de hoje")).Should().BeFalse();
    }

    [Fact]
    public async Task O_pulso_nao_e_regravado_a_cada_leitura()
    {
        await CrmPassou();
        var primeiro = (await db.SemDorNoCrm.AsNoTracking().SingleAsync()).PerguntasLidasEm;
        relogio = relogio.AddSeconds(5);
        await CrmPassou();
        (await db.SemDorNoCrm.AsNoTracking().SingleAsync()).PerguntasLidasEm.Should().Be(primeiro);
        relogio = relogio.Add(SemDorNosAppsRegras.PulsoGravadoACada);
        await CrmPassou();
        (await db.SemDorNoCrm.AsNoTracking().SingleAsync()).PerguntasLidasEm.Should().Be(relogio);
    }

    private ClinicSemDorBridge Ponte() => new(db, Fila());

    private static SemDorAlert Aviso(string chave, string nome = "Maria José Fictícia", string link = "https://crm.exemplo/#inbox/c1")
        => new(chave, nome, "Sintoma grave", DateTimeOffset.UtcNow.AddMinutes(-3), link);

    [Fact]
    public async Task A_lista_de_avisos_substitui_a_anterior_e_so_guarda_o_primeiro_nome()
    {
        var agora = DateTimeOffset.UtcNow;
        (await Ponte().Alerts(new(agora.AddMinutes(-1), [Aviso("semdor:aaa"), Aviso("semdor:bbb")]), default)).Status.Should().Be("recorded");
        (await Ponte().Alerts(new(agora, [Aviso("semdor:bbb")]), default)).Status.Should().Be("recorded");

        var avisos = await db.AvisosDoSemDor.AsNoTracking().ToListAsync();
        avisos.Should().ContainSingle().Which.Chave.Should().Be("semdor:bbb");
        avisos[0].PrimeiroNome.Should().Be("Maria");
        (await Ponte().Alerts(new(agora.AddMinutes(-2), [Aviso("semdor:aaa")]), default)).Status.Should().Be("stale",
            "a lista mais velha não traz de volta o aviso resolvido");
        (await db.AvisosDoSemDor.AsNoTracking().Select(a => a.Chave).ToListAsync()).Should().Equal("semdor:bbb");
    }

    [Fact]
    public async Task O_sino_so_mostra_a_lista_atual_e_sem_lista_nenhuma_a_secao_nem_aparece()
    {
        var leitura = new AvisosDoSemDorService(db);
        var (avisos, atual, ligado) = await leitura.ListarAsync(relogio);
        (avisos.Count, atual, ligado).Should().Be((0, false, false));

        // A ponte grava na hora da clínica; o app compara com o relógio do posto, que está em
        // Brasília. O CI roda em UTC: a hora de "agora" do teste é a da clínica, não a da máquina.
        var agora = DateTimeOffset.UtcNow;
        var naClinica = TimeZoneInfo.ConvertTime(agora, TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")).DateTime;
        await Ponte().Alerts(new(agora, [Aviso("semdor:aaa")]), default);
        (avisos, atual, ligado) = await leitura.ListarAsync(naClinica);
        (avisos.Count, atual, ligado).Should().Be((1, true, true));

        (avisos, atual, ligado) = await leitura.ListarAsync(naClinica.Add(AvisosDoSemDorRegras.ListaValePor).AddMinutes(1));
        (avisos.Count, atual, ligado).Should().Be((0, false, true), "lista velha não diz 'sem resposta' de quem talvez já foi respondido");
    }

    [Fact]
    public void Regras_do_aviso_no_sino()
    {
        AvisosDoSemDorRegras.Quando(new DateTime(2026, 10, 23, 9, 42, 0), relogio).Should().Be("hoje, 09:42");
        AvisosDoSemDorRegras.Quando(new DateTime(2026, 10, 22, 18, 10, 0), relogio).Should().Be("22/10, 18:10");
        AvisosDoSemDorRegras.LinkSeguro("https://crm.exemplo/#inbox/c1").Should().BeTrue();
        AvisosDoSemDorRegras.LinkSeguro("http://crm.exemplo/").Should().BeFalse();
        AvisosDoSemDorRegras.LinkSeguro("https://a:b@crm.exemplo/").Should().BeFalse();
        AvisosDoSemDorRegras.Frase("Maria", "Sintoma grave").Should().Contain("Maria").And.Contain("sintoma grave");
    }

    [Theory]
    [InlineData("semdor:ok", "http://crm.exemplo/#inbox/c1")]
    [InlineData("outra:chave", "https://crm.exemplo/#inbox/c1")]
    public async Task Aviso_fora_do_formato_e_recusado(string chave, string link)
        => await Assert.ThrowsAsync<InvalidOperationException>(() => Ponte().Alerts(new(DateTimeOffset.UtcNow, [Aviso(chave, link: link)]), default));
}
