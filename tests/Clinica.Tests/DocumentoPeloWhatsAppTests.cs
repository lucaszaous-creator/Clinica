using Clinica.Application.Servicos;
using Clinica.Application.Tablet;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O documento pedido pelo WhatsApp (@SemDor fase 4, out/2026): a frase, o que falta e o texto
/// proposto — puros.
/// </summary>
public class DocumentoPeloWhatsAppRegrasTests
{
    [Theory]
    [InlineData("atestado Maria Silva 2 dias", "atestado", "maria silva", 2)]
    [InlineData("Por favor, emite um atestado para a Maria 3 dias", "atestado", "maria", 3)]
    [InlineData("faz um atestado de dois dias pra Maria", "atestado", "maria", 2)]
    [InlineData("Atestado de comparecimento da Maria", "comparecimento", "maria", null)]
    [InlineData("comparecimento Maria Silva", "comparecimento", "maria silva", null)]
    [InlineData("Relatório médico Maria: evolução boa", "relatorio", "maria", null)]
    [InlineData("pedido de exame Maria: hemograma", "exame", "maria", null)]
    public void Le_o_tipo_o_nome_e_os_dias(string texto, string tipo, string nome, int? dias)
    {
        var d = DocumentoPeloWhatsAppRegras.Ler(texto)!;
        (d.Tipo, d.Nome, d.Dias).Should().Be((tipo, nome, dias));
    }

    [Fact]
    public void O_conteudo_depois_dos_dois_pontos_fica_como_foi_escrito()
        => DocumentoPeloWhatsAppRegras.Ler("receita Maria: Dipirona 500 mg, 6/6 h")!.Texto.Should().Be("Dipirona 500 mg, 6/6 h");

    [Theory]
    [InlineData("quem pediu atestado?")]
    [InlineData("minha agenda de hoje")]
    [InlineData("remarcar Maria 10h")]
    public void Pergunta_e_outro_pedido_nao_viram_documento(string texto)
        => DocumentoPeloWhatsAppRegras.Ler(texto).Should().BeNull();

    [Fact]
    public void Diz_o_que_falta_antes_de_criar_o_rascunho()
    {
        DocumentoPeloWhatsAppRegras.OQueFalta(DocumentoPeloWhatsAppRegras.Ler("atestado Maria")!).Should().Contain("Quantos dias");
        DocumentoPeloWhatsAppRegras.OQueFalta(DocumentoPeloWhatsAppRegras.Ler("atestado Maria 90 dias")!).Should().Contain("de 1 a 60");
        DocumentoPeloWhatsAppRegras.OQueFalta(DocumentoPeloWhatsAppRegras.Ler("receita Maria")!).Should().Contain("não completa");
        DocumentoPeloWhatsAppRegras.OQueFalta(DocumentoPeloWhatsAppRegras.Ler("atestado 2 dias")!).Should().Contain("Diga de quem");
        DocumentoPeloWhatsAppRegras.OQueFalta(DocumentoPeloWhatsAppRegras.Ler("comparecimento Maria")!).Should().BeNull();
    }

    [Fact]
    public void O_atestado_proposto_leva_os_dias_e_o_link_leva_o_token_depois_do_cerquilha()
    {
        DocumentoPeloWhatsAppRegras.TextoProposto(DocumentoPeloWhatsAppRegras.Ler("atestado Maria 1 dia")!).Should().Contain("1 dia de afastamento");
        var token = DocumentoPeloWhatsAppRegras.NovoToken();
        token.Should().HaveLength(64);
        DocumentoPeloWhatsAppRegras.Link("https://portal.exemplo/", token).Should().Be("https://portal.exemplo/profissional/assinar.html#" + token);
    }
}

/// <summary>A ponte: a frase do médico vira RASCUNHO com link de uso único — nunca documento.</summary>
public sealed class DocumentoPeloWhatsAppServiceTests : IDisposable
{
    private readonly SqliteConnection conexao = new("Data Source=:memory:");
    private readonly ClinicaDbContext db;
    private readonly DateTime relogio = new(2026, 10, 22, 7, 0, 0);
    private readonly OpcoesDocumentoPeloWhatsApp ligado = new(true, "https://portal.exemplo");
    private readonly Profissional helena = new() { Nome = "Dra. Helena Duarte", Ativo = true };
    private readonly Paciente maria = new() { Nome = "Maria José Fictícia" };
    private readonly Paciente mariana = new() { Nome = "Maria Clara Outra" };
    private UsuarioSistema medica = null!;
    private const string Celular = "5522999422253";

    public DocumentoPeloWhatsAppServiceTests()
    {
        conexao.Open();
        db = new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(conexao).Options);
        db.Database.EnsureCreated();
        db.Profissionais.Add(helena);
        db.Pacientes.AddRange(maria, mariana);
        db.SaveChanges();
        medica = new()
        {
            Nome = "Helena Duarte", Login = "helena", Perfil = PerfilAcesso.Profissional, ProfissionalId = helena.Id, CriadoEm = new(2026, 1, 1),
            TelefoneWhatsApp = Celular, WhatsAppLigadoEm = new(2026, 10, 1),
        };
        db.Usuarios.Add(medica);
        db.Agendamentos.AddRange(
            new Agendamento { PacienteId = maria.Id, DataHora = new DateTime(2026, 10, 22, 10, 0, 0), ProfissionalId = helena.Id },
            new Agendamento { PacienteId = mariana.Id, DataHora = new DateTime(2026, 10, 10, 9, 0, 0), ProfissionalId = helena.Id, Status = StatusAgendamento.Realizado });
        db.SaveChanges();
        db.ChangeTracker.Clear();
    }

    public void Dispose() { db.Dispose(); conexao.Dispose(); }

    private AssistenteWhatsAppService Servico(OpcoesDocumentoPeloWhatsApp? opcoes = null) => new(db, () => relogio, opcoes ?? ligado);

    [Fact]
    public async Task Desligado_a_frase_nao_vira_rascunho()
    {
        await Servico(OpcoesDocumentoPeloWhatsApp.Desligado).ResponderAsync(Celular, "atestado Maria José 2 dias", "wamid.d0");
        (await db.PedidosDocumentoWhatsApp.CountAsync()).Should().Be(0);
        new OpcoesDocumentoPeloWhatsApp(true, "http://sem-https").Ligado.Should().BeFalse("o link só sai para o portal em https");
    }

    [Fact]
    public async Task Atestado_vira_rascunho_com_link_e_o_token_so_vai_ao_celular()
    {
        var r = await Servico().ResponderAsync(Celular, "atestado Maria José 2 dias", "wamid.d1");
        r.Texto.Should().Contain("*Atestado em rascunho* para Maria").And.Contain("https://portal.exemplo/profissional/assinar.html#");
        var token = r.Texto![(r.Texto.IndexOf('#') + 1)..].Split('\n')[0];

        var p = await db.PedidosDocumentoWhatsApp.AsNoTracking().SingleAsync();
        (p.PacienteId, p.Tipo, p.DiasAfastamento, p.UsuarioId).Should().Be((maria.Id, "atestado", 2, medica.Id));
        p.Texto.Should().Contain("2 dias de afastamento");
        p.TokenHash.Should().Be(ContratoTablet.Hash(token)).And.NotBe(token);
        p.ExpiraEm.Should().Be(relogio.AddMinutes(15));
        (await db.DocumentosClinicos.CountAsync()).Should().Be(0, "o documento só nasce no portal");

        var noChat = await db.MensagensEquipe.AsNoTracking().OrderBy(m => m.Id).Select(m => m.Texto).ToListAsync();
        noChat.Should().HaveCount(2);
        noChat[1].Should().NotContain(token).And.Contain("só para o seu celular");
    }

    [Fact]
    public async Task A_mesma_mensagem_repetida_nao_gera_outro_link()
    {
        await Servico().ResponderAsync(Celular, "comparecimento Maria José", "wamid.d2");
        var r = await Servico().ResponderAsync(Celular, "comparecimento Maria José", "wamid.d2");
        r.Texto.Should().Contain("já gerou um link");
        (await db.PedidosDocumentoWhatsApp.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Nome_de_duas_pacientes_pede_mais_do_nome()
    {
        var r = await Servico().ResponderAsync(Celular, "comparecimento Maria", "wamid.d3");
        r.Texto.Should().Contain("Achei 2 pacientes").And.Contain("Maria Clara Outra");
        (await db.PedidosDocumentoWhatsApp.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Quem_nao_prescreve_nao_pede_documento()
    {
        db.Usuarios.Add(new UsuarioSistema { Nome = "Rayssa", Login = "rayssa", Perfil = PerfilAcesso.Recepcao, CriadoEm = new(2026, 1, 1),
            TelefoneWhatsApp = "5522988887777", WhatsAppLigadoEm = new(2026, 10, 1) });
        await db.SaveChangesAsync();
        var r = await Servico().ResponderAsync("5522988887777", "atestado Maria José 2 dias", "wamid.d4");
        r.Texto.Should().Contain("é para quem prescreve");
        (await db.PedidosDocumentoWhatsApp.CountAsync()).Should().Be(0);
    }
}
