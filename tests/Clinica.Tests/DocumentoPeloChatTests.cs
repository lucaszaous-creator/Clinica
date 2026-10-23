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
/// O documento pedido ao @SemDor no chat dos apps (decisão da direção de 23/10/2026): a frase da
/// fase 4 emite na hora, pela mesma emissão do Consultório, e a resposta traz o documento anexado
/// (a 2ª via em PDF). Só quem prescreve, só paciente da própria agenda, nada com dúvida — e, no
/// canal, o documento vai em particular.
/// </summary>
public sealed class DocumentoPeloChatTests : IDisposable
{
    private readonly SqliteConnection conexao = new("Data Source=:memory:");
    private readonly ClinicaDbContext db;
    private readonly DateTime relogio = new(2026, 10, 23, 9, 0, 0);
    private readonly Profissional helena = new() { Nome = "Dra. Helena Duarte", Ativo = true };
    private readonly Paciente maria = new() { Nome = "Maria José Fictícia" };
    private UsuarioSistema medica = null!;
    private UsuarioSistema rayssa = null!;

    public DocumentoPeloChatTests()
    {
        conexao.Open();
        db = new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(conexao).Options);
        db.Database.EnsureCreated();
        db.Profissionais.Add(helena);
        db.Pacientes.Add(maria);
        db.SaveChanges();
        medica = new() { Nome = "Helena Duarte", Login = "helena", Perfil = PerfilAcesso.Profissional, ProfissionalId = helena.Id, CriadoEm = new(2026, 1, 1) };
        rayssa = new() { Nome = "Rayssa Lima", Login = "rayssa", Perfil = PerfilAcesso.Recepcao, CriadoEm = new(2026, 1, 1) };
        db.Usuarios.AddRange(medica, rayssa);
        db.Agendamentos.Add(new Agendamento { PacienteId = maria.Id, DataHora = new DateTime(2026, 10, 23, 10, 0, 0), ProfissionalId = helena.Id });
        db.SaveChanges();
        db.ChangeTracker.Clear();
    }

    public void Dispose() { db.Dispose(); conexao.Dispose(); }

    private ChatEquipeService Chat() => new(db, () => relogio);

    private async Task<MensagemEquipe> Ultima(string conversa)
        => await db.MensagensEquipe.AsNoTracking().Where(m => m.Conversa == conversa).OrderBy(m => m.Id).LastAsync();

    [Fact]
    public async Task O_atestado_nasce_e_volta_anexado_a_conversa()
    {
        var sistema = ChatEquipeRegras.Sistema(medica.Id);
        await Chat().EnviarAsync(medica.Id, sistema, new("atestado Maria José 2 dias"));

        var doc = await db.DocumentosClinicos.AsNoTracking().SingleAsync();
        (doc.Tipo, doc.PacienteId, doc.ProfissionalId, doc.DiasAfastamento).Should().Be((TipoDocumentoClinico.Atestado, maria.Id, helena.Id, 2));
        doc.Corpo.Should().Contain("2 dias de afastamento");
        doc.Numero.Should().NotBeNullOrWhiteSpace();

        var resposta = await Ultima(sistema);
        resposta.DoSistema.Should().BeTrue();
        (resposta.DocumentoClinicoId, resposta.PacienteId).Should().Be((doc.Id, maria.Id));
        resposta.DocumentoRotulo.Should().Contain(doc.Numero);
        resposta.Texto.Should().Contain("emitido para Maria José Fictícia").And.Contain("sem assinatura digital");
        (await db.Auditoria.AsNoTracking().CountAsync()).Should().BeGreaterThan(0, "a emissão grava a trilha no mesmo SaveChanges");
    }

    [Theory]
    [InlineData("prescrição Maria José: dipirona 500 mg de 6/6 h", TipoDocumentoClinico.Receita)]
    [InlineData("comparecimento Maria José", TipoDocumentoClinico.Comparecimento)]
    [InlineData("exame Maria José: hemograma completo", TipoDocumentoClinico.PedidoExame)]
    [InlineData("laudo Maria José: evolução do tratamento", TipoDocumentoClinico.RelatorioEvolucao)]
    public async Task Cada_tipo_sai_como_o_do_Consultorio(string frase, TipoDocumentoClinico tipo)
    {
        await Chat().EnviarAsync(medica.Id, ChatEquipeRegras.Sistema(medica.Id), new(frase));
        (await db.DocumentosClinicos.AsNoTracking().SingleAsync()).Tipo.Should().Be(tipo);
    }

    [Theory]
    [InlineData("emitir documento para a Maria")]
    [InlineData("atestado Maria José")]
    [InlineData("receita Maria José")]
    [InlineData("atestado Joana Inexistente 2 dias")]
    public async Task Com_duvida_pergunta_e_nao_emite(string frase)
    {
        await Chat().EnviarAsync(medica.Id, ChatEquipeRegras.Sistema(medica.Id), new(frase));
        (await db.DocumentosClinicos.CountAsync()).Should().Be(0);
        (await Ultima(ChatEquipeRegras.Sistema(medica.Id))).DocumentoClinicoId.Should().BeNull();
    }

    // Por CPF (pedido da direção, 23/10): com ou sem pontuação, sozinho ou com o nome.
    private const string CpfDaMaria = "529.982.247-25";

    [Theory]
    [InlineData("atestado 529.982.247-25 2 dias", "", 2)]
    [InlineData("atestado Maria cpf 52998224725 3 dias", "maria", 3)]
    [InlineData("comparecimento 52998224725", "", null)]
    public void O_cpf_e_lido_e_nao_vira_dias_nem_nome(string frase, string nome, int? dias)
    {
        var d = DocumentoPeloWhatsAppRegras.Ler(frase)!;
        (d.Cpf, d.Nome, d.Dias).Should().Be(("52998224725", nome, dias));
        DocumentoPeloWhatsAppRegras.OQueFalta(d).Should().BeNull();
    }

    [Fact]
    public void Cpf_que_nao_confere_e_recusado()
        => DocumentoPeloWhatsAppRegras.OQueFalta(DocumentoPeloWhatsAppRegras.Ler("atestado 123.456.789-00 2 dias")!)
            .Should().Contain("não confere");

    private async Task DuasMarias()
    {
        var mariaClara = new Paciente { Nome = "Maria Clara Outra" };
        db.Pacientes.Add(mariaClara);
        await db.SaveChangesAsync();
        await db.Pacientes.Where(p => p.Id == maria.Id).ExecuteUpdateAsync(x => x.SetProperty(p => p.Documento, CpfDaMaria));
        db.Agendamentos.Add(new Agendamento { PacienteId = mariaClara.Id, DataHora = new DateTime(2026, 10, 22, 15, 0, 0), ProfissionalId = helena.Id, Status = StatusAgendamento.Realizado });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }

    [Fact]
    public async Task Com_duas_Marias_o_nome_curto_pergunta_e_o_cpf_resolve()
    {
        await DuasMarias();
        var sistema = ChatEquipeRegras.Sistema(medica.Id);
        await Chat().EnviarAsync(medica.Id, sistema, new("atestado Maria 2 dias"));
        (await Ultima(sistema)).Texto.Should().Contain("Achei 2 pacientes").And.Contain("CPF");
        (await db.DocumentosClinicos.CountAsync()).Should().Be(0);

        await Chat().EnviarAsync(medica.Id, sistema, new("atestado " + CpfDaMaria + " 2 dias"));
        (await db.DocumentosClinicos.AsNoTracking().SingleAsync()).PacienteId.Should().Be(maria.Id);
    }

    [Fact]
    public async Task Nome_e_cpf_de_pacientes_diferentes_nao_emite()
    {
        await DuasMarias();
        await Chat().EnviarAsync(medica.Id, ChatEquipeRegras.Sistema(medica.Id), new("atestado Maria Clara " + CpfDaMaria + " 2 dias"));
        (await db.DocumentosClinicos.CountAsync()).Should().Be(0);
        (await Ultima(ChatEquipeRegras.Sistema(medica.Id))).Texto.Should().Contain("Não achei esse CPF");
    }

    [Fact]
    public async Task Quem_nao_prescreve_nao_emite()
    {
        await Chat().EnviarAsync(rayssa.Id, ChatEquipeRegras.Sistema(rayssa.Id), new("atestado Maria José 2 dias"));
        (await db.DocumentosClinicos.CountAsync()).Should().Be(0);
        (await Ultima(ChatEquipeRegras.Sistema(rayssa.Id))).Texto.Should().Be(DocumentoPeloChatRegras.SoQuemPrescreve);
    }

    [Fact]
    public async Task No_canal_o_documento_vai_em_particular()
    {
        await Chat().EnviarAsync(medica.Id, ChatEquipeRegras.CanalGeral, new("@SemDor atestado Maria José 1 dia"));

        var noCanal = await Ultima(ChatEquipeRegras.CanalGeral);
        noCanal.Texto.Should().Be(DocumentoPeloChatRegras.EmParticular(medica.Nome));
        noCanal.DocumentoClinicoId.Should().BeNull();
        noCanal.Texto.Should().NotContain("Maria");
        var particular = await Ultima(ChatEquipeRegras.Sistema(medica.Id));
        particular.DocumentoClinicoId.Should().Be((await db.DocumentosClinicos.AsNoTracking().SingleAsync()).Id);
    }
}
