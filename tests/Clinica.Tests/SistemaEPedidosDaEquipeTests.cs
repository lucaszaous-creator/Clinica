using Clinica.Application.Abstracoes;
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
/// As regras do Sistema SemDor (out/2026): como a pergunta é lida, o selo de cada horário e o
/// cartão da agenda — com as mesmas palavras do balcão e do CRM.
/// </summary>
public class SistemaEquipeRegrasTests
{
    private static readonly DateOnly Hoje = new(2026, 10, 22); // quinta-feira
    private static readonly IReadOnlyList<(int, string)> Profissionais = [(1, "Dra. Helena Duarte"), (2, "Carlos Mota"), (3, "Carla Mota")];

    [Theory]
    [InlineData("Minha agenda de hoje", IntencaoDoSistema.Agenda)]
    [InlineData("agenda", IntencaoDoSistema.Agenda)]
    [InlineData("quem confirmou amanhã?", IntencaoDoSistema.Confirmaram)]
    [InlineData("Quem não confirmou", IntencaoDoSistema.NaoConfirmaram)]
    [InlineData("quem está sem resposta", IntencaoDoSistema.NaoConfirmaram)]
    [InlineData("Quem pediu para remarcar", IntencaoDoSistema.Remarcar)]
    [InlineData("Próximo paciente", IntencaoDoSistema.Proximo)]
    [InlineData("quem já chegou", IntencaoDoSistema.Chegaram)]
    [InlineData("faltas de ontem", IntencaoDoSistema.Faltas)]
    [InlineData("Meus pedidos", IntencaoDoSistema.Pedidos)]
    [InlineData("pedir para a recepção remarcar a Maria", IntencaoDoSistema.NovoPedido)]
    [InlineData("paciente Maria José", IntencaoDoSistema.Paciente)]
    [InlineData("ajuda", IntencaoDoSistema.Ajuda)]
    [InlineData("bom dia", IntencaoDoSistema.Saudacao)]
    [InlineData("qual a senha do wifi", IntencaoDoSistema.NaoEntendi)]
    [InlineData("sexta", IntencaoDoSistema.Agenda)]
    public void Le_a_intencao(string texto, IntencaoDoSistema esperada)
        => SistemaEquipeRegras.Interpretar(texto, Hoje, Profissionais).Intencao.Should().Be(esperada);

    [Theory]
    [InlineData("agenda de hoje", 0)]
    [InlineData("agenda de amanhã", 1)]
    [InlineData("agenda depois de amanhã", 2)]
    [InlineData("ontem", -1)]
    [InlineData("agenda de sexta", 1)]
    [InlineData("agenda de quinta", 0)]
    [InlineData("agenda de segunda", 4)]
    [InlineData("agenda 24/10", 2)]
    [InlineData("agenda dia 25", 3)]
    public void Le_o_dia(string texto, int dias)
        => SistemaEquipeRegras.Interpretar(texto, Hoje, Profissionais).Dia.Should().Be(Hoje.AddDays(dias));

    [Fact]
    public void Dia_do_mes_que_ja_passou_e_o_do_mes_que_vem()
        => SistemaEquipeRegras.Interpretar("agenda dia 5", Hoje, Profissionais).Dia.Should().Be(new DateOnly(2026, 11, 5));

    [Fact]
    public void Acha_o_profissional_pelo_nome_e_nao_escolhe_entre_dois_iguais()
    {
        SistemaEquipeRegras.Interpretar("agenda da Helena amanhã", Hoje, Profissionais).ProfissionalId.Should().Be(1);
        SistemaEquipeRegras.Interpretar("agenda do carlos mota", Hoje, Profissionais).ProfissionalId.Should().Be(2);
        SistemaEquipeRegras.Interpretar("agenda da mota", Hoje, Profissionais).ProfissionalId.Should().BeNull("dois Mota: o sistema não escolhe por palpite");
    }

    [Fact]
    public void O_nome_procurado_guarda_o_acento()
        => SistemaEquipeRegras.Interpretar("paciente José Antônio de hoje", Hoje, Profissionais).Termo.Should().Be("José Antônio");

    [Fact]
    public void O_selo_usa_as_palavras_do_balcao()
    {
        SistemaEquipeRegras.Situacao(StatusAgendamento.Agendado, EtapaFila.Aguardando, SituacaoConfirmacao.Confirmou).Selo.Should().Be("Confirmou");
        SistemaEquipeRegras.Situacao(StatusAgendamento.Agendado, EtapaFila.Aguardando, SituacaoConfirmacao.PediuRemarcar).Selo.Should().Be("Não vem — remarcar");
        SistemaEquipeRegras.Situacao(StatusAgendamento.Agendado, EtapaFila.Aguardando, SituacaoConfirmacao.AvisadoSemResposta).Selo.Should().Be("Não confirmou");
        SistemaEquipeRegras.Situacao(StatusAgendamento.Agendado, EtapaFila.Aguardando, null).Selo.Should().Be("Sem lembrete");
        SistemaEquipeRegras.Situacao(StatusAgendamento.Agendado, EtapaFila.Chegou, SituacaoConfirmacao.Confirmou).Selo
            .Should().Be("Chegou", "o que aconteceu no balcão vale mais que a resposta ao lembrete");
        SistemaEquipeRegras.Situacao(StatusAgendamento.Realizado, EtapaFila.Finalizado, null).Selo.Should().Be("Atendido");
        SistemaEquipeRegras.Situacao(StatusAgendamento.Faltou, EtapaFila.ForaDaFila, null).Selo.Should().Be("Faltou");
    }

    private static HorarioDoSistema H(int id, int hora, SituacaoConfirmacao? c, StatusAgendamento s = StatusAgendamento.Agendado,
        EtapaFila e = EtapaFila.Aguardando)
        => new(id, Hoje.ToDateTime(new TimeOnly(hora, 0)), id, $"Paciente {id}", "Acupuntura", "Dra. Helena Duarte", s, e, c);

    [Fact]
    public void Cartao_da_agenda_conta_e_lista_cada_horario()
    {
        var agora = Hoje.ToDateTime(new TimeOnly(9, 30));
        var horarios = new[]
        {
            H(1, 8, null, StatusAgendamento.Realizado, EtapaFila.Finalizado),
            H(2, 10, SituacaoConfirmacao.Confirmou),
            H(3, 11, SituacaoConfirmacao.PediuRemarcar),
            H(4, 14, SituacaoConfirmacao.AvisadoSemResposta),
            H(5, 15, null, StatusAgendamento.Cancelado, EtapaFila.ForaDaFila),
        };
        var (texto, cartao) = SistemaEquipeRegras.Agenda("Sua agenda", Hoje, Hoje, horarios, agora, false);
        cartao.Titulo.Should().Be("Sua agenda");
        cartao.Subtitulo.Should().Be("Hoje · qui, 22/10");
        cartao.Linhas!.Should().HaveCount(4, "o cancelado não conta na agenda");
        cartao.Resumo!.Single(r => r.Rotulo == "horários").Valor.Should().Be(4);
        cartao.Resumo!.Single(r => r.Rotulo == "confirmaram").Valor.Should().Be(1);
        cartao.Resumo!.Single(r => r.Rotulo == "remarcar").Valor.Should().Be(1);
        cartao.Resumo!.Single(r => r.Rotulo == "não confirmaram").Valor.Should().Be(1);
        cartao.Linhas!.Single(l => l.AgendamentoId == 3).PodePedir.Should().BeTrue();
        cartao.Linhas!.Single(l => l.AgendamentoId == 1).PodePedir.Should().BeFalse("já foi atendido");
        texto.Should().Contain("11:00 Paciente 3 — Acupuntura [Não vem — remarcar]");
        CartaoChat.DeJson(cartao.ParaJson()).Should().BeEquivalentTo(cartao, "o cartão atravessa o banco e a ponte como JSON");
    }

    [Fact]
    public void Filtro_mostra_so_quem_pediu_para_remarcar()
    {
        var agora = Hoje.ToDateTime(new TimeOnly(7, 0));
        var (_, cartao) = SistemaEquipeRegras.Agenda("Sua agenda", Hoje, Hoje,
            [H(2, 10, SituacaoConfirmacao.Confirmou), H(3, 11, SituacaoConfirmacao.PediuRemarcar)], agora, false, IntencaoDoSistema.Remarcar);
        cartao.Linhas!.Select(l => l.AgendamentoId).Should().Equal(3);
        cartao.Titulo.Should().Be("Quem pediu para remarcar ou cancelar");
    }

    [Fact]
    public void Json_estragado_nao_derruba_a_mensagem()
        => CartaoChat.DeJson("{nao é json").Should().BeNull();
}

/// <summary>
/// O ciclo completo no banco: o médico pergunta a agenda ao Sistema SemDor, pede à recepção para
/// remarcar, a recepção assume e conclui, e o desfecho chega ao médico — tocando como uma direta.
/// </summary>
public sealed class SistemaEPedidosDaEquipeTests : IDisposable
{
    private readonly SqliteConnection conexao = new("Data Source=:memory:");
    private readonly ClinicaDbContext db;
    private DateTime relogio = new(2026, 10, 22, 7, 0, 0);
    private readonly ChatEquipeService chat;
    private readonly PedidosRecepcaoService pedidos;
    private readonly Profissional helena = new() { Nome = "Dra. Helena Duarte", Ativo = true };
    private readonly UsuarioSistema rayssa = new() { Nome = "Rayssa Lima", Login = "rayssa", Perfil = PerfilAcesso.Recepcao, CriadoEm = new(2026, 1, 1) };
    private UsuarioSistema medica = null!;
    private readonly UsuarioSistema financeiro = new() { Nome = "Fabio Contas", Login = "fabio", Perfil = PerfilAcesso.Financeiro, PermissoesNegadas = Permissao.VerAgenda, CriadoEm = new(2026, 1, 1) };
    private readonly Paciente maria = new() { Nome = "Maria José Fictícia" };
    private Agendamento dez = null!;

    public SistemaEPedidosDaEquipeTests()
    {
        conexao.Open();
        db = new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(conexao).Options);
        db.Database.EnsureCreated();
        db.Profissionais.Add(helena);
        db.Pacientes.Add(maria);
        db.SaveChanges();
        medica = new() { Nome = "Helena Duarte", Login = "helena", Perfil = PerfilAcesso.Profissional, ProfissionalId = helena.Id, CriadoEm = new(2026, 1, 1) };
        db.Usuarios.AddRange(rayssa, medica, financeiro);
        dez = new Agendamento { PacienteId = maria.Id, DataHora = new DateTime(2026, 10, 22, 10, 0, 0), ProfissionalId = helena.Id };
        var onze = new Agendamento { PacienteId = maria.Id, DataHora = new DateTime(2026, 10, 22, 11, 0, 0), ProfissionalId = helena.Id };
        db.Agendamentos.AddRange(dez, onze);
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
        pedidos = new PedidosRecepcaoService(db, () => relogio);
    }

    public void Dispose() { db.Dispose(); conexao.Dispose(); }

    private static readonly PreferenciasSomChat Prefs = new();

    [Fact]
    public async Task Medica_pergunta_a_agenda_e_recebe_o_cartao_com_quem_pediu_para_remarcar()
    {
        var conversa = ChatEquipeRegras.Sistema(medica.Id);
        await chat.EnviarAsync(medica.Id, conversa, new("minha agenda de hoje"));
        var aberta = await chat.AbrirAsync(medica.Id, conversa);
        aberta.Titulo.Should().Be(ChatEquipeRegras.NomeDoSistema);
        aberta.Mensagens.Should().HaveCount(2);
        aberta.Mensagens[0].Minha.Should().BeTrue();
        var resposta = aberta.Mensagens[1];
        resposta.DoSistema.Should().BeTrue();
        resposta.Minha.Should().BeFalse("quem escreveu foi o sistema, embora a linha tenha o id dela");
        resposta.AutorNome.Should().Be(ChatEquipeRegras.NomeDoSistema);
        resposta.Cartao!.Linhas!.Select(l => l.Selo).Should().Equal("Não vem — remarcar", "Sem lembrete");
        (await chat.PainelAsync(medica.Id)).Sistema!.NaoLidas.Should().Be(0, "a resposta à própria pergunta já chega lida");
        await FluentActions.Awaiting(() => chat.EditarAsync(medica.Id, resposta.Id, "outra coisa"))
            .Should().ThrowAsync<UnauthorizedAccessException>("resposta do sistema não se corrige");
    }

    [Fact]
    public async Task Ninguem_le_a_conversa_do_sistema_de_outra_pessoa()
        => await FluentActions.Awaiting(() => chat.AbrirAsync(rayssa.Id, ChatEquipeRegras.Sistema(medica.Id)))
            .Should().ThrowAsync<UnauthorizedAccessException>();

    [Fact]
    public async Task Sem_agenda_nem_profissional_a_resposta_diz_por_que()
    {
        var conversa = ChatEquipeRegras.Sistema(financeiro.Id);
        await chat.EnviarAsync(financeiro.Id, conversa, new("agenda de hoje"));
        var r = (await chat.AbrirAsync(financeiro.Id, conversa)).Mensagens[^1];
        r.Texto.Should().Contain("não está ligado a um profissional");
    }

    [Fact]
    public async Task Pedido_vai_da_medica_a_recepcao_e_o_desfecho_volta_para_ela()
    {
        var chave = Guid.NewGuid();
        var criado = await pedidos.CriarAsync(medica.Id, new(chave, "remarcar", null, dez.Id, "Para a semana que vem, de manhã"));
        criado.PacienteNome.Should().Be("Maria José Fictícia");
        criado.HorarioEm.Should().Be(dez.DataHora);
        (await pedidos.CriarAsync(medica.Id, new(chave, "remarcar", null, dez.Id, "repetido"))).Id
            .Should().Be(criado.Id, "o mesmo comando não cria um segundo pedido");

        // A recepção vê o cartão no # Recepção, e o pedido na lista dela.
        var recepcao = await chat.AbrirAsync(rayssa.Id, ChatEquipeRegras.CanalRecepcao);
        var cartao = recepcao.Mensagens.Single().Cartao!;
        cartao.PedidoId.Should().Be(criado.Id);
        cartao.Situacao.Should().Be(PedidosRecepcaoRegras.Aberto);
        (await pedidos.ListarAsync(rayssa.Id)).Single().PodeTratar.Should().BeTrue();
        (await pedidos.ListarAsync(medica.Id)).Single().PodeTratar.Should().BeFalse();
        await FluentActions.Awaiting(() => pedidos.AssumirAsync(medica.Id, criado.Id))
            .Should().ThrowAsync<UnauthorizedAccessException>("quem pede não trata o próprio pedido");

        var desde = (await chat.PulsoAsync(medica.Id, 0, Prefs)).UltimaMensagemId;
        relogio = relogio.AddMinutes(5);
        await pedidos.AssumirAsync(rayssa.Id, criado.Id);
        (await chat.AbrirAsync(medica.Id, ChatEquipeRegras.Sistema(medica.Id))).Mensagens.Single().Cartao!.Situacao
            .Should().Be(PedidosRecepcaoRegras.EmAndamento, "o cartão de quem pediu acompanha");

        relogio = relogio.AddMinutes(5);
        await FluentActions.Awaiting(() => pedidos.EncerrarAsync(rayssa.Id, criado.Id, false, ""))
            .Should().ThrowAsync<InvalidOperationException>("não feito pede o motivo");
        await pedidos.EncerrarAsync(rayssa.Id, criado.Id, true, "Remarcada para ter, 27/10 às 09:00.");

        var pulso = await chat.PulsoAsync(medica.Id, desde, Prefs);
        var aviso = pulso.Novidades.Single();
        aviso.Som.Should().Be(SomChat.Direta, "o desfecho chega sozinho e toca como uma direta");
        aviso.Titulo.Should().Be(ChatEquipeRegras.NomeDoSistema);
        aviso.Previa.Should().Contain("Rayssa Lima concluiu o seu pedido");
        (await chat.PainelAsync(medica.Id)).Sistema!.NaoLidas.Should().Be(1);
        (await chat.AbrirAsync(rayssa.Id, ChatEquipeRegras.CanalRecepcao)).Mensagens.Single().Cartao!.Situacao
            .Should().Be(PedidosRecepcaoRegras.Concluido);
        await FluentActions.Awaiting(() => pedidos.EncerrarAsync(rayssa.Id, criado.Id, true, "de novo"))
            .Should().ThrowAsync<InvalidOperationException>("encerrado não volta");
        (await pedidos.ListarAsync(rayssa.Id)).Should().BeEmpty("a lista da recepção é do que está em aberto");
    }

    [Fact]
    public async Task Remarcar_sem_horario_e_recusado()
        => await FluentActions.Awaiting(() => pedidos.CriarAsync(medica.Id, new(Guid.NewGuid(), "remarcar", maria.Id, null, "")))
            .Should().ThrowAsync<InvalidOperationException>();

    [Fact]
    public async Task Horarios_do_paciente_para_o_formulario()
        => (await pedidos.HorariosAsync(medica.Id, maria.Id)).Select(h => h.AgendamentoId).Should().HaveCount(2);

    [Fact]
    public async Task Pergunta_meus_pedidos_lista_o_que_ela_pediu()
    {
        await pedidos.CriarAsync(medica.Id, new(Guid.NewGuid(), "avisar", maria.Id, null, "Trazer os exames"));
        var conversa = ChatEquipeRegras.Sistema(medica.Id);
        await chat.EnviarAsync(medica.Id, conversa, new("meus pedidos"));
        var r = (await chat.AbrirAsync(medica.Id, conversa)).Mensagens[^1];
        r.Cartao!.Titulo.Should().Be("Seus pedidos à recepção");
        r.Cartao.Linhas!.Single().Titulo.Should().Be("Avisar o paciente · Maria José Fictícia");
    }
}
