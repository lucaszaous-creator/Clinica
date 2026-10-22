using Clinica.Application.Modelos;
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
/// @SemDor em qualquer conversa (out/2026, fase 3 — docs/semdor-assistente.md): marcar @SemDor num
/// canal ou numa direta faz o sistema responder ALI, e só à vista de todos quando todos podem ver a
/// agenda e a ficha; senão a resposta vai em particular e o canal recebe só o aviso.
/// </summary>
public sealed class SemDorNaConversaTests : IDisposable
{
    private readonly SqliteConnection conexao = new("Data Source=:memory:");
    private readonly ClinicaDbContext db;
    private readonly DateTime relogio = new(2026, 10, 22, 7, 0, 0);
    private readonly ChatEquipeService chat;
    private readonly UsuarioSistema rayssa = new() { Nome = "Rayssa Lima", Login = "rayssa", Perfil = PerfilAcesso.Recepcao, CriadoEm = new(2026, 1, 1) };
    private readonly UsuarioSistema carla = new() { Nome = "Carla Mendes", Login = "carla", Perfil = PerfilAcesso.Recepcao, CriadoEm = new(2026, 1, 1) };
    private readonly UsuarioSistema fabio = new() { Nome = "Fabio Contas", Login = "fabio", Perfil = PerfilAcesso.Financeiro, PermissoesNegadas = Permissao.VerAgenda, CriadoEm = new(2026, 1, 1) };

    public SemDorNaConversaTests()
    {
        conexao.Open();
        db = new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(conexao).Options);
        db.Database.EnsureCreated();
        var helena = new Profissional { Nome = "Dra. Helena Duarte", Ativo = true };
        var maria = new Paciente { Nome = "Maria José Fictícia" };
        db.Profissionais.Add(helena);
        db.Pacientes.Add(maria);
        db.Usuarios.AddRange(rayssa, carla, fabio);
        db.SaveChanges();
        var dez = new Agendamento { PacienteId = maria.Id, DataHora = new DateTime(2026, 10, 22, 10, 0, 0), ProfissionalId = helena.Id };
        db.Agendamentos.Add(dez);
        db.SaveChanges();
        db.Contatos.Add(new ContatoCampanha
        {
            PacienteId = maria.Id, AgendamentoId = dez.Id, Tipo = TipoContato.ConfirmacaoSessao, Status = StatusContato.Respondido,
            Comentario = RodadaDeConfirmacao.RespostaRemarcar, Origem = ContatoCampanha.OrigemDeAgendamento(dez.Id),
            Referencia = DateOnly.FromDateTime(dez.DataHora), EnviadoEm = relogio.AddHours(-20), RespondidoEm = relogio.AddHours(-19),
        });
        db.SaveChanges();
        db.ChangeTracker.Clear();
        chat = new ChatEquipeService(db, () => relogio);
    }

    public void Dispose() { db.Dispose(); conexao.Dispose(); }

    [Theory]
    [InlineData("@SemDor quem confirmou hoje?", true)]
    [InlineData("bom dia @semdor, agenda de hoje", true)]
    [InlineData("@SEMDOR: agenda", true)]
    [InlineData("escreva para contato@semdor.com.br", false)]
    [InlineData("@SemDorinha quem confirmou", false)]
    [InlineData("agenda de hoje", false)]
    public void Reconhece_a_mencao_como_palavra_inteira(string texto, bool cita)
        => ChatEquipeRegras.CitaOSemDor(texto).Should().Be(cita);

    [Fact]
    public void A_pergunta_vai_sem_a_mencao()
    {
        ChatEquipeRegras.SemAMencaoDoSemDor("@SemDor quem confirmou hoje?").Should().Be("quem confirmou hoje?");
        ChatEquipeRegras.SemAMencaoDoSemDor("bom dia @semdor, agenda de hoje").Should().Be("bom dia , agenda de hoje");
    }

    [Fact]
    public async Task Na_direta_entre_quem_ve_a_agenda_a_resposta_fica_ali_para_os_dois()
    {
        var direta = ChatEquipeRegras.Direta(rayssa.Id, carla.Id);
        await chat.EnviarAsync(rayssa.Id, direta, new("@SemDor agenda de hoje"));

        var vista = await chat.AbrirAsync(carla.Id, direta);
        vista.Mensagens.Should().HaveCount(2);
        var resposta = vista.Mensagens[1];
        resposta.DoSistema.Should().BeTrue();
        resposta.AutorNome.Should().Be(ChatEquipeRegras.NomeDoSistema);
        resposta.Cartao!.Linhas!.Select(l => l.Selo).Should().Contain("Não vem — remarcar");
        ((await chat.PainelAsync(rayssa.Id)).Sistema?.NaoLidas ?? 0).Should().Be(0, "a resposta não foi para a conversa particular");
    }

    [Fact]
    public async Task No_canal_com_quem_nao_ve_a_agenda_a_resposta_vai_em_particular()
    {
        await chat.EnviarAsync(rayssa.Id, ChatEquipeRegras.CanalGeral, new("@SemDor agenda de hoje"));

        var geral = await chat.AbrirAsync(fabio.Id, ChatEquipeRegras.CanalGeral);
        geral.Mensagens.Should().HaveCount(2);
        geral.Mensagens[1].DoSistema.Should().BeTrue();
        geral.Mensagens[1].Cartao.Should().BeNull("o canal recebe só o aviso, sem paciente nem horário");
        geral.Mensagens[1].Texto.Should().Be(ChatEquipeRegras.AvisoDeRespostaEmParticular(rayssa.Nome));
        geral.Mensagens.Should().NotContain(m => m.Texto.Contains("Maria"));

        var particular = await chat.AbrirAsync(rayssa.Id, ChatEquipeRegras.Sistema(rayssa.Id));
        particular.Mensagens.Should().ContainSingle(m => m.DoSistema && m.Cartao != null);
    }

    [Fact]
    public async Task Sem_a_mencao_o_sistema_fica_quieto()
    {
        var direta = ChatEquipeRegras.Direta(rayssa.Id, carla.Id);
        await chat.EnviarAsync(rayssa.Id, direta, new("agenda de hoje?"));
        (await chat.AbrirAsync(carla.Id, direta)).Mensagens.Should().ContainSingle();
    }
}
