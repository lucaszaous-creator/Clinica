using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
using Clinica.Crm.Bridge;
using Clinica.Crm.Contracts;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// A remarcação pedida pelo PACIENTE (@SemDor, out/2026 — docs/semdor-assistente.md), de ponta a ponta
/// pela ponte: o "Remarcar" do lembrete abre o pedido no # Recepção, a recepção oferece horários, o CRM
/// busca a oferta e conta o resultado, o paciente escolhe e um clique remarca e põe a confirmação na fila.
/// </summary>
public sealed class RemarcacaoPeloPacienteTests : IDisposable
{
    private readonly SqliteConnection conexao = new("Data Source=:memory:");
    private readonly ClinicaDbContext db;
    private readonly ClinicAgendaBridge ponte;
    private readonly RemarcacaoPeloPacienteService servico;
    private readonly AgendaService agenda;
    private readonly Agendamento horario;
    private readonly UsuarioSistema recepcao;

    public RemarcacaoPeloPacienteTests()
    {
        conexao.Open();
        db = new(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(conexao).Options);
        db.Database.EnsureCreated();
        var repo = new ClinicaRepositorio(db);
        agenda = new AgendaService(repo, new AtendimentoService(repo));
        ponte = new ClinicAgendaBridge(db, agenda);
        servico = new RemarcacaoPeloPacienteService(db, agenda, new BuscaDeVagasService(repo));
        var dia = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "America/Sao_Paulo").Date.AddDays(3).AddHours(9);
        if (dia.DayOfWeek == DayOfWeek.Sunday) dia = dia.AddDays(1);
        horario = new Agendamento
        {
            Paciente = new Paciente { Nome = "MARIA DA SILVA", Telefone = "(11) 99999-0001" },
            Profissional = new Profissional { Nome = "Dra. Fictícia" }, DataHora = dia, DuracaoMinutos = 30,
        };
        recepcao = new UsuarioSistema { Nome = "Rayssa", Login = "rayssa", SenhaHash = "x", SenhaSalt = "x", Perfil = PerfilAcesso.Recepcao };
        db.Agendamentos.Add(horario);
        db.Usuarios.Add(recepcao);
        db.SaveChanges();
    }

    private ClinicDecision Remarcar() => new(Guid.NewGuid().ToString("N"), horario.Id, ClinicAgendaBridge.Revision(horario),
        ClinicAgendaBridge.Phone(horario.Paciente!.Telefone), "reschedule", "msg-lembrete");

    private async Task<PedidoRecepcao> PedidoAsync() => await db.PedidosRecepcao.AsNoTracking().SingleAsync();

    private async Task<int> PedidoDoPacienteAsync()
    {
        Assert.Equal("reschedule-requested", (await ponte.Decide(Remarcar(), default)).Status);
        return (await PedidoAsync()).Id;
    }

    [Fact]
    public async Task Remarcar_no_lembrete_abre_o_pedido_do_paciente_no_canal_da_recepcao()
    {
        var comando = Remarcar();
        await ponte.Decide(comando, default);
        await ponte.Decide(comando, default); // o CRM reenvia: não abre outro

        var p = await PedidoAsync();
        Assert.Equal(RemarcacaoPeloPacienteRegras.OrigemPaciente, p.Origem);
        Assert.Equal("remarcar", p.Tipo);
        Assert.Equal("5511999990001", p.TelefonePaciente);
        Assert.Equal(horario.Id, p.AgendamentoId);
        var cartao = await db.MensagensEquipe.AsNoTracking().SingleAsync(m => m.Id == p.MensagemRecepcaoId);
        Assert.Equal(ChatEquipeRegras.CanalRecepcao, cartao.Conversa);
        Assert.True(cartao.DoSistema);
        var desenho = CartaoChat.DeJson(cartao.Cartao)!;
        Assert.NotNull(desenho.Oferta);
        Assert.Empty(desenho.Oferta!.Oferecidos!);
        // O usuário técnico é o autor, mas não é da equipe: fora da lista de usuários e sem entrar.
        var tecnico = await db.Usuarios.AsNoTracking().SingleAsync(u => u.Login == UsuarioTecnico.Login);
        Assert.False(tecnico.Ativo);
        Assert.DoesNotContain(await new ClinicaRepositorio(db).UsuariosAsync(), u => u.Login == UsuarioTecnico.Login);
    }

    // @SemDor fase 2: o paciente ESCREVEU ("quinta não dá, tem sexta de tarde?"); a IA do CRM leu um
    // pedido de remarcação e mandou a preferência na nota, que vai para o pedido como leitura de máquina.
    [Fact]
    public async Task Texto_livre_lido_pela_ia_leva_a_preferencia_ao_pedido()
    {
        await ponte.Decide(Remarcar() with { Note = "sexta à tarde" }, default);
        var p = await PedidoAsync();
        p.Detalhe.Should().Be("Preferência do paciente (lida pelo @SemDor): sexta à tarde");
        await Assert.ThrowsAsync<InvalidOperationException>(() => ponte.Decide(Remarcar() with { Note = new string('x', 301) }, default));
    }

    [Fact]
    public async Task Do_pedido_a_remarcacao_pelo_whatsapp_de_ponta_a_ponta()
    {
        var pedidoId = await PedidoDoPacienteAsync();
        var vagas = await servico.VagasAsync(recepcao.Id, pedidoId);
        Assert.InRange(vagas.Count, 2, RemarcacaoPeloPacienteRegras.VagasSugeridas);

        await servico.OferecerAsync(recepcao.Id, pedidoId, [vagas[1].Inicio, vagas[0].Inicio]);
        var p = await PedidoAsync();
        Assert.Equal(PedidosRecepcaoRegras.EmAndamento, p.Situacao);
        Assert.Equal("Rayssa", p.ResponsavelNome);

        // O CRM busca a oferta: o texto e os dois botões, na ordem dos horários.
        var oferta = Assert.Single(await servico.MensagensPendentesAsync());
        Assert.Equal($"oferta:{pedidoId}:{p.OfertaToken}", oferta.Id);
        Assert.Equal("5511999990001", oferta.Telefone);
        Assert.StartsWith("Olá, Maria!", oferta.Texto);
        Assert.Equal(2, oferta.Opcoes.Count);
        Assert.Equal(RemarcacaoPeloPacienteRegras.IdDaOpcao(p.OfertaToken!, 0), oferta.Opcoes[0].Id);
        Assert.Equal(RemarcacaoPeloPacienteRegras.RotuloDoBotao(vagas[0].Inicio), oferta.Opcoes[0].Titulo);

        await servico.ConfirmarMensagemAsync(oferta.Id, true, null);
        Assert.Empty(await servico.MensagensPendentesAsync());

        // O paciente toca o segundo botão. O WhatsApp de número antigo chega sem o nono dígito.
        var toque = RemarcacaoPeloPacienteRegras.LerOpcao(oferta.Opcoes[1].Id)!.Value;
        var resposta = await servico.EscolherAsync(toque.Chave, toque.Indice, "551199990001");
        Assert.StartsWith("Recebemos a sua escolha", resposta);
        p = await PedidoAsync();
        Assert.Equal(vagas[1].Inicio, p.HorarioEscolhido);
        Assert.Contains(await db.MensagensEquipe.AsNoTracking().Where(m => m.Conversa == ChatEquipeRegras.CanalRecepcao).ToListAsync(),
            m => m.Texto.Contains("escolheu") && m.Cartao == null);

        var novo = await servico.RemarcarAsync(recepcao.Id, pedidoId, "rayssa");
        Assert.Equal(vagas[1].Inicio, novo);
        Assert.Equal(novo, (await db.Agendamentos.AsNoTracking().SingleAsync(a => a.Id == horario.Id)).DataHora);
        p = await PedidoAsync();
        Assert.Equal(PedidosRecepcaoRegras.Concluido, p.Situacao);

        // A confirmação vai ao paciente pela mesma fila, dizendo o que a clínica FEZ.
        var confirmacao = Assert.Single(await servico.MensagensPendentesAsync());
        Assert.Equal($"resposta:{pedidoId}", confirmacao.Id);
        Assert.Empty(confirmacao.Opcoes);
        Assert.Equal(RemarcacaoPeloPacienteRegras.TextoRemarcado("MARIA DA SILVA", novo), confirmacao.Texto);
        await servico.ConfirmarMensagemAsync(confirmacao.Id, true, null);
        Assert.Empty(await servico.MensagensPendentesAsync());

        // Tocar de novo depois de encerrado não reabre nada.
        Assert.Equal(RemarcacaoPeloPacienteRegras.TextoOfertaVencida, await servico.EscolherAsync(toque.Chave, 0, "5511999990001"));
    }

    [Fact]
    public async Task Horario_ocupado_entre_a_escolha_e_o_clique_recusa_sem_mexer_na_agenda()
    {
        var pedidoId = await PedidoDoPacienteAsync();
        var vaga = (await servico.VagasAsync(recepcao.Id, pedidoId))[0].Inicio;
        await servico.OferecerAsync(recepcao.Id, pedidoId, [vaga]);
        var chave = (await PedidoAsync()).OfertaToken!;
        await servico.EscolherAsync(chave, 0, "5511999990001");

        db.Agendamentos.Add(new Agendamento { Paciente = new Paciente { Nome = "Outra pessoa" }, ProfissionalId = horario.ProfissionalId, DataHora = vaga, DuracaoMinutos = 30 });
        await db.SaveChangesAsync();

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() => servico.RemarcarAsync(recepcao.Id, pedidoId, "rayssa"));
        Assert.Contains("ocupado", erro.Message);
        Assert.Equal(horario.DataHora, (await db.Agendamentos.AsNoTracking().SingleAsync(a => a.Id == horario.Id)).DataHora);
        Assert.Equal(PedidosRecepcaoRegras.EmAndamento, (await PedidoAsync()).Situacao);
        Assert.Empty((await servico.MensagensPendentesAsync()).Where(m => m.Id.StartsWith("resposta:")));
    }

    [Fact]
    public async Task Oferta_recusa_mais_de_tres_horario_ocupado_e_quem_nao_trata_pedidos()
    {
        var pedidoId = await PedidoDoPacienteAsync();
        var vagas = await servico.VagasAsync(recepcao.Id, pedidoId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => servico.OferecerAsync(recepcao.Id, pedidoId, vagas.Take(4).Select(v => v.Inicio).ToList()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => servico.OferecerAsync(recepcao.Id, pedidoId, [horario.DataHora]));

        var medico = new UsuarioSistema { Nome = "Médico", Login = "medico", SenhaHash = "x", SenhaSalt = "x", Perfil = PerfilAcesso.Profissional };
        db.Usuarios.Add(medico);
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => servico.OferecerAsync(medico.Id, pedidoId, [vagas[0].Inicio]));
    }

    [Fact]
    public async Task Oferta_nova_vence_a_velha_e_falha_do_crm_aparece_no_cartao()
    {
        var pedidoId = await PedidoDoPacienteAsync();
        var vagas = await servico.VagasAsync(recepcao.Id, pedidoId);
        await servico.OferecerAsync(recepcao.Id, pedidoId, [vagas[0].Inicio]);
        var velha = (await PedidoAsync()).OfertaToken!;
        await servico.ConfirmarMensagemAsync(Assert.Single(await servico.MensagensPendentesAsync()).Id, false, "janela de 24 h fechada");
        var p = await PedidoAsync();
        var cartao = CartaoChat.DeJson((await db.MensagensEquipe.AsNoTracking().SingleAsync(m => m.Id == p.MensagemRecepcaoId)).Cartao)!;
        Assert.Equal("janela de 24 h fechada", cartao.Oferta!.Falha);

        await servico.OferecerAsync(recepcao.Id, pedidoId, [vagas[1].Inicio]);
        Assert.NotEqual(velha, (await PedidoAsync()).OfertaToken);
        Assert.Equal(RemarcacaoPeloPacienteRegras.TextoOfertaVencida, await servico.EscolherAsync(velha, 0, "5511999990001"));
        Assert.Null((await PedidoAsync()).HorarioEscolhido);
    }

    [Fact]
    public void Regras_do_botao_e_da_coluna()
    {
        var h = new DateTime(2026, 10, 24, 15, 30, 0);
        Assert.Equal("sáb 24/10 15:30", RemarcacaoPeloPacienteRegras.RotuloDoBotao(h));
        Assert.True(RemarcacaoPeloPacienteRegras.RotuloDoBotao(h).Length <= 20); // o teto do WhatsApp
        Assert.Equal(("1A2B3C4D", 1), RemarcacaoPeloPacienteRegras.LerOpcao("OFERTA 1A2B3C4D 2"));
        Assert.Null(RemarcacaoPeloPacienteRegras.LerOpcao("OFERTA 1A2B3C4D 4"));
        Assert.Null(RemarcacaoPeloPacienteRegras.LerOpcao("REMARCAR 1A2B3C4D5E6F"));
        var dois = new[] { h, h.AddHours(1) };
        Assert.Equal(dois, RemarcacaoPeloPacienteRegras.Ler(RemarcacaoPeloPacienteRegras.Gravar(dois)));
        Assert.Equal("Maria", RemarcacaoPeloPacienteRegras.PrimeiroNome("MARIA DA SILVA"));
        Assert.Contains("remarcada para sábado, 24/10, às 15:30", RemarcacaoPeloPacienteRegras.TextoRemarcado("Maria", h));
    }

    public void Dispose()
    {
        db.Dispose();
        conexao.Dispose();
    }
}
