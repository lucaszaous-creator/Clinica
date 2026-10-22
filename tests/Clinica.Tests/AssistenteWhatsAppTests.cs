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
/// A equipe pergunta ao sistema pelo WhatsApp da clínica (out/2026): o número, a frase do pedido
/// e o texto da resposta — puros.
/// </summary>
public class AssistenteWhatsAppRegrasTests
{
    private static readonly DateOnly Hoje = new(2026, 10, 22); // quinta-feira

    [Theory]
    [InlineData("(22) 99942-2253", "5522999422253")]
    [InlineData("+55 22 99942-2253", "5522999422253")]
    [InlineData("552299422253", "5522999422253")] // a Meta sem o nono dígito
    [InlineData("022999422253", "5522999422253")]
    [InlineData("22 2500-0002", null)]            // fixo
    [InlineData("552225000002", null)]
    [InlineData("", null)]
    public void O_numero_tem_uma_chave_so(string entrada, string? chave)
        => TelefoneDaEquipe.Chave(entrada).Should().Be(chave);

    [Theory]
    [InlineData("Remarcar Maria Silva 24/10 10h", "remarcar", "maria silva")]
    [InlineData("por favor, pede pra recepção cancelar o João de amanhã", "cancelar", "joao")]
    [InlineData("encaixa a Ana hoje à tarde", "encaixar", "ana")]
    [InlineData("Avisar Maria que vou atrasar 20 minutos", "avisar", "maria")]
    public void Le_o_pedido_escrito(string texto, string tipo, string nome)
    {
        var p = AssistenteWhatsAppRegras.LerPedido(texto, Hoje);
        p.Should().NotBeNull();
        p!.Tipo.Should().Be(tipo);
        p.Nome.Should().Be(nome);
        p.Texto.Should().Be(texto.Trim());
    }

    [Fact]
    public void Le_o_dia_e_a_hora_do_pedido()
    {
        var p = AssistenteWhatsAppRegras.LerPedido("remarcar maria 24/10 10:30", Hoje)!;
        p.Dia.Should().Be(new DateOnly(2026, 10, 24));
        p.Hora.Should().Be(new TimeOnly(10, 30));
        AssistenteWhatsAppRegras.LerPedido("cancelar joão amanhã 9h", Hoje)!.Dia.Should().Be(Hoje.AddDays(1));
        AssistenteWhatsAppRegras.LerPedido("cancelar joão", Hoje)!.Dia.Should().BeNull("sem dia, procura nos próximos 30");
    }

    [Theory]
    [InlineData("Quem pediu para remarcar")]
    [InlineData("minha agenda de hoje")]
    [InlineData("quem não confirmou amanhã?")]
    [InlineData("pedidos")]
    public void Pergunta_nao_vira_pedido(string texto)
        => AssistenteWhatsAppRegras.LerPedido(texto, Hoje).Should().BeNull("perguntar sobre a agenda não pode criar um pedido que ninguém fez");

    [Fact]
    public void Recado_livre_para_a_recepcao()
    {
        var p = AssistenteWhatsAppRegras.LerPedido("Recepção: ligar para o laboratório", Hoje)!;
        p.Tipo.Should().Be("outro");
        p.Texto.Should().Be("ligar para o laboratório");
    }

    [Fact]
    public void O_nome_bate_pelo_comeco_das_palavras()
    {
        AssistenteWhatsAppRegras.NomeBate("maria sil", "Maria José da Silva").Should().BeTrue();
        AssistenteWhatsAppRegras.NomeBate("jose", "Maria José da Silva").Should().BeTrue("sem acento acha com acento");
        AssistenteWhatsAppRegras.NomeBate("ana", "Mariana Lopes").Should().BeFalse();
    }

    [Fact]
    public void A_agenda_vira_texto_de_whatsapp_e_lista_cortada_diz_quantas_faltam()
    {
        var linhas = Enumerable.Range(0, 200).Select(i => new LinhaDoCartao("10:00", $"Paciente com nome comprido {i}", "Acupuntura", "Confirmou")).ToList();
        var cartao = new CartaoChat("agenda", "Sua agenda", "Hoje · qui, 22/10", [new("horários", 200)], linhas, ["Agenda de amanhã"]);
        var t = AssistenteWhatsAppRegras.Texto("", cartao);
        t.Should().StartWith("*Sua agenda*\nHoje · qui, 22/10");
        t.Should().Contain("10:00 Paciente com nome comprido 0 — Acupuntura · _Confirmou_");
        t.Should().Contain("…e mais").And.Contain("\"Agenda de amanhã\"");
        t.Length.Should().BeLessThanOrEqualTo(AssistenteWhatsAppRegras.TamanhoMaximo);
    }
}

/// <summary>
/// O ciclo pelo banco: o médico pergunta pelo celular, a resposta vem em nome do login dele e fica
/// na conversa com o sistema; o pedido escrito nasce com o horário; o desfecho entra na fila do
/// WhatsApp e sai dela quando o CRM trata.
/// </summary>
public sealed class AssistenteWhatsAppServiceTests : IDisposable
{
    private readonly SqliteConnection conexao = new("Data Source=:memory:");
    private readonly ClinicaDbContext db;
    private DateTime relogio = new(2026, 10, 22, 7, 0, 0);
    private readonly AssistenteWhatsAppService servico;
    private readonly Profissional helena = new() { Nome = "Dra. Helena Duarte", Ativo = true };
    private readonly UsuarioSistema rayssa = new() { Nome = "Rayssa Lima", Login = "rayssa", Perfil = PerfilAcesso.Recepcao, CriadoEm = new(2026, 1, 1) };
    private UsuarioSistema medica = null!;
    private readonly Paciente maria = new() { Nome = "Maria José Fictícia" };
    private Agendamento dez = null!;

    public AssistenteWhatsAppServiceTests()
    {
        conexao.Open();
        db = new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(conexao).Options);
        db.Database.EnsureCreated();
        db.Profissionais.Add(helena);
        db.Pacientes.Add(maria);
        db.SaveChanges();
        medica = new()
        {
            Nome = "Helena Duarte", Login = "helena", Perfil = PerfilAcesso.Profissional, ProfissionalId = helena.Id, CriadoEm = new(2026, 1, 1),
            TelefoneWhatsApp = "5522999422253", WhatsAppLigadoEm = new(2026, 10, 1),
        };
        db.Usuarios.AddRange(rayssa, medica);
        dez = new Agendamento { PacienteId = maria.Id, DataHora = new DateTime(2026, 10, 22, 10, 0, 0), ProfissionalId = helena.Id };
        db.Agendamentos.Add(dez);
        db.SaveChanges();
        db.ChangeTracker.Clear();
        servico = new AssistenteWhatsAppService(db, () => relogio);
    }

    public void Dispose() { db.Dispose(); conexao.Dispose(); }

    [Fact]
    public async Task Numero_que_nao_e_da_equipe_segue_o_fluxo_do_paciente()
    {
        (await servico.ResponderAsync("5522988887777", "oi", "wamid.1")).DaEquipe.Should().BeFalse();
        (await servico.NumerosAsync()).Should().Equal("5522999422253");
    }

    [Fact]
    public async Task Medica_pergunta_a_agenda_pelo_celular_e_a_conversa_fica_no_chat()
    {
        // A Meta pode mandar o número sem o nono dígito.
        var r = await servico.ResponderAsync("552299422253", "Qual a minha agenda de hoje?", "wamid.2");
        r.DaEquipe.Should().BeTrue();
        r.Texto.Should().StartWith("*Sua agenda*").And.Contain("10:00 Maria José Fictícia");

        var chat = new ChatEquipeService(db, () => relogio);
        var aberta = await chat.AbrirAsync(medica.Id, ChatEquipeRegras.Sistema(medica.Id));
        aberta.Mensagens.Should().HaveCount(2);
        aberta.Mensagens[0].Texto.Should().Be("Qual a minha agenda de hoje?");
        aberta.Mensagens[1].Cartao.Should().NotBeNull("no app a resposta continua sendo o cartão");
        (await chat.PainelAsync(medica.Id)).Sistema!.NaoLidas.Should().Be(0, "ela leu no celular");
    }

    [Fact]
    public async Task Pedido_escrito_acha_o_horario_e_chega_a_recepcao_uma_vez_so()
    {
        var r = await servico.ResponderAsync("5522999422253", "remarcar Maria 10h", "wamid.3");
        r.Texto.Should().Contain("Pedido enviado à recepção").And.Contain("Maria José Fictícia");
        await servico.ResponderAsync("5522999422253", "remarcar Maria 10h", "wamid.3"); // a Meta repetiu

        var lista = await new PedidosRecepcaoService(db, () => relogio).ListarAsync(rayssa.Id);
        lista.Should().ContainSingle().Which.AgendamentoId.Should().Be(dez.Id);
        lista[0].Detalhe.Should().Be("remarcar Maria 10h");
    }

    [Fact]
    public async Task Pedido_sem_horario_achado_nao_cria_nada_e_diz_onde_procurou()
    {
        var r = await servico.ResponderAsync("5522999422253", "cancelar Joaquim", "wamid.4");
        r.Texto.Should().Contain("Não achei horário marcado de \"joaquim\" na sua agenda");
        (await new PedidosRecepcaoService(db, () => relogio).ListarAsync(rayssa.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task O_desfecho_entra_na_fila_do_whatsapp_e_sai_quando_o_crm_trata()
    {
        await servico.ResponderAsync("5522999422253", "remarcar Maria 10h", "wamid.5");
        var pedidos = new PedidosRecepcaoService(db, () => relogio);
        var id = (await pedidos.ListarAsync(rayssa.Id)).Single().Id;
        (await servico.AvisosPendentesAsync()).Should().BeEmpty("ainda não foi encerrado");

        relogio = relogio.AddMinutes(30);
        await pedidos.EncerrarAsync(rayssa.Id, id, true, "Remarcada para ter, 27/10 às 09:00.");
        var aviso = (await servico.AvisosPendentesAsync()).Should().ContainSingle().Subject;
        aviso.Telefone.Should().Be("5522999422253");
        aviso.Texto.Should().Contain("Rayssa Lima concluiu o seu pedido").And.Contain("27/10");

        await servico.ConfirmarAvisoAsync(id, false, "fora da janela de 24 h");
        (await servico.AvisosPendentesAsync()).Should().BeEmpty();
        (await db.EventosPedidoRecepcao.AsNoTracking().Where(e => e.PedidoRecepcaoId == id && e.Acao == "aviso-whatsapp").SingleAsync())
            .Observacao.Should().Contain("NÃO foi pelo WhatsApp");
    }

    [Fact]
    public async Task Pedido_criado_antes_de_ligar_o_numero_nao_vai_pelo_whatsapp()
    {
        var pedidos = new PedidosRecepcaoService(db, () => relogio);
        var p = await pedidos.CriarAsync(medica.Id, new NovoPedidoRecepcao(Guid.NewGuid(), "outro", null, null, "teste"));
        await db.Usuarios.Where(u => u.Id == medica.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.WhatsAppLigadoEm, relogio.AddMinutes(1)));
        await pedidos.EncerrarAsync(rayssa.Id, p.Id, true, "ok");
        (await servico.AvisosPendentesAsync()).Should().BeEmpty("ligar o número não despeja desfechos antigos");
    }
}
