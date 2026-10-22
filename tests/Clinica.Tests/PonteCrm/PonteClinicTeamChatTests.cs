using Clinica.Application.Servicos;
using Clinica.Crm.Bridge;
using Clinica.Crm.Contracts;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

// O chat da equipe no CRM (out/2026): a ponte serve o MESMO chat dos apps em nome do login da
// sessão do CRM — e só para quem pode entrar no CRM.
public sealed class PonteClinicTeamChatTests : IDisposable
{
    readonly SqliteConnection connection = new("Data Source=:memory:");
    readonly ClinicaDbContext db;
    readonly ClinicTeamChatBridge bridge;
    readonly UsuarioSistema rayssa = new() { Nome = "Rayssa Lima", Login = "rayssa", Perfil = PerfilAcesso.Recepcao, CriadoEm = new(2026, 1, 1) };
    readonly UsuarioSistema medico = new() { Nome = "Paulo Lima", Login = "paulo", Perfil = PerfilAcesso.Profissional, CriadoEm = new(2026, 1, 1),
        PermissoesNegadas = Permissao.EditarAgenda };
    readonly Paciente maria = new() { Nome = "Maria Fictícia" };

    public PonteClinicTeamChatTests()
    {
        connection.Open(); db = new(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(connection).Options); db.Database.EnsureCreated();
        db.Usuarios.AddRange(rayssa, medico); db.Pacientes.Add(maria); db.SaveChanges();
        bridge = new(db, new ChatEquipeService(db), new PedidosRecepcaoService(db));
    }

    public void Dispose() { db.Dispose(); connection.Dispose(); }

    static int Status(IResult r) => r switch { IStatusCodeHttpResult s => s.StatusCode ?? 200, _ => 200 };
    static T Valor<T>(IResult r) => (T)((IValueHttpResult)r).Value!;

    [Fact] public async Task Quem_nao_entra_no_CRM_nao_usa_o_chat_pela_ponte()
    {
        Assert.Equal(403, Status(await bridge.Panel(new("paulo"), default)));
        Assert.Equal(403, Status(await bridge.Panel(new("ninguem"), default)));
        Assert.Equal(403, Status(await bridge.Panel(new(""), default)));
    }

    [Fact] public async Task A_secretaria_ve_o_sistema_os_canais_e_a_equipe()
    {
        var painel = Valor<TeamChatPanel>(await bridge.Panel(new("rayssa"), default));
        Assert.Equal("system", painel.Items[0].Kind);
        Assert.Equal(ChatEquipeRegras.NomeDoSistema, painel.Items[0].Title);
        Assert.Contains(painel.Items, i => i.Key == ChatEquipeRegras.CanalRecepcao && i.Kind == "channel");
        Assert.Contains(painel.Items, i => i.Kind == "direct" && i.PersonId == medico.Id);
    }

    [Fact] public async Task Mensagem_do_CRM_chega_ao_medico_no_app()
    {
        var direta = ChatEquipeRegras.Direta(rayssa.Id, medico.Id);
        Assert.Equal(200, Status(await bridge.Send(new("rayssa", direta, Text: "Dr., a Maria pediu para remarcar."), default)));
        var aberta = await new ChatEquipeService(db).AbrirAsync(medico.Id, direta);
        Assert.Equal("Dr., a Maria pediu para remarcar.", aberta.Mensagens.Single().Texto);
        Assert.Equal("Rayssa Lima", aberta.Mensagens.Single().AutorNome);
    }

    [Fact] public async Task Conversa_alheia_e_recusa_com_a_frase_do_servico()
    {
        var r = await bridge.Open(new("rayssa", ChatEquipeRegras.Sistema(medico.Id)), default);
        Assert.Equal(403, Status(r));
        Assert.Equal(409, Status(await bridge.Send(new("rayssa", ChatEquipeRegras.CanalRecepcao, Text: "   "), default)));
    }

    [Fact] public async Task Pedido_do_medico_e_tratado_pelo_CRM_e_o_cartao_chega_desenhavel()
    {
        var pedido = await new PedidosRecepcaoService(db).CriarAsync(medico.Id, new(Guid.NewGuid(), "avisar", maria.Id, null, "Trazer os exames"));
        var lista = Valor<TeamRequest[]>(await bridge.Requests(new("rayssa"), default));
        Assert.True(Assert.Single(lista).CanHandle);

        var canal = Valor<TeamChatConversation>(await bridge.Open(new("rayssa", ChatEquipeRegras.CanalRecepcao), default));
        var card = Assert.Single(canal.Messages).Card!;
        Assert.Equal(pedido.Id, card.RequestId);
        Assert.Equal("Aberto", card.RequestState);

        Assert.Equal(200, Status(await bridge.Act(new("rayssa", pedido.Id, "take"), default)));
        var feito = Valor<TeamRequest>(await bridge.Act(new("rayssa", pedido.Id, "done", "Avisada por SMS."), default));
        Assert.Equal("Concluido", feito.State);
        Assert.Equal(409, Status(await bridge.Act(new("rayssa", pedido.Id, "done", "de novo"), default)));
        Assert.Equal(409, Status(await bridge.Act(new("rayssa", pedido.Id, "apagar"), default)));
    }

    [Fact] public async Task Pergunta_ao_sistema_pelo_CRM()
    {
        var conversa = ChatEquipeRegras.Sistema(rayssa.Id);
        Assert.Equal(200, Status(await bridge.Send(new("rayssa", conversa, Text: "ajuda"), default)));
        var aberta = Valor<TeamChatConversation>(await bridge.Open(new("rayssa", conversa), default));
        Assert.True(aberta.Messages[^1].FromSystem);
        Assert.Equal("ajuda", aberta.Messages[^1].Card!.Kind);
        Assert.NotEmpty(aberta.Messages[^1].Card!.Suggestions);
    }
}
